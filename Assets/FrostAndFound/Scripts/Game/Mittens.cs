using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Your hands, as knitted wool mittens (ported from the team's LowPolyHand in roshpro/frosty). Each mitten is ONE
    /// closed surface — ribbed cuff → palm → mitt over the four fingers → domed tip, with the thumb grown out of an
    /// opening in the palm — whose rings follow the hand-tracking joints, so it bends as one soft piece. With
    /// controllers (or the desktop rig) a posed hand wraps the controller: trigger curls the index, grip curls the
    /// rest. Each mitten publishes a hold point inside the curled palm for snowballs. Hidden from the Polaroid.
    /// </summary>
    /// <summary>What a mitten is doing: following your hand, or snapped into a grip around something it holds.</summary>
    public enum MittenGrip { Free, Ball, Camera, Photo }

    [DefaultExecutionOrder(100)] // after the rig has sampled this frame's joints; before the camera/photo/snowball place themselves
    public class Mittens : MonoBehaviour
    {
        WoolMitten m_L, m_R;
        Material m_Mat;

        public static Mittens Create()
        {
            var go = new GameObject("Mittens");
            go.layer = Layers.PlayerHands;
            var m = go.AddComponent<Mittens>();
            m.m_Mat = new Material(FrostAssets.Shader(FrostAssets.ShaderId.Wool)) { name = "Wool" };
            m.m_L = new WoolMitten(go.transform, HandSide.Left, m.m_Mat);
            m.m_R = new WoolMitten(go.transform, HandSide.Right, m.m_Mat);
            return m;
        }

        void LateUpdate()
        {
            var rig = PlayerRig.Active;
            if (!rig) return;
            // the desktop stand-in's hands aren't controllers: there the camera, photo and snowball keep their own
            // screen placements and the mittens just curl into the grip
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

    /// <summary>One knitted mitten mesh, re-shaped every frame around the hand.</summary>
    class WoolMitten
    {
        // shape
        const float ThumbRadius = 0.012f, FingerRadius = 0.0105f, PalmThickness = 0.034f, GloveBulk = 1.35f, CuffLength = 0.058f;
        // yarn
        static readonly Color GloveColor = new Color(0.72f, 0.07f, 0.09f);
        static readonly Color CuffColor = new Color(0.95f, 0.92f, 0.84f);
        static readonly Color StripeColor = new Color(0.07f, 0.42f, 0.18f);
        // controller pose (right hand; left is mirrored)
        static readonly Vector3 ControllerOffsetPos = new Vector3(0.03f, -0.015f, -0.095f);
        static readonly Vector3 ControllerOffsetEuler = new Vector3(30f, 0f, -90f);
        const float RestCurl = 0.15f, MinHoldCurl = 0.35f;
        // held things, in the hand's own frame (right hand: wrist at the origin, fingers +Z, palm facing -Y, thumb -X;
        // the left hand mirrors x). A snowball cups in the palm; the camera's side presses into the palm with the
        // thumb along its top and the fingers curling round its front edge, lens pointing where your fingers point.
        static readonly Vector3 BallCenter = new Vector3(0.004f, -0.054f, 0.058f);
        // the camera grip, in camera space for the LEFT hand (x mirrors for the right): the mitten stands at the camera's
        // left end, fingers up and palm against the end; the mitt curls over the top-left edge with its tip on the shutter
        // and the thumb tucks under the bottom, so the screen faces you, clear of the hand
        static readonly Vector3 CamPalm = new Vector3(-0.092f, 0.005f, -0.012f);
        static readonly Vector3 CamWrist = new Vector3(-0.094f, -0.052f, -0.014f);
        static readonly Vector3[] CamJoints =
        {
            new Vector3(-0.080f, -0.022f, -0.036f), new Vector3(-0.070f, -0.046f, -0.032f), new Vector3(-0.056f, -0.055f, -0.020f), new Vector3(-0.042f, -0.057f, -0.006f),   // thumb, under the bottom
            new Vector3(-0.092f, 0.046f, -0.030f), new Vector3(-0.090f, 0.084f, -0.028f), new Vector3(-0.070f, 0.094f, -0.022f), new Vector3(-0.050f, 0.090f, -0.014f),     // index, tip on the shutter
            new Vector3(-0.094f, 0.048f, -0.010f), new Vector3(-0.092f, 0.088f, -0.008f), new Vector3(-0.071f, 0.097f, -0.004f), new Vector3(-0.050f, 0.092f, 0.004f),      // middle
            new Vector3(-0.093f, 0.046f, 0.010f), new Vector3(-0.091f, 0.084f, 0.012f), new Vector3(-0.072f, 0.094f, 0.014f), new Vector3(-0.053f, 0.089f, 0.020f),        // ring
            new Vector3(-0.091f, 0.042f, 0.028f), new Vector3(-0.089f, 0.072f, 0.030f), new Vector3(-0.074f, 0.084f, 0.031f), new Vector3(-0.058f, 0.081f, 0.034f),        // little
        };
        Vector3 CamMirror(Vector3 v) => new Vector3(m_Side == HandSide.Left ? v.x : -v.x, v.y, v.z);

        // procedural controller hand in a right-handed frame: wrist at origin, fingers along +Z, palm facing -Y,
        // thumb on -X. Knuckles arch slightly (middle highest).
        static readonly Vector3[] KnuckleRest =
        {
            new Vector3(-0.022f, -0.012f, 0.028f),
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
        static readonly Vector3 ThumbRestDir = new Vector3(-0.55f, -0.25f, 0.8f).normalized;
        static readonly Vector3 ThumbFoldTarget = new Vector3(1f, -0.6f, 0.2f).normalized;
        const float FingerCurlDeg = 72f, ThumbCurlDeg = 35f;

        // ---- topology ----
        // Body: BodyRings rings of S sides (+1 seam vertex each), a cap at the cuff and at the tip.
        //   0-6 cuff (stripe, lip back to the wrist) | 7-12 palm | 13-19 mitt over the four fingers | 20-22 domed tip
        // Thumb: the body quads in rings PatchR0..PatchR1 × sides PatchK0..PatchK1 are left out; the thumb tube's
        //   first ring IS that opening's edge loop, so the thumb grows out of the palm with no seam.
        const int S = 32;   // sides per ring: smooth enough that the silhouette doesn't facet
        const int CuffRings = 7, PalmRings = 6, MittRings = 7, DomeRings = 3;
        const int BodyRings = CuffRings + PalmRings + MittRings + DomeRings;
        const int PatchR0 = CuffRings, PatchR1 = CuffRings + 3;
        const int PatchK0 = S / 2 - 4, PatchK1 = S / 2 + 4;   // a quarter of the way round: a full thumb root
        const int Loop = 2 * ((PatchR1 - PatchR0) + (PatchK1 - PatchK0));
        const int ThumbRings = 8;
        const float BodyStitches = 22f, ThumbStitches = 10f;
        const int BodyCapStart = BodyRings * (S + 1), BodyCapEnd = BodyCapStart + 1;
        const int ThumbStart = BodyCapEnd + 1;
        const int ThumbCap = ThumbStart + ThumbRings * (Loop + 1);
        const int VertexCount = ThumbCap + 1;
        // the thumb opening's centre, measured from the index side toward the ring frame's "up" (which points out of the
        // palm): a little toward the palm, where a thumb really grows from
        const float ThumbSideDeg = 25f;

        static int BV(int r, int k) => r * (S + 1) + k;
        static int TV(int r, int m) => ThumbStart + r * (Loop + 1) + m;

        static readonly float[] CuffAt = { -1f, -0.62f, -0.62f, -0.44f, -0.44f, -0.1f, -0.1f };
        static readonly float[] PalmAt = { 0.05f, 0.22f, 0.4f, 0.58f, 0.76f, 0.92f };
        static readonly float[] DomeDeg = { 35f, 60f, 80f };
        static readonly float[] ThumbRadiusMul = { 0f, 1.3f, 1.12f, 1.05f, 1f, 0.95f };
        static readonly float[] ThumbDomeDeg = { 50f, 78f };

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
        readonly int[] _loopR = new int[Loop];
        readonly int[] _loopK = new int[Loop];
        readonly float[] _phi = new float[Loop + 1];
        readonly float[] _cosK = new float[S + 1];
        readonly float[] _sinK = new float[S + 1];
        readonly Vector3[] _bc = new Vector3[BodyRings], _bx = new Vector3[BodyRings], _bt = new Vector3[BodyRings];
        readonly float[] _bw = new float[BodyRings], _bh = new float[BodyRings], _bu = new float[BodyRings];
        readonly Color[] _bcol = new Color[BodyRings];
        readonly Vector3[] _lc = new Vector3[4], _lx = new Vector3[4];
        readonly float[] _lw = new float[4], _lh = new float[4];
        readonly Vector3[] _tc = new Vector3[ThumbRings], _tt = new Vector3[ThumbRings];
        readonly float[] _tr = new float[ThumbRings];

        public WoolMitten(Transform parent, HandSide side, Material mat)
        {
            m_Side = side;
            var go = new GameObject(side + " Mitten", typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = Layers.PlayerHands;
            m_Root = go.transform;
            m_Root.SetParent(parent, false);
            HoldPoint = new GameObject(side + " Mitten Hold").transform;
            HoldPoint.SetParent(parent, false);

            // the thumb opening's edge loop, counter-clockwise seen from outside
            int li = 0;
            for (int k = PatchK0; k < PatchK1; k++) { _loopR[li] = PatchR0; _loopK[li++] = k; }
            for (int r = PatchR0; r < PatchR1; r++) { _loopR[li] = r; _loopK[li++] = PatchK1; }
            for (int k = PatchK1; k > PatchK0; k--) { _loopR[li] = PatchR1; _loopK[li++] = k; }
            for (int r = PatchR1; r > PatchR0; r--) { _loopR[li] = r; _loopK[li++] = PatchK0; }

            var idx = new List<int>(6 * (BodyRings * S + ThumbRings * Loop) + 3 * (2 * S + Loop));
            void Quad(int a0, int b0, int c0, int d0) { idx.Add(a0); idx.Add(b0); idx.Add(c0); idx.Add(b0); idx.Add(d0); idx.Add(c0); }
            for (int r = 0; r + 1 < BodyRings; r++)
                for (int k = 0; k < S; k++)
                {
                    if (r >= PatchR0 && r < PatchR1 && k >= PatchK0 && k < PatchK1) continue; // thumb opening
                    Quad(BV(r, k), BV(r, k + 1), BV(r + 1, k), BV(r + 1, k + 1));
                }
            for (int k = 0; k < S; k++)
            {
                idx.Add(BodyCapStart); idx.Add(BV(0, k + 1)); idx.Add(BV(0, k));
                idx.Add(BodyCapEnd); idx.Add(BV(BodyRings - 1, k)); idx.Add(BV(BodyRings - 1, k + 1));
            }
            for (int r = 0; r + 1 < ThumbRings; r++)
                for (int m = 0; m < Loop; m++)
                    Quad(TV(r, m), TV(r, m + 1), TV(r + 1, m), TV(r + 1, m + 1));
            for (int m = 0; m < Loop; m++) { idx.Add(ThumbCap); idx.Add(TV(ThumbRings - 1, m)); idx.Add(TV(ThumbRings - 1, m + 1)); }

            m_Mesh = new Mesh { name = "WoolMitten" };
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
        Vector3 L2WDir(Vector3 local) => m_FrameRot * new Vector3(local.x * m_Mirror, local.y, local.z);

        public void Pose(HandRig hand, MittenGrip grip, bool publishMounts)
        {
            bool visible = hand.IsTracked && hand.Anchor && Frame(hand);
            if (visible)
            {
                switch (grip)
                {
                    case MittenGrip.Free: visible = hand.HasJoints ? FromJoints(hand) : FromController(hand); break;
                    case MittenGrip.Ball: PoseInFrame(0.5f, 0.56f, 0.55f); break;      // fingers wrapped round the ball
                    case MittenGrip.Camera:
                    {
                        CameraPose(hand, publishMounts, out var pc, out var qc);
                        // pinching presses the mitt's tip down onto the shutter
                        float press = Mathf.Clamp01((hand.Trigger - 0.2f) / 0.6f);
                        for (int i = 0; i < 20; i++)
                        {
                            var j = CamJoints[i];
                            if (i >= 4 && (i & 3) >= 2) j.y -= press * 0.008f * ((i & 3) - 1) * 0.5f;
                            _joints[i] = pc + qc * CamMirror(j);
                        }
                        _wrist = pc + qc * CamMirror(CamWrist);
                        m_CamPos = pc; m_CamRot = qc;
                        break;
                    }
                    case MittenGrip.Photo: PoseInFrame(0.4f, 0.82f, 0.66f); break;     // thumb and finger pinch the border
                }
            }
            if (m_Root.gameObject.activeSelf != visible) m_Root.gameObject.SetActive(visible);
            hand.Hold = visible && publishMounts ? HoldPoint : null;
            hand.HasCameraGrip = visible && publishMounts && grip == MittenGrip.Camera;
            hand.HasPhotoGrip = visible && publishMounts && grip == MittenGrip.Photo;
            if (!visible) return;
            Apply();
            HoldPoint.SetPositionAndRotation(L2W(BallCenter), m_FrameRot);
            if (hand.HasCameraGrip) hand.CameraGrip = new UnityEngine.Pose(m_CamPos, m_CamRot);   // the camera's root
            if (hand.HasPhotoGrip) hand.PhotoGrip = (_joints[3] + _joints[7]) * 0.5f;
        }

        /// <summary>The hand's frame: from the tracked joints, or the posed controller hand.</summary>
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
                m_FrameRot = Quaternion.LookRotation(fwd, -palm);
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

        /// <summary>Snap the mitten into a grip: fingers curled by index/grip, thumb folded by thumb (0..1).</summary>
        void PoseInFrame(float index, float grip, float thumb)
        {
            _wrist = m_FrameWrist;
            for (int f = 0; f < 5; f++)
            {
                Vector3 pos = KnuckleRest[f];
                Vector3 dir;
                Quaternion bend;
                if (f == 0)
                {
                    dir = ThumbRestDir;
                    bend = Quaternion.AngleAxis(thumb * ThumbCurlDeg, Vector3.Cross(dir, ThumbFoldTarget).normalized);
                }
                else
                {
                    dir = Vector3.forward;
                    bend = Quaternion.AngleAxis((f == 1 ? index : grip) * FingerCurlDeg, Vector3.right);
                }
                _joints[f * 4] = L2W(pos);
                for (int s = 0; s < 3; s++)
                {
                    dir = bend * dir;
                    pos += dir * SegLen[f][s];
                    _joints[f * 4 + s + 1] = L2W(pos);
                }
            }
        }

        bool FromJoints(HandRig hand)
        {
            _wrist = hand.Wrist;
            for (int i = 0; i < 20; i++) _joints[i] = hand.Joints[i];
            return true;
        }

        bool FromController(HandRig hand)
        {
            float trigger = Mathf.Clamp01(hand.Trigger), grip = Mathf.Clamp01(hand.Grip);
            bool thumbDown = grip > 0.6f;
            float mirror = m_Side == HandSide.Left ? -1f : 1f;
            var e = ControllerOffsetEuler;
            var q = Quaternion.Euler(e.x, e.y * mirror, e.z * mirror);
            var p = ControllerOffsetPos;
            p.x *= mirror;
            var anchor = hand.Anchor;
            _wrist = ToWorld(anchor, p, q, Vector3.zero, mirror);
            for (int f = 0; f < 5; f++)
            {
                Vector3 pos = KnuckleRest[f];
                Vector3 dir;
                Quaternion bend;
                if (f == 0)
                {
                    dir = ThumbRestDir;
                    float tc = thumbDown ? 0.7f : RestCurl;
                    bend = Quaternion.AngleAxis(tc * ThumbCurlDeg, Vector3.Cross(dir, ThumbFoldTarget).normalized);
                }
                else
                {
                    dir = Vector3.forward;
                    float curl = Mathf.Lerp(RestCurl, 1f, f == 1 ? trigger : grip);
                    if (f > 1) curl = Mathf.Max(curl, MinHoldCurl);
                    bend = Quaternion.AngleAxis(curl * FingerCurlDeg, Vector3.right);
                }
                _joints[f * 4] = ToWorld(anchor, p, q, pos, mirror);
                for (int s = 0; s < 3; s++)
                {
                    dir = bend * dir;
                    pos += dir * SegLen[f][s];
                    _joints[f * 4 + s + 1] = ToWorld(anchor, p, q, pos, mirror);
                }
            }
            return true;
        }

        static Vector3 ToWorld(Transform anchor, Vector3 offsetPos, Quaternion offsetRot, Vector3 local, float mirror)
        {
            local.x *= mirror;
            return anchor.TransformPoint(offsetPos + offsetRot * local);
        }

        void Apply()
        {
            var toLocal = m_Root.worldToLocalMatrix;
            float sign = m_Side == HandSide.Right ? 1f : -1f;

            Vector3 fwd = _joints[8] - _wrist;            // wrist → middle knuckle
            Vector3 across = _joints[4] - _joints[16];    // little knuckle → index knuckle
            Vector3 back = Vector3.Cross(fwd, across).normalized * sign;   // back-of-hand normal
            if (back.sqrMagnitude < 1e-6f) back = Vector3.up;
            Vector3 f = fwd.sqrMagnitude > 1e-8f ? fwd.normalized : Vector3.forward;
            Vector3 kx = across.sqrMagnitude > 1e-8f ? across.normalized : Vector3.right;

            float bulk = GloveBulk;
            float width = across.magnitude + FingerRadius * 2.4f * bulk;
            float h = PalmThickness * bulk;
            float mittH = FingerRadius * bulk * 1.6f;
            Vector3 knuckleMid = (_joints[4] + _joints[16]) * 0.5f;
            float palmLen = Mathf.Max(0.02f, Vector3.Dot(knuckleMid - _wrist, f));
            Vector3 palmBase = _wrist + back * h * 0.05f;
            float wristHalfW = width * 0.5f * 0.74f, wristHalfH = h * 0.5f * 0.88f;

            Color glove = GloveColor; glove.a = 1f;
            Color cuff = CuffColor; cuff.a = 0f;
            Color stripe = StripeColor; stripe.a = 0f;

            // cuff (0-6): ribbed, flared, green stripe; ring 6 steps back in to the wrist (the cuff's lip)
            for (int r = 0; r < CuffRings; r++)
            {
                float at = CuffAt[r] * CuffLength;
                _bc[r] = palmBase + f * at;
                _bx[r] = kx;
                _bu[r] = 1f;
                if (r == CuffRings - 1) { _bw[r] = wristHalfW; _bh[r] = wristHalfH; _bcol[r] = glove; continue; }
                float flare = Mathf.Lerp(1.04f, 1.15f, Mathf.Clamp01(-at / CuffLength));   // a snug ribbed cuff, gently flared
                _bw[r] = wristHalfW * flare;
                _bh[r] = wristHalfH * flare * 1.08f;
                _bcol[r] = r == 2 || r == 3 ? stripe : cuff;
            }

            // palm (7-12): widens toward the knuckles, slightly flatter on the palm side
            for (int i = 0; i < PalmRings; i++)
            {
                int r = CuffRings + i;
                float t = PalmAt[i];
                _bc[r] = palmBase + f * (palmLen * t);
                _bx[r] = kx;
                _bw[r] = width * 0.5f * Mathf.Lerp(0.76f, 0.98f, Mathf.SmoothStep(0f, 1f, t));
                _bh[r] = h * 0.5f;
                _bu[r] = 0.82f;
                _bcol[r] = glove;
            }

            // mitt levels: averaged joints of the four fingers, knuckles → fingertips
            for (int lvl = 0; lvl < 4; lvl++)
            {
                Vector3 a = _joints[4 + lvl], b = _joints[8 + lvl], c = _joints[12 + lvl], d = _joints[16 + lvl];
                _lc[lvl] = (a + b + c + d) * 0.25f;
                var ax = a - d;
                _lx[lvl] = ax.sqrMagnitude > 1e-8f ? ax.normalized : kx;
                float k = lvl / 3f;
                _lw[lvl] = Mathf.Max(ax.magnitude * 0.5f + FingerRadius * bulk * 1.15f, width * 0.38f) * Mathf.Lerp(1f, 0.93f, k);
                _lh[lvl] = Mathf.Lerp(h * 0.5f, mittH, k);
            }
            Vector3 tipPoint = _lc[3];
            Vector3 tipDir = Dir(_lc[2], _lc[3], f);
            float dome = Mathf.Max(mittH * 1.1f, _lw[3] * 0.8f);   // round the tip in plan view too
            float pull = Mathf.Min(dome * 0.6f, Vector3.Distance(_lc[2], tipPoint) * 0.45f);
            _lc[3] = tipPoint - tipDir * pull;

            // mitt (13-19): the levels, with a Catmull-Rom ring between each pair so the curl reads smooth
            int m0 = CuffRings + PalmRings;
            for (int i = 0; i < 4; i++)
            {
                int r = m0 + 2 * i;
                _bc[r] = _lc[i]; _bx[r] = _lx[i]; _bw[r] = _lw[i]; _bh[r] = _lh[i]; _bu[r] = 0.88f; _bcol[r] = glove;
                if (i == 3) break;
                Vector3 prev = i == 0 ? _bc[m0 - 1] : _lc[i - 1];
                Vector3 next = i + 2 <= 3 ? _lc[i + 2] : _lc[3] + (_lc[3] - _lc[2]);
                _bc[r + 1] = 0.5625f * (_lc[i] + _lc[i + 1]) - 0.0625f * (prev + next);
                _bx[r + 1] = (_lx[i] + _lx[i + 1]).normalized;
                _bw[r + 1] = (_lw[i] + _lw[i + 1]) * 0.5f;
                _bh[r + 1] = (_lh[i] + _lh[i + 1]) * 0.5f;
                _bu[r + 1] = 0.88f;
                _bcol[r + 1] = glove;
            }

            // dome (20-22): an elliptical cap over the fingertips
            int last = m0 + MittRings - 1;
            for (int j = 0; j < DomeRings; j++)
            {
                int r = last + 1 + j;
                float beta = DomeDeg[j] * Mathf.Deg2Rad;
                float sc = Mathf.Pow(Mathf.Cos(beta), 0.85f);
                _bc[r] = _lc[3] + tipDir * dome * Mathf.Sin(beta);
                _bx[r] = _lx[3];
                _bw[r] = _bw[last] * sc;
                _bh[r] = _bh[last] * sc;
                _bu[r] = 1f;
                _bcol[r] = glove;
            }

            // tangents from neighbouring distinct rings (stripe/lip rings share a position)
            for (int r = 0; r < BodyRings; r++)
            {
                int p = r - 1, n = r + 1;
                while (p >= 0 && (_bc[p] - _bc[r]).sqrMagnitude < 1e-8f) p--;
                while (n < BodyRings && (_bc[n] - _bc[r]).sqrMagnitude < 1e-8f) n++;
                Vector3 from = p >= 0 ? _bc[p] : _bc[r];
                Vector3 to = n < BodyRings ? _bc[n] : _bc[r];
                _bt[r] = Dir(from, to, f);
            }

            // ring angles: the thumb opening is centred on the thumb side
            float thumbAngle = (m_Side == HandSide.Right ? ThumbSideDeg : 180f - ThumbSideDeg) * Mathf.Deg2Rad;
            float a0 = thumbAngle - Mathf.PI;
            for (int k = 0; k <= S; k++)
            {
                float ang = a0 + k * Mathf.PI * 2f / S;
                _cosK[k] = Mathf.Cos(ang);
                _sinK[k] = Mathf.Sin(ang);
            }

            float v = 0f;
            for (int r = 0; r < BodyRings; r++)
            {
                if (r > 0) v += Vector3.Distance(_bc[r - 1], _bc[r]);
                Vector3 t = _bt[r];
                Vector3 up = Vector3.Cross(t, _bx[r]) * sign;
                up -= t * Vector3.Dot(up, t);
                up = up.sqrMagnitude > 1e-8f ? up.normalized : Vector3.Cross(t, Vector3.right).normalized;
                Vector3 right = Vector3.Cross(up, t);
                for (int k = 0; k <= S; k++)
                {
                    float cs = _cosK[k], sn = _sinK[k];
                    float yh = sn > 0f ? _bh[r] * _bu[r] : _bh[r];   // "up" here points out of the palm: flatten that side
                    int i = BV(r, k);
                    _wp[i] = _bc[r] + right * (cs * _bw[r]) + up * (sn * yh);
                    _uvs[i] = new Vector2(k / (float)S * BodyStitches, v);
                    _colors[i] = _bcol[r];
                }
            }
            // the cuff's opening: recessed and dark, so it reads as the inside of the mitten, not a pointed cap
            Color inside = CuffColor * 0.28f; inside.a = 0f;
            _wp[BodyCapStart] = _bc[0] + _bt[0] * 0.018f;
            _normals[BodyCapStart] = -_bt[0];
            _uvs[BodyCapStart] = new Vector2(BodyStitches * 0.5f, 0f);
            _colors[BodyCapStart] = inside;
            _wp[BodyCapEnd] = _lc[3] + tipDir * dome;
            _normals[BodyCapEnd] = tipDir;
            _uvs[BodyCapEnd] = new Vector2(BodyStitches * 0.5f, v + dome);
            _colors[BodyCapEnd] = glove;

            // body normals from the surface itself (handles tapering domes and the cuff lip)
            for (int r = 0; r < BodyRings; r++)
            {
                int rp = Mathf.Min(r + 1, BodyRings - 1), rm = Mathf.Max(r - 1, 0);
                for (int k = 0; k < S; k++)
                {
                    int kp = (k + 1) % S, km = (k + S - 1) % S;
                    Vector3 n = Vector3.Cross(_wp[BV(r, kp)] - _wp[BV(r, km)], _wp[BV(rp, k)] - _wp[BV(rm, k)]);
                    if (n.sqrMagnitude < 1e-14f) n = _wp[BV(r, k)] - _bc[r];
                    _normals[BV(r, k)] = n.normalized;
                }
                _normals[BV(r, S)] = _normals[BV(r, 0)];
            }

            BuildThumb(back, f, glove);

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

        /// The thumb: ring 0 is the opening's edge loop (same positions and normals as the body),
        /// later rings follow the thumb joints and end in a dome.
        void BuildThumb(Vector3 back, Vector3 f, Color glove)
        {
            Vector3 p0 = Vector3.zero, n0 = Vector3.zero;
            for (int m = 0; m < Loop; m++)
            {
                int bi = BV(_loopR[m], _loopK[m]);
                p0 += _wp[bi];
                n0 += _normals[bi];
            }
            p0 /= Loop;
            n0 = n0.sqrMagnitude > 1e-10f ? n0.normalized : back;

            float rT = ThumbRadius * GloveBulk;
            Vector3 j1 = _joints[1], j2 = _joints[2], j3 = _joints[3];
            Vector3 d1 = Dir(j1, j2, f), d2 = Dir(j2, j3, d1);
            // push the first rings outward so the thumb leaves the palm instead of running inside it
            _tc[0] = p0;
            _tc[1] = Vector3.Lerp(p0, j1, 0.5f) + n0 * rT * 0.9f;
            _tc[2] = j1 + n0 * rT * 0.35f;
            _tc[3] = Vector3.Lerp(j1, j2, 0.5f);
            _tc[4] = j2;
            _tc[5] = j3 - d2 * rT * 0.9f;
            for (int r = 1; r <= 5; r++) _tr[r] = rT * ThumbRadiusMul[r];
            float dome = rT * 1.05f;
            for (int j = 0; j < ThumbDomeDeg.Length; j++)
            {
                float beta = ThumbDomeDeg[j] * Mathf.Deg2Rad;
                _tc[6 + j] = _tc[5] + d2 * dome * Mathf.Sin(beta);
                _tr[6 + j] = _tr[5] * Mathf.Pow(Mathf.Cos(beta), 0.85f);
            }
            for (int r = 0; r < ThumbRings; r++)
            {
                Vector3 from = r > 0 ? _tc[r - 1] : _tc[r] - n0 * 0.01f;
                Vector3 to = r + 1 < ThumbRings ? _tc[r + 1] : _tc[r] + d2 * 0.01f;
                _tt[r] = Dir(from, to, d2);
            }

            // angle of each loop vertex around the opening's normal, so every ring lines up with the opening
            Vector3 up1 = back - n0 * Vector3.Dot(back, n0);
            up1 = up1.sqrMagnitude > 1e-8f ? up1.normalized : Vector3.Cross(n0, Vector3.right).normalized;
            Vector3 right1 = Vector3.Cross(up1, n0);
            float prevPhi = 0f;
            for (int m = 0; m < Loop; m++)
            {
                Vector3 w = _wp[BV(_loopR[m], _loopK[m])] - p0;
                float phi = Mathf.Atan2(Vector3.Dot(w, up1), Vector3.Dot(w, right1));
                if (m > 0)
                {
                    while (phi <= prevPhi) phi += Mathf.PI * 2f;
                    while (phi - prevPhi > Mathf.PI * 2f) phi -= Mathf.PI * 2f;
                }
                _phi[m] = phi;
                prevPhi = phi;
            }
            _phi[Loop] = _phi[0] + Mathf.PI * 2f;

            float v = 0f;
            for (int r = 0; r < ThumbRings; r++)
            {
                if (r > 0) v += Vector3.Distance(_tc[r - 1], _tc[r]);
                FrameAt(r, back, out var right, out var up);
                for (int m = 0; m <= Loop; m++)
                {
                    int i = TV(r, m);
                    if (r == 0)
                    {
                        int bi = BV(_loopR[m % Loop], _loopK[m % Loop]);
                        _wp[i] = _wp[bi];
                        _normals[i] = _normals[bi];
                    }
                    else
                    {
                        float cs = Mathf.Cos(_phi[m]), sn = Mathf.Sin(_phi[m]);
                        _wp[i] = _tc[r] + right * (cs * _tr[r]) + up * (sn * _tr[r] * 0.9f);
                    }
                    _uvs[i] = new Vector2((_phi[m] - _phi[0]) / (Mathf.PI * 2f) * ThumbStitches, v);
                    _colors[i] = glove;
                }
            }
            _wp[ThumbCap] = _tc[5] + d2 * dome;
            _normals[ThumbCap] = d2;
            _uvs[ThumbCap] = new Vector2(ThumbStitches * 0.5f, v + dome * 0.5f);
            _colors[ThumbCap] = glove;

            for (int r = 1; r < ThumbRings; r++)
            {
                int rp = Mathf.Min(r + 1, ThumbRings - 1), rm = r - 1;
                for (int m = 0; m < Loop; m++)
                {
                    int mp = (m + 1) % Loop, mm = (m + Loop - 1) % Loop;
                    Vector3 n = Vector3.Cross(_wp[TV(r, mp)] - _wp[TV(r, mm)], _wp[TV(rp, m)] - _wp[TV(rm, m)]);
                    if (n.sqrMagnitude < 1e-14f) n = _wp[TV(r, m)] - _tc[r];
                    _normals[TV(r, m)] = n.normalized;
                }
                _normals[TV(r, Loop)] = _normals[TV(r, 0)];
            }
        }

        void FrameAt(int r, Vector3 back, out Vector3 right, out Vector3 up)
        {
            Vector3 t = _tt[r];
            up = back - t * Vector3.Dot(back, t);
            up = up.sqrMagnitude > 1e-8f ? up.normalized : Vector3.Cross(t, Vector3.right).normalized;
            right = Vector3.Cross(up, t);
        }

        static Vector3 Dir(Vector3 a, Vector3 b, Vector3 fallback)
        {
            var d = b - a;
            return d.sqrMagnitude > 1e-10f ? d.normalized : fallback;
        }
    }
}
