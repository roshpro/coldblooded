using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// The "noir case file" UI kit: true-size rounded panels with ink outlines and soft drop shadows, pills, rubber
    /// stamps, paper, procedural icons and a type scale. Everything is world-space (this is an XR game) and every
    /// colour comes from the Palette.
    ///
    /// Type scale (TMP world units: 1.0 ≈ 0.115 m line height):
    ///   Display  Roboto Black      titles, stamps          0.9 – 1.6
    ///   Heading  Roboto Bold       names, buttons, labels  0.14 – 0.3
    ///   Body     Inter SemiBold    dialogue, captions      0.2 – 0.3
    ///   Mono     Roboto Mono Bold  case-file typewriter    0.14 – 0.3
    ///   Hand     Inter SemiBold It marker notes on photos  0.1 – 0.2
    /// </summary>
    public static class UIKit
    {
        public enum Face { Display, Heading, Body, Mono, Hand, Regular }
        public enum Icon { Snowflake, Pinch, Trigger, Check, Off, Play, Restart, Hand, Camera, ArrowLeft, ArrowRight, Pause, Snowball, Magnifier, Clock }

        // ------------------------------------------------------------------ fonts
        public static TMP_FontAsset Font(Face f)
        {
            var a = FrostAssets.Instance;
            TMP_FontAsset fa = null;
            if (a)
            {
                switch (f)
                {
                    case Face.Display: fa = a.displayFont; break;
                    case Face.Heading: fa = a.headingFont; break;
                    case Face.Body: fa = a.bodyFont; break;
                    case Face.Mono: fa = a.monoFont; break;
                    case Face.Hand: fa = a.handFont; break;
                    case Face.Regular: fa = a.regularFont ? a.regularFont : a.bodyFont; break;
                }
                if (!fa) fa = a.speechFont;
            }
            return fa ? fa : TMP_Settings.defaultFontAsset;
        }

        static TMP_FontAsset s_Script;
        /// <summary>Real handwriting (Caveat, Resources/Fonts/Caveat.ttf) for the case journal, built at runtime as a
        /// dynamic TMP font; falls back to the Hand face.</summary>
        public static TMP_FontAsset ScriptFont()
        {
            if (s_Script) return s_Script;
            var f = Resources.Load<Font>("Fonts/Caveat");
            if (f) s_Script = TMP_FontAsset.CreateFontAsset(f);
            if (s_Script) s_Script.name = "Caveat (dynamic)";
            else Debug.LogWarning("[Frost] Handwriting font missing; using the Hand face");
            return s_Script ? s_Script : Font(Face.Hand);
        }

        public static TextMeshPro Text(Transform parent, string name, string text, Face face, float size, Color color,
            Vector2 box, TextAlignmentOptions align = TextAlignmentOptions.Center, float spacing = 0f, bool wrap = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = parent ? parent.gameObject.layer : Layers.UI3D;
            var t = go.AddComponent<TextMeshPro>();
            t.font = Font(face);
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.characterSpacing = spacing;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true;
            t.extraPadding = true;
            t.fontStyle = face == Face.Display || face == Face.Heading ? FontStyles.UpperCase : FontStyles.Normal;
            t.rectTransform.sizeDelta = box;
            var r = t.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingOrder = Order.Text;
            return t;
        }

        /// <summary>Explicit draw order for transparent UI (sorting order beats distance sorting).</summary>
        public static class Order
        {
            public const int Shadow = -10, Glass = 0, Decor = 1, Scrim = 2, Icon = 6, Text = 10, Overlay = 14, Cursor = 30;
        }

        /// <summary>A soft drop shadow under display text (TMP underlay) so it reads over imagery.</summary>
        public static void Shadowed(TMP_Text t, float softness = 0.45f, float alpha = 0.6f)
        {
            var m = t.fontMaterial;
            if (!m || !m.HasProperty(ShaderUtilities.ID_UnderlayColor)) return;
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, alpha));
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.25f);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.4f);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);
        }

        /// <summary>Preferred size of a string in a face/size with an optional wrap width.</summary>
        public static Vector2 Measure(TextMeshPro t, string text, float maxWidth)
        {
            var v = t.GetPreferredValues(text, maxWidth, 10f);
            return new Vector2(Mathf.Min(v.x, maxWidth), v.y);
        }

        // ------------------------------------------------------------------ meshes
        static readonly Dictionary<string, Mesh> s_Meshes = new Dictionary<string, Mesh>();

        /// <summary>A flat rounded rectangle in XY, facing -Z (toward the viewer of a board), centred, true size in metres.</summary>
        public static Mesh RoundedRect(float w, float h, float r, int seg = 7)
        {
            w = Mathf.Max(w, 0.002f); h = Mathf.Max(h, 0.002f);
            r = Mathf.Clamp(r, 0f, Mathf.Min(w, h) * 0.5f);
            string key = $"rr_{w:0.0000}_{h:0.0000}_{r:0.0000}_{seg}";
            if (s_Meshes.TryGetValue(key, out var m) && m) return m;
            var outline = RoundedOutline(w, h, r, seg);
            var verts = new List<Vector3> { Vector3.zero };
            var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            foreach (var p in outline) { verts.Add(new Vector3(p.x, p.y, 0)); uvs.Add(new Vector2(p.x / w + 0.5f, p.y / h + 0.5f)); }
            var tris = new List<int>();
            int n = outline.Count;
            for (int i = 0; i < n; i++) { tris.Add(0); tris.Add(1 + i); tris.Add(1 + (i + 1) % n); }
            m = new Mesh { name = key };
            m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            var normals = new Vector3[verts.Count]; for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.back;
            m.normals = normals;
            m.RecalculateBounds();
            s_Meshes[key] = m;
            return m;
        }

        /// <summary>A rounded-rectangle ring (for stamp borders): outer size w×h, stroke thickness t.</summary>
        public static Mesh RoundedFrame(float w, float h, float r, float t, int seg = 7)
        {
            string key = $"rf_{w:0.0000}_{h:0.0000}_{r:0.0000}_{t:0.0000}_{seg}";
            if (s_Meshes.TryGetValue(key, out var m) && m) return m;
            var outer = RoundedOutline(w, h, r, seg);
            var inner = RoundedOutline(w - 2 * t, h - 2 * t, Mathf.Max(0f, r - t), seg);
            var verts = new List<Vector3>();
            foreach (var p in outer) verts.Add(p);
            foreach (var p in inner) verts.Add(p);
            int n = outer.Count;
            var tris = new List<int>();
            for (int i = 0; i < n; i++)
            {
                int a = i, b = (i + 1) % n, c = n + i, d = n + (i + 1) % n;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
            m = new Mesh { name = key };
            m.SetVertices(verts); m.SetTriangles(tris, 0);
            var normals = new Vector3[verts.Count]; for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.back;
            m.normals = normals;
            m.RecalculateBounds();
            s_Meshes[key] = m;
            return m;
        }

        /// <summary>A triangle pointing down from (0,0): a speech-bubble tail.</summary>
        public static Mesh Tail(float w, float h, float lean)
        {
            string key = $"tail_{w:0.000}_{h:0.000}_{lean:0.000}";
            if (s_Meshes.TryGetValue(key, out var m) && m) return m;
            m = new Mesh { name = key };
            m.vertices = new[] { new Vector3(-w * 0.5f, 0, 0), new Vector3(w * 0.5f, 0, 0), new Vector3(lean, -h, 0) };
            m.triangles = new[] { 0, 1, 2 };
            m.normals = new[] { Vector3.back, Vector3.back, Vector3.back };
            m.RecalculateBounds();
            s_Meshes[key] = m;
            return m;
        }

        /// <summary>A ring segment (spinner arc), centred, outer radius r, stroke t, sweeping clockwise from 12 o'clock.</summary>
        public static Mesh Arc(float r, float t, float degrees, int seg = 40)
        {
            string key = $"arc_{r:0.0000}_{t:0.0000}_{degrees:0.0}_{seg}";
            if (s_Meshes.TryGetValue(key, out var m) && m) return m;
            var verts = new Vector3[(seg + 1) * 2];
            var tris = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float a = (90f - degrees * i / seg) * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                verts[i * 2] = d * r;
                verts[i * 2 + 1] = d * (r - t);
                if (i == seg) break;
                int o = i * 2, k = i * 6;
                tris[k] = o; tris[k + 1] = o + 2; tris[k + 2] = o + 1;
                tris[k + 3] = o + 1; tris[k + 4] = o + 2; tris[k + 5] = o + 3;
            }
            m = new Mesh { name = key, vertices = verts, triangles = tris };
            m.RecalculateBounds();
            s_Meshes[key] = m;
            return m;
        }

        // y of a rounded rect's top edge at x (straight run, then the corner arc)
        static float EdgeTopY(float x, float w, float h, float r)
        {
            float cx = Mathf.Abs(x) - (w * 0.5f - r);
            if (cx <= 0f) return h * 0.5f;
            cx = Mathf.Min(cx, r);
            return h * 0.5f - r + Mathf.Sqrt(Mathf.Max(0f, r * r - cx * cx));
        }

        /// <summary>
        /// Snow resting on a panel's top edge: a soft, lumpy ridge that follows the edge and thins out before the
        /// corners. Vertex colours shade it bright on top and cool blue underneath. Panel-local, faces -Z.
        /// </summary>
        public static Mesh SnowCap(float w, float h, float r, int seed, float thickness = 0.011f)
        {
            var rng = new System.Random(seed);
            float Rnd() => (float)rng.NextDouble();
            int lumps = Mathf.Clamp(Mathf.RoundToInt(w / 0.15f), 3, 9);
            var lx = new float[lumps]; var lw = new float[lumps]; var lh = new float[lumps];
            for (int i = 0; i < lumps; i++)
            {
                lx[i] = (-0.5f + (i + 0.2f + 0.6f * Rnd()) / lumps) * w;
                lw[i] = w / lumps * (0.45f + 0.45f * Rnd());
                lh[i] = 0.45f + 0.55f * Rnd();
            }
            float x0 = -w * 0.5f + r * 0.3f, x1 = w * 0.5f - r * 0.3f;
            int n = Mathf.Clamp(Mathf.RoundToInt((x1 - x0) / 0.006f), 24, 220);
            var verts = new Vector3[(n + 1) * 2];
            var cols = new Color[(n + 1) * 2];
            var tris = new int[n * 6];
            Color top = Palette.Snow, under = Color.Lerp(Palette.SnowShadow, Palette.Snow, 0.3f);
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, x = Mathf.Lerp(x0, x1, t);
                float bump = 0f;
                for (int k = 0; k < lumps; k++) { float d = (x - lx[k]) / lw[k]; bump += lh[k] * Mathf.Exp(-d * d); }
                float taper = Mathf.SmoothStep(0f, 1f, Mathf.Min(t, 1f - t) / 0.09f);
                float th = (thickness * (0.4f + 0.6f * Mathf.Clamp01(bump)) + 0.0012f) * taper;
                float y = EdgeTopY(x, w, h, r) - 0.0022f;          // sink a hair into the panel so there's no gap
                verts[i * 2] = new Vector3(x, y, 0f);
                verts[i * 2 + 1] = new Vector3(x, y + 0.0022f + th, 0f);
                cols[i * 2] = under; cols[i * 2 + 1] = top;
                if (i == n) break;
                int o = i * 2, k6 = i * 6;
                tris[k6] = o; tris[k6 + 1] = o + 1; tris[k6 + 2] = o + 3;
                tris[k6 + 3] = o; tris[k6 + 4] = o + 3; tris[k6 + 5] = o + 2;
            }
            var m = new Mesh { name = "Snow Cap", vertices = verts, colors = cols, triangles = tris };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// A frozen rim along a panel's bottom edge with icicles of varied length hanging from it, clustered the way
        /// real ones form. Vertex colours run from milky ice at the root to a clear tip, with a glint down one side.
        /// Panel-local, faces -Z. scale shrinks the lengths for tight layouts.
        /// </summary>
        public static Mesh Icicles(float w, float h, float r, int seed, float scale = 1f)
        {
            var rng = new System.Random(seed);
            float Rnd() => (float)rng.NextDouble();
            var verts = new List<Vector3>(); var cols = new List<Color>(); var tris = new List<int>();
            Color root = new Color(0.88f, 0.96f, 1f, 0.92f), mid = new Color(0.74f, 0.9f, 1f, 0.7f);
            Color tip = new Color(0.68f, 0.87f, 1f, 0.4f), shine = new Color(1f, 1f, 1f, 0.8f), shineOff = new Color(1f, 1f, 1f, 0f);
            float y0 = -h * 0.5f + 0.0012f;
            float xA = -w * 0.5f + r * 0.85f, xB = w * 0.5f - r * 0.85f;
            void Quad(Vector3 tl, Vector3 tr, Vector3 br, Vector3 bl, Color ct, Color cb)
            {
                int b = verts.Count;
                verts.Add(tl); verts.Add(tr); verts.Add(br); verts.Add(bl);
                cols.Add(ct); cols.Add(ct); cols.Add(cb); cols.Add(cb);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
            }
            // the frozen rim
            float rimH = 0.0024f * Mathf.Max(0.6f, scale);
            Quad(new Vector3(xA - r * 0.35f, y0 + 0.0006f), new Vector3(xB + r * 0.35f, y0 + 0.0006f),
                 new Vector3(xB + r * 0.35f, y0 - rimH), new Vector3(xA - r * 0.35f, y0 - rimH), root, mid);
            float x = xA + 0.006f + Rnd() * 0.02f;
            while (x < xB - 0.004f)
            {
                bool longOne = Rnd() < 0.28f;
                float len = (longOne ? 0.02f + Rnd() * 0.016f : 0.006f + Rnd() * 0.011f) * scale;
                float wid = (0.004f + len * 0.2f) * (0.85f + 0.3f * Rnd());
                float lean = (Rnd() - 0.5f) * wid * 0.5f;
                float top = y0 - rimH * 0.5f;
                int b = verts.Count;
                verts.Add(new Vector3(x, top - 0.0012f, 0f)); cols.Add(root);                                   // 0 centre
                verts.Add(new Vector3(x - wid * 0.5f, top, 0f)); cols.Add(root);                                // 1 root left
                verts.Add(new Vector3(x - wid * 0.3f + lean * 0.5f, top - len * 0.5f, 0f)); cols.Add(mid);      // 2 mid left
                verts.Add(new Vector3(x + lean, top - len, 0f)); cols.Add(tip);                                  // 3 tip
                verts.Add(new Vector3(x + wid * 0.32f + lean * 0.5f, top - len * 0.46f, 0f)); cols.Add(mid);    // 4 mid right
                verts.Add(new Vector3(x + wid * 0.5f, top, 0f)); cols.Add(root);                                // 5 root right
                int[] fan = { 5, 4, 4, 3, 3, 2, 2, 1, 1, 5 };
                for (int f = 0; f < fan.Length; f += 2) { tris.Add(b); tris.Add(b + fan[f]); tris.Add(b + fan[f + 1]); }
                // a glint running down the left flank
                float gw = 0.0011f * Mathf.Max(0.7f, scale);
                var g0 = new Vector3(x - wid * 0.2f, top - 0.001f, 0f);
                var g1 = new Vector3(x - wid * 0.1f + lean * 0.55f, top - len * 0.62f, 0f);
                Quad(g0, g0 + new Vector3(gw, 0f, 0f), g1 + new Vector3(gw * 0.5f, 0f, 0f), g1, shine, shineOff);
                x += wid + (Rnd() < 0.35f ? 0.0015f + Rnd() * 0.003f : 0.01f + Rnd() * 0.045f);
            }
            var m = new Mesh { name = "Icicles" };
            m.SetVertices(verts); m.SetColors(cols); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        // clockwise outline seen from -Z (so the triangle fan faces -Z)
        static List<Vector3> RoundedOutline(float w, float h, float r, int seg)
        {
            var pts = new List<Vector3>();
            float hx = w * 0.5f - r, hy = h * 0.5f - r;
            // corners: top-right, bottom-right, bottom-left, top-left — swept in decreasing angle (clockwise from -Z)
            Vector2[] centers = { new Vector2(-hx, hy), new Vector2(hx, hy), new Vector2(hx, -hy), new Vector2(-hx, -hy) };
            float[] start = { 180f, 90f, 0f, -90f };
            for (int c = 0; c < 4; c++)
            {
                for (int i = 0; i <= seg; i++)
                {
                    float a = (start[c] - 90f * i / seg) * Mathf.Deg2Rad;
                    pts.Add(new Vector3(centers[c].x + Mathf.Cos(a) * r, centers[c].y + Mathf.Sin(a) * r, 0));
                }
            }
            return pts;
        }

        // ------------------------------------------------------------------ panels
        public class PanelParts { public Transform Root, Fill, Outline, Shadow; public Material FillMat; }

        /// <summary>Fill + ink outline + soft drop shadow. Front faces -Z.</summary>
        public static PanelParts Panel(Transform parent, string name, Vector2 size, float radius, Color fill,
            Color? outline = null, float outlineWidth = 0.004f, bool shadow = true, Texture tex = null, float shadowAlpha = 0.22f)
        {
            var p = new PanelParts();
            p.Root = new GameObject(name).transform;
            p.Root.SetParent(parent, false);
            p.Root.gameObject.layer = parent ? parent.gameObject.layer : Layers.UI3D;
            p.FillMat = tex ? Palette.UnlitTextured(tex, fill, false, name + " Fill") : Palette.Unlit(fill);
            p.Fill = Quad(p.Root, "Fill", RoundedRect(size.x, size.y, radius), p.FillMat, 0f);
            if (outline.HasValue)
                p.Outline = Quad(p.Root, "Outline", RoundedRect(size.x + outlineWidth * 2f, size.y + outlineWidth * 2f, radius + outlineWidth), Palette.Unlit(outline.Value), 0.0012f);
            if (shadow)
            {
                var s = Quad(p.Root, "Shadow", RoundedRect(size.x + outlineWidth * 2f + 0.006f, size.y + outlineWidth * 2f + 0.006f, radius + outlineWidth + 0.003f),
                    Palette.Unlit(Palette.Ink.WithAlpha(shadowAlpha), true), 0.0024f);
                s.localPosition += new Vector3(0.006f, -0.009f, 0);
                s.GetComponent<MeshRenderer>().sortingOrder = Order.Shadow;
                p.Shadow = s;
            }
            return p;
        }

        public static Transform Quad(Transform parent, string name, Mesh mesh, Material mat, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = parent ? parent.gameObject.layer : Layers.UI3D;
            go.transform.localPosition = new Vector3(0, 0, z);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.transform;
        }

        /// <summary>A pill with a label (name tags, chips). Returns the root and its width.</summary>
        public static Transform Pill(Transform parent, string name, string text, Face face, float textSize, Color fill, Color textColor,
            out float width, float height = 0.034f, float padX = 0.014f, Color? outline = null, bool shadow = false)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.gameObject.layer = parent ? parent.gameObject.layer : Layers.UI3D;
            var label = Text(root, "Label", text, face, textSize, textColor, new Vector2(1f, height), TextAlignmentOptions.Center, face == Face.Heading ? 6f : 0f, false);
            label.transform.localPosition = new Vector3(0, 0, -0.002f);
            float w = Measure(label, label.text, 1f).x + padX * 2f;
            label.rectTransform.sizeDelta = new Vector2(w, height);
            Panel(root, "Shape", new Vector2(w, height), height * 0.5f, fill, outline, 0.003f, shadow);
            width = w;
            return root;
        }

        /// <summary>A rubber stamp: bold word in a rounded border, rotated, ink-worn.</summary>
        public static Transform Stamp(Transform parent, string word, float textSize, Color color, float angle, float alpha = 1f)
        {
            var root = new GameObject("Stamp " + word).transform;
            root.SetParent(parent, false);
            root.gameObject.layer = parent ? parent.gameObject.layer : Layers.UI3D;
            var t = Text(root, "Word", word, Face.Display, textSize, color.WithAlpha(alpha), new Vector2(2f, 0.3f), TextAlignmentOptions.Center, 8f, false);
            var size = Measure(t, word, 3f);
            float w = size.x + textSize * 0.09f, h = size.y * 0.78f + textSize * 0.03f;
            t.rectTransform.sizeDelta = new Vector2(w, h);
            float stroke = Mathf.Max(0.004f, textSize * 0.012f);
            Quad(root, "Border", RoundedFrame(w, h, h * 0.16f, stroke), Palette.Unlit(color.WithAlpha(alpha), true), 0.001f).GetComponent<MeshRenderer>().sortingOrder = Order.Text;
            root.localRotation = Quaternion.Euler(0, 0, angle);
            return root;
        }

        public static Transform IconQuad(Transform parent, Icon icon, float size, Color color, float z = -0.002f)
        {
            var q = Quad(parent, "Icon " + icon, ProcMesh.Quad(), Palette.UnlitTextured(IconTex(icon), color, true, "Icon " + icon), z);
            q.localScale = new Vector3(size, size, 1f);
            q.GetComponent<MeshRenderer>().sortingOrder = Order.Icon;
            return q;
        }

        // ------------------------------------------------------------------ textures
        static Texture2D s_Paper;
        /// <summary>Subtle fibrous paper grain (multiply by the material colour).</summary>
        public static Texture2D Paper
        {
            get
            {
                if (s_Paper) return s_Paper;
                const int S = 256;
                s_Paper = new Texture2D(S, S, TextureFormat.RGBA32, true) { name = "Paper", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
                var px = new Color[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.5f + Mathf.PerlinNoise(x * 0.21f + 7f, y * 0.21f) * 0.3f + Mathf.PerlinNoise(x * 0.9f, y * 0.03f) * 0.2f;
                        float v = 0.93f + n * 0.09f;
                        px[y * S + x] = new Color(v, v, v * 0.995f, 1f);
                    }
                s_Paper.SetPixels(px);
                s_Paper.Apply(true, false);
                return s_Paper;
            }
        }

        static readonly Dictionary<Icon, Texture2D> s_Icons = new Dictionary<Icon, Texture2D>();

        public static Texture2D IconTex(Icon icon)
        {
            if (s_Icons.TryGetValue(icon, out var t) && t) return t;
            const int S = 96;
            t = new Texture2D(S, S, TextureFormat.RGBA32, true) { name = "Icon_" + icon, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    var p = new Vector2((x + 0.5f) / S * 2f - 1f, (y + 0.5f) / S * 2f - 1f);
                    float d = IconSdf(icon, p);
                    float a = Mathf.Clamp01(0.5f - d * S * 0.5f);
                    px[y * S + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels(px);
            t.Apply(true, false);
            s_Icons[icon] = t;
            return t;
        }

        // signed distances in [-1,1]² space (negative inside)
        static float Circle(Vector2 p, Vector2 c, float r) => Vector2.Distance(p, c) - r;
        static float Box(Vector2 p, Vector2 c, Vector2 h, float r = 0f)
        {
            var q = new Vector2(Mathf.Abs(p.x - c.x) - h.x + r, Mathf.Abs(p.y - c.y) - h.y + r);
            return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - r;
        }
        static float Seg(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            var pa = p - a; var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }
        static float Ring(Vector2 p, Vector2 c, float r, float t) => Mathf.Abs(Vector2.Distance(p, c) - r) - t;

        static float IconSdf(Icon icon, Vector2 p)
        {
            switch (icon)
            {
                case Icon.Snowflake:
                {
                    float d = Circle(p, Vector2.zero, 0.14f);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        d = Mathf.Min(d, Seg(p, Vector2.zero, dir * 0.82f, 0.075f));
                        var mid = dir * 0.5f;
                        var perp = new Vector2(-dir.y, dir.x);
                        d = Mathf.Min(d, Seg(p, mid, mid + (dir + perp) * 0.22f, 0.06f));
                        d = Mathf.Min(d, Seg(p, mid, mid + (dir - perp) * 0.22f, 0.06f));
                    }
                    return d;
                }
                case Icon.Pinch:
                {
                    // thumb and index meeting
                    float d = Seg(p, new Vector2(-0.55f, -0.7f), new Vector2(-0.05f, 0.1f), 0.17f);
                    d = Mathf.Min(d, Seg(p, new Vector2(0.55f, -0.7f), new Vector2(0.05f, 0.1f), 0.17f));
                    d = Mathf.Min(d, Ring(p, new Vector2(0, 0.42f), 0.22f, 0.05f));
                    return d;
                }
                case Icon.Trigger:
                {
                    // a controller trigger: curved finger rest + arrow
                    float d = Box(p, new Vector2(0, -0.1f), new Vector2(0.38f, 0.62f), 0.32f);
                    d = Mathf.Max(d, -Box(p, new Vector2(0.18f, -0.25f), new Vector2(0.26f, 0.42f), 0.2f));
                    return d;
                }
                case Icon.Check: return Mathf.Min(Seg(p, new Vector2(-0.6f, 0f), new Vector2(-0.2f, -0.45f), 0.13f), Seg(p, new Vector2(-0.2f, -0.45f), new Vector2(0.62f, 0.5f), 0.13f));
                case Icon.Off: return Mathf.Min(Seg(p, new Vector2(-0.5f, -0.5f), new Vector2(0.5f, 0.5f), 0.12f), Seg(p, new Vector2(-0.5f, 0.5f), new Vector2(0.5f, -0.5f), 0.12f));
                case Icon.Play:
                {
                    var a = new Vector2(-0.4f, -0.6f); var b = new Vector2(-0.4f, 0.6f); var c = new Vector2(0.6f, 0f);
                    float e1 = Cross(b - a, p - a), e2 = Cross(c - b, p - b), e3 = Cross(a - c, p - c);
                    bool inside = e1 <= 0 && e2 <= 0 && e3 <= 0;
                    float dist = Mathf.Min(Seg(p, a, b, 0f), Mathf.Min(Seg(p, b, c, 0f), Seg(p, c, a, 0f)));
                    return (inside ? -dist : dist) - 0.05f;
                }
                case Icon.Restart:
                {
                    float ang = Mathf.Atan2(p.y, p.x);
                    float d = Ring(p, Vector2.zero, 0.55f, 0.11f);
                    if (ang > 0.2f && ang < 1.2f) d = Mathf.Max(d, 0.1f);
                    // arrow head
                    d = Mathf.Min(d, Seg(p, new Vector2(0.52f, 0.2f), new Vector2(0.82f, 0.48f), 0.1f));
                    d = Mathf.Min(d, Seg(p, new Vector2(0.52f, 0.2f), new Vector2(0.2f, 0.42f), 0.1f));
                    return d;
                }
                case Icon.Hand:
                {
                    float d = Box(p, new Vector2(0, -0.25f), new Vector2(0.42f, 0.4f), 0.25f);
                    for (int i = 0; i < 4; i++) d = Mathf.Min(d, Seg(p, new Vector2(-0.3f + i * 0.2f, 0f), new Vector2(-0.3f + i * 0.2f, 0.7f - Mathf.Abs(i - 1.5f) * 0.12f), 0.085f));
                    d = Mathf.Min(d, Seg(p, new Vector2(0.35f, -0.25f), new Vector2(0.7f, 0.15f), 0.09f));
                    return d;
                }
                case Icon.Camera:
                {
                    float d = Box(p, new Vector2(0, -0.08f), new Vector2(0.78f, 0.5f), 0.14f);
                    d = Mathf.Min(d, Box(p, new Vector2(-0.35f, 0.5f), new Vector2(0.2f, 0.12f), 0.05f));
                    d = Mathf.Max(d, -Circle(p, new Vector2(0.05f, -0.08f), 0.3f));
                    d = Mathf.Min(d, Circle(p, new Vector2(0.05f, -0.08f), 0.17f));
                    return d;
                }
                case Icon.ArrowLeft: return Mathf.Min(Seg(p, new Vector2(0.35f, 0.6f), new Vector2(-0.3f, 0f), 0.14f), Seg(p, new Vector2(-0.3f, 0f), new Vector2(0.35f, -0.6f), 0.14f));
                case Icon.ArrowRight: return Mathf.Min(Seg(p, new Vector2(-0.35f, 0.6f), new Vector2(0.3f, 0f), 0.14f), Seg(p, new Vector2(0.3f, 0f), new Vector2(-0.35f, -0.6f), 0.14f));
                case Icon.Pause: return Mathf.Min(Box(p, new Vector2(-0.28f, 0), new Vector2(0.14f, 0.55f), 0.06f), Box(p, new Vector2(0.28f, 0), new Vector2(0.14f, 0.55f), 0.06f));
                case Icon.Snowball:
                {
                    float d = Circle(p, Vector2.zero, 0.62f);
                    d = Mathf.Max(d, -Circle(p, new Vector2(-0.22f, 0.2f), 0.12f));
                    return d;
                }
                case Icon.Clock: return Mathf.Min(Ring(p, Vector2.zero, 0.66f, 0.1f), Mathf.Min(Seg(p, Vector2.zero, new Vector2(0, 0.42f), 0.09f), Seg(p, Vector2.zero, new Vector2(0.3f, -0.12f), 0.09f)));
                case Icon.Magnifier: return Mathf.Min(Ring(p, new Vector2(-0.15f, 0.15f), 0.42f, 0.1f), Seg(p, new Vector2(0.15f, -0.15f), new Vector2(0.65f, -0.65f), 0.13f));
            }
            return 1f;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
