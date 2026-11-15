using UnityEngine;

namespace FrostAndFound
{
    public enum GestureKind { Camera, ScoopThrow, ShowPhoto }

    /// <summary>
    /// A small looping hand-drawn gesture animation (no backing card), played from a sprite sheet in
    /// Resources/Gestures (8 × 6 cells of 256 px; cell 0 is blank, so the loop is cells 1–47).
    /// </summary>
    public class GestureCard : MonoBehaviour
    {
        const int Cols = 8, Rows = 6, FirstFrame = 1, FrameCount = 47;
        const float Fps = 24f;

        Material m_Mat;
        Transform m_Content;
        float m_Shown, m_Target = 1f, m_StartTime;

        public GestureKind Kind { get; private set; }
        public bool IsShowing => m_Target > 0f;

        static Texture2D Sheet(GestureKind kind)
        {
            string path = kind switch
            {
                GestureKind.Camera => "Gestures/gesture_pinch_camera_sheet",
                GestureKind.ShowPhoto => "Gestures/gesture_show_photo_sheet",
                _ => "Gestures/gesture_scoop_throw_sheet",
            };
            var tex = Resources.Load<Texture2D>(path);
            if (tex) tex.wrapMode = TextureWrapMode.Clamp;
            else Debug.LogWarning("[Frost] Gesture sprite sheet missing for " + kind);
            return tex;
        }

        /// <summary>Builds a card <paramref name="size"/> metres square. Front faces -Z, like the rest of the UI.</summary>
        public static GestureCard Create(Transform parent, GestureKind kind, float size)
        {
            var go = new GameObject("Gesture · " + kind);
            go.layer = Layers.UI3D;
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<GestureCard>();
            g.Kind = kind;
            g.m_Content = new GameObject("Content").transform;
            g.m_Content.SetParent(go.transform, false);
            // just the hand-drawn lines floating in space, no backing card
            g.m_Mat = Palette.UnlitTextured(Sheet(kind), Color.white, true, "Gesture " + kind);
            var anim = UIKit.Quad(g.m_Content, "Animation", ProcMesh.Quad(), g.m_Mat, -0.002f);
            anim.localScale = new Vector3(size, size, 1f);
            var mr = anim.GetComponent<MeshRenderer>();
            mr.sortingOrder = UIKit.Order.Overlay;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetLayerRecursive(Layers.UI3D);
            g.m_StartTime = Time.unscaledTime;
            g.m_Content.localScale = Vector3.zero;
            g.SetFrame(FirstFrame);
            return g;
        }

        public void Show()
        {
            if (m_Target <= 0f) m_StartTime = Time.unscaledTime;   // restart the loop each time it reappears
            m_Target = 1f;
            gameObject.SetActive(true);
        }

        public void Hide() => m_Target = 0f;

        void SetFrame(int frame)
        {
            int col = frame % Cols, row = frame / Cols;
            // texture v runs bottom-up; the sheet's row 0 is at the top
            m_Mat.SetVector("_BaseMap_ST", new Vector4(1f / Cols, 1f / Rows, col / (float)Cols, 1f - (row + 1) / (float)Rows));
        }

        void Update()
        {
            m_Shown = Mathf.MoveTowards(m_Shown, m_Target, Time.unscaledDeltaTime / 0.25f);
            m_Content.localScale = Vector3.one * (m_Target > 0f ? Ease.OutBack(m_Shown) : Ease.OutCubic(m_Shown));
            if (m_Shown <= 0f && m_Target <= 0f) { gameObject.SetActive(false); return; }
            int f = (int)((Time.unscaledTime - m_StartTime) * Fps) % FrameCount;
            SetFrame(FirstFrame + f);
        }

        void OnDestroy() { if (m_Mat) Destroy(m_Mat); }
    }

    /// <summary>
    /// In-game gesture tutorials: a card that floats near the edge of your view on the side of the hand that does
    /// the gesture (camera hand for the pinch, free hand for scoop &amp; throw and for showing a photo). One at a time.
    /// </summary>
    public class GestureHint : MonoBehaviour
    {
        static GestureHint s_I;
        GestureCard m_Card;
        HandSide m_Side;

        public static bool IsShowing(GestureKind kind) => s_I && s_I.m_Card && s_I.m_Card.Kind == kind && s_I.m_Card.IsShowing;
        /// <summary>Some other tutorial is up (so two hints don't keep swapping places).</summary>
        public static bool OtherShowing(GestureKind kind) => s_I && s_I.m_Card && s_I.m_Card.Kind != kind && s_I.m_Card.IsShowing;

        public static void Show(GestureKind kind)
        {
            if (!s_I) s_I = new GameObject("[Frost Gesture Hint]").AddComponent<GestureHint>();
            s_I.ShowCard(kind);
        }

        public static void Hide(GestureKind kind)
        {
            if (s_I && s_I.m_Card && s_I.m_Card.Kind == kind) s_I.m_Card.Hide();
        }

        void ShowCard(GestureKind kind)
        {
            if (m_Card && m_Card.Kind != kind) { Destroy(m_Card.gameObject); m_Card = null; }
            if (!m_Card)
            {
                m_Card = GestureCard.Create(transform, kind, 0.16f);
                Place(true);
            }
            m_Card.Show();
            Debug.Log("[Frost] Gesture hint: " + kind);
        }

        void LateUpdate() => Place(false);

        void Place(bool snap)
        {
            var rig = PlayerRig.Active;
            if (!rig || !m_Card) return;
            m_Side = m_Card.Kind == GestureKind.Camera ? rig.CameraHand.Side : rig.FreeHand.Side;
            var head = rig.Head;
            var anchor = HToast.GazeAnchor(rig, 0.8f, 6f);
            var right = Vector3.Cross(Vector3.up, (anchor - head.position).Flat().normalized);
            var target = anchor + right * (m_Side == HandSide.Right ? 0.24f : -0.24f);
            transform.position = snap ? target : Ease.Damp(transform.position, target, 6f, Time.unscaledDeltaTime);
            transform.rotation = Quaternion.LookRotation((transform.position - head.position).normalized, Vector3.up);
        }
    }
}
