using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Snow in either hand. Reach toward the floor with an open hand, close it in the snow, and a snowball packs into
    /// that mitten with a crunch. Open your hand mid-swing to throw: the ball leaves with your hand's real release
    /// velocity, plus a light aim nudge. The camera hand only scoops while the camera is down; a scoop is a fist
    /// down by the snow, while the camera wants a light pinch with the hand held up, so the two never get confused.
    /// </summary>
    [DefaultExecutionOrder(200)] // after the mittens have placed this frame's hold points
    public class SnowballHand : MonoBehaviour
    {
        public static SnowballHand Instance { get; private set; }
        public static event System.Action Formed;

        class HandState
        {
            public HandSide Side;
            public HandRig Hand;
            public Snowball Ball;
            public float LastOpen = -10f, FormTime, Cooldown;
        }
        readonly HandState m_L = new HandState { Side = HandSide.Left }, m_R = new HandState { Side = HandSide.Right };

        public bool IsHolding => m_L.Ball || m_R.Ball;
        public bool IsHoldingIn(HandRig hand) => hand != null && ((m_L.Hand == hand && m_L.Ball) || (m_R.Hand == hand && m_R.Ball));
        public bool Enabled { get; set; } = true;
        public int ThrowCount { get; private set; }

        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        void Update()
        {
            var rig = PlayerRig.Active;
            var snow = SnowSurface.Instance;
            if (!rig || !snow) return;
            Tick(m_L, rig.Left, rig, snow);
            Tick(m_R, rig.Right, rig, snow);
        }

        // the ball sits cupped in the mitten (its hold point), not at the bare palm bone
        void LateUpdate()
        {
            foreach (var st in new[] { m_L, m_R })
                if (st.Ball && st.Hand != null && st.Hand.Hold) st.Ball.Hold(st.Hand.Hold);
        }

        void Tick(HandState st, HandRig hand, PlayerRig rig, SnowSurface snow)
        {
            st.Hand = hand;
            if (!hand.IsTracked || !hand.Palm) { if (st.Ball) DropBall(st); return; }
            var tune = FrostTuning.I;
            if (st.Cooldown > 0f) st.Cooldown -= Time.deltaTime;
            if (hand.Grip < tune.gripOpen) st.LastOpen = Time.time;

            if (st.Ball == null)
            {
                if (!Enabled || st.Cooldown > 0f) return;
                if (hand == rig.CameraHand)
                {
                    if (!rig.IsXR) return;   // the desktop stand-in scoops with the free hand only
                    var cam = PolaroidCamera.Instance;
                    if (cam && (cam.IsRaised || cam.PrintWaiting)) return;   // that hand is busy with the camera
                }
                var palm = hand.Palm.position;
                float above = palm.y - snow.SurfaceHeightAt(palm);
                bool inSnowZone = above < tune.scoopHeight && above > -0.15f && SnowNear(snow, palm);
                // reaching down for a dropped photo: that hand picks the photo up instead of scooping snow
                if (inSnowZone && PhotoHolder.Instance && PhotoHolder.Instance.PhotoInReach(hand)) return;
                bool closing = hand.Grip > tune.gripClosed;
                bool wasOpenRecently = Time.time - st.LastOpen < 1.8f;
                if (inSnowZone && closing && wasOpenRecently) FormBall(st, hand, rig, snow, palm);
            }
            else
            {
                // a little grow-in so the ball visibly packs
                float k = Mathf.Clamp01((Time.time - st.FormTime) / 0.16f);
                st.Ball.transform.localScale = Vector3.one * tune.snowballRadius * 2f * Ease.OutBack(k);
                st.Ball.Hold(hand.Hold ? hand.Hold : hand.Palm);
                if (hand.Grip < tune.gripOpen && Time.time - st.FormTime > 0.2f) Release(st, hand, rig);
            }
        }

        /// <summary>Snow under the palm or just beside it (forgiving near drifts and furniture edges).</summary>
        static bool SnowNear(SnowSurface snow, Vector3 p)
        {
            if (snow.HasSnowAt(p)) return true;
            const float r = 0.07f;
            return snow.HasSnowAt(p + new Vector3(r, 0, 0)) || snow.HasSnowAt(p - new Vector3(r, 0, 0)) ||
                   snow.HasSnowAt(p + new Vector3(0, 0, r)) || snow.HasSnowAt(p - new Vector3(0, 0, r));
        }

        void FormBall(HandState st, HandRig hand, PlayerRig rig, SnowSurface snow, Vector3 palm)
        {
            var tune = FrostTuning.I;
            var ground = new Vector3(palm.x, snow.SurfaceHeightAt(palm), palm.z);
            snow.Stamp(ground, Random.Range(0f, 360f), StampShape.Scoop, 0.15f, 1f);
            Fx.SnowPuff(ground + Vector3.up * 0.02f, Vector3.up, 16, 0.8f);
            if (!SatieSound.Fire("scoop", ground, SatieSound.Snow)) AudioDirector.PlayAt(Sfx.ScoopCrunch, ground, 0.9f, Random.Range(0.93f, 1.07f));
            hand.Haptic(0.5f, 0.08f);
            st.Ball = Snowball.Create(hand.Palm.position, tune.snowballRadius);
            DepthOcclusion.Exempt(st.Ball.gameObject);   // the hand holding it must not cut it out
            st.Ball.transform.localScale = Vector3.zero;
            st.FormTime = Time.time;
            if (hand == rig.FreeHand) PhotoHolder.Instance?.TuckAway();
            if (rig is DesktopPlayerRig d) d.NotifyHolding();
            Formed?.Invoke();
        }

        void Release(HandState st, HandRig hand, PlayerRig rig)
        {
            var tune = FrostTuning.I;
            var ball = st.Ball;
            st.Ball = null;
            st.Cooldown = 0.35f;
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
            if (hand == rig.FreeHand) PhotoHolder.Instance?.Restore(0.45f);
        }

        void DropBall(HandState st)
        {
            if (!st.Ball) return;
            st.Ball.Launch(Vector3.zero, Vector3.zero);
            st.Ball = null;
        }
    }
}
