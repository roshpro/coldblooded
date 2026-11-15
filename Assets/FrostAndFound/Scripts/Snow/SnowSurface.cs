using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace FrostAndFound
{
    public enum StampShape { BootLeft, BootRight, Scoop, Crater, KidBootLeft, KidBootRight, SledTrack }

    /// <summary>
    /// The snow layer on the detected floor. A grid mesh spans the room's bounds; a mask texture keeps the
    /// snow inside the floor polygon (and off furniture footprints) and piles drifts against the walls; a trail
    /// texture records every footprint, scoop and snowball crater, and the Snow shader displaces the surface.
    /// </summary>
    public class SnowSurface : MonoBehaviour
    {
        public static SnowSurface Instance { get; private set; }

        public float thickness = 0.05f;
        public float driftHeight = 0.07f;
        [Tooltip("Tallest of the soft flat-topped mounds scattered over the open floor (m).")]
        public float moundHeight = 0.035f;
        [Tooltip("Average floor area (m²) per mound.")]
        public float areaPerMound = 1.8f;
        [Tooltip("Tallest of the big drifts the scenery heaps up in bigger rooms, around trees and in corners (m).")]
        public float bigDriftHeight = 0.34f;
        float[] m_Relief;   // mound + drift height per mask texel (m), for SurfaceHeightAt
        Color32[] m_MaskPx; // R = snow alpha, G = wall drift, B = mound, A = big drift (kept for later edits)
        bool m_MaskDirty;

        // prints fill back in: every trail texel remembers when it starts to fade (in quarter seconds since the
        // snow was laid); only 16×16 tiles that still hold a print are visited
        const int FadeTile = 16;
        const float FadeTick = 0.25f;
        ushort[] m_FadeAt;
        bool[] m_TileLive;
        readonly List<int> m_LiveTiles = new List<int>();
        int m_TilesX, m_TilesY;
        float m_T0, m_NextFade;

        RoomInfo m_Room;
        Rect m_Rect;
        Texture2D m_Mask, m_Trail;
        int m_TrailW, m_TrailH;
        bool m_TrailDirty;
        float m_LastApply;
        Material m_Mat;
        byte[] m_MaskAlpha; int m_MaskW, m_MaskH;

        public float SurfaceHeight => (m_Room != null ? m_Room.FloorY : 0f) + thickness;
        public RoomInfo Room => m_Room;

        public static SnowSurface Create(RoomInfo room)
        {
            var go = new GameObject("Snow Surface");
            go.layer = Layers.Snow;
            var s = go.AddComponent<SnowSurface>();
            s.Build(room);
            return s;
        }

        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        void Build(RoomInfo room)
        {
            Instance = this;
            m_Room = room;
            var b = room.Bounds;
            float pad = 0.05f;
            m_Rect = Rect.MinMaxRect(b.xMin - pad, b.yMin - pad, b.xMax + pad, b.yMax + pad);

            transform.position = new Vector3(m_Rect.xMin, room.FloorY + 0.002f, m_Rect.yMin);
            transform.localScale = new Vector3(m_Rect.width, 1f, m_Rect.height);

            // ---- mask (alpha + drift)
            float maskTexel = 0.03f;
            m_MaskW = Mathf.Clamp(Mathf.CeilToInt(m_Rect.width / maskTexel), 32, 384);
            m_MaskH = Mathf.Clamp(Mathf.CeilToInt(m_Rect.height / maskTexel), 32, 384);
            m_Mask = new Texture2D(m_MaskW, m_MaskH, TextureFormat.RGBA32, false, true) { name = "SnowMask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[m_MaskW * m_MaskH];
            m_MaskAlpha = new byte[px.Length];
            m_Relief = new float[px.Length];
            var edges = new float[px.Length];
            var drifts = new float[px.Length];
            int open = 0;
            for (int y = 0; y < m_MaskH; y++)
                for (int x = 0; x < m_MaskW; x++)
                {
                    int i = y * m_MaskW + x;
                    var p = TexelToWorld(x, y);
                    float edge = room.EdgeDistance(p);
                    float alpha = Mathf.Clamp01((edge + 0.01f) / 0.05f);
                    if (alpha > 0f && room.IsBlocked != null && room.IsBlocked(new Vector3(p.x, room.FloorY, p.y), 0.0f)) alpha = 0f;
                    float drift = edge > 0f ? Mathf.Pow(Mathf.Clamp01(1f - edge / 0.38f), 2f) : 0f;
                    drift *= 0.75f + 0.25f * Mathf.PerlinNoise(p.x * 3.1f, p.y * 3.1f);
                    m_MaskAlpha[i] = (byte)(alpha * 255);
                    edges[i] = edge;
                    drifts[i] = drift;
                    if (alpha > 0.5f) open++;
                }

            // ---- mounds: soft flat-topped humps scattered over the open floor, so the snow has body
            // (B channel, 0..1 of moundHeight). They flatten out into the drifts near walls and furniture.
            var rng = new System.Random(1234);
            float area = open * (m_Rect.width / m_MaskW) * (m_Rect.height / m_MaskH);
            int moundCount = Mathf.Max(1, Mathf.RoundToInt(area / Mathf.Max(0.3f, areaPerMound)));
            var mounds = new List<Vector4>(moundCount);   // centre xz, radius, height (0..1)
            for (int tries = 0; mounds.Count < moundCount && tries < moundCount * 12; tries++)
            {
                var c = new Vector2(Mathf.Lerp(m_Rect.xMin, m_Rect.xMax, (float)rng.NextDouble()), Mathf.Lerp(m_Rect.yMin, m_Rect.yMax, (float)rng.NextDouble()));
                if (room.EdgeDistance(c) < 0.25f) continue;
                if (room.IsBlocked != null && room.IsBlocked(new Vector3(c.x, room.FloorY, c.y), 0f)) continue;
                mounds.Add(new Vector4(c.x, c.y, Mathf.Lerp(0.5f, 1.3f, (float)rng.NextDouble()), Mathf.Lerp(0.43f, 1f, (float)rng.NextDouble())));
            }
            for (int y = 0; y < m_MaskH; y++)
                for (int x = 0; x < m_MaskW; x++)
                {
                    int i = y * m_MaskW + x;
                    var p = TexelToWorld(x, y);
                    float mound = 0f;
                    foreach (var m in mounds)
                    {
                        float t = Vector2.Distance(p, new Vector2(m.x, m.y)) / m.z;
                        if (t < 1f) mound = Mathf.Max(mound, m.w * (1f - Smooth(Smooth(Mathf.InverseLerp(0.5f, 1f, t)))));
                    }
                    mound *= Smooth(Mathf.Clamp01(edges[i] / 0.3f));
                    m_Relief[i] = mound * moundHeight + drifts[i] * driftHeight;
                    px[i] = new Color32(m_MaskAlpha[i], (byte)(drifts[i] * 255), (byte)(mound * 255), 0);
                }
            m_MaskPx = px;
            m_Mask.SetPixels32(px);
            m_Mask.Apply(false, false);

            // ---- trail (depressions)
            float trailTexel = 0.0085f;
            m_TrailW = Mathf.Clamp(Mathf.CeilToInt(m_Rect.width / trailTexel), 64, 1024);
            m_TrailH = Mathf.Clamp(Mathf.CeilToInt(m_Rect.height / trailTexel), 64, 1024);
            m_Trail = new Texture2D(m_TrailW, m_TrailH, TextureFormat.R8, false, true) { name = "SnowTrail", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var data = m_Trail.GetPixelData<byte>(0);
            for (int i = 0; i < data.Length; i++) data[i] = 0;
            m_Trail.Apply(false, false);
            m_FadeAt = new ushort[m_TrailW * m_TrailH];
            m_TilesX = (m_TrailW + FadeTile - 1) / FadeTile;
            m_TilesY = (m_TrailH + FadeTile - 1) / FadeTile;
            m_TileLive = new bool[m_TilesX * m_TilesY];
            m_T0 = Time.time;

            // ---- mesh + material
            float vertSpacing = Mathf.Max(0.035f, Mathf.Max(m_Rect.width, m_Rect.height) / 170f);
            int gx = Mathf.Clamp(Mathf.CeilToInt(m_Rect.width / vertSpacing), 8, 220);
            int gz = Mathf.Clamp(Mathf.CeilToInt(m_Rect.height / vertSpacing), 8, 220);
            var mesh = ProcMesh.Grid(gx, gz);
            mesh.bounds = new Bounds(new Vector3(0.5f, 0.25f, 0.5f), new Vector3(1f, 0.6f, 1f));   // room for the big drifts
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = gameObject.AddComponent<MeshRenderer>();
            m_Mat = new Material(FrostAssets.Shader(FrostAssets.ShaderId.Snow)) { name = "Snow" };
            m_Mat.SetColor("_SnowColor", Palette.Snow);
            m_Mat.SetColor("_ShadeColor", Palette.SnowShade);
            m_Mat.SetColor("_DeepColor", Palette.SnowPrint);
            m_Mat.SetTexture("_MaskMap", m_Mask);
            m_Mat.SetTexture("_TrailMap", m_Trail);
            m_Mat.SetFloat("_Thickness", thickness);
            m_Mat.SetFloat("_DriftHeight", driftHeight);
            m_Mat.SetFloat("_MoundHeight", moundHeight);
            m_Mat.SetFloat("_BigDriftHeight", bigDriftHeight);
            m_Mat.SetFloat("_TexelSize", 1f / Mathf.Max(m_TrailW, m_TrailH));
            mr.sharedMaterial = m_Mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;

            // a thin collider so snowballs and dropped things land on the snow
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0.5f, thickness * 0.5f, 0.5f);
            col.size = new Vector3(1f, thickness, 1f);
        }

        void LateUpdate()
        {
            if (Time.time >= m_NextFade)
            {
                m_NextFade = Time.time + FadeTick;
                FadePrints();
            }
            if (m_MaskDirty)
            {
                m_Mask.SetPixels32(m_MaskPx);
                m_Mask.Apply(false, false);
                m_MaskDirty = false;
            }
            // batch texture uploads (at most ~30/s)
            if (m_TrailDirty && Time.unscaledTime - m_LastApply > 0.033f)
            {
                m_Trail.Apply(false, false);
                m_TrailDirty = false;
                m_LastApply = Time.unscaledTime;
            }
        }

        public Vector2 WorldToUV(Vector3 world) =>
            new Vector2((world.x - m_Rect.xMin) / m_Rect.width, (world.z - m_Rect.yMin) / m_Rect.height);

        /// <summary>True where there is snow (inside the room, not under furniture).</summary>
        public bool HasSnowAt(Vector3 world)
        {
            if (m_MaskAlpha == null) return false;
            var uv = WorldToUV(world);
            if (uv.x < 0 || uv.y < 0 || uv.x >= 1 || uv.y >= 1) return false;
            int x = Mathf.Clamp((int)(uv.x * m_MaskW), 0, m_MaskW - 1), y = Mathf.Clamp((int)(uv.y * m_MaskH), 0, m_MaskH - 1);
            return m_MaskAlpha[y * m_MaskW + x] > 140;
        }

        /// <summary>Stamps a depression into the snow. size in metres (length of the shape), depth 0..1. It holds
        /// for a while (boots and sled runners ~40 s, scoops and craters longer) and then fills back in, as if
        /// fresh snow were settling into it.</summary>
        public void Stamp(Vector3 world, float yawDegrees, StampShape shape, float size, float depth = 1f)
        {
            if (m_Trail == null || !HasSnowAt(world)) return;
            var tune = FrostTuning.I;
            float hold = shape == StampShape.Scoop || shape == StampShape.Crater ? tune.divotHoldSeconds : tune.printHoldSeconds;
            ushort fadeAt = Ticks(Time.time + hold);
            var data = m_Trail.GetPixelData<byte>(0);
            float texelM = m_Rect.width / m_TrailW;
            int radius = Mathf.CeilToInt(size * 0.75f / texelM) + 1;
            var uv = WorldToUV(world);
            int cx = Mathf.RoundToInt(uv.x * m_TrailW), cy = Mathf.RoundToInt(uv.y * m_TrailH);
            float rad = yawDegrees * Mathf.Deg2Rad;
            float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                if (y < 0 || y >= m_TrailH) continue;
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    if (x < 0 || x >= m_TrailW) continue;
                    // local metres, rotated into the stamp frame (+z = forward)
                    float wx = (x - cx) * texelM, wz = (y - cy) * texelM;
                    float lx = wx * cs - wz * sn, lz = wx * sn + wz * cs;
                    float v = ShapeValue(shape, lx / size, lz / size) * depth;
                    if (v <= 0.004f) continue;
                    int i = y * m_TrailW + x;
                    byte nb = (byte)Mathf.Clamp(v * 255f, 0, 255);
                    if (nb > data[i]) data[i] = nb;
                    if (fadeAt > m_FadeAt[i]) m_FadeAt[i] = fadeAt;
                    int tile = (y / FadeTile) * m_TilesX + x / FadeTile;
                    if (!m_TileLive[tile]) { m_TileLive[tile] = true; m_LiveTiles.Add(tile); }
                }
            }
            m_TrailDirty = true;
        }

        ushort Ticks(float time) => (ushort)Mathf.Clamp(Mathf.CeilToInt((time - m_T0) / FadeTick), 0, ushort.MaxValue);

        /// <summary>Old prints get shallower a step at a time until they're gone (fully over printFadeSeconds).</summary>
        void FadePrints()
        {
            if (m_Trail == null || m_LiveTiles.Count == 0) return;
            var data = m_Trail.GetPixelData<byte>(0);
            ushort now = Ticks(Time.time);
            int step = Mathf.Max(1, Mathf.RoundToInt(255f * FadeTick / Mathf.Max(1f, FrostTuning.I.printFadeSeconds)));
            bool changed = false;
            for (int k = m_LiveTiles.Count - 1; k >= 0; k--)
            {
                int tile = m_LiveTiles[k];
                int x0 = (tile % m_TilesX) * FadeTile, y0 = (tile / m_TilesX) * FadeTile;
                int x1 = Mathf.Min(x0 + FadeTile, m_TrailW), y1 = Mathf.Min(y0 + FadeTile, m_TrailH);
                bool any = false;
                for (int y = y0; y < y1; y++)
                {
                    int row = y * m_TrailW;
                    for (int x = x0; x < x1; x++)
                    {
                        int i = row + x;
                        byte v = data[i];
                        if (v == 0) continue;
                        if (now >= m_FadeAt[i])
                        {
                            v = (byte)Mathf.Max(0, v - step);
                            data[i] = v;
                            changed = true;
                        }
                        if (v > 0) any = true;
                    }
                }
                if (!any)
                {
                    m_TileLive[tile] = false;
                    m_LiveTiles[k] = m_LiveTiles[m_LiveTiles.Count - 1];
                    m_LiveTiles.RemoveAt(m_LiveTiles.Count - 1);
                }
            }
            if (changed) m_TrailDirty = true;
        }

        /// <summary>Heaps a big soft drift into the snow (the scenery's mounds and the snow banked around tree trunks).
        /// radius in metres along its long axis; squash shortens the other axis; height01 is 0..1 of bigDriftHeight.</summary>
        public void AddDrift(Vector3 world, float radius, float height01, float squash = 1f, float yawDegrees = 0f, int seed = 0)
        {
            if (m_MaskPx == null || radius <= 0.01f) return;
            float texX = m_Rect.width / m_MaskW, texZ = m_Rect.height / m_MaskH;
            int rx = Mathf.CeilToInt(radius / texX) + 1, rz = Mathf.CeilToInt(radius / texZ) + 1;
            var uv = WorldToUV(world);
            int cx = Mathf.RoundToInt(uv.x * m_MaskW - 0.5f), cy = Mathf.RoundToInt(uv.y * m_MaskH - 0.5f);
            float rad = yawDegrees * Mathf.Deg2Rad, cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
            float sq = Mathf.Clamp(squash, 0.3f, 1f);
            float ox = seed * 3.17f, oz = seed * 7.31f;
            for (int y = cy - rz; y <= cy + rz; y++)
            {
                if (y < 0 || y >= m_MaskH) continue;
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    if (x < 0 || x >= m_MaskW) continue;
                    int i = y * m_MaskW + x;
                    if (m_MaskAlpha[i] < 40) continue;
                    var p = TexelToWorld(x, y);
                    float dx = p.x - world.x, dz = p.y - world.z;
                    float lx = dx * cs - dz * sn, lz = (dx * sn + dz * cs) / sq;
                    // a lumpy outline so it reads as wind-heaped snow, not a dome
                    float wobble = 0.82f + 0.3f * Mathf.PerlinNoise(p.x * 2.3f + ox, p.y * 2.3f + oz);
                    float t = Mathf.Sqrt(lx * lx + lz * lz) / (radius * wobble);
                    if (t >= 1f) continue;
                    float h = Smooth(Smooth(1f - t)) * height01 * (0.9f + 0.2f * Mathf.PerlinNoise(p.x * 5.1f + oz, p.y * 5.1f + ox));
                    h *= m_MaskAlpha[i] / 255f;
                    byte nb = (byte)Mathf.Clamp(h * 255f, 0f, 255f);
                    if (nb <= m_MaskPx[i].a) continue;
                    m_MaskPx[i].a = nb;
                    m_Relief[i] = m_MaskPx[i].b / 255f * moundHeight + m_MaskPx[i].g / 255f * driftHeight + nb / 255f * bigDriftHeight;
                }
            }
            m_MaskDirty = true;
        }

        /// <summary>Normalised shape in a 1×1 box centred on the origin (x across, z along).</summary>
        static float ShapeValue(StampShape shape, float x, float z)
        {
            switch (shape)
            {
                case StampShape.BootLeft:
                case StampShape.BootRight:
                case StampShape.KidBootLeft:
                case StampShape.KidBootRight:
                {
                    // sole: a ball (front) and a heel (back) joined by a waist, with tread lines
                    float side = (shape == StampShape.BootLeft || shape == StampShape.KidBootLeft) ? 1f : -1f;
                    float ball = Ellipse(x - 0.03f * side, z - 0.18f, 0.2f, 0.26f);
                    float heel = Ellipse(x, z + 0.27f, 0.16f, 0.19f);
                    float waist = Ellipse(x - 0.01f * side, z - 0.02f, 0.13f, 0.3f);
                    float v = Mathf.Max(ball, Mathf.Max(heel, waist));
                    float tread = 0.82f + 0.18f * Mathf.Abs(Mathf.Sin(z * 40f));
                    return v * tread;
                }
                case StampShape.Scoop:
                {
                    float r = Mathf.Sqrt(x * x + z * z) / 0.5f;
                    return r < 1f ? Mathf.SmoothStep(1f, 0.4f, r) : 0f;
                }
                case StampShape.Crater:
                {
                    float r = Mathf.Sqrt(x * x + z * z) / 0.5f;
                    return r < 1f ? 0.7f * (1f - r * r) : 0f;
                }
                case StampShape.SledTrack:
                {
                    // two runners
                    float runner = Mathf.Max(Band(x - 0.28f, 0.07f), Band(x + 0.28f, 0.07f));
                    return Mathf.Abs(z) < 0.5f ? runner * 0.7f : 0f;
                }
            }
            return 0f;
        }

        static float Ellipse(float x, float z, float rx, float rz)
        {
            float d = (x * x) / (rx * rx) + (z * z) / (rz * rz);
            return d < 1f ? Mathf.SmoothStep(1f, 0.55f, d) : 0f;
        }

        static float Band(float x, float w) => Mathf.Abs(x) < w ? 1f - Mathf.Abs(x) / w * 0.4f : 0f;

        /// <summary>Snow surface height at a point, including mounds and wall drifts (ignores the small dunes and prints).</summary>
        public float HeightAt(Vector3 world) => SurfaceHeightAt(world);

        public float SurfaceHeightAt(Vector3 world)
        {
            float floor = m_Room != null ? m_Room.FloorY : 0f;
            if (m_MaskAlpha == null || m_Relief == null) return floor + thickness;
            var uv = WorldToUV(world);
            if (uv.x < 0 || uv.y < 0 || uv.x >= 1 || uv.y >= 1) return floor;
            int i = Mathf.Clamp((int)(uv.y * m_MaskH), 0, m_MaskH - 1) * m_MaskW + Mathf.Clamp((int)(uv.x * m_MaskW), 0, m_MaskW - 1);
            return m_MaskAlpha[i] > 140 ? floor + 0.002f + thickness + m_Relief[i] : floor;
        }

        Vector2 TexelToWorld(int x, int y) =>
            new Vector2(Mathf.Lerp(m_Rect.xMin, m_Rect.xMax, (x + 0.5f) / m_MaskW), Mathf.Lerp(m_Rect.yMin, m_Rect.yMax, (y + 0.5f) / m_MaskH));

        static float Smooth(float t) => t * t * (3f - 2f * t);

        void OnDestroy()
        {
            if (m_Mask) Destroy(m_Mask);
            if (m_Trail) Destroy(m_Trail);
            if (m_Mat) Destroy(m_Mat);
        }
    }
}
