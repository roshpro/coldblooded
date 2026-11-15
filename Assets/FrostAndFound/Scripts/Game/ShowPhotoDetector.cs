using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Show a photo (doc §5.2): look at a kid and push the photo out toward them. A photo just held up never counts:
    /// it takes a forward push (slow is fine) while you're facing them. The kid your photo's clue points to is easier to
    /// show (a wider aim, a little further away) than everyone else, and it's always one kid at a time: after a show
    /// you pull the photo back in before you can push it at anyone again. You always show your newest photo.
    /// </summary>
    public class ShowPhotoDetector : MonoBehaviour
    {
        // facing: how far off your gaze a kid can be (the clue's kid gets more leeway), and the hand's aim
        const float FaceDegrees = 18f, FaceDegreesNeeded = 30f, HandDegrees = 40f;
        // the push: this much forward (toward the kid), as slowly as you like (within PushWindow), ending with the arm
        // out. A photo that's just held out there, still, doesn't count.
        const float PushDistance = 0.1f, PushWindow = 3f, RearmPullback = 0.08f, SampleEvery = 0.05f;
        const int Samples = 64;

        Kid m_Candidate;
        float m_Cooldown;
        bool m_Armed = true;
        float m_PeakReach;
        readonly Vector3[] m_Pos = new Vector3[Samples];
        readonly float[] m_Time = new float[Samples];
        int m_Count, m_Head;

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

            var hand = rig.FreeHand;
            bool canShow = holder.Visible && holder.Current.IsDeveloped && hand.IsTracked && hand.Palm && !(SnowballHand.Instance && SnowballHand.Instance.IsHoldingIn(hand))
                           && GameManager.I && GameManager.I.AllowsGameplay;
            if (!canShow) { Clear(holder); return; }

            var tune = FrostTuning.I;
            var head = rig.Head.position;
            var palm = hand.Palm.position;
            Record(palm);
            var gaze = rig.Head.forward.Flat();
            gaze = gaze.sqrMagnitude > 1e-4f ? gaze.normalized : rig.FlatForward;
            var reachDir = (palm - head).Flat();
            var needed = director.NeededFor(holder.Current.Data);

            // who you're facing, with your photo hand out in front toward them (the clue's kid wins near-ties)
            Kid target = null;
            float best = float.MaxValue;
            foreach (var kid in Kid.All)
            {
                if (!kid || kid.IsStunned) continue;
                bool isNeeded = kid == needed;
                var toKid = (kid.HeadPoint - head).Flat();
                float dist = toKid.magnitude;
                if (dist < 0.25f || dist > tune.showRange + (isNeeded ? 0.4f : 0f)) continue;
                float face = Vector3.Angle(gaze, toKid);
                if (face > (isNeeded ? FaceDegreesNeeded : FaceDegrees)) continue;
                if (reachDir.sqrMagnitude > 1e-4f && Vector3.Angle(reachDir, toKid) > HandDegrees) continue;
                float score = face + dist * 4f - (isNeeded ? 12f : 0f);
                if (score < best) { best = score; target = kid; }
            }
            holder.FaceTarget = target ? target.Rig.Head : null;
            m_Candidate = target;

            // re-arm once the photo's been pulled back in (or the arm dropped): one push, one show
            float extension = Vector3.Distance(palm, head);
            if (!m_Armed)
            {
                m_PeakReach = Mathf.Max(m_PeakReach, extension);
                if (extension < tune.armExtendedDistance - 0.05f || extension < m_PeakReach - RearmPullback) { m_Armed = true; m_Count = 0; }
                return;
            }
            if (!target || m_Cooldown > 0f) return;
            if (extension < tune.armExtendedDistance || palm.y < head.y - 0.8f) return;
            if (!Pushed(palm, (target.HeadPoint - head).Flat().normalized)) return;

            // a push at them: this counts
            m_Armed = false;
            m_PeakReach = extension;
            if (SnowballFight.Active) { SnowballFight.I.NotNow(target); return; }   // mid-fight nobody's looking at photos
            if (!director.CanShow || target.Busy) return;
            m_Cooldown = 2.5f;
            AudioDirector.PlayAt(Sfx.UiClick, palm, 0.4f, 1.3f);
            hand.Haptic(0.25f, 0.05f);
            director.ShowPhoto(target, holder.Current.Data);
            Shown?.Invoke(target);
        }

        void Clear(PhotoHolder holder)
        {
            holder.FaceTarget = null;
            m_Candidate = null;
            m_Count = 0;
            m_Armed = true;
        }

        void Record(Vector3 palm)
        {
            if (m_Count > 0 && Time.time - m_Time[m_Head] < SampleEvery) return;
            m_Head = (m_Head + 1) % Samples;
            m_Pos[m_Head] = palm;
            m_Time[m_Head] = Time.time;
            if (m_Count < Samples) m_Count++;
        }

        // has the hand moved PushDistance toward the kid over the last few seconds (however slowly)?
        bool Pushed(Vector3 palm, Vector3 toward)
        {
            float most = 0f;
            for (int i = 1; i < m_Count; i++)
            {
                int k = (m_Head - i + Samples) % Samples;
                if (Time.time - m_Time[k] > PushWindow) break;
                most = Mathf.Max(most, Vector3.Dot((palm - m_Pos[k]).Flat(), toward));
            }
            return most >= PushDistance;
        }
    }
}
