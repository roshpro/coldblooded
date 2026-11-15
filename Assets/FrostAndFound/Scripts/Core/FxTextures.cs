using UnityEngine;

namespace FrostAndFound
{
    /// <summary>Small procedural textures: particle sprites, the hand-drawn evidence circle, stamps, icons.</summary>
    public static class FxTextures
    {
        static Texture2D s_Dot, s_Flake, s_Star, s_Circle, s_Ring, s_Clump, s_Note, s_Heart, s_WallSplat, s_Glow;

        public static Texture2D SoftDot => s_Dot ? s_Dot : (s_Dot = Make("SoftDot", 64, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float a = Mathf.Clamp01(1f - d);
            return new Color(1, 1, 1, a * a * (3f - 2f * a));
        }));

        /// <summary>A bright little core in a wide soft halo, for things that glow (the northern lights' motes and orbs).</summary>
        public static Texture2D Glow => s_Glow ? s_Glow : (s_Glow = Make("Glow", 64, (u, v) =>
        {
            float d2 = ((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 4f;   // 0 at the centre, 1 at the edge
            float a = 0.8f * Mathf.Exp(-d2 * 22f) + 0.38f * Mathf.Exp(-d2 * 5f);
            return new Color(1, 1, 1, Mathf.Clamp01(a * Mathf.Clamp01((1f - d2) * 4f)));
        }));

        public static Texture2D Flake => s_Flake ? s_Flake : (s_Flake = Make("Snowflake", 64, (u, v) =>
        {
            var p = new Vector2(u - 0.5f, v - 0.5f) * 2f;
            float r = p.magnitude;
            float ang = Mathf.Atan2(p.y, p.x);
            float arms = Mathf.Abs(Mathf.Cos(ang * 3f));
            float star = Mathf.Clamp01(1f - r / (0.35f + 0.6f * Mathf.Pow(arms, 8f)));
            float core = Mathf.Clamp01(1f - r / 0.45f);
            float a = Mathf.Max(star, core * core);
            return new Color(1, 1, 1, Mathf.Clamp01(a * 1.4f));
        }));

        /// <summary>Four-point twinkle star for clue glints.</summary>
        public static Texture2D Star => s_Star ? s_Star : (s_Star = Make("Glint", 64, (u, v) =>
        {
            var p = new Vector2(u - 0.5f, v - 0.5f) * 2f;
            float cross = Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(p.x) * 12f) * Mathf.Clamp01(1f - Mathf.Abs(p.y)),
                                    Mathf.Clamp01(1f - Mathf.Abs(p.y) * 12f) * Mathf.Clamp01(1f - Mathf.Abs(p.x)));
            float glow = Mathf.Clamp01(1f - p.magnitude * 2.2f);
            return new Color(1, 1, 1, Mathf.Clamp01(cross * 1.5f + glow * glow));
        }));

