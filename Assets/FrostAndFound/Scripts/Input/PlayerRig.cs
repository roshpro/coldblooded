using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Tracks recent poses so a throw can read the hand's real release velocity.
    /// Peak-window averaging: the average of the few samples around the fastest moment in the last ~120 ms,
    /// which matches what a person perceives as "the throw" far better than the last frame's velocity.
    /// </summary>
    public class VelocityTracker
    {
        const int N = 24;
        readonly Vector3[] m_Pos = new Vector3[N];
        readonly Quaternion[] m_Rot = new Quaternion[N];
        readonly float[] m_Time = new float[N];
        int m_Head = -1, m_Count;

        public void Clear() { m_Count = 0; m_Head = -1; }

        public void Sample(Vector3 pos, Quaternion rot, float time)
        {
            m_Head = (m_Head + 1) % N;
            m_Pos[m_Head] = pos; m_Rot[m_Head] = rot; m_Time[m_Head] = time;
            if (m_Count < N) m_Count++;
        }

        int Idx(int back) => (m_Head - back + N * 4) % N;

        public Vector3 Instant
        {
            get
            {
                if (m_Count < 2) return Vector3.zero;
                int a = Idx(0), b = Idx(1);
                float dt = m_Time[a] - m_Time[b];
                return dt > 1e-5f ? (m_Pos[a] - m_Pos[b]) / dt : Vector3.zero;
            }
        }

        public Vector3 ReleaseVelocity(float window = 0.12f)
        {
            if (m_Count < 3) return Instant;
            float now = m_Time[Idx(0)];
            int peak = -1; float peakSpeed = -1f;
            int usable = 0;
            for (int i = 0; i < m_Count - 1; i++)
            {
                int a = Idx(i), b = Idx(i + 1);
                if (now - m_Time[a] > window) break;
                float dt = m_Time[a] - m_Time[b];
                if (dt <= 1e-5f) continue;
                float s = (m_Pos[a] - m_Pos[b]).magnitude / dt;
                usable++;
                if (s > peakSpeed) { peakSpeed = s; peak = i; }
            }
            if (peak < 0 || usable == 0) return Instant;
            // average the segment velocities in [peak-1, peak+1]
            Vector3 sum = Vector3.zero; int n = 0;
            for (int i = Mathf.Max(0, peak - 1); i <= Mathf.Min(m_Count - 2, peak + 1); i++)
            {
                int a = Idx(i), b = Idx(i + 1);
                float dt = m_Time[a] - m_Time[b];
                if (dt <= 1e-5f) continue;
                sum += (m_Pos[a] - m_Pos[b]) / dt; n++;
            }
            return n > 0 ? sum / n : Instant;
        }

        public Vector3 AngularVelocity
        {
            get
            {
                if (m_Count < 3) return Vector3.zero;
                int a = Idx(0), b = Idx(2);
                float dt = m_Time[a] - m_Time[b];
                if (dt <= 1e-5f) return Vector3.zero;
                var dq = m_Rot[a] * Quaternion.Inverse(m_Rot[b]);
                dq.ToAngleAxis(out float ang, out Vector3 axis);
                if (ang > 180f) ang -= 360f;
                if (float.IsNaN(axis.x)) return Vector3.zero;
                return axis * (ang * Mathf.Deg2Rad / dt);
            }
        }
    }

    /// <summary>How a hand is holding on to something: a thumb–index pinch, or a closed fist.</summary>
    public enum GrabStyle { None, Pinch, Fist }

    /// <summary>One tracked hand or controller, in a device-agnostic shape the game reads.</summary>
    public class HandRig
    {
        public HandSide Side;
        /// <summary>Follows the grip pose of the controller or the wrist/palm of a tracked hand.</summary>
        public Transform Anchor;
        /// <summary>Where held things sit (centre of the closed mitten).</summary>
        public Transform Palm;
        /// <summary>Fingertip / controller tip used for poking buttons.</summary>
        public Transform Tip;

        public bool IsTracked;
        public bool IsOpticalHand;      // true = hand tracking, false = controller / simulated
        public float Grip;              // 0..1 (grip trigger, or fist curl)
        public float Trigger;           // 0..1 (index trigger, or index pinch strength)
        public bool TriggerDown;        // went past the press threshold this frame
        public bool TriggerHeld;
        public bool PrimaryDown;        // A/X button (desktop: unused)
        /// <summary>0..1, how far into the "camera" gesture this hand is: thumb and index closing in from a few
        /// centimetres apart (hand tracking) or the index trigger's travel (controllers). Desktop: always 1.</summary>
        public float PhotoPose;

        /// <summary>Hand-tracking joints in world space (thumb, index, middle, ring, little × base→tip), valid when HasJoints.</summary>
        public readonly Vector3[] Joints = new Vector3[20];
        public Vector3 Wrist;
        public bool HasJoints;
        /// <summary>Between thumb and index tips (hand tracking), or just in front of the fingers otherwise.</summary>
        public Vector3 PinchPoint => HasJoints ? (Joints[3] + Joints[7]) * 0.5f : (Tip ? Tip.position : (Anchor ? Anchor.position : Vector3.zero));
        /// <summary>Thumb tip to index tip in metres (hand tracking); +inf without joints.</summary>
        public float PinchGap => HasJoints ? Vector3.Distance(Joints[3], Joints[7]) : float.MaxValue;

        /// <summary>Holding on (a pinch or a fist, with a short release debounce so one lost tracking frame doesn't let go).</summary>
        public bool GrabHeld;
        /// <summary>Started / stopped holding on this frame.</summary>
        public bool GrabDown, GrabUp;
        /// <summary>What closed first when the hold started (None while not holding).</summary>
        public GrabStyle Grab;
        /// <summary>Set by the mittens each frame: where a held snowball sits, cupped in the mitten's palm.</summary>
        public Transform Hold;
        /// <summary>Set by the mittens: where the camera body sits when this mitten holds it (valid while HasCameraGrip).</summary>
        public UnityEngine.Pose CameraGrip;
        public bool HasCameraGrip;
        /// <summary>Set by the mittens: between the mitten's thumb and finger when it pinches a photo (valid while HasPhotoGrip).</summary>
        public Vector3 PhotoGrip;
        public bool HasPhotoGrip;

        public readonly VelocityTracker Tracker = new VelocityTracker();
        /// <summary>Desktop simulation can dictate the release velocity of a scripted throw.</summary>
        public Vector3? ReleaseVelocityOverride;

        public System.Action<float, float> HapticImpl;
        public void Haptic(float amplitude, float seconds) => HapticImpl?.Invoke(amplitude, seconds);

        bool m_PinchHeld, m_FistHeld;
        float m_GrabLostAt = -1f;
        /// <summary>Grab state from a pinch strength (or index trigger) and a fist curl (or grip trigger), both 0..1.</summary>
        public void UpdateGrab(float pinch, float fist, float debounce)
        {
            m_PinchHeld = m_PinchHeld ? pinch > 0.35f : pinch > 0.7f;
            m_FistHeld = m_FistHeld ? fist > 0.35f : fist > 0.6f;
            bool was = GrabHeld;
            if (m_PinchHeld || m_FistHeld) { GrabHeld = true; m_GrabLostAt = -1f; }
            else if (GrabHeld)
            {
                if (m_GrabLostAt < 0f) m_GrabLostAt = Time.time;
                if (Time.time - m_GrabLostAt >= debounce) GrabHeld = false;
            }
            GrabDown = GrabHeld && !was;
            GrabUp = was && !GrabHeld;
            if (GrabDown) Grab = m_PinchHeld ? GrabStyle.Pinch : GrabStyle.Fist;
            if (!GrabHeld) Grab = GrabStyle.None;
        }

        bool m_PrevTriggerHeld;
        public void UpdateTrigger(float value, float pressAt = 0.7f, float releaseAt = 0.45f)
        {
            Trigger = value;
            bool held = m_PrevTriggerHeld ? value > releaseAt : value > pressAt;
            TriggerDown = held && !m_PrevTriggerHeld;
            TriggerHeld = held;
            m_PrevTriggerHeld = held;
        }

        public Vector3 ReleaseVelocity()
        {
            if (ReleaseVelocityOverride.HasValue) return ReleaseVelocityOverride.Value;
            var t = FrostTuning.I;
            return Tracker.ReleaseVelocity() * (IsOpticalHand ? t.throwMultiplierHands : t.throwMultiplierControllers);
        }
    }

    /// <summary>
    /// The player's body as the game sees it: a head, two hands, and a couple of buttons.
    /// MetaPlayerRig (Quest via Meta XR SDK) and DesktopPlayerRig (keyboard + mouse simulation) implement it.
    /// </summary>
    public abstract class PlayerRig : MonoBehaviour
    {
        public static PlayerRig Active { get; protected set; }

        public readonly HandRig Left = new HandRig { Side = HandSide.Left };
        public readonly HandRig Right = new HandRig { Side = HandSide.Right };

        public abstract Transform Head { get; }
        public abstract Camera ViewCamera { get; }
        public abstract bool IsXR { get; }
        /// <summary>Pause / menu button pressed this frame.</summary>
        public abstract bool MenuDown { get; }
        /// <summary>The floor height under the player in world space.</summary>
        public float FloorY { get; set; }

        public HandRig CameraHand => FrostPrefs.CameraHand == HandSide.Left ? Left : Right;
        public HandRig FreeHand => FrostPrefs.FreeHand == HandSide.Left ? Left : Right;
        public HandRig Hand(HandSide s) => s == HandSide.Left ? Left : Right;

        public Vector3 FeetPosition
        {
            get
            {
                var p = Head.position;
                p.y = FloorY;
                return p;
            }
        }

        public Vector3 FlatForward
        {
            get
            {
                var f = Head.forward.Flat();
                return f.sqrMagnitude < 1e-4f ? Vector3.forward : f.normalized;
            }
        }

        protected void SampleTrackers()
        {
            float t = Time.time;
            if (Left.Anchor) Left.Tracker.Sample(Left.Palm ? Left.Palm.position : Left.Anchor.position, Left.Anchor.rotation, t);
            if (Right.Anchor) Right.Tracker.Sample(Right.Palm ? Right.Palm.position : Right.Anchor.position, Right.Anchor.rotation, t);
        }

        protected virtual void OnEnable() { Active = this; }
        protected virtual void OnDisable() { if (Active == this) Active = null; }

        /// <summary>Puts the player at a pose (desktop only; XR players walk for real).</summary>
        public virtual void Teleport(Vector3 feet, Quaternion facing) { }
    }
}
