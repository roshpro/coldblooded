using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Superhot-style punches: swing a hand into a kid fast enough and they're dazed exactly as if a snowball hit
    /// them (stagger, spiral eyes, head rolling, stars, and a line). The fist is swept from last frame's spot to
    /// this one, so a quick jab can't skip through a kid between frames, and a hand that's already resting inside
    /// one doesn't count. The camera hand doesn't punch while it's holding the camera up.
    /// </summary>
    [DefaultExecutionOrder(120)]   // after the rig has sampled this frame's hands
    public class PlayerPunch : MonoBehaviour
    {
        static readonly RaycastHit[] s_Hits = new RaycastHit[8];
        readonly Vector3[] m_Prev = new Vector3[2], m_Vel = new Vector3[2];
        readonly bool[] m_Has = new bool[2];
        readonly float[] m_Ready = new float[2];

        void LateUpdate()
        {
            var rig = PlayerRig.Active;
            if (!rig || !rig.IsXR) return;   // the desktop stand-in's hands are screen props
            Step(rig, rig.Left, 0);
            Step(rig, rig.Right, 1);
        }

        void Step(PlayerRig rig, HandRig hand, int i)
        {
            if (!hand.IsTracked || !hand.Anchor) { m_Has[i] = false; return; }
            var p = Fist(hand);
            float dt = Time.deltaTime;
            if (!m_Has[i] || dt <= 1e-5f) { m_Prev[i] = p; m_Vel[i] = Vector3.zero; m_Has[i] = true; return; }
            var from = m_Prev[i];
            var d = p - from;
            m_Prev[i] = p;
            m_Vel[i] = Vector3.Lerp(m_Vel[i], d / dt, 0.6f);   // a little smoothing so tracking jitter isn't a punch

            var tune = FrostTuning.I;
            if (Time.time < m_Ready[i] || m_Vel[i].magnitude < tune.punchSpeed) return;
            var cam = PolaroidCamera.Instance;
            if (cam && cam.IsRaised && hand == rig.CameraHand) return;
            float dist = d.magnitude;
            if (dist < 1e-4f) return;

            int n = Physics.SphereCastNonAlloc(from, tune.punchRadius, d / dist, s_Hits, dist, Layers.Mask(Layers.Kid), QueryTriggerInteraction.Collide);
            Kid best = null; RaycastHit bestHit = default; float bestDist = float.MaxValue;
            for (int h = 0; h < n; h++)
            {
                var kid = s_Hits[h].collider.GetComponentInParent<Kid>();
                if (!kid || s_Hits[h].distance >= bestDist) continue;
                best = kid; bestHit = s_Hits[h]; bestDist = s_Hits[h].distance;
            }
            if (!best) return;

            var point = bestHit.point != Vector3.zero ? bestHit.point : p;
            bool head = bestHit.collider.name == "Head Collider";
            best.OnPunched(point, m_Vel[i], head);
            hand.Haptic(0.9f, 0.09f);
            m_Ready[i] = Time.time + tune.punchCooldown;
        }

        /// <summary>The striking surface: the knuckles (hand tracking), or the controller's grip.</summary>
        static Vector3 Fist(HandRig hand)
        {
            if (hand.HasJoints) return (hand.Joints[8] + hand.Joints[12]) * 0.5f;
            return hand.Palm ? hand.Palm.position : hand.Anchor.position;
        }
    }
}
