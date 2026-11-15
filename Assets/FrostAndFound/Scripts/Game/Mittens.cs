using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>What a glove is doing: following your hand, or snapped into a grip around something it holds.</summary>
    public enum MittenGrip { Free, Ball, Camera, Photo }

    /// <summary>
    /// Your hands, as red knitted wool gloves: a rounded palm with a ribbed cuff and five separate, plump fingers.
    /// Every finger is a smooth tube (a spline through its knuckles, tapering to a domed tip) that starts inside the
    /// palm, so the glove reads as one soft piece. Hand tracking never drives the mesh with raw joints: each finger's
    /// curl and spread, the thumb's fold and the hand's size are read from tracking, smoothed and clamped, then a clean
    /// procedural hand is posed from them, so the gloves can't crumple or jitter. With controllers (or the desktop rig)
    /// trigger curls the index and grip the rest. Each glove publishes a hold point in the curled palm for snowballs.
    /// Hidden from the Polaroid. (Class kept as "Mittens" so nothing else has to change.)
    /// </summary>
    [DefaultExecutionOrder(100)] // after the rig has sampled this frame's joints; before the camera/photo/snowball place themselves
    public class Mittens : MonoBehaviour
    {
        WoolGlove m_L, m_R;
        Material m_Mat;

        public static Mittens Create()
        {
            var go = new GameObject("Gloves");
            go.layer = Layers.PlayerHands;
            var m = go.AddComponent<Mittens>();
            m.m_Mat = new Material(FrostAssets.Shader(FrostAssets.ShaderId.Wool)) { name = "Wool" };
            m.m_L = new WoolGlove(go.transform, HandSide.Left, m.m_Mat);
            m.m_R = new WoolGlove(go.transform, HandSide.Right, m.m_Mat);
            return m;
        }

        void LateUpdate()
        {
            var rig = PlayerRig.Active;
            if (!rig) return;
            // the desktop stand-in's hands aren't controllers: there the camera, photo and snowball keep their own
            // screen placements and the gloves just curl into the grip
            m_L.Pose(rig.Left, GripFor(rig, rig.Left), rig.IsXR);
            m_R.Pose(rig.Right, GripFor(rig, rig.Right), rig.IsXR);
        }

        static MittenGrip GripFor(PlayerRig rig, HandRig hand)
        {
            if (SnowballHand.Instance && SnowballHand.Instance.IsHoldingIn(hand)) return MittenGrip.Ball;
            var cam = PolaroidCamera.Instance;
            if (cam && cam.IsRaised && rig.CameraHand == hand) return MittenGrip.Camera;
            if (rig.FreeHand == hand && PhotoHolder.Instance && PhotoHolder.Instance.Visible) return MittenGrip.Photo;
            return MittenGrip.Free;
        }

        void OnDestroy()
        {
            m_L?.Dispose(); m_R?.Dispose();
            if (m_Mat) Destroy(m_Mat);
        }
    }

    /// <summary>One knitted glove mesh, re-shaped every frame around the hand.</summary>
    class WoolGlove
    {
        // ---------------------------------------------------------------- look
        const float FingerRadius = 0.0104f, ThumbRadius = 0.0122f, PalmThickness = 0.029f, Bulk = 1.22f, CuffLength = 0.058f;
        const float KnuckleSpread = 1.1f;   // fingers sit a touch further apart than bare ones (the wool is thick)
        static readonly float[] FingerRadiusMul = { 1f, 1f, 1.05f, 0.98f, 0.86f };
        static readonly Color GloveColor = new Color(0.74f, 0.08f, 0.1f);
        static readonly Color CuffColor = new Color(0.6f, 0.05f, 0.07f);
        static readonly Color StripeColor = new Color(0.96f, 0.93f, 0.86f);

        // controller pose (right hand; left is mirrored)
        static readonly Vector3 ControllerOffsetPos = new Vector3(0.03f, -0.015f, -0.095f);
        static readonly Vector3 ControllerOffsetEuler = new Vector3(30f, 0f, -90f);
        const float RestCurl = 0.15f, MinHoldCurl = 0.35f;
        // held things, in the hand's own frame (right hand: wrist at the origin, fingers +Z, palm facing -Y, thumb -X;
        // the left hand mirrors x). A snowball cups in the palm.
        static readonly Vector3 BallCenter = new Vector3(0.004f, -0.054f, 0.058f);
        // the camera grip, in camera space for the LEFT hand (x mirrors for the right): the glove stands at the camera's
        // left end, fingers up and palm against the end; the fingers curl over the top-left edge with the index tip on
        // the shutter and the thumb tucks under the bottom, so the screen faces you, clear of the hand
        static readonly Vector3 CamPalm = new Vector3(-0.092f, 0.005f, -0.012f);
        static readonly Vector3 CamWrist = new Vector3(-0.094f, -0.052f, -0.014f);
        static readonly Vector3[] CamJoints =
        {
            new Vector3(-0.080f, -0.022f, -0.036f), new Vector3(-0.070f, -0.046f, -0.032f), new Vector3(-0.056f, -0.055f, -0.020f), new Vector3(-0.042f, -0.057f, -0.006f),   // thumb, under the bottom
            new Vector3(-0.092f, 0.040f, -0.032f), new Vector3(-0.090f, 0.072f, -0.030f), new Vector3(-0.070f, 0.082f, -0.024f), new Vector3(-0.050f, 0.078f, -0.016f),     // index, tip on the shutter
            new Vector3(-0.094f, 0.042f, -0.010f), new Vector3(-0.092f, 0.076f, -0.008f), new Vector3(-0.071f, 0.085f, -0.004f), new Vector3(-0.050f, 0.080f, 0.004f),      // middle
            new Vector3(-0.093f, 0.040f, 0.012f), new Vector3(-0.091f, 0.072f, 0.014f), new Vector3(-0.072f, 0.082f, 0.016f), new Vector3(-0.053f, 0.077f, 0.022f),        // ring
            new Vector3(-0.091f, 0.036f, 0.032f), new Vector3(-0.089f, 0.060f, 0.034f), new Vector3(-0.074f, 0.072f, 0.035f), new Vector3(-0.058f, 0.069f, 0.038f),        // little
        };
        Vector3 CamMirror(Vector3 v) => new Vector3(m_Side == HandSide.Left ? v.x : -v.x, v.y, v.z);

        // procedural hand in a right-handed frame: wrist at origin, fingers along +Z, palm facing -Y, thumb on -X
        static readonly Vector3[] KnuckleRest =
        {
            new Vector3(-0.026f, -0.012f, 0.026f),
            new Vector3(-0.024f, -0.001f, 0.088f),
            new Vector3(-0.004f, 0.001f, 0.092f),
            new Vector3(0.015f, 0f, 0.088f),
            new Vector3(0.032f, -0.004f, 0.078f),
        };
        static readonly float[][] SegLen =
        {
            new[] { 0.040f, 0.032f, 0.026f },
            new[] { 0.040f, 0.024f, 0.020f },
            new[] { 0.045f, 0.027f, 0.022f },
            new[] { 0.042f, 0.026f, 0.021f },
            new[] { 0.033f, 0.019f, 0.018f },
        };
        static readonly float[] RestSpread = { 0f, -3f, 0f, 3.5f, 8f };   // degrees about the back-of-hand axis (+ = toward the little finger)
        static readonly Vector3 ThumbRestDir = new Vector3(-0.78f, -0.32f, 0.56f).normalized;   // out to the side, so it shows from the back too
        static readonly Vector3 ThumbFoldTarget = new Vector3(1f, -0.6f, 0.2f).normalized;
        const float FingerCurlDeg = 72f, ThumbCurlDeg = 35f;

        // hand tracking → a few smoothed, clamped numbers (never the raw joints)
        const float MaxTrackedCurl = 1f;
        const float CurlSmoothing = 0.045f, SpreadSmoothing = 0.08f, ThumbSmoothing = 0.06f, ScaleSmoothing = 0.8f, RotSmoothing = 0.025f;   // seconds
        readonly float[] m_TCurl = new float[5], m_TSpread = new float[5];
        float m_TThumb, m_TScale = 1f;
        bool m_Tracking;                               // false until the first tracked frame after the hand (re)appears
        Quaternion m_TrackedRot = Quaternion.identity;

        // pose parameters for PoseHand
        readonly float[] _curl = new float[5], _spread = new float[5];

        // ---------------------------------------------------------------- topology
        // Palm: rings of S sides (+1 seam vertex): 0-6 ribbed cuff (stripe, lip back to the wrist) | 7-12 palm | 13-14
        //   a rounded knuckle end, then caps at the cuff opening and over the knuckles.
        // Fingers (thumb first): FR spline rings + FDome dome rings of F sides, a cap inside the palm and one at the tip.
        const int S = 28, F = 14;
        const int CuffRings = 7, PalmRings = 6, PalmDomeRings = 2, PR = CuffRings + PalmRings + PalmDomeRings;
        const int FR = 13, FDome = 3, FRings = FR + FDome;
        const int PalmVerts = PR * (S + 1) + 2;
        const int FingerVerts = FRings * (F + 1) + 2;
        const int VertexCount = PalmVerts + 5 * FingerVerts;
        const float PalmStitches = 20f, FingerStitches = 7f, ThumbStitches = 8f;

        static int PV(int r, int k) => r * (S + 1) + k;
        static int PalmCapStart => PR * (S + 1);
        static int PalmCapEnd => PR * (S + 1) + 1;
        static int FBase(int f) => PalmVerts + f * FingerVerts;
        static int FV(int f, int r, int k) => FBase(f) + r * (F + 1) + k;
        static int FCapStart(int f) => FBase(f) + FRings * (F + 1);
        static int FCapEnd(int f) => FCapStart(f) + 1;

        static readonly float[] CuffAt = { -1f, -0.62f, -0.62f, -0.44f, -0.44f, -0.1f, -0.1f };
        static readonly float[] PalmAt = { 0.05f, 0.24f, 0.43f, 0.62f, 0.8f, 0.93f };
        static readonly float[] PalmDomeDeg = { 45f, 72f };
        static readonly float[] FingerDomeDeg = { 38f, 64f, 82f };

        readonly HandSide m_Side;
        readonly Transform m_Root;
        readonly Mesh m_Mesh;
        public readonly Transform HoldPoint;

        readonly Vector3[] _joints = new Vector3[20];
        Vector3 _wrist;
        readonly Vector3[] _wp = new Vector3[VertexCount];
        readonly Vector3[] _verts = new Vector3[VertexCount];
        readonly Vector3[] _normals = new Vector3[VertexCount];
        readonly Vector2[] _uvs = new Vector2[VertexCount];
        readonly Color[] _colors = new Color[VertexCount];
        readonly Vector3[] _pc = new Vector3[PR];                 // palm ring centres
        readonly Vector3[] _fc = new Vector3[FRings], _ft = new Vector3[FRings];
        readonly float[] _fr = new float[FRings];
        readonly Vector3[] _ctrl = new Vector3[7];
        readonly Vector3[] _ringCentre = new Vector3[PR + 5 * FRings];

        public WoolGlove(Transform parent, HandSide side, Material mat)
        {
            m_Side = side;
            var go = new GameObject(side + " Glove", typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = Layers.PlayerHands;
            m_Root = go.transform;
            m_Root.SetParent(parent, false);
            HoldPoint = new GameObject(side + " Glove Hold").transform;
            HoldPoint.SetParent(parent, false);

            var idx = new List<int>(6 * (PR * S + 5 * FRings * F) + 3 * (2 * S + 10 * F));
            void Quad(int a0, int b0, int c0, int d0) { idx.Add(a0); idx.Add(b0); idx.Add(c0); idx.Add(b0); idx.Add(d0); idx.Add(c0); }
            for (int r = 0; r + 1 < PR; r++)
                for (int k = 0; k < S; k++) Quad(PV(r, k), PV(r, k + 1), PV(r + 1, k), PV(r + 1, k + 1));
            for (int k = 0; k < S; k++)
            {
                idx.Add(PalmCapStart); idx.Add(PV(0, k + 1)); idx.Add(PV(0, k));
                idx.Add(PalmCapEnd); idx.Add(PV(PR - 1, k)); idx.Add(PV(PR - 1, k + 1));
            }
            for (int f = 0; f < 5; f++)
            {
                for (int r = 0; r + 1 < FRings; r++)
                    for (int k = 0; k < F; k++) Quad(FV(f, r, k), FV(f, r, k + 1), FV(f, r + 1, k), FV(f, r + 1, k + 1));
                for (int k = 0; k < F; k++)
                {
                    idx.Add(FCapStart(f)); idx.Add(FV(f, 0, k + 1)); idx.Add(FV(f, 0, k));
                    idx.Add(FCapEnd(f)); idx.Add(FV(f, FRings - 1, k)); idx.Add(FV(f, FRings - 1, k + 1));
                }
            }

            m_Mesh = new Mesh { name = "WoolGlove" };
            m_Mesh.MarkDynamic();
            m_Mesh.vertices = _verts;
            m_Mesh.SetTriangles(idx, 0);
            go.GetComponent<MeshFilter>().sharedMesh = m_Mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = false;
            go.SetActive(false);
        }

        public void Dispose() { if (m_Mesh) Object.Destroy(m_Mesh); }

        // this frame's hand frame
        Vector3 m_FrameWrist; Quaternion m_FrameRot = Quaternion.identity; float m_Mirror = 1f;
        Vector3 L2W(Vector3 local) => m_FrameWrist + m_FrameRot * new Vector3(local.x * m_Mirror, local.y, local.z);

        public void Pose(HandRig hand, MittenGrip grip, bool publishMounts)
        {
            bool visible = hand.IsTracked && hand.Anchor && Frame(hand);
            if (visible)
            {
                switch (grip)
                {
                    case MittenGrip.Free: visible = hand.HasJoints ? FromJoints(hand) : FromController(hand); break;
                    case MittenGrip.Ball: Grip(0.5f, 0.56f, 0.55f); break;      // fingers wrapped round the ball
                    case MittenGrip.Camera:
                    {
                        CameraPose(hand, publishMounts, out var pc, out var qc);
                        // pinching presses the index tip down onto the shutter
                        float press = Mathf.Clamp01((hand.Trigger - 0.2f) / 0.6f);
                        for (int i = 0; i < 20; i++)
                        {
                            var j = CamJoints[i];
                            if (i >= 4 && i < 8 && (i & 3) >= 2) j.y -= press * 0.008f * ((i & 3) - 1) * 0.5f;
                            _joints[i] = pc + qc * CamMirror(j);
                        }
                        _wrist = pc + qc * CamMirror(CamWrist);
                        m_CamPos = pc; m_CamRot = qc;
                        break;
                    }
                    case MittenGrip.Photo: Grip(0.4f, 0.82f, 0.66f); break;     // thumb and finger pinch the border
                }
            }
            if (m_Root.gameObject.activeSelf != visible) m_Root.gameObject.SetActive(visible);
            if (!visible || !hand.HasJoints) m_Tracking = false;   // a lost hand starts fresh (no easing from a stale pose)
            hand.Hold = visible && publishMounts ? HoldPoint : null;
            hand.HasCameraGrip = visible && publishMounts && grip == MittenGrip.Camera;
            hand.HasPhotoGrip = visible && publishMounts && grip == MittenGrip.Photo;
            if (!visible) return;
            Apply();
            HoldPoint.SetPositionAndRotation(L2W(BallCenter), m_FrameRot);
            if (hand.HasCameraGrip) hand.CameraGrip = new UnityEngine.Pose(m_CamPos, m_CamRot);   // the camera's root
            if (hand.HasPhotoGrip) hand.PhotoGrip = (_joints[3] + _joints[7]) * 0.5f;
        }

        /// <summary>The hand's frame: from the tracked joints (lightly smoothed), or the posed controller hand.</summary>
        bool Frame(HandRig hand)
        {
            m_Mirror = m_Side == HandSide.Left ? -1f : 1f;
            if (hand.HasJoints)
            {
                var j = hand.Joints;
                Vector3 fwd = j[8] - hand.Wrist, across = j[4] - j[16];
                var palm = Vector3.Cross(fwd, across) * (m_Side == HandSide.Right ? 1f : -1f);   // points out of the palm
                if (fwd.sqrMagnitude < 1e-8f || palm.sqrMagnitude < 1e-10f) return false;
                m_FrameWrist = hand.Wrist;
                var target = Quaternion.LookRotation(fwd, -palm);
                m_TrackedRot = m_Tracking ? Quaternion.Slerp(m_TrackedRot, target, 1f - Mathf.Exp(-Time.deltaTime / RotSmoothing)) : target;
                m_FrameRot = m_TrackedRot;
                return true;
            }
            var e = ControllerOffsetEuler;
            var q = Quaternion.Euler(e.x, e.y * m_Mirror, e.z * m_Mirror);
            var p = ControllerOffsetPos;
            p.x *= m_Mirror;
            m_FrameWrist = hand.Anchor.TransformPoint(p);
            m_FrameRot = hand.Anchor.rotation * q;
            return true;
        }

        Vector3 m_CamPos; Quaternion m_CamRot = Quaternion.identity;

        /// <summary>Where the held camera goes: upright (no roll), aimed between where your hand points and where you look,
        /// with the grip's palm on your real palm. The desktop stand-in keeps the camera at its screen spot.</summary>
        void CameraPose(HandRig hand, bool xr, out Vector3 pos, out Quaternion rot)
        {
            if (!xr)
            {
                pos = hand.Anchor.position;
                rot = hand.Anchor.rotation;
                return;
            }
            var head = PlayerRig.Active ? PlayerRig.Active.Head : null;
            var handFwd = hand.HasJoints ? (hand.Joints[8] - hand.Wrist) : hand.Anchor.forward;
            var aim = head ? Vector3.Slerp(handFwd.normalized, head.forward, 0.6f) : handFwd.normalized;
            if (Mathf.Abs(Vector3.Dot(aim, Vector3.up)) > 0.95f) aim = head ? head.forward : Vector3.forward;
            rot = Quaternion.LookRotation(aim, Vector3.up);
            var palm = hand.Palm ? hand.Palm.position : hand.Anchor.position;
            pos = palm - rot * CamMirror(CamPalm);
        }

        /// <summary>Snap the glove into a grip: index curl, the other fingers' curl, thumb fold (0..1).</summary>
        void Grip(float index, float grip, float thumb)
        {
            _curl[1] = index;
            for (int f = 2; f < 5; f++) _curl[f] = grip;
            for (int f = 0; f < 5; f++) _spread[f] = RestSpread[f] * (1f - 0.6f * grip);   // fingers close up as they curl
            PoseHand(thumb, 1f);
        }

        /// <summary>The procedural hand from <see cref="_curl"/>, <see cref="_spread"/>, the thumb fold and a size.</summary>
        void PoseHand(float thumb, float scale)
        {
            _wrist = m_FrameWrist;
            float midX = KnuckleRest[2].x;
            for (int f = 0; f < 5; f++)
            {
                Vector3 pos = KnuckleRest[f];
                if (f > 0) pos.x = midX + (pos.x - midX) * KnuckleSpread;
                pos *= scale;
                Vector3 dir;
                Quaternion bend;
                if (f == 0)
                {
                    dir = ThumbRestDir;
                    bend = Quaternion.AngleAxis(thumb * ThumbCurlDeg, Vector3.Cross(dir, ThumbFoldTarget).normalized);
                }
                else
                {
                    var yaw = Quaternion.AngleAxis(_spread[f], Vector3.up);
                    dir = yaw * Vector3.forward;
                    bend = Quaternion.AngleAxis(_curl[f] * FingerCurlDeg, yaw * Vector3.right);
                }
                _joints[f * 4] = L2W(pos);
                for (int s = 0; s < 3; s++)
                {
                    dir = bend * dir;
                    pos += dir * (SegLen[f][s] * scale);
                    _joints[f * 4 + s + 1] = L2W(pos);
                }
            }
        }

        bool FromController(HandRig hand)
        {
            float trigger = Mathf.Clamp01(hand.Trigger), grip = Mathf.Clamp01(hand.Grip);
            _curl[1] = Mathf.Lerp(RestCurl, 1f, trigger);
            for (int f = 2; f < 5; f++) _curl[f] = Mathf.Max(Mathf.Lerp(RestCurl, 1f, grip), MinHoldCurl);
            for (int f = 0; f < 5; f++) _spread[f] = RestSpread[f] * (1f - 0.6f * grip);
            PoseHand(grip > 0.6f ? 0.7f : RestCurl, 1f);
            return true;
        }

        /// <summary>
        /// Hand tracking: each finger's curl and spread, the thumb's fold and the hand's size, read from the tracked joints,
        /// clamped to what a gloved hand can do and smoothed, then posed procedurally. Feeding raw joints straight into
        /// the mesh let tracking noise and odd joint solves pull the knit out of shape.
        /// </summary>
        bool FromJoints(HandRig hand)
        {
            var j = hand.Joints;
            var inv = Quaternion.Inverse(m_FrameRot);
            Vector3 Local(Vector3 w) { var l = inv * (w - m_FrameWrist); l.x *= m_Mirror; return l; }

            float dt = Time.deltaTime;
            bool snap = !m_Tracking;
            float Smooth(float cur, float target, float tau) => snap ? target : Mathf.Lerp(cur, target, 1f - Mathf.Exp(-dt / tau));

            for (int f = 1; f < 5; f++)
            {
                // spread: the first segment's heading across the back of the hand
                var d0 = Local(j[f * 4 + 1]) - Local(j[f * 4]);
                float yaw = Mathf.Atan2(d0.x, d0.z) * Mathf.Rad2Deg;
                float spread = Mathf.Clamp(yaw, RestSpread[f] - 12f, RestSpread[f] + 16f);
                var heading = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));
                // curl: fitted to the procedural bend (each segment turns by curl × FingerCurlDeg more than the last)
                float angle = 0f, prev = 0f, fit = 0f;
                for (int s = 0; s < 3; s++)
                {
                    var d = Local(j[f * 4 + s + 1]) - Local(j[f * 4 + s]);
                    float raw = Mathf.Atan2(-d.y, Vector3.Dot(d, heading)) * Mathf.Rad2Deg;   // + = toward the palm
                    angle = s == 0 ? raw : angle + Mathf.DeltaAngle(prev, raw);              // unwrapped: a fist doesn't flip
                    prev = raw;
                    fit += (s + 1) * angle;
                }
                float curl = Mathf.Clamp(fit / (14f * FingerCurlDeg), 0f, MaxTrackedCurl);
                m_TCurl[f] = Smooth(m_TCurl[f], curl, CurlSmoothing);
                m_TSpread[f] = Smooth(m_TSpread[f], spread, SpreadSmoothing);
                _curl[f] = m_TCurl[f];
                _spread[f] = m_TSpread[f];
            }

            // the thumb: how far it has folded from rest toward the palm
            var td = Local(j[3]) - Local(j[1]);
            float thumb = 0f;
            if (td.sqrMagnitude > 1e-8f)
            {
                td.Normalize();
                if (Vector3.Dot(td, ThumbFoldTarget) > Vector3.Dot(ThumbRestDir, ThumbFoldTarget))
                    thumb = Mathf.Clamp01(Vector3.Angle(ThumbRestDir, td) / (2.5f * ThumbCurlDeg));
            }
            m_TThumb = Smooth(m_TThumb, thumb, ThumbSmoothing);

            // the hand's size (wrist → middle knuckle), so the gloves fit big and small hands
            float scale = Mathf.Clamp((j[8] - hand.Wrist).magnitude / KnuckleRest[2].magnitude, 0.85f, 1.2f);
            m_TScale = Smooth(m_TScale, scale, ScaleSmoothing);

            m_Tracking = true;
            PoseHand(m_TThumb, m_TScale);
            return true;
        }

        // ---------------------------------------------------------------- mesh
        void Apply()
        {
            float sign = m_Side == HandSide.Right ? 1f : -1f;
            Vector3 fwd = _joints[8] - _wrist;            // wrist → middle knuckle
            Vector3 across = _joints[4] - _joints[16];    // little knuckle → index knuckle
            Vector3 palmN = Vector3.Cross(fwd, across).normalized * sign;   // out of the palm
            if (palmN.sqrMagnitude < 1e-6f) palmN = Vector3.down;
            Vector3 backN = -palmN;
            Vector3 f = fwd.sqrMagnitude > 1e-8f ? fwd.normalized : Vector3.forward;

            Color glove = GloveColor; glove.a = 1f;      // stocking stitch
            Color cuff = CuffColor; cuff.a = 0f;         // ribbing
            Color stripe = StripeColor; stripe.a = 0f;

            // ---- palm + cuff
            float width = across.magnitude + FingerRadius * 2.2f * Bulk;
            float h = PalmThickness * Bulk;
            Vector3 knuckleMid = (_joints[4] + _joints[16]) * 0.5f;
            float palmLen = Mathf.Max(0.02f, Vector3.Dot(knuckleMid - _wrist, f));
            Vector3 palmBase = _wrist;
            float wristHalfW = width * 0.5f * 0.74f, wristHalfH = h * 0.5f * 0.9f;
            float v = 0f;
            for (int r = 0; r < PR; r++)
            {
                Vector3 c; float w, hb, hp; Color col;
                if (r < CuffRings)
                {
                    float at = CuffAt[r] * CuffLength;
                    c = palmBase + f * at;
                    if (r == CuffRings - 1) { w = wristHalfW; hb = hp = wristHalfH; col = glove; }
                    else
                    {
                        float flare = Mathf.Lerp(1.04f, 1.15f, Mathf.Clamp01(-at / CuffLength));
                        w = wristHalfW * flare; hb = hp = wristHalfH * flare * 1.08f;
                        col = r == 2 || r == 3 ? stripe : cuff;
                    }
                }
                else if (r < CuffRings + PalmRings)
                {
                    float t = PalmAt[r - CuffRings];
                    c = palmBase + f * (palmLen * t);
                    w = width * 0.5f * Mathf.Lerp(0.78f, 1f, Mathf.SmoothStep(0f, 1f, t));
                    hb = h * 0.5f; hp = h * 0.5f * 0.85f;   // a little flatter on the palm side
                    col = glove;
                }
                else
                {
                    // a rounded knuckle end; the fingers grow out of it
                    float beta = PalmDomeDeg[r - CuffRings - PalmRings] * Mathf.Deg2Rad;
                    float sc = Mathf.Pow(Mathf.Cos(beta), 0.6f), dome = h * 0.42f;
                    c = palmBase + f * (palmLen * PalmAt[PalmRings - 1] + dome * Mathf.Sin(beta));
                    w = width * 0.5f * sc; hb = h * 0.5f * sc; hp = h * 0.5f * 0.85f * sc;
                    col = glove;
                }
                if (r > 0 && (c - _pc[r - 1]).sqrMagnitude > 1e-10f) v += Vector3.Distance(c, _pc[r - 1]);
                _pc[r] = c;
                _ringCentre[r] = c;
                Ring(PV(r, 0), S, c, f, backN, w, hb, hp, v, PalmStitches, col);
            }
            {
                // the cuff's opening: recessed and dark, so it reads as the inside of the glove
                Color inside = CuffColor * 0.3f; inside.a = 0f;
                _wp[PalmCapStart] = _pc[0] + f * 0.018f; _normals[PalmCapStart] = -f;
                _uvs[PalmCapStart] = new Vector2(PalmStitches * 0.5f, 0f); _colors[PalmCapStart] = inside;
                float dome = h * 0.42f;
                _wp[PalmCapEnd] = palmBase + f * (palmLen * PalmAt[PalmRings - 1] + dome); _normals[PalmCapEnd] = f;
                _uvs[PalmCapEnd] = new Vector2(PalmStitches * 0.5f, v + dome); _colors[PalmCapEnd] = glove;
            }
            Normals(PV, PR, S, 0);

            // ---- fingers (thumb first)
            Vector3 palmMid = palmBase + f * (palmLen * 0.45f);
            for (int fi = 0; fi < 5; fi++)
            {
                float r0 = fi == 0 ? ThumbRadius * Bulk : FingerRadius * Bulk * FingerRadiusMul[fi];
                Vector3 k0 = _joints[fi * 4], j1 = _joints[fi * 4 + 1], j2 = _joints[fi * 4 + 2], j3 = _joints[fi * 4 + 3];
                Vector3 tipDir = Dir(j2, j3, f);
                // start buried in the palm so the finger grows out of the knit with no visible seam
                Vector3 root = fi == 0 ? Vector3.Lerp(k0, palmMid, 0.5f) : k0 - Dir(k0, j1, f) * 0.018f;
                float rTip = r0 * 0.86f;
                _ctrl[0] = root - (k0 - root);               // phantom ends for the Catmull-Rom spline
                _ctrl[1] = root; _ctrl[2] = k0; _ctrl[3] = j1; _ctrl[4] = j2; _ctrl[5] = j3 - tipDir * rTip * 0.55f;
                _ctrl[6] = _ctrl[5] + (_ctrl[5] - j2);
                for (int r = 0; r < FR; r++)
                {
                    float u = r / (float)(FR - 1) * 4f;
                    int seg = Mathf.Min(3, (int)u);
                    _fc[r] = CatmullRom(_ctrl[seg], _ctrl[seg + 1], _ctrl[seg + 2], _ctrl[seg + 3], u - seg);
                    float along = u / 4f;
                    _fr[r] = r0 * (fi == 0 ? Mathf.Lerp(1.12f, 0.88f, along) : Mathf.Lerp(1.05f, 0.86f, along));
                }
                for (int r = 0; r < FR; r++) _ft[r] = Dir(_fc[Mathf.Max(0, r - 1)], _fc[Mathf.Min(FR - 1, r + 1)], tipDir);
                float domeLen = _fr[FR - 1] * 1.05f;
                for (int d = 0; d < FDome; d++)
                {
                    float beta = FingerDomeDeg[d] * Mathf.Deg2Rad;
                    int r = FR + d;
                    _ft[r] = _ft[FR - 1];
                    _fc[r] = _fc[FR - 1] + _ft[r] * domeLen * Mathf.Sin(beta);
                    _fr[r] = _fr[FR - 1] * Mathf.Pow(Mathf.Cos(beta), 0.85f);
                }

                // parallel-transported ring frames: no twisting however far the finger curls
                Vector3 up = backN - _ft[0] * Vector3.Dot(backN, _ft[0]);
                if (up.sqrMagnitude < 1e-8f) up = Vector3.Cross(_ft[0], across);
                up.Normalize();
                float fv = 0f, stitches = fi == 0 ? ThumbStitches : FingerStitches;
                for (int r = 0; r < FRings; r++)
                {
                    if (r > 0)
                    {
                        fv += Vector3.Distance(_fc[r - 1], _fc[r]);
                        up -= _ft[r] * Vector3.Dot(up, _ft[r]);
                        up = up.sqrMagnitude > 1e-8f ? up.normalized : Vector3.Cross(_ft[r], across).normalized;
                    }
                    _ringCentre[PR + fi * FRings + r] = _fc[r];
                    Ring(FV(fi, r, 0), F, _fc[r], _ft[r], up, _fr[r], _fr[r], _fr[r] * 0.94f, fv, stitches, glove);
                }
                int cs = FCapStart(fi), ce = FCapEnd(fi);
                _wp[cs] = _fc[0] - _ft[0] * _fr[0] * 0.5f; _normals[cs] = -_ft[0];
                _uvs[cs] = new Vector2(stitches * 0.5f, 0f); _colors[cs] = glove;
                _wp[ce] = _fc[FR - 1] + _ft[FR - 1] * domeLen; _normals[ce] = _ft[FR - 1];
                _uvs[ce] = new Vector2(stitches * 0.5f, fv + domeLen * 0.5f); _colors[ce] = glove;
                int capturedF = fi;
                Normals((r, k) => FV(capturedF, r, k), FRings, F, PR + fi * FRings);
            }

            var toLocal = m_Root.worldToLocalMatrix;
            for (int i = 0; i < VertexCount; i++)
            {
                _verts[i] = toLocal.MultiplyPoint3x4(_wp[i]);
                _normals[i] = toLocal.MultiplyVector(_normals[i]).normalized;
            }
            m_Mesh.SetVertices(_verts);
            m_Mesh.SetNormals(_normals);
            m_Mesh.SetUVs(0, _uvs);
            m_Mesh.SetColors(_colors);
            m_Mesh.RecalculateBounds();
        }

        /// <summary>One ring of a tube: centre, tangent, an "up" toward the back of the hand, half-width, and half-heights
        /// on the back and palm sides.</summary>
        void Ring(int start, int sides, Vector3 c, Vector3 t, Vector3 upHint, float w, float hBack, float hPalm, float v, float stitches, Color col)
        {
            Vector3 up = upHint - t * Vector3.Dot(upHint, t);
            up = up.sqrMagnitude > 1e-8f ? up.normalized : Vector3.Cross(t, Vector3.right).normalized;
            Vector3 right = Vector3.Cross(up, t);
            for (int k = 0; k <= sides; k++)
            {
                float a = k * Mathf.PI * 2f / sides, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                int i = start + k;
                _wp[i] = c + right * (cs * w) + up * (sn * (sn > 0f ? hBack : hPalm));
                _uvs[i] = new Vector2(k / (float)sides * stitches, v);
                _colors[i] = col;
            }
        }

        /// <summary>Smooth normals from the surface itself, turned to face away from each ring's centre.</summary>
        void Normals(System.Func<int, int, int> V, int rings, int sides, int centreOffset)
        {
            for (int r = 0; r < rings; r++)
            {
                int rp = Mathf.Min(r + 1, rings - 1), rm = Mathf.Max(r - 1, 0);
                Vector3 centre = _ringCentre[centreOffset + r];
                for (int k = 0; k < sides; k++)
                {
                    int kp = (k + 1) % sides, km = (k + sides - 1) % sides;
                    Vector3 p = _wp[V(r, k)];
                    Vector3 n = Vector3.Cross(_wp[V(r, kp)] - _wp[V(r, km)], _wp[V(rp, k)] - _wp[V(rm, k)]);
                    Vector3 outward = p - centre;
                    if (n.sqrMagnitude < 1e-14f) n = outward;
                    if (Vector3.Dot(n, outward) < 0f) n = -n;
                    _normals[V(r, k)] = n.normalized;
                }
                _normals[V(r, sides)] = _normals[V(r, 0)];
            }
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        static Vector3 Dir(Vector3 a, Vector3 b, Vector3 fallback)
        {
            var d = b - a;
            return d.sqrMagnitude > 1e-10f ? d.normalized : fallback;
        }
    }
}
