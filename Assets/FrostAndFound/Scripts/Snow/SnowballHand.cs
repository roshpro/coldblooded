using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// The free hand's snow logic. Reach toward the floor with an open hand, close it in the snow, and a snowball
    /// packs into your palm with a crunch (your current photo tucks away). Open your hand mid-swing to throw —
    /// the ball leaves with your hand's real release velocity, plus a light aim nudge.
    /// </summary>
    [DefaultExecutionOrder(200)] // after the mittens have placed this frame's hold point
    public class SnowballHand : MonoBehaviour
    {
        public static SnowballHand Instance { get; private set; }
        public static event System.Action Formed;

        Snowball m_Ball;
        float m_LastOpenTime = -10f;
        float m_FormTime;
        float m_ReleaseCooldown;
        HandRig m_Hand;

        public bool IsHolding => m_Ball != null;
        public bool Enabled { get; set; } = true;
        public int ThrowCount { get; private set; }

        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        // the ball sits inside the curled mitten (its hold point), not at the bare palm bone
        void LateUpdate()
        {
            if (m_Ball && m_Hand != null && m_Hand.Hold) m_Ball.Hold(m_Hand.Hold);
        }

        void Update()
        {
            var rig = PlayerRig.Active;
            var snow = SnowSurface.Instance;
            if (!rig || !snow) return;
            var hand = rig.FreeHand;
            if (m_Hand != hand && m_Ball) { DropBall(); }
            m_Hand = hand;
            if (!hand.IsTracked || !hand.Palm) return;
            var tune = FrostTuning.I;
            if (m_ReleaseCooldown > 0f) m_ReleaseCooldown -= Time.deltaTime;

            if (hand.Grip < tune.gripOpen) m_LastOpenTime = Time.time;

            if (m_Ball == null)
            {
                if (!Enabled || m_ReleaseCooldown > 0f) return;
                var palm = hand.Palm.position;
                float above = palm.y - snow.SurfaceHeightAt(palm);
                bool inSnowZone = above < tune.scoopHeight && above > -0.12f && snow.HasSnowAt(palm);
                bool closing = hand.Grip > tune.gripClosed;
                bool wasOpenRecently = Time.time - m_LastOpenTime < 1.0f;
                if (inSnowZone && closing && wasOpenRecently) FormBall(hand, snow, palm);
            }
            else
            {
                // a little grow-in so the ball visibly packs
                float k = Mathf.Clamp01((Time.time - m_FormTime) / 0.16f);
                m_Ball.transform.localScale = Vector3.one * tune.snowballRadius * 2f * Ease.OutBack(k);
                m_Ball.Hold(hand.Palm);
                if (hand.Grip < tune.gripOpen && Time.time - m_FormTime > 0.2f) Release(hand);
            }
        }

        void FormBall(HandRig hand, SnowSurface snow, Vector3 palm)
        {
            var tune = FrostTuning.I;
            var ground = new Vector3(palm.x, snow.SurfaceHeightAt(palm), palm.z);
            snow.Stamp(ground, Random.Range(0f, 360f), StampShape.Scoop, 0.15f, 1f);
            Fx.SnowPuff(ground + Vector3.up * 0.02f, Vector3.up, 16, 0.8f);
            AudioDirector.PlayAt(Sfx.ScoopCrunch, ground, 0.9f, Random.Range(0.93f, 1.07f));
            hand.Haptic(0.5f, 0.08f);
            m_Ball = Snowball.Create(hand.Palm.position, tune.snowballRadius);
            m_Ball.transform.localScale = Vector3.zero;
            m_FormTime = Time.time;
            PhotoHolder.Instance?.TuckAway();
            if (PlayerRig.Active is DesktopPlayerRig d) d.NotifyHolding();
            Formed?.Invoke();
        }

        void Release(HandRig hand)
        {
            var tune = FrostTuning.I;
            var ball = m_Ball;
            m_Ball = null;
            m_ReleaseCooldown = 0.35f;
            var v = hand.ReleaseVelocity();
            float speed = v.magnitude;
            ball.transform.localScale = Vector3.one * tune.snowballRadius * 2f;
            if (speed < tune.minThrowSpeed)
            {
                // just let it go
                ball.Launch(v, Vector3.zero);
            }
            else
            {
                if (speed > tune.maxThrowSpeed) v = v / speed * tune.maxThrowSpeed;
                v = SnowballTargets.Assist(ball.transform.position, v);
                ball.Launch(v, hand.Tracker.AngularVelocity);
                ThrowCount++;
                hand.Haptic(0.3f, 0.05f);
            }
            PhotoHolder.Instance?.Restore(0.45f);
        }

        void DropBall()
        {
            if (!m_Ball) return;
            m_Ball.Launch(Vector3.zero, Vector3.zero);
            m_Ball = null;
        }
    }
}
