using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Snow settles on your real furniture too: a soft, sparkling layer on the table, the top of a low cabinet, the bed
    /// and the TV, shaped from Space Setup's volumes (nothing when the room has no furniture marked). Couches are left
    /// alone (their box top is the backrest, not where snow would lie), and so is anything above about chest height,
    /// which you'd see from underneath.
    /// </summary>
    public static class FurnitureSnow
    {
        public static GameObject Create(RoomInfo room)
        {
            var go = new GameObject("Furniture Snow");
            if (room == null || room.Furniture.Count == 0) return go;
            var mat = new Material(Palette.Toon(Palette.Snow, false, 0.35f)) { name = "Furniture Snow" };
            mat.SetColor("_ShadeColor", Palette.SnowShade);
            mat.SetFloat("_Sparkle", 0.7f);
            mat.SetColor("_SparkleColor", Color.white);
            int n = 0;
            foreach (var f in room.Furniture)
            {
                if (f.Kind == FurnitureKind.Couch || f.Kind == FurnitureKind.Lamp || f.Kind == FurnitureKind.Plant) continue;
                float top = f.TopY - room.FloorY;
                if (top < 0.25f || top > 1.35f) continue;
                float hx = f.HalfWidth - 0.012f, hz = f.HalfDepth - 0.012f;   // a hair inside the real edges
                if (hx < 0.04f || hz < 0.03f) continue;
                float thick = Mathf.Clamp(Mathf.Min(hx, hz) * 0.12f, 0.018f, 0.045f);
                var part = new GameObject("Snow on " + f.Kind);
                part.transform.SetParent(go.transform, false);
                part.transform.SetPositionAndRotation(new Vector3(f.Center.x, f.TopY + 0.004f, f.Center.z), Quaternion.LookRotation(f.Forward, Vector3.up));
                part.AddComponent<MeshFilter>().sharedMesh = Pillow(hx, hz, thick, n * 17 + 5);
                var mr = part.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // real furniture casts no virtual shadow
                mr.receiveShadows = true;
                n++;
            }
            Debug.Log($"[Frost] Snow settled on {n} piece(s) of your furniture");
            if (n > 0) go.AddComponent<FurnitureSnowSettle>();
            return go;
        }

        /// <summary>A heightfield of settled snow over a hx×hz footprint: flat-ish on top with soft lumps, rounding
        /// down to nothing at the edges (local x across, z along, y up).</summary>
        static Mesh Pillow(float hx, float hz, float thick, int seed)
        {
            int nx = Mathf.Clamp(Mathf.CeilToInt(hx * 2f / 0.05f), 6, 40), nz = Mathf.Clamp(Mathf.CeilToInt(hz * 2f / 0.05f), 6, 40);
            float edge = Mathf.Min(0.07f, 0.4f * Mathf.Min(hx, hz));   // how far in from the edge it takes to reach full depth
            var verts = new List<Vector3>((nx + 1) * (nz + 1));
            var uvs = new List<Vector2>(verts.Capacity);
            var rng = new System.Random(seed);
            float ox = (float)rng.NextDouble() * 50f, oz = (float)rng.NextDouble() * 50f;
            for (int z = 0; z <= nz; z++)
                for (int x = 0; x <= nx; x++)
                {
                    float u = x / (float)nx, v = z / (float)nz;
                    float px = Mathf.Lerp(-hx, hx, Bias(u)), pz = Mathf.Lerp(-hz, hz, Bias(v));
                    float dx = hx - Mathf.Abs(px), dz = hz - Mathf.Abs(pz);
                    float rim = Smooth(dx / edge) * Smooth(dz / edge);
                    float lumps = 0.82f + 0.3f * Mathf.PerlinNoise(ox + px * 5f, oz + pz * 5f);
                    verts.Add(new Vector3(px, thick * Mathf.Sqrt(rim) * lumps, pz));
                    uvs.Add(new Vector2(u, v));
                }
            var tris = new List<int>(nx * nz * 6);
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    int a = z * (nx + 1) + x, b = a + nx + 1;
                    tris.Add(a); tris.Add(b); tris.Add(a + 1);
                    tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
                }
            var m = new Mesh { name = "Furniture Snow" };
            m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // grid lines bunch up towards the edges, where the snow rounds off
        static float Bias(float u) => 0.5f + 0.5f * Mathf.Sign(u - 0.5f) * Mathf.Pow(Mathf.Abs(u - 0.5f) * 2f, 0.65f);
        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    }

    /// <summary>The furniture's snow comes and goes with the floor's (none on the title screen; it settles in during
    /// the opening with the rest).</summary>
    public class FurnitureSnowSettle : MonoBehaviour
    {
        Transform[] m_Parts;
        Renderer[] m_Renderers;
        float m_Shown = -1f;

        void Awake()
        {
            m_Renderers = GetComponentsInChildren<Renderer>(true);
            m_Parts = new Transform[m_Renderers.Length];
            for (int i = 0; i < m_Renderers.Length; i++) m_Parts[i] = m_Renderers[i].transform;
        }

        void LateUpdate()
        {
            var snow = SnowSurface.Instance;
            float c = snow ? Mathf.Clamp01(snow.Coverage) : 1f;
            if (Mathf.Abs(c - m_Shown) < 0.002f) return;
            m_Shown = c;
            for (int i = 0; i < m_Parts.Length; i++)
            {
                if (!m_Parts[i]) continue;
                m_Renderers[i].enabled = c > 0.01f;
                var s = m_Parts[i].localScale;
                m_Parts[i].localScale = new Vector3(s.x, Mathf.Max(0.001f, c), s.z);   // the layer thickens as it settles
            }
        }
    }
}
