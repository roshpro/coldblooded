using UnityEngine;

namespace FrostAndFound
{
    /// <summary>Colour grades the real room can wear (Quest passthrough). None = the room exactly as it is.</summary>
    public enum RoomGrade { None, Flash, Icy, Alarm, Noir }

    /// <summary>
    /// The real room tells the story too: the passthrough's colour grade follows the game. The opening drains it to
    /// film noir (RoomTint), the Polaroid's flash lights it up for a blink, a snowball fight turns it ice-blue (pulsing
    /// red while the alarm rings) and back, and the colour returns at CASE CLOSED.
    /// Headset only: the rig turns these into passthrough colour LUTs; elsewhere they're ignored.
    /// </summary>
    public class PassthroughMood : MonoBehaviour
    {
        static PassthroughMood s_I;

        RoomGrade m_Base = RoomGrade.None, m_From = RoomGrade.None;
        float m_Blend = 1f, m_BlendRate = 1f;   // m_From → m_Base, 0..1
        bool m_Held;   // a blend someone else is driving frame by frame (RoomTint's colour coming back), not a timed fade
        // a short accent over the base mood: the flash, the alarm pulses
        RoomGrade m_Accent = RoomGrade.None;
        float m_AccentStart, m_AccentLength, m_AccentPeak;
        int m_AccentPulses;

        public static RoomGrade Base => s_I ? s_I.m_Base : RoomGrade.None;

        public static void Create(GameObject host)
        {
            if (!s_I) s_I = host.AddComponent<PassthroughMood>();
        }

        void OnDestroy()
        {
            if (s_I == this) s_I = null;
            var rig = PlayerRig.Active;
            if (rig) rig.SetPassthroughGrade(RoomGrade.None, RoomGrade.None, 0f);
        }

        /// <summary>Settle the room into a mood over `seconds`.</summary>
        public static void Set(RoomGrade grade, float seconds)
        {
            var m = s_I;
            if (!m || (m.m_Base == grade && !m.m_Held)) return;
            m.m_Held = false;
            m.m_From = m.m_Blend >= 0.5f ? m.m_Base : m.m_From;   // mid-fade: start from the nearer end
            m.m_Base = grade;
            m.m_Blend = seconds <= 0f ? 1f : 0f;
            m.m_BlendRate = seconds <= 0f ? 1000f : 1f / seconds;
            Debug.Log($"[Frost] Room mood: {grade} ({seconds:0.0} s)");
        }

        /// <summary>Hold the room part-way between two grades (call it every frame to drive your own fade).</summary>
        public static void Hold(RoomGrade from, RoomGrade to, float t)
        {
            var m = s_I;
            if (!m) return;
            m.m_From = from;
            m.m_Base = to;
            m.m_Blend = Mathf.Clamp01(t);
            m.m_Held = true;
        }

        /// <summary>The Polaroid's flash: your real room blows out bright for a blink, as if the bulb lit it.</summary>
        public static void Flash() => Accent(RoomGrade.Flash, 0.55f, 1, 0.8f);

        /// <summary>The fight alarm: the room pulses red, like a siren light.</summary>
        public static void Alarm(int pulses = 3) => Accent(RoomGrade.Alarm, 0.6f * pulses, pulses, 0.75f);

        static void Accent(RoomGrade grade, float seconds, int pulses, float peak)
        {
            var m = s_I;
            if (!m) return;
            m.m_Accent = grade;
            m.m_AccentStart = Time.unscaledTime;
            m.m_AccentLength = Mathf.Max(0.05f, seconds);
            m.m_AccentPulses = Mathf.Max(1, pulses);
            m.m_AccentPeak = peak;
        }

        float AccentWeight()
        {
            if (m_Accent == RoomGrade.None) return 0f;
            float t = (Time.unscaledTime - m_AccentStart) / m_AccentLength;
            if (t >= 1f) { m_Accent = RoomGrade.None; return 0f; }
            if (m_Accent == RoomGrade.Flash) return m_AccentPeak * (1f - t) * (1f - t);   // instant pop, quick decay
            float s = Mathf.Sin(Mathf.PI * m_AccentPulses * t);
            return m_AccentPeak * s * s;   // `pulses` smooth swells
        }

        void Update()
        {
            if (!m_Held) m_Blend = Mathf.MoveTowards(m_Blend, 1f, m_BlendRate * Time.unscaledDeltaTime);
            var rig = PlayerRig.Active;
            if (!rig) return;
            float accent = AccentWeight();
            if (accent > 0f)
            {
                if (!m_Held && m_Blend < 1f && m_Blend >= 0.5f) m_Blend = 1f;   // a flash hides the end of a fade
                rig.SetPassthroughGrade(m_Blend >= 0.5f ? m_Base : m_From, m_Accent, accent);
            }
            else if (m_Blend < 1f) rig.SetPassthroughGrade(m_From, m_Base, m_Held ? m_Blend : Ease.InOutCubic(m_Blend));
            else rig.SetPassthroughGrade(m_Base, m_Base, 1f);
        }

        // ------------------------------------------------------------------------------------ the grades themselves
        /// <summary>What a real-world colour (display values, 0..1) looks like under a grade. Baked into LUTs.</summary>
        public static Color Grade(RoomGrade grade, Color c)
        {
            float l = Mathf.Clamp01(0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b);
            switch (grade)
            {
                case RoomGrade.Flash:
                {
                    // a flashbulb: everything jumps towards a clean, slightly cool white, shadows lifted the most
                    var o = new Color(1f - Cube(1f - c.r), 1f - Cube(1f - c.g), 1f - Cube(1f - c.b));
                    return Color.Lerp(o, new Color(0.97f, 0.99f, 1f), 0.3f);
                }
                case RoomGrade.Icy:
                {
                    // half-desaturated, pushed cold, blue lifted into the shadows: the room turns to ice
                    var d = Color.Lerp(c, new Color(l, l, l), 0.5f);
                    float lift = (1f - l) * (1f - l);
                    return Clamp(new Color(d.r * 0.78f + 0.01f * lift, d.g * 0.94f + 0.05f * lift, d.b * 1.14f + 0.12f * lift));
                }
                case RoomGrade.Alarm:
                {
                    var red = new Color(l * 1.25f + 0.14f, l * 0.32f + 0.01f, l * 0.3f + 0.01f);
                    return Clamp(Color.Lerp(c, red, 0.75f));
                }
                case RoomGrade.Noir:
                {
                    // black-and-white film, worn for the whole investigation: a gentle S-curve, a touch darker, a faint silver cast
                    float s = l * l * (3f - 2f * l);
                    float g = Mathf.Clamp01(Mathf.Lerp(l, s, 0.45f) * 0.95f);
                    return new Color(g * 0.98f, g * 0.99f, Mathf.Min(1f, g * 1.03f));
                }
                default: return c;
            }
        }

        /// <summary>A grade baked as a res³ colour cube, red varying fastest (the order passthrough LUTs expect).</summary>
        public static Color[] BakeLut(RoomGrade grade, int res = 16)
        {
            var lut = new Color[res * res * res];
            float k = 1f / (res - 1);
            int i = 0;
            for (int b = 0; b < res; b++)
                for (int g = 0; g < res; g++)
                    for (int r = 0; r < res; r++)
                        lut[i++] = Grade(grade, new Color(r * k, g * k, b * k));
            return lut;
        }

        static float Cube(float x) => x * x * x;
        static Color Clamp(Color c) => new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
    }
}
