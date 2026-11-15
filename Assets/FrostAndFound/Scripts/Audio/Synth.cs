using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    public enum Sfx
    {
        Footstep, ScoopCrunch, Shutter, Whirr, Whoosh, Splat, Bonk, Tweet, Sting, Twinkle, Marker, StampSlam,
        UiClick, Plop, Scribble, BookThud, Whistle, Fanfare, RoomTone, Boing, Gasp, UiHover, Alarm, Aurora,
        // the opening sequence
        Knock, Thud, DoorBang, RecordScratch, Siren, Jingle
    }

    /// <summary>
    /// Synthesised placeholder audio: every sound in the game exists from minute one, and each is replaced the
    /// moment a real clip named after it lands in Resources/SFX (e.g. Resources/SFX/Shutter.wav).
    /// All generators are pure functions of (sample rate, seed) and run off the main thread.
    /// </summary>
    public static class Synth
    {
        public const int SR = 32000;

        // ------------------------------------------------------------------ helpers
        static float Saw(double phase) => (float)(2.0 * (phase - Math.Floor(phase + 0.5)));
        static float Sq(double phase) => (phase - Math.Floor(phase)) < 0.5 ? 1f : -1f;
        static float Sin(double phase) => (float)Math.Sin(phase * 2.0 * Math.PI);
        static float Mtof(float m) => 440f * Mathf.Pow(2f, (m - 69f) / 12f);

        struct Biquad
        {
            float a0, a1, a2, b1, b2, z1, z2;
            public static Biquad BandPass(float f, float q, int sr)
            {
                var bq = new Biquad();
                float w = 2f * Mathf.PI * Mathf.Clamp(f, 20f, sr * 0.45f) / sr, alpha = Mathf.Sin(w) / (2f * q), cos = Mathf.Cos(w), n = 1f + alpha;
                bq.a0 = alpha / n; bq.a1 = 0; bq.a2 = -alpha / n; bq.b1 = -2f * cos / n; bq.b2 = (1f - alpha) / n;
                return bq;
            }
            public static Biquad LowPass(float f, float q, int sr)
            {
                var bq = new Biquad();
                float w = 2f * Mathf.PI * Mathf.Clamp(f, 20f, sr * 0.45f) / sr, alpha = Mathf.Sin(w) / (2f * q), cos = Mathf.Cos(w), n = 1f + alpha;
                bq.a0 = (1f - cos) / 2f / n; bq.a1 = (1f - cos) / n; bq.a2 = bq.a0; bq.b1 = -2f * cos / n; bq.b2 = (1f - alpha) / n;
                return bq;
            }
            public static Biquad HighPass(float f, float q, int sr)
            {
                var bq = new Biquad();
                float w = 2f * Mathf.PI * Mathf.Clamp(f, 20f, sr * 0.45f) / sr, alpha = Mathf.Sin(w) / (2f * q), cos = Mathf.Cos(w), n = 1f + alpha;
                bq.a0 = (1f + cos) / 2f / n; bq.a1 = -(1f + cos) / n; bq.a2 = bq.a0; bq.b1 = -2f * cos / n; bq.b2 = (1f - alpha) / n;
                return bq;
            }
            public float Process(float x)
            {
                float y = a0 * x + z1;
                z1 = a1 * x - b1 * y + z2;
                z2 = a2 * x - b2 * y;
                return y;
            }
        }

        static float[] Buf(float seconds) => new float[Mathf.Max(1, (int)(seconds * SR))];

        static void Fade(float[] d, float inSec = 0.004f, float outSec = 0.006f)
        {
            int fi = Mathf.Min(d.Length / 2, (int)(inSec * SR)), fo = Mathf.Min(d.Length / 2, (int)(outSec * SR));
            for (int i = 0; i < fi; i++) d[i] *= i / (float)fi;
            for (int i = 0; i < fo; i++) d[d.Length - 1 - i] *= i / (float)fo;
        }

        static void Normalize(float[] d, float peak = 0.9f)
        {
            float m = 1e-6f;
            foreach (var v in d) m = Mathf.Max(m, Mathf.Abs(v));
            float k = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        static float Env(float t, float attack, float decay) => t < attack ? t / Mathf.Max(attack, 1e-5f) : Mathf.Exp(-(t - attack) / Mathf.Max(decay, 1e-5f));

        // ------------------------------------------------------------------ sound effects
        public static float[] Generate(Sfx s, int seed)
        {
            var rng = new System.Random(seed * 7919 + (int)s * 104729);
            float N() => (float)(rng.NextDouble() * 2.0 - 1.0);
            switch (s)
            {
                case Sfx.Footstep: return Crunch(0.17f, 9, 0.3f, rng);
                case Sfx.ScoopCrunch: return Crunch(0.42f, 26, 0.55f, rng, squeak: true);
                case Sfx.Shutter:
                {
                    var d = Buf(0.32f);
                    var hp = Biquad.HighPass(2500, 0.7f, SR);
                    var lp = Biquad.LowPass(420, 0.8f, SR);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)SR;
                        float click = t < 0.006f ? hp.Process(N()) * (1f - t / 0.006f) : 0f;           // ka-
                        float tc = t - 0.07f;
                        float chunk = 0f;
                        if (tc > 0f)
                        {
                            float f = Mathf.Lerp(95f, 55f, Mathf.Clamp01(tc / 0.08f));
                            chunk = Mathf.Sin(2f * Mathf.PI * f * tc) * Env(tc, 0.002f, 0.06f) * 0.9f   // CHUNK
                                    + lp.Process(N()) * Env(tc, 0.001f, 0.035f) * 1.2f
                                    + Mathf.Sin(2f * Mathf.PI * 1850f * tc) * Env(tc, 0.001f, 0.02f) * 0.15f;
                        }
                        d[i] = click * 0.8f + chunk;
                    }
                    Normalize(d); Fade(d, 0.0005f); return d;
                }
                case Sfx.Whirr:
                {
                    var d = Buf(0.5f);
                    var lp = Biquad.LowPass(1400, 0.9f, SR);
                    double ph = 0;
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)SR;
                        float f = 170f + 25f * Mathf.Sin(t * 9f);
                        ph += f / SR;
                        float am = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 34f * t);
                        d[i] = lp.Process(Saw(ph) * 0.6f + Saw(ph * 2.01) * 0.3f + N() * 0.08f) * am * Mathf.Sin(Mathf.PI * t / 0.5f);
                    }
                    Normalize(d, 0.6f); Fade(d); return d;
                }
                case Sfx.Whoosh:
                {
                    var d = Buf(0.38f);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float u = i / (float)SR / 0.38f;
                        d[i] = N() * Ease.Bump(u);
                    }
                    // swept band-pass in blocks (cheap)
                    var outd = new float[d.Length];
                    var f = Biquad.BandPass(600, 1.1f, SR);
                    for (int i = 0; i < d.Length; i++)
                    {
                        if (i % 256 == 0) f = Biquad.BandPass(Mathf.Lerp(450f, 2000f, Ease.Bump(i / (float)d.Length)), 1.1f, SR);
                        outd[i] = f.Process(d[i]);
                    }
                    Normalize(outd, 0.7f); Fade(outd, 0.02f, 0.05f); return outd;
                }
                case Sfx.Splat:
                {
                    var d = Buf(0.26f);
                    var lp = Biquad.LowPass(1300, 0.7f, SR);
                    var lp2 = Biquad.LowPass(300, 0.9f, SR);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)SR;
                        d[i] = lp.Process(N()) * Env(t, 0.003f, 0.06f) + lp2.Process(N()) * Env(t, 0.001f, 0.03f) * 2f;
                    }
                    Normalize(d, 0.85f); Fade(d, 0.001f); return d;
                }
                case Sfx.Bonk:
                {
                    var d = Buf(0.6f);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)SR;
                        float drop = 1f - 0.12f * Mathf.Clamp01(t / 0.1f);
                        float wood = (Mathf.Sin(2f * Mathf.PI * 520f * drop * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 1190f * drop * t)) * Env(t, 0.001f, 0.07f);
                        float tb = t - 0.03f, boing = 0f;
                        if (tb > 0f)
                        {
                            float f = Mathf.Lerp(260f, 620f, Mathf.Clamp01(tb / 0.35f)) * (1f + 0.06f * Mathf.Sin(2f * Mathf.PI * 17f * tb));
                            boing = Mathf.Sin(2f * Mathf.PI * f * tb) * Env(tb, 0.01f, 0.25f) * 0.45f;
                        }
                        d[i] = wood + boing;
                    }
                    Normalize(d); Fade(d, 0.0005f); return d;
                }
                case Sfx.Tweet:
                {
                    var d = Buf(0.5f);
                    for (int c = 0; c < 3; c++)
                    {
                        int start = (int)((0.03f + c * 0.15f) * SR);
                        float len = 0.07f, f0 = 2900f + c * 200f, f1 = 4300f;
                        double ph = 0;
                        for (int i = 0; i < len * SR && start + i < d.Length; i++)
                        {
                            float t = i / (float)SR, u = t / len;
                            ph += Mathf.Lerp(f0, f1, u * u) / SR;
                            d[start + i] += Sin(ph) * Ease.Bump(u) * 0.7f;
                        }
                    }
                    Fade(d); return d;
                }
                case Sfx.Sting: return Sting(rng);
                case Sfx.Knock: return Knock(rng);
                case Sfx.Thud: return Thud(rng);
                case Sfx.DoorBang: return DoorBang(rng);
                case Sfx.RecordScratch: return RecordScratch(rng);
                case Sfx.Siren: return Siren();
                case Sfx.Jingle: return Jingle(rng);
                case Sfx.Alarm: return Alarm();
                case Sfx.Aurora: return Aurora(rng);
                case Sfx.Twinkle:
                {
                    var d = Buf(0.6f);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)SR;
                        d[i] = (Mathf.Sin(2f * Mathf.PI * 2637f * t) * Env(t, 0.002f, 0.18f) + 0.5f * Mathf.Sin(2f * Mathf.PI * 3951f * t) * Env(t, 0.002f, 0.12f)
                               + 0.4f * Mathf.Sin(2f * Mathf.PI * 5274f * (t - 0.06f)) * (t > 0.06f ? Env(t - 0.06f, 0.002f, 0.15f) : 0f)) * 0.5f;
                    }
                    Fade(d); return d;
                }
                case Sfx.Marker:
                {
                    var d = Buf(0.3f);
                    var bp = Biquad.BandPass(2600, 1.5f, SR);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)SR;
                        float wob = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 22f * t);
                        d[i] = bp.Process(N()) * wob * Ease.Bump(t / 0.3f);
                    }
                    Normalize(d, 0.5f); Fade(d); return d;
                }
                case Sfx.StampSlam:
                {
                    var d = Buf(0.4f);
                    var lp = Biquad.LowPass(800, 0.8f, SR);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)SR;
                        d[i] = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(90f, 50f, Mathf.Clamp01(t / 0.1f)) * t) * Env(t, 0.001f, 0.08f)
                               + lp.Process(N()) * Env(t, 0.0005f, 0.04f) * 1.4f;
                    }
                    Normalize(d); Fade(d, 0.0005f); return d;
                }
                case Sfx.UiHover:
                {
                    var d = Buf(0.03f);
                    for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; d[i] = Mathf.Sin(2f * Mathf.PI * 2350f * t) * Env(t, 0.0008f, 0.006f) * 0.5f; }
                    Fade(d, 0.0005f, 0.004f); return d;
                }
                case Sfx.UiClick:
                {
                    var d = Buf(0.06f);
                    var bp = Biquad.BandPass(1800, 1.5f, SR);
                    for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; d[i] = bp.Process(N()) * Env(t, 0.0005f, 0.004f) * 0.5f + Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(1500f, 1100f, t / 0.06f) * t) * Env(t, 0.001f, 0.014f); }
                    Normalize(d, 0.55f); Fade(d, 0.0005f); return d;
                }
                case Sfx.Plop:
                {
                    var d = Buf(0.18f);
                    double ph = 0;
                    for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; ph += Mathf.Lerp(480f, 140f, Mathf.Clamp01(t / 0.09f)) / SR; d[i] = Sin(ph) * Env(t, 0.002f, 0.05f); }
                    Fade(d); return d;
                }
                case Sfx.Scribble:
                {
                    var d = Buf(0.9f);
                    var bp = Biquad.BandPass(4200, 1.2f, SR);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)SR;
                        float strokes = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 6.5f * t + Mathf.Sin(t * 13f)));
                        d[i] = bp.Process(N()) * strokes * strokes;
                    }
                    Normalize(d, 0.45f); Fade(d, 0.02f, 0.05f); return d;
                }
                case Sfx.BookThud:
                {
                    var d = Buf(0.5f);
                    var lp = Biquad.LowPass(500, 0.8f, SR);
                    for (int k = 0; k < 3; k++)
                    {
                        int st = (int)((k * 0.11f + (float)rng.NextDouble() * 0.03f) * SR);
                        for (int i = 0; i < 0.12f * SR && st + i < d.Length; i++)
                        {
                            float t = i / (float)SR;
                            d[st + i] += (lp.Process(N()) * 1.5f + Mathf.Sin(2f * Mathf.PI * (110f - k * 15f) * t)) * Env(t, 0.001f, 0.035f);
                        }
                    }
                    Normalize(d, 0.8f); Fade(d, 0.0005f); return d;
                }
                case Sfx.Whistle: return Whistle(rng);
                case Sfx.Fanfare: return Fanfare();
                case Sfx.RoomTone:
                {
                    var d = Buf(6f);
                    float b = 0f;
                    var lp = Biquad.LowPass(380, 0.6f, SR);
                    for (int i = 0; i < d.Length; i++) { b = Mathf.Clamp(b + N() * 0.02f, -1f, 1f) * 0.998f; d[i] = lp.Process(b); }
                    // make the loop seamless by cross-fading the tail into the head
                    int x = SR / 2;
                    for (int i = 0; i < x; i++) { float k = i / (float)x; d[i] = d[i] * k + d[d.Length - x + i] * (1f - k); }
                    Array.Resize(ref d, d.Length - x);
                    Normalize(d, 0.5f); return d;
                }
                case Sfx.Boing:
                {
                    var d = Buf(0.35f);
                    double ph = 0;
                    for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; ph += (300f + 380f * Mathf.Clamp01(t / 0.3f)) * (1f + 0.08f * Mathf.Sin(2f * Mathf.PI * 22f * t)) / SR; d[i] = Sin(ph) * Env(t, 0.005f, 0.14f) * 0.85f; }
                    Fade(d); return d;
                }
                case Sfx.Gasp:
                {
                    var d = Buf(0.35f);
                    var bp = Biquad.BandPass(1600, 1.1f, SR);
                    for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; d[i] = bp.Process(N()) * Ease.Bump(t / 0.35f); }
                    Normalize(d, 0.5f); Fade(d); return d;
                }
            }
            return Buf(0.05f);
        }

        static float[] Crunch(float seconds, int grains, float body, System.Random rng, bool squeak = false)
        {
            var d = Buf(seconds);
            float N() => (float)(rng.NextDouble() * 2.0 - 1.0);
            var lp = Biquad.LowPass(260, 0.7f, SR);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                d[i] = lp.Process(N()) * Env(t, 0.004f, seconds * 0.25f) * body * 2f;
            }
            for (int g = 0; g < grains; g++)
            {
                int start = (int)(Mathf.Pow((float)rng.NextDouble(), 1.6f) * (seconds * 0.8f) * SR);
                var bp = Biquad.BandPass(1400f + (float)rng.NextDouble() * 3200f, 1.4f, SR);
                float amp = 0.4f + (float)rng.NextDouble() * 0.8f;
                int len = (int)(0.008f * SR + rng.NextDouble() * 0.012f * SR);
                for (int i = 0; i < len && start + i < d.Length; i++)
                    d[start + i] += bp.Process(N()) * amp * Env(i / (float)SR, 0.0005f, 0.004f) * 3f;
            }
            if (squeak)
            {
                int st = (int)(0.12f * SR);
                double ph = 0;
                for (int i = 0; i < 0.12f * SR && st + i < d.Length; i++)
                {
                    float t = i / (float)SR;
                    ph += (880f + 120f * Mathf.Sin(2f * Mathf.PI * 30f * t)) / SR;
                    d[st + i] += Sin(ph) * Ease.Bump(t / 0.12f) * 0.18f;
                }
            }
            Normalize(d, 0.9f); Fade(d, 0.001f, 0.02f);
            return d;
        }

        static void Brass(float[] d, int start, float freq, float seconds, float amp, float bright = 1f)
        {
            var lp = Biquad.LowPass(900f * bright, 0.9f, SR);
            double ph = 0, ph2 = 0;
            int len = (int)(seconds * SR);
            for (int i = 0; i < len && start + i < d.Length; i++)
            {
                float t = i / (float)SR;
                float vib = 1f + (t > 0.15f ? 0.006f * Mathf.Sin(2f * Mathf.PI * 5.2f * t) : 0f);
                ph += freq * vib / SR; ph2 += freq * 1.003f * vib / SR;
                float env = Mathf.Min(1f, t / 0.025f) * Mathf.Exp(-t / (seconds * 1.6f)) * Mathf.Clamp01((seconds - t) / 0.06f);
                if (i % 64 == 0) lp = Biquad.LowPass(Mathf.Lerp(600f, 2400f * bright, Mathf.Clamp01(env)), 0.9f, SR);
                d[start + i] += lp.Process((Saw(ph) + Saw(ph2)) * 0.5f) * env * amp;
            }
        }

        /// <summary>The northern lights: a glassy major-ninth pad swelling in over a few seconds, with slow
        /// shimmering bell chimes drifting over it (about 11 s).</summary>
        static float[] Aurora(System.Random rng)
        {
            var d = Buf(11f);
            float[] chord = { 48, 55, 59, 62, 64 };   // C, G, B, D, E: wide and open
            for (int n = 0; n < chord.Length; n++)
            {
                float f = Mtof(chord[n]);
                double ph1 = 0, ph2 = 0;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)SR;
                    float vib = 1f + 0.003f * Mathf.Sin(2f * Mathf.PI * (0.27f + n * 0.06f) * t);
                    ph1 += f * vib / SR;
                    ph2 += f * 1.004f * vib / SR;   // a slow chorus between the two voices
                    float env = Mathf.Clamp01(t / 3.5f) * Mathf.Clamp01((11f - t) / 4.5f);
                    d[i] += (Sin(ph1) + Sin(ph2)) * 0.11f * env;
                }
            }
            float[] bells = { 72, 74, 76, 79, 83, 84, 86, 88, 91 };
            for (float tc = 1.4f; tc < 9.4f; tc += 0.38f + (float)rng.NextDouble() * 0.75f)
            {
                float f = Mtof(bells[rng.Next(bells.Length)]);
                int start = (int)(tc * SR);
                double ph = 0;
                for (int i = 0; start + i < d.Length && i < 2.6f * SR; i++)
                {
                    float t = i / (float)SR;
                    ph += f / SR;
                    d[start + i] += (Sin(ph) + 0.3f * Sin(ph * 2.76)) * 0.1f * Mathf.Exp(-t * 2.1f) * Mathf.Clamp01(t / 0.006f);
                }
            }
            Normalize(d, 0.7f); Fade(d, 0.05f, 1.5f);
            return d;
        }

        /// <summary>The snowball-fight alarm: a buzzing two-tone siren that whoops up and down for about 2.6 s.</summary>
        static float[] Alarm()
        {
            var d = Buf(2.6f);
            var lp = Biquad.LowPass(3200, 0.7f, SR);
            double ph = 0, ph2 = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float sweep = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * 1.6f * t);   // whoop up and down, 1.6 times a second
                float f = Mathf.Lerp(600f, 980f, sweep);
                ph += f / SR; ph2 += f * 1.5f / SR;
                float tone = Sq(ph) * 0.34f + Saw(ph2) * 0.22f + Sin(ph) * 0.34f;
                float buzz = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 22f * t);
                d[i] = lp.Process(tone * buzz);
            }
            Normalize(d, 0.85f); Fade(d, 0.01f, 0.3f);
            return d;
        }

        // ------------------------------------------------------------------ the opening sequence
        /// <summary>One knuckle rap on a wooden door: a woody knock with a low body.</summary>
        static float[] Knock(System.Random rng)
        {
            var d = Buf(0.28f);
            var bp = Biquad.BandPass(320f, 1.4f, SR);
            var bp2 = Biquad.BandPass(1300f, 3f, SR);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float n = (float)(rng.NextDouble() * 2 - 1);
                d[i] = bp.Process(n) * Env(t, 0.001f, 0.035f) * 2.4f
                     + bp2.Process(n) * Env(t, 0.0005f, 0.012f) * 0.9f
                     + Mathf.Sin(2f * Mathf.PI * 150f * t) * Env(t, 0.002f, 0.06f) * 0.7f;
            }
            Normalize(d, 0.95f); Fade(d); return d;
        }

        /// <summary>Something big and heavy hitting the snow behind you.</summary>
        static float[] Thud(System.Random rng)
        {
            var d = Buf(1.1f);
            var lp = Biquad.LowPass(380f, 0.8f, SR);
            double ph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                ph += Mathf.Lerp(95f, 42f, Mathf.Clamp01(t / 0.35f)) / SR;
                float n = (float)(rng.NextDouble() * 2 - 1);
                d[i] = Sin(ph) * Env(t, 0.003f, 0.32f) * 1.1f + lp.Process(n) * Env(t, 0.002f, 0.12f) * 1.6f;
            }
            var crunch = Crunch(0.5f, 30, 0.6f, rng);
            for (int i = 0; i < crunch.Length && i < d.Length; i++) d[i] += crunch[i] * 0.35f;
            // driven hard into a soft clip: the same peak, but far more weight behind it (much louder to the ear)
            Normalize(d, 1f);
            for (int i = 0; i < d.Length; i++) d[i] = (float)System.Math.Tanh(d[i] * 2.6f);
            Normalize(d, 0.98f); Fade(d); return d;
        }

        /// <summary>A door flung open and banging against its stop, with a rattle.</summary>
        static float[] DoorBang(System.Random rng)
        {
            var d = Buf(0.8f);
            var bp = Biquad.BandPass(240f, 1.2f, SR);
            var hp = Biquad.HighPass(1800f, 0.7f, SR);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float n = (float)(rng.NextDouble() * 2 - 1);
                float slam = bp.Process(n) * Env(t, 0.001f, 0.09f) * 2.6f + Mathf.Sin(2f * Mathf.PI * 85f * t) * Env(t, 0.003f, 0.18f);
                // the handle and hinges rattle after the hit
                float rt = t - 0.05f;
                float rattle = rt > 0f ? hp.Process(n) * Env(rt, 0.002f, 0.2f) * (0.5f + 0.5f * Sq(rt * 38.0)) * 0.5f : 0f;
                d[i] = slam + rattle;
            }
            Normalize(d, 0.95f); Fade(d); return d;
        }

        /// <summary>The needle dragged across the record: the music's over.</summary>
        static float[] RecordScratch(System.Random rng)
        {
            var d = Buf(0.6f);
            double ph = 0;
            var bp = Biquad.BandPass(900f, 1.5f, SR);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                // forward-back-forward: the pitch rides the hand
                float u = t / 0.55f;
                float speed = u < 0.35f ? Mathf.Lerp(0.4f, 2.6f, u / 0.35f) : u < 0.6f ? Mathf.Lerp(2.6f, -1.8f, (u - 0.35f) / 0.25f) : Mathf.Lerp(-1.8f, 0.2f, (u - 0.6f) / 0.4f);
                ph += 260f * Mathf.Abs(speed) / SR;
                if (i % 64 == 0) bp = Biquad.BandPass(500f + 900f * Mathf.Abs(speed), 1.6f, SR);
                float n = (float)(rng.NextDouble() * 2 - 1);
                d[i] = (Saw(ph) * 0.45f + bp.Process(n) * 1.8f) * Mathf.Clamp01(Mathf.Abs(speed)) * Env(t, 0.005f, 0.4f);
            }
            Normalize(d, 0.9f); Fade(d); return d;
        }

        /// <summary>Police sirens swelling up: a wail and a hi-lo, together.</summary>
        static float[] Siren()
        {
            var d = Buf(3.2f);
            double a = 0, b = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float wail = 820f + 260f * Mathf.Sin(2f * Mathf.PI * 0.85f * t);
                float hilo = (int)(t / 0.42f) % 2 == 0 ? 960f : 720f;
                a += wail / SR; b += hilo / SR;
                float tri = 1f - 4f * Mathf.Abs((float)(a - Math.Floor(a)) - 0.5f);
                float swell = Mathf.SmoothStep(0f, 1f, t / 1.3f) * Mathf.Clamp01((3.2f - t) / 0.7f);
                d[i] = (tri * 0.55f + Sq(b) * 0.12f + Sin(b) * 0.25f) * swell;
            }
            Normalize(d, 0.75f); Fade(d); return d;
        }

        /// <summary>A bouncy little snow-day tune (glockenspiel, plucked bass, shaker), 120 bpm.</summary>
        static float[] Jingle(System.Random rng)
        {
            const float eighth = 0.25f;
            var d = Buf(8.4f);
            int[] mel =
            {
                72, 76, 79, 76, 72, 76, 79, 84,
                71, 74, 79, 74, 71, 74, 79, 83,
                72, 76, 81, 76, 72, 76, 81, 84,
                77, 81, 84, 81, 79, 83, 86, 83,
            };
            int[] bass = { 48, 48, 48, 48, 43, 43, 43, 43, 45, 45, 45, 45, 41, 41, 43, 43 };
            for (int k = 0; k < mel.Length; k++)
            {
                int start = (int)(k * eighth * SR);
                float f = Mtof(mel[k]);
                int len = (int)(0.7f * SR);
                for (int i = 0; i < len && start + i < d.Length; i++)
                {
                    float t = i / (float)SR;
                    d[start + i] += (Mathf.Sin(2f * Mathf.PI * f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * f * 2.76f * t) * Env(t, 0.001f, 0.08f)) * Env(t, 0.002f, 0.32f) * 0.32f;
                }
            }
            for (int k = 0; k < bass.Length; k++) Pluck(d, (int)(k * 2 * eighth * SR), Mtof(bass[k]), 0.45f, 0.5f, rng);
            var hp = Biquad.HighPass(6000f, 0.7f, SR);
            for (int k = 0; k < 32; k++)
            {
                int start = (int)((k + 0.5f) * eighth * SR);
                for (int i = 0; i < (int)(0.06f * SR) && start + i < d.Length; i++)
                    d[start + i] += hp.Process((float)(rng.NextDouble() * 2 - 1)) * Env(i / (float)SR, 0.002f, 0.02f) * 0.25f;
            }
            Normalize(d, 0.85f); Fade(d, 0.004f, 0.3f); return d;
        }

        static float[] Sting(System.Random rng)
        {
            var d = Buf(1.9f);
            // dun - dun - DUNNN (with a timpani roll under the last hit)
            Brass(d, 0, 146.83f, 0.22f, 0.7f);
            Brass(d, (int)(0.28f * SR), 174.61f, 0.22f, 0.75f);
            Brass(d, (int)(0.58f * SR), 138.59f, 1.25f, 1f, 1.3f);
            Brass(d, (int)(0.58f * SR), 69.3f, 1.25f, 0.6f, 0.8f);
            var lp = Biquad.LowPass(160, 0.8f, SR);
            for (int i = (int)(0.56f * SR); i < d.Length; i++)
            {
                float t = i / (float)SR - 0.56f;
                d[i] += (lp.Process((float)(rng.NextDouble() * 2 - 1)) * 2.5f + Mathf.Sin(2f * Mathf.PI * 70f * t) * 0.5f) * Env(t, 0.005f, 0.35f);
            }
            Normalize(d, 0.9f); Fade(d, 0.002f, 0.2f);
            return d;
        }

        static float[] Whistle(System.Random rng)
        {
            // an innocent little tune, looped
            float[] notes = { 79, 76, 79, 81, 79, 76, 74, 76, 79, 79, 76, 72, 74 };
            float[] durs = { 0.25f, 0.25f, 0.25f, 0.5f, 0.25f, 0.25f, 0.5f, 0.5f, 0.25f, 0.25f, 0.25f, 0.25f, 1f };
            float total = 0; foreach (var x in durs) total += x;
            var d = Buf(total + 0.8f);
            double ph = 0; float t0 = 0f; float prevF = Mtof(notes[0]);
            for (int n = 0; n < notes.Length; n++)
            {
                float f = Mtof(notes[n]);
                int st = (int)(t0 * SR), len = (int)(durs[n] * SR);
                for (int i = 0; i < len && st + i < d.Length; i++)
                {
                    float t = i / (float)SR;
                    float ff = Mathf.Lerp(prevF, f, Mathf.Clamp01(t / 0.03f)) * (1f + 0.012f * Mathf.Sin(2f * Mathf.PI * 6f * t));
                    ph += ff / SR;
                    float env = Mathf.Min(1f, t / 0.02f) * Mathf.Clamp01((durs[n] - t) / 0.04f + 0.4f);
                    d[st + i] = Sin(ph) * env * 0.5f + (float)(rng.NextDouble() * 2 - 1) * 0.02f * env;
                }
                prevF = f; t0 += durs[n];
            }
            Fade(d, 0.01f, 0.05f);
            return d;
        }

        static float[] Fanfare()
        {
            var d = Buf(2.2f);
            float[] arp = { 60, 64, 67, 72 };
            for (int i = 0; i < arp.Length; i++) Brass(d, (int)(i * 0.13f * SR), Mtof(arp[i]), 0.25f, 0.6f, 1.4f);
            foreach (var m in new float[] { 60, 64, 67, 72 }) Brass(d, (int)(0.55f * SR), Mtof(m), 1.4f, 0.45f, 1.5f);
            Normalize(d, 0.9f); Fade(d, 0.002f, 0.3f);
            return d;
        }

        // ------------------------------------------------------------------ the noir score (92 BPM, D minor, 16 bars)
        public static float[] NoirScore()
        {
            const float bpm = 92f;
            float beat = 60f / bpm;
            int bars = 16;
            var d = Buf(bars * 4 * beat);
            var rng = new System.Random(42);
            // chords: root midi + quality (0 minor, 1 dominant7, 2 half-dim)
            (int root, int q)[] prog = { (50, 0), (50, 0), (55, 0), (50, 0), (46, 1), (45, 1), (50, 0), (45, 1),
                                         (50, 0), (50, 0), (55, 0), (55, 0), (52, 2), (45, 1), (50, 0), (45, 1) };
            int[][] voicing = { new[] { 0, 3, 7, 10 }, new[] { 0, 4, 7, 10 }, new[] { 0, 3, 6, 10 } };

            for (int b = 0; b < bars; b++)
            {
                var (root, q) = prog[b];
                var next = prog[(b + 1) % bars].root;
                // walking pizzicato bass
                int[] walk = { root - 12, root - 12 + (q == 1 ? 4 : 3), root - 12 + 7, next - 12 + ((next - root) > 0 ? -1 : 1) };
                for (int k = 0; k < 4; k++)
                    Pluck(d, (int)((b * 4 + k) * beat * SR), Mtof(walk[k]), 0.55f, 0.6f, rng);
                // pizzicato chord stabs on 2 and 4 (and a sneaky "and" of 4 sometimes)
                foreach (var pos in (b % 2 == 0) ? new[] { 1f, 3f } : new[] { 1f, 3f, 3.5f })
                    foreach (var iv in voicing[q])
                        Pluck(d, (int)((b * 4 + pos) * beat * SR) + rng.Next(0, 200), Mtof(root + 12 + iv), 0.25f, 0.18f, rng);
                // brushes: swish on 2 & 4, light ticks on 8ths
                for (int e = 0; e < 8; e++)
                {
                    bool back = e == 2 || e == 6;
                    Brush(d, (int)((b * 4 + e * 0.5f) * beat * SR), back ? 0.18f : 0.05f, back ? 0.16f : 0.05f, rng);
                }
            }
            // muted trumpet melody (8-bar phrase, played twice with a variation)
            var melody = new List<(float beat, float dur, int midi)>
            {
                (1.5f, .5f, 62), (2f, .5f, 65), (2.5f, .5f, 67), (3f, 1f, 68),
                (4f, 1.5f, 67), (5.5f, .5f, 65), (6f, 2f, 62),
                (8.5f, .5f, 67), (9f, .5f, 70), (9.5f, .5f, 74), (10f, 1.5f, 72), (11.5f, .5f, 70),
                (12f, 2f, 69), (14.5f, .5f, 67), (15f, 1f, 65),
                (16f, 1f, 68), (17f, .5f, 67), (17.5f, .5f, 65), (18f, 1f, 62), (19f, .5f, 65), (19.5f, .5f, 67),
                (20f, 1.5f, 69), (21.5f, .5f, 73), (22f, 2f, 76),
                (24f, .5f, 74), (24.5f, .5f, 72), (25f, .5f, 69), (25.5f, .5f, 67), (26f, 1f, 65), (27f, 1f, 62),
                (28f, 1.5f, 61), (29.5f, .5f, 64), (30f, 1.5f, 69),
            };
            for (int pass = 0; pass < 2; pass++)
                foreach (var (bt, dur, midi) in melody)
                {
                    int m = midi + (pass == 1 && bt >= 16f && bt < 24f ? 12 : 0);
                    float b0 = bt + pass * 32f;
                    MutedTrumpet(d, (int)(b0 * beat * SR), Mtof(m), dur * beat * 0.95f, 0.32f);
                }
            // soft clip and level
            for (int i = 0; i < d.Length; i++) d[i] = (float)Math.Tanh(d[i] * 1.3f);
            Normalize(d, 0.8f);
            return d;
        }

        static void Pluck(float[] d, int start, float freq, float seconds, float amp, System.Random rng)
        {
            // Karplus–Strong: a noise burst in a delay line, averaged as it recirculates — a pizzicato string
            int n = Mathf.Max(2, (int)(SR / freq));
            var line = new float[n];
            for (int i = 0; i < n; i++) line[i] = (float)(rng.NextDouble() * 2 - 1);
            float prev = 0f;
            int len = (int)(seconds * SR);
            for (int i = 0; i < len && start + i < d.Length; i++)
            {
                int idx = i % n;
                float cur = line[idx];
                float avg = 0.5f * (cur + line[(idx + 1) % n]) * 0.992f;
                line[idx] = avg;
                float y = 0.6f * cur + 0.4f * prev; prev = y;
                float env = Mathf.Clamp01((len - i) / (0.03f * SR));
                d[start + i] += y * amp * env;
            }
        }

        static void Brush(float[] d, int start, float seconds, float amp, System.Random rng)
        {
            var hp = Biquad.HighPass(2200, 0.7f, SR);
            int len = (int)(seconds * SR);
            for (int i = 0; i < len && start + i < d.Length; i++)
            {
                float t = i / (float)SR;
                d[start + i] += hp.Process((float)(rng.NextDouble() * 2 - 1)) * amp * Env(t, 0.006f, seconds * 0.35f);
            }
        }

        static void MutedTrumpet(float[] d, int start, float freq, float seconds, float amp)
        {
            var wah = Biquad.BandPass(1100, 2.2f, SR);
            var lp = Biquad.LowPass(2800, 0.7f, SR);
            double ph = 0;
            int len = (int)(seconds * SR);
            for (int i = 0; i < len && start + i < d.Length; i++)
            {
                float t = i / (float)SR;
                float vib = 1f + (t > 0.12f ? 0.005f * Mathf.Sin(2f * Mathf.PI * 5.5f * t) : 0f);
                float scoop = t < 0.04f ? Mathf.Lerp(0.97f, 1f, t / 0.04f) : 1f;   // jazzy scoop into the note
                ph += freq * vib * scoop / SR;
                float env = Mathf.Min(1f, t / 0.035f) * Mathf.Clamp01((seconds - t) / 0.05f);
                if (i % 96 == 0) wah = Biquad.BandPass(Mathf.Lerp(700f, 1500f, env * (0.7f + 0.3f * Mathf.Sin(t * 7f))), 2.2f, SR);
                float x = Saw(ph);
                d[start + i] += lp.Process(wah.Process(x) * 2.2f + x * 0.12f) * env * amp;
            }
        }

        // ------------------------------------------------------------------ placeholder voices ("babble")
        public struct VoiceParams { public float pitch; public float formantShift; public float rate; public float breath; }

        public static VoiceParams Voice(CastId who)
        {
            switch (who)
            {
                case CastId.Dexter: return new VoiceParams { pitch = 255f, formantShift = 1.05f, rate = 13.5f, breath = 0.05f };
                case CastId.Sherman: return new VoiceParams { pitch = 330f, formantShift = 1.2f, rate = 14.5f, breath = 0.03f };
                case CastId.Serena: return new VoiceParams { pitch = 300f, formantShift = 1.15f, rate = 13f, breath = 0.08f };
                case CastId.Lucy: return new VoiceParams { pitch = 285f, formantShift = 1.12f, rate = 14f, breath = 0.04f };
                default: return new VoiceParams { pitch = 205f, formantShift = 0.98f, rate = 12.5f, breath = 0.06f }; // Tony
            }
        }

        static readonly (float f1, float f2)[] k_Vowels = { (730, 1090), (530, 1840), (270, 2290), (570, 840), (300, 870), (660, 1720) };

        public static float[] Babble(string text, CastId who, int seed)
        {
            var vp = Voice(who);
            var rng = new System.Random(seed);
            var events = new List<(float dur, bool voiced, float emphasis, float rise)>();
            string clean = text;
            for (int i = 0; i < clean.Length; i++)
            {
                char c = clean[i];
                if (char.IsLetterOrDigit(c))
                {
                    // a syllable roughly every 2.6 letters
                    int run = 0; bool caps = char.IsUpper(c);
                    while (i < clean.Length && char.IsLetterOrDigit(clean[i]) && run < 3) { caps &= char.IsUpper(clean[i]) || !char.IsLetter(clean[i]); i++; run++; }
                    i--;
                    float rise = 0f;
                    int look = i + 1; while (look < clean.Length && clean[look] == ' ') look++;
                    if (look < clean.Length && clean[look] == '?') rise = 1f;
                    events.Add((1f / vp.rate * (0.8f + (float)rng.NextDouble() * 0.4f), true, caps && run > 1 ? 1.5f : 1f, rise));
                }
                else if (c == ' ') events.Add((0.025f, false, 0, 0));
                else if (c == ',' || c == '–' || c == '—' || c == '-') events.Add((0.12f, false, 0, 0));
                else if (c == '.' || c == '!' || c == '?') events.Add((i + 2 < clean.Length && clean[i + 1] == '.' ? 0.12f : 0.2f, false, 0, 0));
            }
            float total = 0.1f; foreach (var e in events) total += e.dur;
            total = Mathf.Min(total, 30f);
            var d = Buf(total);
            int pos = (int)(0.03f * SR);
            double ph = 0;
            var f1 = Biquad.BandPass(700, 3f, SR); var f2 = Biquad.BandPass(1200, 4f, SR);
            float pitchWalk = 0f;
            foreach (var e in events)
            {
                int len = (int)(e.dur * SR);
                if (!e.voiced) { pos += len; continue; }
                var v = k_Vowels[rng.Next(k_Vowels.Length)];
                f1 = Biquad.BandPass(v.f1 * vp.formantShift, 3f, SR);
                f2 = Biquad.BandPass(v.f2 * vp.formantShift, 4f, SR);
                pitchWalk = Mathf.Clamp(pitchWalk + ((float)rng.NextDouble() - 0.5f) * 0.12f, -0.15f, 0.15f);
                float basePitch = vp.pitch * (1f + pitchWalk) * (e.emphasis > 1f ? 1.12f : 1f);
                bool consonant = rng.NextDouble() < 0.55;
                for (int i = 0; i < len && pos + i < d.Length; i++)
                {
                    float u = i / (float)len;
                    float p = basePitch * (1f + e.rise * 0.35f * u) * (1f - 0.06f * u);
                    ph += p / SR;
                    float src = Saw(ph) * 0.6f + Sin(ph) * 0.4f;
                    float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u * 1.15f));
                    float sample = f1.Process(src) * 1.6f + f2.Process(src) * 0.9f + src * 0.08f;
                    if (consonant && u < 0.18f) sample += (float)(rng.NextDouble() * 2 - 1) * 0.35f * (1f - u / 0.18f);
                    sample += (float)(rng.NextDouble() * 2 - 1) * vp.breath;
                    d[pos + i] += sample * env * e.emphasis * 0.5f;
                }
                pos += len;
            }
            Normalize(d, 0.75f); Fade(d, 0.005f, 0.03f);
            return d;
        }

        public static AudioClip ToClip(string name, float[] data, bool loop = false)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