        /// <summary>The hand-drawn red marker circle that marks a good shot (no text: the photo speaks for itself).</summary>
        static Texture2D s_Frost;
        /// <summary>
        /// Tileable frost crystals for the ice-glass UI: feathery dendrites that branch at ±60° (ice grows in sixes),
        /// drawn soft on black. R = coverage. Built once on the CPU (a few ms, 256²).
        /// </summary>
        public static Texture2D FrostCrystals
        {
            get
            {
                if (s_Frost) return s_Frost;
                const int N = 256;
                var buf = new float[N * N];
                var rng = new System.Random(1337);
                float Rnd() => (float)rng.NextDouble();
                void Plot(float x, float y, float v)
                {
                    int ix = ((Mathf.FloorToInt(x) % N) + N) % N, iy = ((Mathf.FloorToInt(y) % N) + N) % N;
                    int k = iy * N + ix;
                    if (buf[k] < v) buf[k] = v;
                }
                void Branch(float x0, float y0, float ang, float len, float v, int depth)
                {
                    float dx = Mathf.Cos(ang), dy = Mathf.Sin(ang);
                    int steps = Mathf.Max(2, Mathf.CeilToInt(len * 1.5f));
                    for (int s = 0; s <= steps; s++)
                    {
                        float t = s / (float)steps;
                        Plot(x0 + dx * len * t, y0 + dy * len * t, v * (1f - 0.65f * t));
                    }
                    if (depth <= 0) return;
                    int n = Mathf.Max(2, (int)(len / 7f));
                    for (int b = 1; b <= n; b++)
                    {
                        float t = b / (float)(n + 1) + (Rnd() - 0.5f) * 0.08f;
                        float bl = len * (0.46f - 0.3f * t) * (0.7f + 0.6f * Rnd());
                        float bx = x0 + dx * len * t, by = y0 + dy * len * t;
                        Branch(bx, by, ang + 1.0472f, bl, v * 0.82f, depth - 1);
                        Branch(bx, by, ang - 1.0472f, bl, v * 0.82f, depth - 1);
                    }
                }
                for (int i = 0; i < 30; i++)
                    Branch(Rnd() * N, Rnd() * N, Rnd() * Mathf.PI * 2f, 16f + Rnd() * 46f, 0.6f + 0.4f * Rnd(), 2);
                // soften with a 3x3 blur, keeping the crisp spines
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float sum = 0f;
                        for (int oy = -1; oy <= 1; oy++)
                            for (int ox = -1; ox <= 1; ox++)
                                sum += buf[((y + oy + N) % N) * N + ((x + ox + N) % N)];
                        float v = Mathf.Clamp01(buf[y * N + x] * 0.7f + sum / 9f * 0.9f);
                        byte b8 = (byte)(v * 255f);
                        px[y * N + x] = new Color32(b8, b8, b8, 255);
                    }
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, true)
                {
                    name = "FrostCrystals", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 2,
                };
                tex.SetPixels32(px);
                tex.Apply(true, true);
                return s_Frost = tex;
            }
        }

        public static Texture2D EvidenceCircle => s_Circle ? s_Circle : (s_Circle = MakeCircle());

        public static Texture2D Ring => s_Ring ? s_Ring : (s_Ring = Make("Ring", 128, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.12f);
            return new Color(1, 1, 1, a);
        }));

        /// <summary>A lumpy clump of packed snow: an irregular solid edge, a little darker towards the rim so it reads as round.</summary>
        public static Texture2D Clump => s_Clump ? s_Clump : (s_Clump = Make("SnowClump", 64, (u, v) =>
        {
            var p = new Vector2(u - 0.5f, v - 0.5f) * 2f;
            float ang = Mathf.Atan2(p.y, p.x), r = p.magnitude;
            float edge = 0.78f + 0.09f * Mathf.Sin(ang * 3f + 0.7f) + 0.06f * Mathf.Sin(ang * 7f + 2.1f) + 0.03f * Mathf.Sin(ang * 13f);
            float a = Mathf.Clamp01((edge - r) * 14f);
            float shade = Mathf.Lerp(1f, 0.8f, Mathf.SmoothStep(0.35f, 1f, r / edge));
            // a soft highlight up and to the left, a little crease of shadow down and to the right
            shade += 0.06f * Mathf.Clamp01(1f - Vector2.Distance(p, new Vector2(-0.3f, 0.35f)) * 2.2f);
            shade -= 0.05f * Mathf.Clamp01(1f - Vector2.Distance(p, new Vector2(0.35f, -0.3f)) * 2.5f);
            return new Color(shade, shade, shade, a);
        }));

        /// <summary>Snow squashed flat against a wall: a lumpy blob (no spikes) with a few flecks thrown off round it,
        /// shaded like the clump so it reads as packed snow rather than paint.</summary>
        public static Texture2D WallSplat => s_WallSplat ? s_WallSplat : (s_WallSplat = Make("SnowWallSplat", 128, (u, v) =>
        {
            var p = new Vector2(u - 0.5f, v - 0.5f) * 2f;
            float ang = Mathf.Atan2(p.y, p.x), r = p.magnitude;
            float edge = 0.6f + 0.08f * Mathf.Sin(ang * 3f + 0.4f) + 0.05f * Mathf.Sin(ang * 5f + 1.7f) + 0.035f * Mathf.Sin(ang * 9f + 0.9f) + 0.02f * Mathf.Sin(ang * 17f);
            float a = Mathf.Clamp01((edge - r) * 16f);
            float shade = Mathf.Lerp(1f, 0.82f, Mathf.SmoothStep(0.3f, 1f, r / edge));
            shade += 0.05f * Mathf.Clamp01(1f - Vector2.Distance(p, new Vector2(-0.22f, 0.25f)) * 2.4f);
            // flecks of snow thrown off round the edge
            for (int i = 0; i < 6; i++)
            {
                float da = i * 1.07f + 0.5f, dr = 0.76f + 0.1f * ((i * 37) % 3), rad = 0.045f + 0.02f * (i % 3);
                var c = new Vector2(Mathf.Cos(da), Mathf.Sin(da)) * dr;
                float d = Vector2.Distance(p, c);
                float fa = Mathf.Clamp01((rad - d) * 40f);
                if (fa > a) { a = fa; shade = Mathf.Lerp(0.95f, 0.85f, d / rad); }
            }
            return new Color(shade, shade, shade, a);
        }));

        public static Texture2D MusicNote => s_Note ? s_Note : (s_Note = Make("Note", 64, (u, v) =>
        {
            float a = 0f;
            // head
            float hx = (u - 0.38f) / 0.16f, hy = (v - 0.26f) / 0.11f;
            if (hx * hx + hy * hy < 1f) a = 1f;
            // stem
            if (u > 0.5f && u < 0.57f && v > 0.26f && v < 0.85f) a = 1f;
            // flag
            if (u > 0.55f && u < 0.78f && v > 0.62f + (u - 0.55f) * 0.8f && v < 0.85f && v > 0.7f - (u - 0.55f) * 0.2f) a = 1f;
            return new Color(1, 1, 1, a);
        }));

        public static Texture2D Heart => s_Heart ? s_Heart : (s_Heart = Make("Heart", 64, (u, v) =>
        {
            float x = (u - 0.5f) * 2.4f, y = (v - 0.45f) * 2.4f;
            float f = Mathf.Pow(x * x + y * y - 1f, 3f) - x * x * y * y * y;
            return new Color(1, 1, 1, f < 0 ? 1f : 0f);
        }));

        static Texture2D Make(string name, int size, System.Func<float, float, Color> f)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = f((x + 0.5f) / size, (y + 0.5f) / size);
            t.SetPixels(px);
            t.Apply(true, false);
            return t;
        }

        static Texture2D MakeCircle()
        {
            const int S = 256;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, true) { name = "EvidenceCircle", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[S * S];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(1, 1, 1, 0);
            // a slightly wobbly ellipse that overshoots its start, like a marker loop
            var rng = new System.Random(7);
            float wob1 = (float)rng.NextDouble() * 6f, wob2 = (float)rng.NextDouble() * 6f;
            int steps = 900;
            for (int s = 0; s < steps; s++)
            {
                float t01 = s / (float)steps;
                float ang = Mathf.Lerp(-0.5f, Mathf.PI * 2f + 0.65f, t01);
                float r = 0.4f + 0.022f * Mathf.Sin(ang * 3f + wob1) + 0.012f * Mathf.Sin(ang * 7f + wob2) + t01 * 0.03f;
                float cx = 0.5f + Mathf.Cos(ang) * r * 1.08f;
                float cy = 0.5f + Mathf.Sin(ang) * r * 0.92f;
                float width = Mathf.Lerp(4.5f, 7.5f, Mathf.Sin(t01 * Mathf.PI)) * (t01 > 0.92f ? Mathf.Lerp(1f, 0.35f, (t01 - 0.92f) / 0.08f) : 1f);
                Dab(px, S, cx * S, cy * S, width);
            }
            t.SetPixels(px);
            t.Apply(true, false);
            return t;
        }

        static void Dab(Color[] px, int S, float cx, float cy, float r)
        {
            int x0 = Mathf.FloorToInt(cx - r - 1), x1 = Mathf.CeilToInt(cx + r + 1);
            int y0 = Mathf.FloorToInt(cy - r - 1), y1 = Mathf.CeilToInt(cy + r + 1);
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(S - 1, y1); y++)
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(S - 1, x1); x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(r - d + 0.5f) * 0.9f;
                    var c = px[y * S + x];
                    c.a = Mathf.Max(c.a, a);
                    px[y * S + x] = c;
                }
        }

        /// <summary>Renders a word as a rubber-stamp texture (GUILTY / CASE CLOSED) without needing a font.</summary>
        public static Texture2D StampWord(string word, int height = 128)
        {
            const int glyphW = 5, glyphH = 7;
            int scale = height / (glyphH + 4);
            int w = (word.Length * (glyphW + 1) + 3) * scale;
            int h = (glyphH + 4) * scale;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Stamp_" + word, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            var rng = new System.Random(word.GetHashCode());
            // border
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool border = x < scale * 0.8f || y < scale * 0.8f || x > w - scale * 0.8f || y > h - scale * 0.8f;
                    px[y * w + x] = new Color(1, 1, 1, border ? 1f : 0f);
                }
            for (int i = 0; i < word.Length; i++)
            {
                var rows = Glyph(word[i]);
                for (int gy = 0; gy < glyphH; gy++)
                    for (int gx = 0; gx < glyphW; gx++)
                    {
                        if (((rows[gy] >> (glyphW - 1 - gx)) & 1) == 0) continue;
                        int ox = (2 + i * (glyphW + 1) + gx) * scale, oy = (2 + (glyphH - 1 - gy)) * scale;
                        for (int yy = 0; yy < scale; yy++)
                            for (int xx = 0; xx < scale; xx++)
                                px[(oy + yy) * w + ox + xx] = new Color(1, 1, 1, 1);
                    }
            }
            // ink wear
            for (int k = 0; k < px.Length; k++)
                if (px[k].a > 0f && rng.NextDouble() < 0.07) px[k].a = 0.35f;
            t.SetPixels(px);
            t.Apply(true, false);
            return t;
        }

        static int[] Glyph(char c)
        {
            switch (char.ToUpperInvariant(c))
            {
                case 'A': return new[] { 0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 };
                case 'C': return new[] { 0b01110, 0b10001, 0b10000, 0b10000, 0b10000, 0b10001, 0b01110 };
                case 'D': return new[] { 0b11110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b11110 };
                case 'E': return new[] { 0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111 };
                case 'G': return new[] { 0b01110, 0b10001, 0b10000, 0b10111, 0b10001, 0b10001, 0b01111 };
                case 'I': return new[] { 0b01110, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110 };
                case 'L': return new[] { 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b11111 };
                case 'O': return new[] { 0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 };
                case 'S': return new[] { 0b01111, 0b10000, 0b10000, 0b01110, 0b00001, 0b00001, 0b11110 };
                case 'T': return new[] { 0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100 };
                case 'U': return new[] { 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 };
                case 'Y': return new[] { 0b10001, 0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b00100 };
                case '!': return new[] { 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00000, 0b00100 };
                default: return new[] { 0, 0, 0, 0, 0, 0, 0 };
            }
        }
    }
}
