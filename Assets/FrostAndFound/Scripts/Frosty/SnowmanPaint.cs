using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Paints Frosty's clues straight into the snowman model's own texture. Every triangle near a clue is rasterised in
    /// UV space and each texel takes the clue's pattern for its real position on the body, so a splash wraps across UV
    /// seams and keeps the snowman's own shading and snow grain: part of him, not a sticker sitting on top.
    /// </summary>
    public static class SnowmanPaint
    {
        /// <summary>A clue's look. p is where on the surface, in metres in the clue's tangent plane (+y along "up").</summary>
        public delegate bool Pattern(Vector2 p, out Color colour, out float alpha);

        /// <summary>Pattern size multiplier (the snowman's scale).</summary>
        public static float Scale = 1f;

        /// <summary>A readable RGBA copy of any texture (the imported one isn't CPU-readable on device).</summary>
        public static Texture2D Canvas(Texture src)
        {
            int w = src ? src.width : 512, h = src ? src.height : 512;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            if (src) Graphics.Blit(src, rt);
            else { RenderTexture.active = rt; GL.Clear(false, true, Color.white); }
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, false)
            {
                name = "Frosty (painted)",
                wrapMode = src ? src.wrapMode : TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 2,
            };
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        public static void Paint(Color32[] px, int W, int H, Mesh mesh, Matrix4x4 toWorld, Vector3 center, Vector3 normal, Vector3 up, float radius, Pattern pattern)
        {
            if (!mesh || !mesh.isReadable) return;
            var verts = mesh.vertices;
            var uvs = mesh.uv;
            var tris = mesh.triangles;
            if (uvs == null || uvs.Length != verts.Length) return;
            normal = normal.normalized;
            var ty = Vector3.ProjectOnPlane(up, normal);
            if (ty.sqrMagnitude < 1e-6f) ty = Vector3.ProjectOnPlane(Vector3.forward, normal);
            ty.Normalize();
            var tx = Vector3.Cross(normal, ty);
            var wp = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++) wp[i] = toWorld.MultiplyPoint3x4(verts[i]);
            float r2 = radius * radius;
            // each texel is painted once, by the triangle it's most inside. Neighbouring triangles' rasters overlap a
            // little (so seams leave no gaps); blending again in that overlap left a darker line along their shared
            // edge right through the stain
            var claims = new Dictionary<int, (float inside, Color col, float alpha)>();
            const float Margin = 3f;   // texels past a triangle's edge it may still fill, so UV seams through the stain stay brown even in the blurrier mip levels
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int ia = tris[t], ib = tris[t + 1], ic = tris[t + 2];
                Vector3 A = wp[ia], B = wp[ib], C = wp[ic];
                var ctr = (A + B + C) / 3f;
                float reach = radius + Mathf.Max((A - ctr).magnitude, Mathf.Max((B - ctr).magnitude, (C - ctr).magnitude));
                if ((ctr - center).sqrMagnitude > reach * reach) continue;
                if (Vector3.Dot(Vector3.Cross(B - A, C - A), normal) <= 0f) continue;   // the far side of the body
                Vector2 ua = new Vector2(uvs[ia].x * W, uvs[ia].y * H), ub = new Vector2(uvs[ib].x * W, uvs[ib].y * H), uc = new Vector2(uvs[ic].x * W, uvs[ic].y * H);
                float area = Edge(ua, ub, uc);
                if (Mathf.Abs(area) < 1e-6f) continue;
                // texel distance from a point to each edge = its barycentric weight × the triangle's height there
                float ha = Mathf.Abs(area) / Mathf.Max(1e-6f, (uc - ub).magnitude);
                float hb = Mathf.Abs(area) / Mathf.Max(1e-6f, (ua - uc).magnitude);
                float hc = Mathf.Abs(area) / Mathf.Max(1e-6f, (ub - ua).magnitude);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ua.x, Mathf.Min(ub.x, uc.x))) - 1);
                int x1 = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(ua.x, Mathf.Max(ub.x, uc.x))) + 1);
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ua.y, Mathf.Min(ub.y, uc.y))) - 1);
                int y1 = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(ua.y, Mathf.Max(ub.y, uc.y))) + 1);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var q = new Vector2(x + 0.5f, y + 0.5f);
                        float w0 = Edge(ub, uc, q) / area, w1 = Edge(uc, ua, q) / area, w2 = 1f - w0 - w1;
                        float inside = Mathf.Min(w0 * ha, Mathf.Min(w1 * hb, w2 * hc));   // texels inside (negative: outside)
                        if (inside < -Margin) continue;
                        int i = y * W + x;
                        if (claims.TryGetValue(i, out var had) && had.inside >= inside) continue;
                        var P = A * w0 + B * w1 + C * w2;
                        var d = P - center;
                        Color col = default; float al = 0f;
                        if (Mathf.Abs(Vector3.Dot(d, normal)) <= radius * 0.6f)
                        {
                            var s = new Vector2(Vector3.Dot(d, tx), Vector3.Dot(d, ty));
                            if (s.sqrMagnitude <= r2 && !pattern(s, out col, out al)) al = 0f;
                        }
                        claims[i] = (inside, col, al);   // claimed even if unpainted, so a neighbour's margin can't paint it
                    }
            }
            foreach (var kv in claims)
            {
                var (_, col, al) = kv.Value;
                if (al <= 0.001f) continue;
                Color baseC = px[kv.Key];
                var paint = col * Mathf.Lerp(0.72f, 1.04f, baseC.grayscale);   // the snow's own shading shows through
                px[kv.Key] = Color.Lerp(baseC, new Color(paint.r, paint.g, paint.b, baseC.a), Mathf.Clamp01(al));
            }
        }

        static float Edge(Vector2 a, Vector2 b, Vector2 c) => (c.x - a.x) * (b.y - a.y) - (c.y - a.y) * (b.x - a.x);

        static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / 4294967295f;
            }
        }

        // ------------------------------------------------------------------ the clues
        // the cocoa: a pool of overlapping lobes (x, y, radius) and drops flung on past it toward +x (x, y, radius, angle)
        static readonly Vector3[] s_Lobes =
        {
            new Vector3(0f, 0f, 0.056f), new Vector3(0.032f, 0.014f, 0.041f), new Vector3(-0.026f, -0.018f, 0.036f),
            new Vector3(0.05f, -0.022f, 0.027f), new Vector3(-0.01f, 0.03f, 0.029f),
        };
        static readonly Vector4[] s_Drops = new Vector4[6];
        static SnowmanPaint()
        {
            var rng = new System.Random(21);
            for (int i = 0; i < s_Drops.Length; i++)
            {
                float ang = ((float)rng.NextDouble() - 0.5f) * 80f * Mathf.Deg2Rad;
                float dist = 0.085f + (float)rng.NextDouble() * 0.07f;
                s_Drops[i] = new Vector4(Mathf.Cos(ang) * dist, Mathf.Sin(ang) * dist, 0.006f + (float)rng.NextDouble() * 0.007f, ang);
            }
        }

        static Color CocoaLight => Color.Lerp(Palette.CocoaLight, Color.white, 0.18f);
        static Color CocoaMid => Color.Lerp(Palette.Cocoa, Palette.CocoaLight, 0.5f);

        /// <summary>Spilt hot cocoa: an uneven pool with a faint drying ring, a few teardrops flung on past it, and fine
        /// spatter thinning out on that side. Kept light, so it soaks into the snow rather than sitting on it.</summary>
        public static bool Cocoa(Vector2 p, out Color c, out float a)
        {
            p /= Scale;
            a = 0f; c = CocoaMid;
            float f = float.MaxValue;   // roughly the distance to the pool's edge (negative inside)
            foreach (var l in s_Lobes) f = Mathf.Min(f, Vector2.Distance(p, new Vector2(l.x, l.y)) - l.z);
            f += (Mathf.PerlinNoise(p.x * 26f + 11f, p.y * 26f + 4f) - 0.5f) * 0.016f;
            if (f < 0.004f)
            {
                float core = Mathf.Clamp01((0.004f - f) / 0.006f);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(f + 0.004f) / 0.006f);
                c = Color.Lerp(Color.Lerp(CocoaLight, CocoaMid, Mathf.Clamp01(-f / 0.03f)), Palette.Cocoa, ring * 0.45f);
                a = core * 0.64f + ring * 0.08f;
            }
            foreach (var d in s_Drops)
            {
                var dir = new Vector2(Mathf.Cos(d.w), Mathf.Sin(d.w));
                var q = p - new Vector2(d.x, d.y);
                float along = Vector2.Dot(q, dir), across = -q.x * dir.y + q.y * dir.x;
                // a teardrop: round head out front, the tail tapering back toward the pool
                float tail = along < 0f ? Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(-along / (d.z * 2.2f))) : 1f;
                float dd = new Vector2(along / (along < 0f ? 2.2f : 1f), across / tail).magnitude;
                float s = Mathf.Clamp01((d.z - dd) / 0.0025f);
                if (s * 0.5f > a) { a = s * 0.5f; c = CocoaMid; }
            }
            float dist = p.magnitude;
            if (dist > 0.05f && dist < 0.2f)
            {
                int cx = Mathf.FloorToInt(p.x / 0.007f), cy = Mathf.FloorToInt(p.y / 0.007f);
                float h = Hash(cx, cy), h2 = Hash(cx + 91, cy - 37);
                float flungSide = Mathf.Clamp01(0.5f + p.x / Mathf.Max(dist, 1e-4f) * 0.5f);
                if (h < flungSide * Mathf.Clamp01(1f - (dist - 0.05f) / 0.15f) * 0.35f)
                {
                    var ctr = new Vector2((cx + 0.3f + h2 * 0.4f) * 0.007f, (cy + 0.3f + Hash(cx - 5, cy + 13) * 0.4f) * 0.007f);
                    float s = Mathf.Clamp01((0.0012f + h2 * 0.0018f - Vector2.Distance(p, ctr)) / 0.001f);
                    if (s * 0.45f > a) { a = s * 0.45f; c = CocoaMid; }
                }
            }
            c.a = 1f;
            return a > 0f;
        }

        /// <summary>Purple glitter smeared across the face, flecked with brighter specks, a little pink blush.</summary>
        public static bool Glitter(Vector2 p, out Color c, out float a)
        {
            p /= Scale;
            a = 0f; c = Palette.Glitter;
            float e = (p.x / 0.08f) * (p.x / 0.08f) + (p.y / 0.055f) * (p.y / 0.055f);
            float wob = Mathf.PerlinNoise(p.x * 30f + 3f, p.y * 30f + 7f) * 0.5f;
            if (e < 1.2f + wob) a = Mathf.Clamp01((1.2f + wob - e) / 0.35f) * 0.3f;
            float h = Hash(Mathf.FloorToInt(p.x / 0.0045f), Mathf.FloorToInt(p.y / 0.0045f));
            if (e < 1.45f && h > 0.62f) { a = Mathf.Max(a, 0.6f); c = Color.Lerp(Palette.Glitter, Color.white, 0.2f + (h - 0.62f) / 0.38f * 0.5f); }
            var b = new Vector2(p.x / 0.045f, (p.y + 0.05f) / 0.02f);
            if (b.sqrMagnitude < 1f && a < 0.25f) { a = Mathf.Max(a, (1f - b.sqrMagnitude) * 0.25f); c = Palette.PinkBag; }
            c.a = 1f;
            return a > 0f;
        }

        /// <summary>Four ragged scratches of sled-red paint where a runner scraped past, and a pale gouge beside them.</summary>
        public static bool Scrape(Vector2 p, out Color c, out float a)
        {
            p /= Scale;
            a = 0f; c = Palette.SledRed;
            const float ang = 25f * Mathf.Deg2Rad;
            var q = new Vector2(p.x * Mathf.Cos(ang) - p.y * Mathf.Sin(ang), p.x * Mathf.Sin(ang) + p.y * Mathf.Cos(ang));
            for (int i = 0; i < 4; i++)
            {
                float x0 = -0.02f + i * 0.012f, half = 0.12f - i * 0.016f;
                float w = 0.0032f + 0.0016f * Mathf.PerlinNoise(q.y * 45f + i * 3f, i);
                float dx = Mathf.Abs(q.x - x0 - Mathf.Sin(q.y * 14f + i) * 0.002f);
                float ends = Mathf.Clamp01((half - Mathf.Abs(q.y)) / 0.02f);
                float s = Mathf.Clamp01((w - dx) / 0.0015f) * ends;
                s *= Mathf.PerlinNoise(q.y * 22f + i * 5f, 2f) > 0.28f ? 1f : 0.25f;   // paint skips where the runner lifted
                if (s * 0.6f > a) { a = s * 0.6f; c = Color.Lerp(Palette.SledRed, Color.white, 0.12f); }
            }
            float g = Mathf.Clamp01((0.0045f - Mathf.Abs(q.x - 0.03f)) / 0.002f) * Mathf.Clamp01((0.1f - Mathf.Abs(q.y - 0.01f)) / 0.02f);
            if (g * 0.3f > a) { a = g * 0.3f; c = Palette.SnowShadow; }
            c.a = 1f;
            return a > 0f;
        }
    }
}
