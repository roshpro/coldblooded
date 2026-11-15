using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// The reward for closing the case: the northern lights over the room. Six tall curtains of light hang in the sky
    /// beyond the walls and ceiling. Each has a crisp, bright lower edge with fine rays rising out of it to ragged tops:
    /// green low down, cooling through teal and blue to violet and rose up high, with a pink fringe along the bottom
    /// when the display flares. Over time some curtains drift to teal, blue-violet or pink and back. They fold and
    /// ripple slowly while brightness runs along them; the folds that turn edge-on to you burn brightest (you look
    /// through more of the curtain, as with the real thing), and every so often the whole sky surges. Each curtain is
    /// two layers of rays drifting against each other (so they shimmer) in a soft glow. A few glowing motes drift up
    /// out of the snow and the stars come out. The real room dims to half while it's up, a glassy pad swells in and
    /// the kids look up. It stays up behind the Case File until you restart (which reloads the scene and the usual
    /// passthrough).
    /// Cheap enough for Quest: about 6,000 vertices animated on the CPU, all additive, and one small particle system.
    /// </summary>
    public class NorthernLights : MonoBehaviour
    {
        public static NorthernLights Instance { get; private set; }

        const float DimDuring = 0.5f;
        const int Cols = 64;
        const float RevealSeconds = 6f;
        // the rows up a curtain, as a fraction of its height: pink fringe, bright edge, low, middle, upper, top
        const int Rows = 6;
        static readonly float[] s_RowV = { 0f, 0.035f, 0.2f, 0.45f, 0.72f, 1f };
        static readonly float[] s_RowAlpha = { 0.3f, 0.8f, 0.6f, 0.32f, 0.14f, 0f };
        // the colours up a curtain (fringe, edge, low, middle, upper, top) in four moods it drifts through: classic green,
        // teal, blue-violet and pink. Each step goes round through teal, blue and violet rather than through grey.
        static readonly Color[,] s_Moods =
        {
            { new Color(1f, 0.32f, 0.62f), new Color(0.62f, 1f, 0.75f), new Color(0.2f, 1f, 0.5f), new Color(0.12f, 0.9f, 0.7f), new Color(0.35f, 0.55f, 1f), new Color(0.85f, 0.35f, 0.9f) },
            { new Color(0.68f, 0.38f, 1f), new Color(0.55f, 1f, 0.95f), new Color(0.12f, 0.88f, 0.92f), new Color(0.25f, 0.6f, 1f), new Color(0.6f, 0.4f, 1f), new Color(0.95f, 0.35f, 0.85f) },
            { new Color(1f, 0.35f, 0.7f), new Color(0.75f, 0.75f, 1f), new Color(0.45f, 0.5f, 1f), new Color(0.6f, 0.4f, 1f), new Color(0.85f, 0.35f, 0.95f), new Color(1f, 0.35f, 0.75f) },
            { new Color(0.7f, 0.35f, 1f), new Color(1f, 0.65f, 0.88f), new Color(1f, 0.4f, 0.75f), new Color(0.85f, 0.35f, 0.95f), new Color(0.5f, 0.4f, 1f), new Color(0.3f, 0.6f, 1f) },
        };

        class Sheet
        {
            public Mesh mesh;
            public Vector3[] v;
            public Color32[] c;
            public int rows;
        }

        class Curtain
        {
            public float a0, span, r, baseY, height, phase, drift, delay, moodMax;
            public readonly Vector3[] pos = new Vector3[Cols + 1], nrm = new Vector3[Cols + 1];
            public readonly float[] bottom = new float[Cols + 1], top = new float[Cols + 1];
            public readonly float[] bright = new float[Cols + 1], soft = new float[Cols + 1];
            public readonly Color[] col = new Color[(Cols + 1) * Rows];   // each column's colours, bottom to top
            public Sheet front, back, glow;
        }

        readonly List<Curtain> m_Curtains = new List<Curtain>();
        readonly List<Object> m_Owned = new List<Object>();   // meshes and materials made here, cleaned up with it
        Material m_FrontMat, m_BackMat;
        ParticleSystem m_Motes;
        float m_MotesDue;
        Mesh m_Stars;
        Material m_CeilingMat;   // night over your real ceiling (headset), so the sky reads as overhead
        Color[] m_StarColors;
        float[] m_StarPhase, m_StarSpeed, m_StarBase;
        Vector3 m_Center;
        Quaternion m_Frame;
        float m_Born, m_Floor, m_Ceiling;
        readonly Color[] m_Row = new Color[Rows];

        /// <summary>Lights up the sky (once), centred on where you're standing and looking. With `moment`, the full
        /// show straight away (the room dims, the shimmer, everyone looks up); without, the lights just fade in and
        /// stay up quietly (they come in with the intro's snow and hang there all game) until <see cref="Finale"/>.</summary>
        public static NorthernLights Begin(bool moment = true)
        {
            if (Instance) { if (moment) Finale(); return Instance; }
            var go = new GameObject("Northern Lights");
            var nl = go.AddComponent<NorthernLights>();
            nl.Build();
            if (moment) nl.StartCoroutine(nl.Moment());
            return nl;
        }

        /// <summary>CASE CLOSED: the big moment under lights that have been up all along (or lit now, if they weren't).</summary>
        public static void Finale()
        {
            if (!Instance) { Begin(true); return; }
            if (Instance.m_Finale) return;
            Instance.StartCoroutine(Instance.Moment());
        }

        bool m_Finale;
        float m_FinaleAt = -1f;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var o in m_Owned) if (o) Destroy(o);
        }

        void Build()
        {
            Instance = this;
            m_Born = Time.time;
            var rig = PlayerRig.Active;
            var room = RoomService.Current;
            if (rig && rig.ViewCamera) rig.ViewCamera.farClipPlane = Mathf.Max(rig.ViewCamera.farClipPlane, 80f);   // the stars are 32 m out
            m_Center = rig ? rig.FeetPosition : (room != null ? room.Center : Vector3.zero);
            m_Frame = Quaternion.LookRotation(rig ? rig.FlatForward : Vector3.forward, Vector3.up);
            m_Floor = room != null ? room.FloorY : m_Center.y;
            m_Ceiling = room != null ? room.CeilingY : m_Floor + 2.5f;

            var rays = Palette.Particle(RayTexture(), true, "Aurora Rays");
            m_FrontMat = Own(new Material(rays) { name = "Aurora Rays (front)" });
            m_BackMat = Own(new Material(rays) { name = "Aurora Rays (back)" });
            var glowMat = Palette.Particle(Texture2D.whiteTexture, true, "Aurora Glow");

            // (angle from where you face, width in degrees, distance, base height, height, how far it drifts towards pink, fade-in delay)
            var layout = new (float at, float span, float r, float baseY, float h, float mood, float delay)[]
            {
                (0f, 85f, 14f, 5.5f, 12f, 0.5f, 0f),
                (42f, 60f, 18f, 6.5f, 13f, 0.85f, 0.8f),
                (-40f, 65f, 19f, 6.5f, 13f, 0.7f, 1.4f),
                (95f, 55f, 21f, 7.5f, 14f, 1f, 2.2f),
                (-100f, 60f, 22f, 7.5f, 14f, 0.6f, 2.8f),
                (180f, 80f, 20f, 7f, 13f, 0.8f, 3.4f),
            };
            for (int i = 0; i < layout.Length; i++)
            {
                var l = layout[i];
                var cu = new Curtain
                {
                    a0 = l.at * Mathf.Deg2Rad, span = l.span * Mathf.Deg2Rad, r = l.r, baseY = l.baseY, height = l.h,
                    phase = i * 1.7f + 0.4f, drift = (i % 2 == 0 ? 0.25f : -0.2f) * Mathf.Deg2Rad, delay = l.delay, moodMax = l.mood,
                };
                float uScale = cu.span * cu.r / 8f;   // the ray pattern repeats every 8 m along a curtain
                cu.front = MakeSheet("Aurora Curtain " + i, m_FrontMat, Rows, s_RowV, uScale, 0f);
                cu.back = MakeSheet("Aurora Curtain " + i + " (back)", m_BackMat, Rows, s_RowV, uScale * 1.15f, 0.37f);
                cu.glow = MakeSheet("Aurora Curtain " + i + " (glow)", glowMat, 4, new[] { 0f, 0.3f, 0.6f, 1f }, 1f, 0f);
                m_Curtains.Add(cu);
            }
            BuildMotes();
            BuildStars();
            BuildCeilingShade();
            DepthOcclusion.Exempt(gameObject);   // the sky lives beyond the real walls and ceiling: never hidden by them
            Update();   // in place before the first frame draws
        }

        T Own<T>(T o) where T : Object { m_Owned.Add(o); return o; }

        Sheet MakeSheet(string name, Material mat, int rows, float[] rowV, float uScale, float uOffset)
        {
            var sh = new Sheet { rows = rows };
            int n = (Cols + 1) * rows;
            sh.v = new Vector3[n];
            sh.c = new Color32[n];
            var uv = new Vector2[n];
            for (int j = 0; j <= Cols; j++)
                for (int k = 0; k < rows; k++)
                    uv[j * rows + k] = new Vector2(uOffset + uScale * j / Cols, rowV[k]);
            var tris = new int[Cols * (rows - 1) * 6];
            int t = 0;
            for (int j = 0; j < Cols; j++)
                for (int k = 0; k < rows - 1; k++)
                {
                    int a = j * rows + k, b = a + rows;
                    tris[t++] = a; tris[t++] = a + 1; tris[t++] = b;
                    tris[t++] = b; tris[t++] = a + 1; tris[t++] = b + 1;
                }
            sh.mesh = Own(new Mesh { name = name });
            sh.mesh.MarkDynamic();
            sh.mesh.vertices = sh.v;
            sh.mesh.uv = uv;
            sh.mesh.colors32 = sh.c;
            sh.mesh.triangles = tris;
            sh.mesh.bounds = new Bounds(m_Center + Vector3.up * 12f, Vector3.one * 90f);   // it never leaves this box
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = sh.mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return sh;
        }

        /// <summary>How lively the display is: a slow swell and fade, the big surge as it arrives, and a flare every half minute.</summary>
        static float Activity(float t)
        {
            float calm = 0.8f + 0.25f * (Mathf.PerlinNoise(t * 0.06f, 3.7f) * 2f - 1f);
            float arrival = 0.55f * Mathf.Exp(-Sq((t - 9f) / 3f));
            float flares = 0.45f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 0.19f - 2.2f)), 10f);
            return calm + arrival + flares;
        }

        static float Sq(float x) => x * x;

        void Animate(Curtain cu, float t, Vector3 eye, float activity)
        {
            float reveal = Mathf.SmoothStep(0f, 1f, (t - cu.delay) / RevealSeconds);
            float ph = cu.phase, R = cu.r;
            // the line it hangs from, folding and rippling, and how tall it reaches (it grows up out of its edge as it arrives)
            for (int j = 0; j <= Cols; j++)
            {
                float s = j / (float)Cols, x = s * 6.2832f;
                float a = cu.a0 + cu.span * (s - 0.5f) + cu.drift * t;
                float fold = 0.12f * R * Mathf.Sin(1.3f * x + 0.12f * t + ph)
                           + 0.05f * R * Mathf.Sin(3.1f * x - 0.25f * t + 1.7f * ph)
                           + 0.018f * R * (0.6f + 0.4f * activity) * Mathf.Sin(7.3f * x + 0.9f * t + 2.9f * ph);
                var flat = m_Frame * new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                cu.pos[j] = new Vector3(m_Center.x + flat.x * (R + fold), 0f, m_Center.z + flat.z * (R + fold));
                cu.bottom[j] = m_Floor + cu.baseY + 0.35f * Mathf.Sin(2.2f * x + 0.3f * t + ph) + 0.12f * Mathf.Sin(9f * x + 1.6f * t + ph);
                float tall = cu.height * (0.72f + 0.28f * Mathf.PerlinNoise(s * 2.5f + t * 0.04f, ph)) * Mathf.Lerp(0.2f, 1f, reveal);
                cu.top[j] = cu.bottom[j] + tall;
            }
            // brightness: soft ends, patches of light running along it, and the folds that turn edge-on to you burn brightest
            for (int j = 0; j <= Cols; j++)
            {
                float s = j / (float)Cols;
                var T = cu.pos[Mathf.Min(j + 1, Cols)] - cu.pos[Mathf.Max(j - 1, 0)];
                T.y = 0f;
                T.Normalize();
                var N = new Vector3(T.z, 0f, -T.x);   // points back towards you, more or less
                cu.nrm[j] = N;
                var mid = new Vector3(cu.pos[j].x, Mathf.Lerp(cu.bottom[j], cu.top[j], 0.3f), cu.pos[j].z);
                float facing = Mathf.Abs(Vector3.Dot(N, (mid - eye).normalized));
                float foldGlow = Mathf.Lerp(2.3f, 0.75f, Mathf.SmoothStep(0f, 1f, facing / 0.6f));
                float ends = Mathf.SmoothStep(0f, 1f, s / 0.14f) * Mathf.SmoothStep(0f, 1f, (1f - s) / 0.14f);
                float dance = 0.5f + 0.5f * Mathf.PerlinNoise(s * 3.2f - t * 0.35f, ph + 5f);
                cu.soft[j] = reveal * ends * dance * activity;
                cu.bright[j] = cu.soft[j] * foldGlow;
            }
            // colour: green most of the time, drifting to teal, blue-violet or pink and back, in patches along it
            float mood = cu.moodMax * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 0.15f + ph * 2.3f), 1.7f);
            for (int j = 0; j <= Cols; j++)
            {
                RowColours(mood + 0.35f * (Mathf.PerlinNoise(j / (float)Cols * 1.4f + t * 0.03f, ph + 9f) - 0.5f));
                System.Array.Copy(m_Row, 0, cu.col, j * Rows, Rows);
            }
            float fringe = 0.4f + 0.8f * Mathf.Max(0f, activity - 0.9f);   // the pink lower fringe shows when it flares
            FillRays(cu, cu.front, 0f, 1f, 1f, fringe);
            FillRays(cu, cu.back, 0.9f, 0.9f, 0.55f, fringe);
            FillGlow(cu);
        }

        void RowColours(float m)
        {
            int last = s_Moods.GetLength(0) - 1;
            m = Mathf.Clamp01(m) * last;
            int i = Mathf.Min((int)m, last - 1);
            float f = m - i;
            f = f * f * (3f - 2f * f);
            for (int k = 0; k < Rows; k++) m_Row[k] = Color.Lerp(s_Moods[i, k], s_Moods[i + 1, k], f);
        }

        void FillRays(Curtain cu, Sheet sh, float behind, float tallness, float gain, float fringe)
        {
            for (int j = 0; j <= Cols; j++)
            {
                var p = cu.pos[j] - cu.nrm[j] * behind;
                float b = cu.bottom[j] + behind * 0.15f;
                float h = (cu.top[j] - cu.bottom[j]) * tallness;
                float br = cu.bright[j] * gain;
                for (int k = 0; k < Rows; k++)
                {
                    int i = j * Rows + k;
                    sh.v[i] = new Vector3(p.x, b + h * s_RowV[k], p.z);
                    var c = cu.col[i];
                    c.a = Mathf.Clamp01(s_RowAlpha[k] * br * (k == 0 ? fringe : 1f));
                    sh.c[i] = c;
                }
            }
            sh.mesh.vertices = sh.v;
            sh.mesh.colors32 = sh.c;
        }

        /// <summary>A soft haze of the curtain's own light around its bright lower part (a little below it, and fading out
        /// well before the tops).</summary>
        void FillGlow(Curtain cu)
        {
            var sh = cu.glow;
            for (int j = 0; j <= Cols; j++)
            {
                var p = cu.pos[j] - cu.nrm[j] * 0.4f;
                float b = cu.bottom[j], h = cu.top[j] - b, glow = cu.soft[j];
                int i = j * 4;
                sh.v[i] = new Vector3(p.x, b - 1f, p.z);
                sh.v[i + 1] = new Vector3(p.x, b + 0.15f * h, p.z);
                sh.v[i + 2] = new Vector3(p.x, b + 0.45f * h, p.z);
                sh.v[i + 3] = new Vector3(p.x, b + 0.8f * h, p.z);
                var low = cu.col[j * Rows + 2];
                var mid = cu.col[j * Rows + 3];
                var c0 = low; c0.a = 0f;
                var c1 = low; c1.a = Mathf.Clamp01(0.2f * glow);
                var c2 = mid; c2.a = Mathf.Clamp01(0.07f * glow);
                var c3 = mid; c3.a = 0f;
                sh.c[i] = c0; sh.c[i + 1] = c1; sh.c[i + 2] = c2; sh.c[i + 3] = c3;
            }
            sh.mesh.vertices = sh.v;
            sh.mesh.colors32 = sh.c;
        }

        /// <summary>A few glowing motes drifting up out of the snow around you: green, cooling to teal, then rose as they rise.</summary>
        void BuildMotes()
        {
            m_Motes = Fx.MakeSystem("Aurora Motes", transform, FxTextures.Glow, true, 110);
            var mr = m_Motes.GetComponent<ParticleSystemRenderer>();
            mr.sharedMaterial.SetFloat("_NearFade", 0.5f);   // none right in front of your eyes
            var nz = m_Motes.noise; nz.enabled = true;
            nz.strength = 0.14f; nz.frequency = 0.5f; nz.scrollSpeed = 0.15f;
            nz.damping = true; nz.quality = ParticleSystemNoiseQuality.Medium;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(s_Moods[0, 2], 0f), new GradientColorKey(s_Moods[1, 2], 0.5f), new GradientColorKey(s_Moods[3, 2], 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.15f), new GradientAlphaKey(0.6f, 0.75f), new GradientAlphaKey(0f, 1f) });
            var col = m_Motes.colorOverLifetime; col.enabled = true;
            col.color = g;
            var sol = m_Motes.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0.3f)));
        }

        void Mote()
        {
            float a = Random.Range(0f, Mathf.PI * 2f), d = Random.Range(0.8f, 3.2f);
            float hi = Mathf.Clamp(m_Ceiling - m_Floor - 0.6f, 0.5f, 1.6f);
            var p = new Vector3(m_Center.x + Mathf.Cos(a) * d, m_Floor + Random.Range(0.05f, hi), m_Center.z + Mathf.Sin(a) * d);
            var room = RoomService.Current;
            if (room != null) p = room.ClampInside(p, 0.15f);
            m_Motes.Emit(new ParticleSystem.EmitParams
            {
                position = p,
                velocity = new Vector3(Random.Range(-0.03f, 0.03f), Random.Range(0.06f, 0.14f), Random.Range(-0.03f, 0.03f)),
                startSize = Random.Range(0.025f, 0.06f),
                startLifetime = Random.Range(6f, 9f),
                startColor = Color.white,
            }, 1);
        }

        /// <summary>A soft night-blue veil over your real ceiling (headset only; the desktop room lifts its ceiling off
        /// instead): darkest in the middle, lighter towards the walls, drawn before the aurora so the lights and stars
        /// stay bright on top of it.</summary>
        void BuildCeilingShade()
        {
            var rig = PlayerRig.Active;
            var room = RoomService.Current;
            if (!rig || !rig.IsXR || room == null || room.Polygon.Count < 3) return;
            float y = room.CeilingY - 0.02f;
            var c = room.Centroid;
            var verts = new List<Vector3> { new Vector3(c.x, y, c.y) };
            var cols = new List<Color> { new Color(1f, 1f, 1f, 1f) };
            foreach (var p in room.Polygon)
            {
                // a little short of the walls, where the veil thins out
                var q = Vector2.Lerp(p, c, 0.04f);
                verts.Add(new Vector3(q.x, y, q.y));
                cols.Add(new Color(1f, 1f, 1f, 0.55f));
            }
            var tris = new List<int>();
            for (int i = 1; i < verts.Count; i++) { tris.Add(0); tris.Add(i); tris.Add(i == verts.Count - 1 ? 1 : i + 1); }
            var mesh = Own(new Mesh { name = "Ceiling Shade" });
            mesh.SetVertices(verts); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            m_CeilingMat = Own(new Material(Palette.Particle(Texture2D.whiteTexture, false, "Ceiling Shade")) { name = "Ceiling Shade" });
            m_CeilingMat.SetColor("_BaseColor", new Color(0.015f, 0.025f, 0.07f, 0f));
            m_CeilingMat.renderQueue = 2990;   // before the aurora's additive layers
            var go = new GameObject("Ceiling Shade");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m_CeilingMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        void BuildStars()
        {
            const int N = 180;
            var rng = new System.Random(5);
            var v = new Vector3[N * 4]; var uv = new Vector2[N * 4]; var tris = new int[N * 6];
            m_StarColors = new Color[N * 4];
            m_StarPhase = new float[N]; m_StarSpeed = new float[N]; m_StarBase = new float[N];
            var sky = m_Center + Vector3.up * 1.6f;
            for (int i = 0; i < N; i++)
            {
                float az = (float)rng.NextDouble() * Mathf.PI * 2f;
                float el = Mathf.Lerp(12f, 82f, Mathf.Sqrt((float)rng.NextDouble())) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az));
                var pos = sky + dir * 32f;
                float size = Mathf.Lerp(0.08f, 0.28f, (float)(rng.NextDouble() * rng.NextDouble()));
                var right = Vector3.Cross(Vector3.up, dir).normalized;
                if (right.sqrMagnitude < 1e-4f) right = Vector3.right;
                var up = Vector3.Cross(dir, right).normalized;
                right *= size; up *= size;
                int b = i * 4;
                v[b] = pos - right - up; v[b + 1] = pos + right - up; v[b + 2] = pos - right + up; v[b + 3] = pos + right + up;
                uv[b] = new Vector2(0, 0); uv[b + 1] = new Vector2(1, 0); uv[b + 2] = new Vector2(0, 1); uv[b + 3] = new Vector2(1, 1);
                tris[i * 6] = b; tris[i * 6 + 1] = b + 2; tris[i * 6 + 2] = b + 1; tris[i * 6 + 3] = b + 2; tris[i * 6 + 4] = b + 3; tris[i * 6 + 5] = b + 1;
                m_StarPhase[i] = (float)rng.NextDouble() * 6.28f;
                m_StarSpeed[i] = Mathf.Lerp(0.8f, 2.6f, (float)rng.NextDouble());
                m_StarBase[i] = Mathf.Lerp(0.2f, 0.75f, (float)rng.NextDouble());
            }
            m_Stars = Own(new Mesh { name = "Aurora Stars" });
            m_Stars.MarkDynamic();
            m_Stars.vertices = v; m_Stars.uv = uv; m_Stars.colors = m_StarColors; m_Stars.triangles = tris;
            m_Stars.bounds = new Bounds(sky, Vector3.one * 70f);
            var go = new GameObject("Stars");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = m_Stars;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Palette.Particle(FxTextures.SoftDot, true, "Aurora Stars");
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        void Update()
        {
            float t = Time.time - m_Born;
            var rig = PlayerRig.Active;
            var eye = rig && rig.ViewCamera ? rig.ViewCamera.transform.position : m_Center + Vector3.up * 1.6f;
            float activity = Activity(t);
            // CASE CLOSED: the curtains that have hung there all game swell up again
            if (m_FinaleAt >= 0f && Time.time - m_FinaleAt > 0f) activity += 0.6f * Mathf.Exp(-Sq((Time.time - m_FinaleAt - 3f) / 3f));
            foreach (var cu in m_Curtains) Animate(cu, t, eye, activity);
            // the two layers of rays drift against each other, so the curtains shimmer
            m_FrontMat.SetTextureOffset("_BaseMap", new Vector2(t * 0.02f, 0f));
            m_BackMat.SetTextureOffset("_BaseMap", new Vector2(-t * 0.013f, 0f));
            if (m_CeilingMat)
            {
                // a light veil while you play; full night over the ceiling for the finale
                float deep = m_FinaleAt < 0f ? 0f : Mathf.SmoothStep(0f, 1f, (Time.time - m_FinaleAt) / 4f);
                m_CeilingMat.SetColor("_BaseColor", new Color(0.015f, 0.025f, 0.07f, Mathf.Lerp(0.45f, 0.82f, deep) * Mathf.SmoothStep(0f, 1f, t / 4f)));
            }

            for (m_MotesDue += 9f * Mathf.SmoothStep(0f, 1f, (t - 1f) / 4f) * Time.deltaTime; m_MotesDue >= 1f; m_MotesDue -= 1f) Mote();
            if (m_Stars)
            {
                float starFade = Mathf.SmoothStep(0f, 1f, (t - 1f) / 5f);
                for (int i = 0; i < m_StarPhase.Length; i++)
                {
                    float a = m_StarBase[i] * (0.55f + 0.45f * Mathf.Sin(t * m_StarSpeed[i] + m_StarPhase[i])) * starFade;
                    var c = new Color(0.85f, 0.92f, 1f, a);
                    int b = i * 4;
                    m_StarColors[b] = m_StarColors[b + 1] = m_StarColors[b + 2] = m_StarColors[b + 3] = c;
                }
                m_Stars.colors = m_StarColors;
            }
        }

        /// <summary>The moment itself: the room dims, the pad swells, the kids look up and Lucy takes the credit.</summary>
        IEnumerator Moment()
        {
            m_Finale = true;
            m_FinaleAt = Time.time;
            var rig = PlayerRig.Active;
            if (rig) rig.SetPassthroughDim(DimDuring, 4f);
            AudioDirector.Play2D(Sfx.Aurora, 0.75f);
            AudioDirector.MusicBoost = 0.5f;
            yield return new WaitForSeconds(1.5f);
            for (int i = 0; i < 20; i++) Mote();   // a first lift of light off the snow
            // everyone looks up at the main curtain
            var look = new Vector3(m_Center.x, m_Floor + 9f, m_Center.z) + m_Frame * new Vector3(0f, 0f, 14f);
            foreach (var k in Kid.All) if (k && k.Anim) k.Anim.LookOverride = look + Random.insideUnitSphere * 2f;
            yield return new WaitForSeconds(2.5f);
            var lucy = Kid.Find(CastId.Lucy);
            var line = Lines.Get("lucy_aurora");
            if (lucy && line != null && !DialogueRunner.AnyoneSpeaking) DialogueRunner.Say(line, lucy, false);
            yield return new WaitForSeconds(5f);
            foreach (var k in Kid.All) if (k && k.Anim) k.Anim.LookOverride = null;
        }

        /// <summary>
        /// White curtain-of-light texture (colour comes from the vertices): u runs along the curtain and wraps, v goes up it.
        /// A crisp lower edge with its brightest line just above it, and above that bundles of fine rays of different
        /// widths and strengths with darker gaps between, each ray fading out at its own height so the top is ragged.
        /// </summary>
        static Texture2D s_Rays;
        static Texture2D RayTexture()
        {
            if (s_Rays) return s_Rays;
            const int W = 512, H = 128;
            var rng = new System.Random(11);
            float[] n1 = Noise(rng, 22), n2 = Noise(rng, 60), n3 = Noise(rng, 150), nt = Noise(rng, 18), nt2 = Noise(rng, 90);
            var rays = new float[W];
            var tops = new float[W];
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W;
                float r = 0.5f * Wrap(n1, u) + 0.33f * Wrap(n2, u) + 0.17f * Wrap(n3, u);
                r = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.8f, r));   // bundles of rays with darker gaps
                rays[x] = Mathf.Pow(r, 1.4f);
                tops[x] = Mathf.Clamp01(0.45f + 0.45f * Wrap(nt, u) + 0.15f * Wrap(nt2, u));
            }
            s_Rays = new Texture2D(W, H, TextureFormat.RGBA32, true)
            {
                name = "AuroraRays", filterMode = FilterMode.Trilinear, anisoLevel = 2,
                wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp,
            };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float v = (y + 0.5f) / H;
                float bottom = Mathf.SmoothStep(0f, 1f, v / 0.025f);                  // a crisp lower edge
                float rayMix = Mathf.SmoothStep(0f, 1f, (v - 0.02f) / 0.28f);        // a continuous band along the edge, rays above it
                float edge = 1f + 0.55f * Mathf.Exp(-Sq((v - 0.045f) / 0.035f));     // the brightest line, just above the edge
                for (int x = 0; x < W; x++)
                {
                    float ray = Mathf.Lerp(0.85f, 0.1f + 0.9f * rays[x], rayMix);
                    float fade = Mathf.SmoothStep(0f, 1f, (tops[x] - v) / 0.32f);      // each ray fades out at its own height
                    float a = bottom * edge * (ray * fade * 0.78f + 0.08f * (1f - v));
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            }
            s_Rays.SetPixels32(px);
            s_Rays.Apply(true, true);
            return s_Rays;
        }

        static float[] Noise(System.Random rng, int n)
        {
            var a = new float[n];
            for (int i = 0; i < n; i++) a[i] = (float)rng.NextDouble();
            return a;
        }

        /// <summary>Smooth noise around a loop (so the texture wraps): n random values spaced evenly, eased between.</summary>
        static float Wrap(float[] n, float u)
        {
            int L = n.Length;
            float f = u * L;
            int i = Mathf.FloorToInt(f);
            float t = f - i;
            i = ((i % L) + L) % L;
            t = t * t * (3f - 2f * t);
            return Mathf.Lerp(n[i], n[(i + 1) % L], t);
        }
    }
}
