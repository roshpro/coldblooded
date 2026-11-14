using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Your hands, as chunky teal mittens that close with your grip (controllers) or fist (hand tracking).
    /// Hidden from the Polaroid so photos never show your own hands.
    /// </summary>
    public class Mittens : MonoBehaviour
    {
        class M { public Transform root, body, thumb, cuff; public HandRig hand; public float close; }
        M m_L, m_R;

        public static Mittens Create()
        {
            var go = new GameObject("Mittens");
            go.layer = Layers.PlayerHands;
            var m = go.AddComponent<Mittens>();
            m.m_L = m.Build(HandSide.Left);
            m.m_R = m.Build(HandSide.Right);
            return m;
        }

        M Build(HandSide side)
        {
            var m = new M();
            m.root = new GameObject(side + " Mitten").transform;
            m.root.SetParent(transform, false);
            m.root.gameObject.layer = Layers.PlayerHands;
            var wool = Palette.Toon(Palette.Mitten, true, 0.3f);
            var cuffMat = Palette.Toon(Palette.MittenCuff, true, 0.3f);
            float s = side == HandSide.Left ? -1f : 1f;
            m.body = ProcMesh.Part("Mitten", m.root, ProcMesh.Sphere(14, 10), wool, new Vector3(0, 0, 0.035f), new Vector3(0.075f, 0.045f, 0.105f)).transform;
            ProcMesh.Part("Knit", m.body, ProcMesh.Torus(0.45f, 0.06f, 16, 6), Palette.Toon(Palette.Books1, false), new Vector3(0, 0, 0.1f), new Vector3(1.02f, 1f, 1.02f), new Vector3(90, 0, 0));
            m.thumb = ProcMesh.Pivot("Thumb Pivot", m.root, new Vector3(-0.032f * s, -0.005f, 0.0f));
            ProcMesh.Part("Thumb", m.thumb, ProcMesh.Capsule(0.25f, 1f), wool, new Vector3(-0.012f * s, 0, 0.03f), new Vector3(0.03f, 0.06f, 0.03f), new Vector3(90, 0, 0));
            m.cuff = ProcMesh.Part("Cuff", m.root, ProcMesh.Cylinder(14), cuffMat, new Vector3(0, 0, -0.03f), new Vector3(0.068f, 0.03f, 0.052f), new Vector3(90, 0, 0)).transform;
            foreach (var r in m.root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return m;
        }

        void LateUpdate()
        {
            var rig = PlayerRig.Active;
            if (!rig) return;
            Pose(m_L, rig.Left, rig);
            Pose(m_R, rig.Right, rig);
        }

        void Pose(M m, HandRig hand, PlayerRig rig)
        {
            m.hand = hand;
            bool on = hand.IsTracked && hand.Anchor;
            if (m.root.gameObject.activeSelf != on) m.root.gameObject.SetActive(on);
            if (!on) return;
            // the camera hand wraps around the camera body
            bool holdsCamera = hand == rig.CameraHand;
            var anchor = hand.Anchor;
            var pos = holdsCamera ? anchor.TransformPoint(new Vector3(hand.Side == HandSide.Right ? 0.07f : -0.07f, -0.01f, -0.01f)) : anchor.position;
            m.root.SetPositionAndRotation(pos, anchor.rotation * (holdsCamera ? Quaternion.Euler(0, hand.Side == HandSide.Right ? -70f : 70f, 0) : Quaternion.identity));
            float grip = holdsCamera ? 0.85f : Mathf.Clamp01(hand.Grip);
            m.close = Ease.Damp(m.close, grip, 18f, Time.deltaTime);
            float s = hand.Side == HandSide.Left ? -1f : 1f;
            m.body.localScale = new Vector3(0.075f, 0.045f + 0.02f * m.close, 0.105f - 0.03f * m.close);
            m.body.localRotation = Quaternion.Euler(m.close * 35f, 0, 0);
            m.thumb.localRotation = Quaternion.Euler(0, m.close * 35f * s, 0);
            // a gentle squeeze as a trigger/pinch is pressed on the camera hand
            if (holdsCamera) m.body.localRotation *= Quaternion.Euler(hand.Trigger * 10f, 0, 0);
        }
    }
}
