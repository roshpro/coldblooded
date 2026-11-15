using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Cartoon impact lines for Frosty's thud. Three chunky white strokes with an ink outline burst out at the edge
    /// of your view, on the side the thud came from, as if something just landed back there. A second set pops up
    /// over Frosty himself, so when you turn round you see where it happened. The edge set gets out of the way as
    /// soon as you look round. Drawn on the UI layer, so real furniture never hides them.
    /// </summary>
    public class ThudMarks : MonoBehaviour
    {
        const float EdgeYaw = 38f, EdgeDistance = 1f, EdgeLife = 1.7f, AtLife = 4.5f;

        PlayerRig m_Rig;
        Vector3 m_Source;
        float m_Born;
        Group[] m_Edges;
        Group m_At;
        float m_SeenFor;

        class Group
        {
            public Transform root;
            public Transform[] strokes;
            public float[] angles;
            public float rIn, rOut, side, life, born, retractAt = -1f;
            public Vector3 basePos;
        }

        public static void Burst(Vector3 source, PlayerRig rig)
        {
            if (!rig) return;
            var go = new GameObject("[Frost Thud Marks]");
            go.layer = Layers.UI3D;
            var m = go.AddComponent<ThudMarks>();
            m.m_Rig = rig;
            m.m_Source = source;
            m.m_Born = Time.time;
            m.Build();
        }

        void Build()
        {
            var head = m_Rig.Head;
            var to = (m_Source - head.position).Flat();
            var fwd = head.forward.Flat().sqrMagnitude > 1e-4f ? head.forward.Flat().normalized : m_Rig.FlatForward;
            float side = Vector3.Dot(Vector3.Cross(fwd, to.normalized), Vector3.up);   // + right, - left
            bool inView = Vector3.Angle(fwd, to) < 45f;
            if (!inView)
            {
                // straight behind you: both sides at once
                if (Mathf.Abs(side) < 0.3f) m_Edges = new[] { EdgeGroup(1f), EdgeGroup(-1f) };
                else m_Edges = new[] { EdgeGroup(Mathf.Sign(side)) };
            }
            else m_Edges = new Group[0];

            // over Frosty: lines fanning up out of the snow where he landed
            m_At = MakeGroup("Over Frosty", new[] { 52f, 90f, 128f }, 0.2f, 0.42f, 0.026f, AtLife);
            m_At.basePos = m_Source + Vector3.up * 0.22f;
            m_At.root.position = m_At.basePos;
        }

        Group EdgeGroup(float side)
        {
            // the lines burst from just past the edge of your view, pointing back in toward the middle
            float a = side > 0f ? 180f : 0f;
            var g = MakeGroup(side > 0f ? "Edge Right" : "Edge Left", new[] { a - 34f, a, a + 34f }, 0.045f, 0.13f, 0.014f, EdgeLife);
            g.side = side;
            return g;
        }

        Group MakeGroup(string name, float[] angles, float rIn, float rOut, float thick, float life)
        {
            var g = new Group { angles = angles, rIn = rIn, rOut = rOut, life = life, born = Time.time };
            g.root = new GameObject(name).transform;
            g.root.SetParent(transform, false);
            g.root.gameObject.layer = Layers.UI3D;
            g.strokes = new Transform[angles.Length];
            float len = rOut - rIn;
            var fill = Palette.Unlit(Color.white);
            var ink = Palette.Unlit(Palette.Ink);
            for (int i = 0; i < angles.Length; i++)
            {
                var arm = new GameObject("Line " + i).transform;
                arm.SetParent(g.root, false);
                arm.gameObject.layer = Layers.UI3D;
                arm.localRotation = Quaternion.Euler(0f, 0f, angles[i]);
                var stroke = new GameObject("Stroke").transform;   // the pill, centred on its own middle, along local x
                stroke.SetParent(arm, false);
                stroke.gameObject.layer = Layers.UI3D;
                // the middle line is a touch longer, like a hand-drawn burst
                float l = len * (i == 1 ? 1.15f : 0.92f);
                UIKit.Quad(stroke, "Fill", UIKit.RoundedRect(l, thick, thick * 0.5f), fill, 0f);
                float o = thick * 0.55f;
                UIKit.Quad(stroke, "Ink", UIKit.RoundedRect(l + o, thick + o, (thick + o) * 0.5f), ink, 0.002f);
                stroke.localScale = Vector3.zero;
                g.strokes[i] = stroke;
            }
            return g;
        }

        void LateUpdate()
        {
            if (!m_Rig) { Destroy(gameObject); return; }
            var head = m_Rig.Head;
            float t = Time.time - m_Born;

            // once you've turned round and had a look at him, everything packs away
            bool looking = Vector3.Angle(head.forward, m_Source - head.position) < 40f;
            m_SeenFor = looking ? m_SeenFor + Time.deltaTime : 0f;

            bool alive = false;
            foreach (var g in m_Edges)
            {
                if (g.retractAt < 0f && (looking || t > g.life)) g.retractAt = Time.time;
                var fwd = head.forward.Flat().sqrMagnitude > 1e-4f ? head.forward.Flat().normalized : m_Rig.FlatForward;
                var dir = Quaternion.AngleAxis(g.side * EdgeYaw, Vector3.up) * fwd;
                var target = head.position + dir * EdgeDistance + Vector3.up * 0.05f + Vector3.Cross(Vector3.up, dir) * (g.side * 0.04f);
                // it lags a little behind your head, like a sticker on the inside of a comic panel
                g.basePos = t < 0.05f ? target : Ease.Damp(g.basePos, target, 9f, Time.deltaTime);
                alive |= Animate(g, head, t);
            }
            if (m_At.retractAt < 0f && (m_SeenFor > 0.9f || t > m_At.life)) m_At.retractAt = Time.time;
            alive |= Animate(m_At, head, t);
            if (!alive) Destroy(gameObject);
        }

        /// <summary>Shoot out, judder, echo, then shrink away. False once it's gone.</summary>
        bool Animate(Group g, Transform head, float t)
        {
            float shake = t < 0.45f ? (1f - t / 0.45f) * 0.006f : 0f;
            var jitter = shake > 0f ? new Vector3(Mathf.Sin(t * 91f), Mathf.Sin(t * 73f + 1.3f), 0f) * shake : Vector3.zero;
            var toHead = head.position - g.basePos;
            g.root.rotation = toHead.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(-toHead.normalized, Vector3.up) : g.root.rotation;
            g.root.position = g.basePos + g.root.rotation * jitter;

            float retract = g.retractAt < 0f ? 1f : 1f - Mathf.Clamp01((Time.time - g.retractAt) / 0.22f);
            if (retract <= 0f) { g.root.gameObject.SetActive(false); return false; }
            for (int i = 0; i < g.strokes.Length; i++)
            {
                float ti = t - i * 0.035f;   // a ragged stagger, line by line
                float pop = Ease.OutBack(Mathf.Clamp01(ti / 0.14f));
                float echo = ti > 0.5f && ti < 0.8f ? Mathf.Sin((ti - 0.5f) / 0.3f * Mathf.PI) * 0.18f : 0f;   // the second bump of the thud
                float s = Mathf.Max(0f, pop) * (1f + echo) * Ease.OutCubic(retract);
                float r = Mathf.Lerp(g.rIn * 0.6f, (g.rIn + g.rOut) * 0.5f, Mathf.Clamp01(pop)) + echo * (g.rOut - g.rIn) * 0.3f;
                // shrinking away, the lines slide outward as they go, like the burst flying off
                r += (1f - retract) * (g.rOut - g.rIn) * 0.5f;
                g.strokes[i].localPosition = new Vector3(r, 0f, 0f);
                g.strokes[i].localScale = new Vector3(s, Mathf.Min(s, 1.1f), 1f);
            }
            return true;
        }
    }
}
