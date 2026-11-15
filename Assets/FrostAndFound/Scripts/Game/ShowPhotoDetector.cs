using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Show a photo (doc §5.2): hold your free hand out toward a kid within ~1.5 m. They lean in, squint at the
    /// photo and react. You always show your newest photo, so there is no inventory to manage.
    /// </summary>
    public class ShowPhotoDetector : MonoBehaviour
    {
        Kid m_Candidate;
        float m_Dwell;
        Kid m_LastShown;
        float m_Cooldown;

        public Kid Candidate => m_Candidate;
        /// <summary>You just showed a photo to this kid.</summary>
        public static event System.Action<Kid> Shown;

        void Update()
        {
            var rig = PlayerRig.Active;
            var holder = PhotoHolder.Instance;
            var director = CaseDirector.I;
            if (!rig || !holder || director == null) return;
            if (m_Cooldown > 0f) m_Cooldown -= Time.deltaTime;

            Kid target = null;
            var hand = rig.FreeHand;
            bool canShow = holder.Visible && holder.Current.IsDeveloped && hand.IsTracked && hand.Palm && !(SnowballHand.Instance && SnowballHand.Instance.IsHoldingIn(hand))
                           && GameManager.I && GameManager.I.AllowsGameplay;
            if (canShow)
            {
                var tune = FrostTuning.I;
                var head = rig.Head.position;
                var palm = hand.Palm.position;
                var reachDir = (palm - head).Flat();
                float bestAngle = tune.showConeDegrees;
                foreach (var kid in Kid.All)
                {
                    if (!kid) continue;
                    var toKid = (kid.HeadPoint - head).Flat();
                    float dist = toKid.magnitude;
                    if (dist > tune.showRange + 0.35f) continue;
                    if (FrostUtil.FlatDistance(palm, kid.transform.position) > tune.showRange) continue;
                    // the arm counts as "held out" when it's extended (hand far from your head) and the hand is in
                    // front of you toward the kid — holding a photo up to your own face, or an arm hanging at your
                    // side, doesn't count
                    float extension = Vector3.Distance(palm, head);
                    float reach = Vector3.Dot(reachDir, toKid.normalized);
                    if (extension < tune.armExtendedDistance || reach < 0.22f) continue;
                    if (palm.y < head.y - 0.8f) continue;
                    float ang = Vector3.Angle(reachDir, toKid);
                    if (ang < bestAngle) { bestAngle = ang; target = kid; }
                }
            }

            holder.FaceTarget = target ? target.Rig.Head : null;
            if (target != m_Candidate) { m_Candidate = target; m_Dwell = 0f; }
            if (!target) { m_LastShown = null; return; }
            // one show per reach: lower your arm (or turn to someone else) to show again
            if (target == m_LastShown) return;
            if (!director.CanShow || target.Busy) return;

            m_Dwell += Time.deltaTime;
            if (m_Dwell >= FrostTuning.I.showDwellSeconds)
            {
                m_Dwell = 0f;
                m_LastShown = target;
                m_Cooldown = 2.5f;
                AudioDirector.PlayAt(Sfx.UiClick, hand.Palm.position, 0.4f, 1.3f);
                hand.Haptic(0.2f, 0.04f);
                director.ShowPhoto(target, holder.Current.Data);
                Shown?.Invoke(target);
            }
        }
    }
}
