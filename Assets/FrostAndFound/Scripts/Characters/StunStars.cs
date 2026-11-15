using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// One cartoon "seeing stars" star: a camera-facing quad that plays the wobbling star sprite sheet
    /// (Resources/Fx/stun_star_sheet, 6 × 4 cells of 256 px, 24 frames), tinted yellow.
    /// </summary>
    public class StunStar : MonoBehaviour
    {
        const int Cols = 6, Rows = 4, Frames = 24;
        const float Fps = 16f;
        public static readonly Color Tint = new Color(1f, 0.86f, 0.28f);
        static Texture2D s_Sheet;

        Material m_Mat;
        float m_Offset;

        static Texture2D Sheet
        {
            get
            {
                if (!s_Sheet)
                {
                    s_Sheet = Resources.Load<Texture2D>("Fx/stun_star_sheet");
                    if (s_Sheet) s_Sheet.wrapMode = TextureWrapMode.Clamp;
                    else Debug.LogWarning("[Frost] Stun star sprite sheet missing (Resources/Fx/stun_star_sheet)");
                }
                return s_Sheet;
            }
        }

        /// <summary>A star <paramref name="size"/> units across (in the parent's space). Named "Star" so mesh merging skips it.</summary>
        public static StunStar Create(Transform parent, float size, int seed)
        {
            var go = new GameObject("Star");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>().sharedMesh = ProcMesh.Quad();
            var mr = go.AddComponent<MeshRenderer>();
            var s = go.AddComponent<StunStar>();
            s.m_Mat = Palette.UnlitTextured(Sheet, Tint, true, "Stun Star");
            mr.sharedMaterial = s.m_Mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            s.m_Offset = seed * 7.3f;   // each star wobbles out of step with the others
            s.SetFrame(0);
            return s;
        }

        void SetFrame(int frame)
        {
            int col = frame % Cols, row = frame / Cols;
            m_Mat.SetVector("_BaseMap_ST", new Vector4(1f / Cols, 1f / Rows, col / (float)Cols, 1f - (row + 1) / (float)Rows));
        }

        void LateUpdate()
        {
            // face the viewer (the quad's front faces -Z)
            var rig = PlayerRig.Active;
            var eye = rig ? rig.Head : (Camera.main ? Camera.main.transform : null);
            if (eye)
            {
                var d = transform.position - eye.position;
                if (d.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
            }
            SetFrame((int)((Time.time + m_Offset) * Fps) % Frames);
        }

        void OnDestroy() { if (m_Mat) Destroy(m_Mat); }
    }

    /// <summary>A ring of stars orbiting above a dizzy kid's head. Set <see cref="Amount"/> (0–1) to grow/shrink it.</summary>
    public class StunStarRing : MonoBehaviour
    {
        const int Count = 5;
        readonly List<StunStar> m_Stars = new List<StunStar>();
        Kid m_Kid;
        public float Amount;

        public static StunStarRing Create(Kid kid)
        {
            var go = new GameObject("Dizzy Stars");
            var r = go.AddComponent<StunStarRing>();
            r.m_Kid = kid;
            for (int i = 0; i < Count; i++) r.m_Stars.Add(StunStar.Create(go.transform, 1f, i));
            r.LateUpdate();
            return r;
        }

        void LateUpdate()
        {
            bool on = m_Kid && Amount > 0.001f;
            foreach (var s in m_Stars) s.gameObject.SetActive(on);
            if (!on) return;
            var rig = m_Kid.Rig;
            float h = Mathf.Max(0.3f, rig.Height), k = h / 1.05f;
            var head = rig.Head ? rig.Head.position : m_Kid.transform.position + Vector3.up * h * 0.8f;
            var centre = new Vector3(head.x, m_Kid.transform.position.y + h + 0.03f * k, head.z);
            transform.position = centre;
            transform.rotation = Quaternion.identity;
            float spin = Time.time * 3.2f, r = 0.19f * k;
            float size = 0.075f * k * Ease.OutBack(Mathf.Clamp01(Amount));
            for (int i = 0; i < m_Stars.Count; i++)
            {
                float a = spin + i * Mathf.PI * 2f / m_Stars.Count;
                var t = m_Stars[i].transform;
                t.localPosition = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a * 2f + i) * 0.02f * k, Mathf.Sin(a) * r);
                t.localScale = Vector3.one * size;
            }
        }
    }
}
