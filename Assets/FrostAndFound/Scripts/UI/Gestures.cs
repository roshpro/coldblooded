using System.Collections.Generic;
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
    /// In-game gesture tutorials: cards that float near the edge of your view on the side of the hand that does the
    /// gesture (camera hand for the pinch, free hand for scoop &amp; throw and for showing a photo). The take-photo hint
    /// always brings the show-photo hint along, side by side (take, then show), since that's what the photo is for;
    /// otherwise one at a time.
    /// </summary>
    public class GestureHint : MonoBehaviour
    {
        const float Size = 0.16f, Offset = 0.24f, PairGap = 0.175f;
        static GestureHint s_I;
        readonly Dictionary<GestureKind, GestureCard> m_Cards = new Dictionary<GestureKind, GestureCard>();
        bool m_Paired;   // the show-photo card is riding along beside the take-photo card

        static GestureCard Card(GestureKind kind) => s_I && s_I.m_Cards.TryGetValue(kind, out var c) && c ? c : null;

        public static bool IsShowing(GestureKind kind) { var c = Card(kind); return c && c.IsShowing; }

        /// <summary>Some other tutorial is up (so two hints don't keep swapping places).</summary>
        public static bool OtherShowing(GestureKind kind)
        {
            if (!s_I) return false;
            foreach (var kv in s_I.m_Cards) if (kv.Key != kind && kv.Value && kv.Value.IsShowing) return true;
            return false;
        }

        public static void Show(GestureKind kind)
        {
            if (!s_I) s_I = new GameObject("[Frost Gesture Hint]").AddComponent<GestureHint>();
            if (kind == GestureKind.Camera)
            {
                s_I.HideCard(GestureKind.ScoopThrow);
                s_I.m_Paired = true;
                s_I.ShowCard(GestureKind.Camera);
                s_I.ShowCard(GestureKind.ShowPhoto);
                return;
            }
            if (kind == GestureKind.ShowPhoto && s_I.m_Paired && IsShowing(GestureKind.Camera)) return;   // already beside it
            foreach (var kv in s_I.m_Cards) if (kv.Key != kind && kv.Value) kv.Value.Hide();
            s_I.m_Paired = false;
            s_I.ShowCard(kind);
        }

        public static void Hide(GestureKind kind)
        {
            if (!s_I) return;
            // the show-photo card beside the take-photo hint goes when that does
            if (kind == GestureKind.ShowPhoto && s_I.m_Paired && IsShowing(GestureKind.Camera)) return;
            s_I.HideCard(kind);
            if (kind == GestureKind.Camera && s_I.m_Paired) { s_I.HideCard(GestureKind.ShowPhoto); s_I.m_Paired = false; }
        }

        void HideCard(GestureKind kind) { var c = Card(kind); if (c) c.Hide(); }

        void ShowCard(GestureKind kind)
        {
            var c = Card(kind);
            bool fresh = !c || !c.gameObject.activeSelf;
            if (!c) m_Cards[kind] = c = GestureCard.Create(transform, kind, Size);
            if (fresh) Place(c, true);
            c.Show();
            Debug.Log("[Frost] Gesture hint: " + kind);
        }

        void LateUpdate()
        {
            foreach (var c in m_Cards.Values) if (c && c.gameObject.activeSelf) Place(c, false);
        }

        void Place(GestureCard card, bool snap)
        {
            var rig = PlayerRig.Active;
            if (!rig) return;
            bool paired = m_Paired && (card.Kind == GestureKind.Camera || card.Kind == GestureKind.ShowPhoto);
            var side = card.Kind == GestureKind.Camera || paired ? rig.CameraHand.Side : rig.FreeHand.Side;
            // a pair is centred where a single card would be: take-photo on the outside, show-photo next to it
            float lateral = Offset;
            if (paired) lateral += card.Kind == GestureKind.Camera ? PairGap * 0.5f : -PairGap * 0.5f;
            var head = rig.Head;
            var anchor = HToast.GazeAnchor(rig, 0.8f, 6f);
            var right = Vector3.Cross(Vector3.up, (anchor - head.position).Flat().normalized);
            var target = anchor + right * (side == HandSide.Right ? lateral : -lateral);
            var t = card.transform;
            t.position = snap ? target : Ease.Damp(t.position, target, 6f, Time.unscaledDeltaTime);
            t.rotation = Quaternion.LookRotation((t.position - head.position).normalized, Vector3.up);
        }
    }
}
