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
        public static readonly Color GloveColor = new Color(0.74f, 0.08f, 0.1f);
        public static readonly Color CuffColor = new Color(0.6f, 0.05f, 0.07f);

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
            // a tracked camera hand pinches the camera: place it first, so that glove can pin its fingertips to it
            var cam = PolaroidCamera.Instance;
            if (cam) cam.PlaceTracked();
            SetPin(m_L, rig.Left, rig, cam);
            SetPin(m_R, rig.Right, rig, cam);
            // the desktop stand-in's hands aren't controllers: there the camera, photo and snowball keep their own
            // screen placements and the gloves just curl into the grip
            // with hand tracking on Quest the glove is Meta's own tracked hand mesh (MetaPlayerRig): this one only
            // keeps the hold points and stays hidden
            m_L.Hidden = rig.Left.HasNativeMesh;   // hand tracking, or the headset's hands holding the controllers
            m_R.Hidden = rig.Right.HasNativeMesh;
            m_L.Pose(rig.Left, GripFor(rig, rig.Left), rig.IsXR);
            m_R.Pose(rig.Right, GripFor(rig, rig.Right), rig.IsXR);
        }

        static void SetPin(WoolGlove glove, HandRig hand, PlayerRig rig, PolaroidCamera cam)
        {
            glove.PinWeight = cam && rig.CameraHand == hand ? cam.PinWeight : 0f;
            if (glove.PinWeight <= 0f) return;
            glove.PinTop = cam.TopContact;
            glove.PinBottom = cam.BottomContact;
            glove.PinUp = cam.ContactUp;
        }

        static MittenGrip GripFor(PlayerRig rig, HandRig hand)
        {
            if (SnowballHand.Instance && SnowballHand.Instance.IsHoldingIn(hand)) return MittenGrip.Ball;
            // tracked hands keep their real fingers round the camera (tips pinned to its top and bottom); controllers
            // and the desktop stand-in have none, so their gloves take the camera grip
            var cam = PolaroidCamera.Instance;
            if (cam && cam.IsRaised && rig.CameraHand == hand && !hand.IsOpticalHand) return MittenGrip.Camera;
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
        const float FingerRadius = 0.0116f, ThumbRadius = 0.0132f, PalmThickness = 0.029f, Bulk = 1.22f, CuffLength = 0.058f;
        const float KnuckleSpread = 1.12f;   // fingers sit a touch further apart than bare ones (the wool is thick)
        static readonly float[] FingerRadiusMul = { 1f, 1f, 1.05f, 0.98f, 0.86f };
        static Color GloveColor => Mittens.GloveColor;
        static Color CuffColor => Mittens.CuffColor;
        static readonly Color StripeColor = new Color(0.96f, 0.93f, 0.86f);

        // controller pose (right hand; left is mirrored)
        static readonly Vector3 ControllerOffsetPos = new Vector3(0.03f, -0.015f, -0.095f);
        static readonly Vector3 ControllerOffsetEuler = new Vector3(30f, 0f, -90f);
        const float RestCurl = 0.15f, MinHoldCurl = 0.35f;
        // held things, in the hand's own frame (right hand: wrist at the origin, fingers +Z, palm facing -Y, thumb -X;
        // the left hand mirrors x). A snowball cups in the palm.
        static readonly Vector3 BallCenter = new Vector3(0.004f, -0.054f, 0.058f);
        // the camera grip, in camera space for the LEFT hand (x mirrors for the right), for the compact Polaroid
        // (body x ±0.0525, y -0.0225..0.049 with the sloped top, z -0.045..0.03): the glove stands at the camera's
        // left end, palm against it; the index curls over the top onto the shutter, the middle lies beside it, the
        // ring and little fingers hook round the front-left corner, and the thumb rests on the back's top-left corner,
        // clear of the screen. Used for controllers (no real fingers); tracked hands keep their own fingers.
        static readonly Vector3 CamPalm = new Vector3(-0.0545f, -0.022f, -0.001f);
        static readonly Vector3 CamWrist = new Vector3(-0.070f, -0.068f, 0.002f);
        static readonly Vector3[] CamJoints =
        {
            new Vector3(-0.058f, -0.042f, -0.024f), new Vector3(-0.061f, -0.012f, -0.051f), new Vector3(-0.050f, 0.012f, -0.056f), new Vector3(-0.042f, 0.032f, -0.056f),   // thumb, on the back's top corner
            new Vector3(-0.068f, 0.022f, -0.028f), new Vector3(-0.063f, 0.060f, -0.026f), new Vector3(-0.045f, 0.068f, -0.024f), new Vector3(-0.028f, 0.063f, -0.022f),     // index, tip on the shutter
            new Vector3(-0.069f, 0.024f, -0.004f), new Vector3(-0.065f, 0.064f, -0.002f), new Vector3(-0.046f, 0.069f, 0.000f), new Vector3(-0.030f, 0.062f, 0.002f),       // middle, over the top
            new Vector3(-0.068f, 0.021f, 0.018f), new Vector3(-0.063f, 0.052f, 0.022f), new Vector3(-0.047f, 0.050f, 0.030f), new Vector3(-0.036f, 0.040f, 0.040f),         // ring, round the front corner
            new Vector3(-0.066f, 0.016f, 0.036f), new Vector3(-0.060f, 0.040f, 0.046f), new Vector3(-0.048f, 0.030f, 0.046f), new Vector3(-0.040f, 0.018f, 0.042f),         // little
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
        // One closed surface, welded so the shading runs straight across every join:
        //  • Palm: PR rings of S sides (+1 UV-seam vertex): 0-6 ribbed cuff (stripe, lip back to the wrist), 7-12 palm.
        //    The last ring sits just behind the knuckles and its outline is shared out between the four fingers.
        //  • Fingers (index → little): ring 0 IS the finger's slice of that knuckle outline, closed by "web" edges
        //    through the hand that it shares with its neighbours, so every finger grows straight out of the palm and
        //    neighbours meet in a crotch. The next rings round off into the finger, which ends in a domed tip.
        //  • Thumb: grows out of an opening on the palm's lower index side; its ring 0 is that opening's edge loop.
        const int NA = 4, NW = 3;                  // segments along a finger's top/bottom arc, and down a web
        const int F = 2 * NA + 2 * NW;             // segments round a finger
        const int OuterArc = 2 * NA + NW;          // the index/little finger's outer arc (top, side, bottom)
        const int S = 2 * OuterArc + 4 * NA;       // segments round the palm
        // the knuckle outline runs little side → over the back → index side → under the palm → back to the little side
        const int KLittle = 0, KRingTop = OuterArc, KMidTop = KRingTop + NA, KIndex = KMidTop + NA, KMidBot = KIndex + OuterArc, KRingBot = KMidBot + NA;
        static readonly int[] WebTop = { KIndex, KMidTop, KRingTop }, WebBot = { KMidBot, KRingBot, 0 };   // index|middle, middle|ring, ring|little
        const int CuffRings = 7, PalmRings = 6, PR = CuffRings + PalmRings, KR = PR - 1;
        const int FR = 12, FDome = 3, FRings = FR + FDome;
        const int PatchR0 = CuffRings, PatchR1 = CuffRings + 3;
        const int PatchK0 = KIndex + 3, PatchK1 = KIndex + 10;   // round the index side, turned a little toward the palm
        const int Loop = 2 * ((PatchR1 - PatchR0) + (PatchK1 - PatchK0));
        const int ThumbRings = 8;
        const int PalmCap = PR * (S + 1);
        const int PalmVerts = PalmCap + 1;
        const int FingerVerts = FRings * (F + 1) + 1;
        const int ThumbStart = PalmVerts + 4 * FingerVerts;
        const int ThumbCap = ThumbStart + ThumbRings * (Loop + 1);
        const int VertexCount = ThumbCap + 1;
        const float PalmStitches = 22f, FingerStitches = 7f, ThumbStitches = 8f;
        const float PalmSquare = 2.6f, FingerSquare = 2.35f;   // superellipse exponents: above 2 gives a firmer, slightly squared edge
        const float Tau = Mathf.PI * 2f;

        static int PV(int r, int k) => r * (S + 1) + k;
        static int FV(int f, int r, int m) => PalmVerts + f * FingerVerts + r * (F + 1) + m;
        static int FCap(int f) => PalmVerts + f * FingerVerts + FRings * (F + 1);
        static int TV(int r, int m) => ThumbStart + r * (Loop + 1) + m;

        static readonly float[] CuffAt = { -1f, -0.62f, -0.62f, -0.44f, -0.44f, -0.1f, -0.1f };
        static readonly float[] PalmAt = { 0.05f, 0.24f, 0.43f, 0.62f, 0.8f, 0.96f };
        static readonly float[] FingerDomeDeg = { 38f, 64f, 82f };
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
        readonly Vector3[] _nacc = new Vector3[VertexCount];
        readonly Vector2[] _uvs = new Vector2[VertexCount];
        readonly Color[] _colors = new Color[VertexCount];
        readonly int[] _weld = new int[VertexCount];      // the vertex each one coincides with (itself if none)
        readonly int[] _tris;
        readonly int[] _partStart = new int[7];           // triangle ranges: palm, index, middle, ring, little, thumb
        bool m_Oriented, m_RestBaked;
        readonly Vector3[] _rest = new Vector3[VertexCount];   // each vertex on the open, resting glove (UV1)
        readonly Vector3[] _restN = new Vector3[VertexCount];  // and its normal there (UV2)
        readonly float[] _ang = new float[S + 1], _ax = new float[S + 1], _ay = new float[S + 1];
        readonly float[] _kx = new float[4];
        readonly Vector3[] _pc = new Vector3[PR];
        readonly Vector3[] _fcen = new Vector3[4 * FRings];
        readonly Vector3[] _fc = new Vector3[FRings], _ft = new Vector3[FRings], _fx = new Vector3[FRings], _fy = new Vector3[FRings];
        readonly float[] _fr = new float[FRings];
        readonly float[] _x0 = new float[F + 1], _y0 = new float[F + 1], _fphi = new float[F + 1];
        readonly Vector3[] _ctrl = new Vector3[7];
        readonly int[] _loopR = new int[Loop], _loopK = new int[Loop];
        readonly float[] _phi = new float[Loop + 1];
        readonly Vector3[] _tc = new Vector3[ThumbRings], _tt = new Vector3[ThumbRings];
        readonly float[] _tr = new float[ThumbRings];

        public WoolGlove(Transform parent, HandSide side, Material mat)
        {
            m_Side = side;
            var go = new GameObject(side + " Glove", typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = Layers.PlayerHands;
            m_Root = go.transform;
            m_Root.SetParent(parent, false);
            HoldPoint = new GameObject(side + " Glove Hold").transform;
            HoldPoint.SetParent(parent, false);

            // the thumb opening's edge loop
            int li = 0;
            for (int k = PatchK0; k < PatchK1; k++) { _loopR[li] = PatchR0; _loopK[li++] = k; }
            for (int r = PatchR0; r < PatchR1; r++) { _loopR[li] = r; _loopK[li++] = PatchK1; }
            for (int k = PatchK1; k > PatchK0; k--) { _loopR[li] = PatchR1; _loopK[li++] = k; }
            for (int r = PatchR1; r > PatchR0; r--) { _loopR[li] = r; _loopK[li++] = PatchK0; }
            BuildWelds();

            var idx = new List<int>(6 * (PR * S + 4 * FRings * F + ThumbRings * Loop) + 3 * (S + 4 * F + Loop));
            void Quad(int a0, int b0, int c0, int d0) { idx.Add(a0); idx.Add(b0); idx.Add(c0); idx.Add(b0); idx.Add(d0); idx.Add(c0); }
            for (int r = 0; r + 1 < PR; r++)
                for (int k = 0; k < S; k++)
                {
                    if (r >= PatchR0 && r < PatchR1 && k >= PatchK0 && k < PatchK1) continue;   // thumb opening
                    Quad(PV(r, k), PV(r, k + 1), PV(r + 1, k), PV(r + 1, k + 1));
                }
            for (int k = 0; k < S; k++) { idx.Add(PalmCap); idx.Add(PV(0, k + 1)); idx.Add(PV(0, k)); }
            for (int f = 0; f < 4; f++)
            {
                _partStart[1 + f] = idx.Count;
                for (int r = 0; r + 1 < FRings; r++)
                    for (int m = 0; m < F; m++) Quad(FV(f, r, m), FV(f, r, m + 1), FV(f, r + 1, m), FV(f, r + 1, m + 1));
                for (int m = 0; m < F; m++) { idx.Add(FCap(f)); idx.Add(FV(f, FRings - 1, m)); idx.Add(FV(f, FRings - 1, m + 1)); }
            }
            _partStart[5] = idx.Count;
            for (int r = 0; r + 1 < ThumbRings; r++)
                for (int m = 0; m < Loop; m++) Quad(TV(r, m), TV(r, m + 1), TV(r + 1, m), TV(r + 1, m + 1));
            for (int m = 0; m < Loop; m++) { idx.Add(ThumbCap); idx.Add(TV(ThumbRings - 1, m)); idx.Add(TV(ThumbRings - 1, m + 1)); }
            _partStart[6] = idx.Count;
            _tris = idx.ToArray();

            m_Mesh = new Mesh { name = "WoolGlove" };
            m_Mesh.MarkDynamic();
            m_Mesh.vertices = _verts;
            m_Mesh.SetTriangles(_tris, 0);
            go.GetComponent<MeshFilter>().sharedMesh = m_Mesh;
            var mr = m_Renderer = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = false;
            go.SetActive(false);
        }

        /// <summary>Which vertices are the same point: UV seams, each finger's ring 0 on the knuckle outline and the
        /// webs between neighbours, and the thumb's ring 0 on its opening.</summary>
        void BuildWelds()
        {
            for (int i = 0; i < VertexCount; i++) _weld[i] = i;
            for (int r = 0; r < PR; r++) _weld[PV(r, S)] = PV(r, 0);
            int K(int k) => PV(KR, k % S);
            // a finger's ring 0 runs: up its own web (the one toward the little finger), along the top, down the
            // neighbour's web, back along the bottom; the index and little fingers wrap the hand's sides instead
            _weld[FV(0, 0, 0)] = K(KMidBot);
            for (int i = 0; i <= OuterArc; i++) _weld[FV(0, 0, NW + i)] = K(KIndex + i);
            _weld[FV(1, 0, 0)] = K(KRingBot);
            for (int i = 0; i <= NA; i++) _weld[FV(1, 0, NW + i)] = K(KMidTop + i);
            for (int i = 1; i < NW; i++) _weld[FV(1, 0, NW + NA + i)] = FV(0, 0, NW - i);
            for (int i = 0; i <= NA; i++) _weld[FV(1, 0, 2 * NW + NA + i)] = K(KMidBot + i);
            _weld[FV(2, 0, 0)] = K(S);
            for (int i = 0; i <= NA; i++) _weld[FV(2, 0, NW + i)] = K(KRingTop + i);
            for (int i = 1; i < NW; i++) _weld[FV(2, 0, NW + NA + i)] = FV(1, 0, NW - i);
            for (int i = 0; i <= NA; i++) _weld[FV(2, 0, 2 * NW + NA + i)] = K(KRingBot + i);
            for (int i = 0; i <= OuterArc; i++) _weld[FV(3, 0, i)] = K(KLittle + i);
            for (int i = 1; i < NW; i++) _weld[FV(3, 0, OuterArc + i)] = FV(2, 0, NW - i);
            for (int f = 0; f < 4; f++)
                for (int r = 0; r < FRings; r++) _weld[FV(f, r, F)] = FV(f, r, 0);
            for (int m = 0; m < Loop; m++) _weld[TV(0, m)] = PV(_loopR[m], _loopK[m]);
            for (int r = 0; r < ThumbRings; r++) _weld[TV(r, Loop)] = TV(r, 0);
            for (int i = 0; i < VertexCount; i++)
            {
                int w = i;
                while (_weld[w] != w) w = _weld[w];
                _weld[i] = w;
            }
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
                    case MittenGrip.Free:
                        visible = hand.HasJoints ? FromJoints(hand) : FromController(hand);
                        if (visible && hand.HasJoints && PinWeight > 0f && !Hidden) PinTips(hand);
                        break;
                    case MittenGrip.Ball: Grip(0.5f, 0.56f, 0.55f); break;      // fingers wrapped round the ball
                    case MittenGrip.Camera:
                    {
                        CameraPose(hand, publishMounts, out var pc, out var qc);
                        // pinching presses the index tip down onto the shutter
                        float press = Mathf.Clamp01((hand.Trigger - 0.2f) / 0.6f);
                        for (int i = 0; i < 20; i++)
                        {
                            var j = CamJoints[i];
                            if (i >= 4 && i < 8 && (i & 3) >= 2) j.y -= press * 0.006f * ((i & 3) - 1) * 0.5f;
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
            if (m_Renderer.enabled == Hidden) m_Renderer.enabled = !Hidden;
            if (!visible || !hand.HasJoints) m_Tracking = false;   // a lost hand starts fresh (no easing from a stale pose)
            hand.Hold = visible && publishMounts ? HoldPoint : null;
            hand.HasCameraGrip = visible && publishMounts && grip == MittenGrip.Camera && !Hidden;
            // the native hand mesh shows your real fingers, so a photo sits in your real pinch instead of the glove's
            hand.HasPhotoGrip = visible && publishMounts && grip == MittenGrip.Photo && !Hidden;
            if (!visible) return;
            if (!Hidden) Apply();
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

        /// <summary>Another mesh shows this hand (Meta's tracked hand mesh): keep the pose and hold points, don't draw.</summary>
        public bool Hidden;
        MeshRenderer m_Renderer;

        // the camera pinched between thumb and index (set by Mittens each frame)
        public float PinWeight;
        public Vector3 PinTop, PinBottom, PinUp;
        static readonly float[] PinShare = { 0f, 0.35f, 0.7f, 1f };   // how much of the correction each joint takes

        /// <summary>
        /// Tracking is loose around a held object, so the glove's index tip is pinned onto the camera's top (on the
        /// shutter, pressing it as you pinch) and its thumb tip onto the bottom face; each finger bends to reach,
        /// the correction growing toward the tip so the knuckles stay where your hand is.
        /// </summary>
        void PinTips(HandRig hand)
        {
            float press = Mathf.Clamp01((hand.Trigger - 0.5f) / 0.4f);
            float rIndex = FingerRadius * Bulk * FingerRadiusMul[1] * m_TScale, rThumb = ThumbRadius * Bulk * m_TScale;
            PinChain(1, PinTop + PinUp * (rIndex * 0.8f - press * 0.004f));
            PinChain(0, PinBottom - PinUp * (rThumb * 0.8f));
        }

        void PinChain(int f, Vector3 target)
        {
            var delta = Vector3.ClampMagnitude(target - _joints[f * 4 + 3], 0.05f) * PinWeight;
            for (int s = 1; s < 4; s++) _joints[f * 4 + s] += delta * PinShare[s];
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
            if (!m_RestBaked) BakeRest();
            Shape(out var f, out var up, out var palmBase);
            if (!m_Oriented) Orient(palmBase, f);
            GatherNormals();
            var toLocal = m_Root.worldToLocalMatrix;
            for (int i = 0; i < VertexCount; i++)
            {
                var n = _nacc[_weld[i]];
                _verts[i] = toLocal.MultiplyPoint3x4(_wp[i]);
                _normals[i] = toLocal.MultiplyVector(n.sqrMagnitude > 1e-24f ? n : up).normalized;
            }
            m_Mesh.SetVertices(_verts);
            m_Mesh.SetNormals(_normals);
            m_Mesh.SetColors(_colors);
            m_Mesh.RecalculateBounds();
        }

        /// <summary>
        /// Once: shape the glove on an open, resting hand (wrist at the origin, fingers +Z, palm -Y) and keep every
        /// vertex's position there as UV1. The wool shader lays its stitches out in that space at a fixed size in
        /// metres, so the knit rides on the surface: curling, spreading or a fat knuckle slice never stretch it.
        /// </summary>
        void BakeRest()
        {
            var joints = (Vector3[])_joints.Clone();
            var wrist = _wrist;
            var curl = (float[])_curl.Clone();
            var spread = (float[])_spread.Clone();
            var frameWrist = m_FrameWrist;
            var frameRot = m_FrameRot;

            m_FrameWrist = Vector3.zero;
            m_FrameRot = Quaternion.identity;
            for (int f = 0; f < 5; f++) { _curl[f] = 0.06f; _spread[f] = RestSpread[f]; }
            PoseHand(0.1f, 1f);
            Shape(out _, out _, out _);
            GatherNormals();
            for (int i = 0; i < VertexCount; i++)
            {
                // un-mirrored, so both hands share one pattern (the shader only uses the normal's direction per axis)
                _rest[i] = new Vector3(_wp[i].x * m_Mirror, _wp[i].y, _wp[i].z);
                var n = _nacc[_weld[i]];
                _restN[i] = n.sqrMagnitude > 1e-24f ? n.normalized : Vector3.up;
            }
            m_Mesh.SetUVs(1, _rest);
            m_Mesh.SetUVs(2, _restN);

            System.Array.Copy(joints, _joints, joints.Length);
            _wrist = wrist;
            System.Array.Copy(curl, _curl, curl.Length);
            System.Array.Copy(spread, _spread, spread.Length);
            m_FrameWrist = frameWrist;
            m_FrameRot = frameRot;
            m_RestBaked = true;
        }

        /// <summary>Area-weighted normals summed over every face, per welded vertex (so they're smooth across joins).</summary>
        void GatherNormals()
        {
            System.Array.Clear(_nacc, 0, VertexCount);
            for (int t = 0; t < _tris.Length; t += 3)
            {
                int a = _weld[_tris[t]], b = _weld[_tris[t + 1]], c = _weld[_tris[t + 2]];
                var n = Vector3.Cross(_wp[b] - _wp[a], _wp[c] - _wp[a]);
                _nacc[a] += n; _nacc[b] += n; _nacc[c] += n;
            }
        }

        /// <summary>Every vertex position for this frame's joints (welded copies included).</summary>
        void Shape(out Vector3 f, out Vector3 up, out Vector3 palmBase)
        {
            float sign = m_Side == HandSide.Right ? 1f : -1f;
            Vector3 fwd = _joints[8] - _wrist;                       // wrist → middle knuckle
            f = fwd.sqrMagnitude > 1e-8f ? fwd.normalized : Vector3.forward;
            Vector3 xA = _joints[16] - _joints[4];                   // index knuckle → little knuckle
            xA -= f * Vector3.Dot(xA, f);
            xA = xA.sqrMagnitude > 1e-10f ? xA.normalized : Vector3.Cross(Vector3.up, f).normalized;
            up = Vector3.Cross(f, xA) * sign;                        // out of the back of the hand

            Color glove = GloveColor; glove.a = 1f;      // stocking stitch
            Color cuff = CuffColor; cuff.a = 0f;         // ribbing
            Color stripe = StripeColor; stripe.a = 0f;

            float W = 0.5f * (Vector3.Distance(_joints[4], _joints[16]) + FingerRadius * 2.2f * Bulk);   // half-width at the knuckles
            float h = PalmThickness * Bulk;
            Vector3 knuckleMid = (_joints[4] + _joints[16]) * 0.5f;
            float palmLen = Mathf.Max(0.02f, Vector3.Dot(knuckleMid - _wrist, f));
            palmBase = _wrist;
            Vector3 cK = palmBase + f * (palmLen * PalmAt[PalmRings - 1]);

            // ---- the knuckle outline, shared out between the fingers: boundaries halfway between neighbouring knuckles
            for (int i = 0; i < 4; i++) _kx[i] = Vector3.Dot(_joints[4 + 4 * i] - cK, xA);
            float gap = W * 0.16f, lim = W * 0.84f;
            float b1 = Mathf.Clamp((_kx[0] + _kx[1]) * 0.5f, -lim, lim - 2f * gap);
            float b2 = Mathf.Clamp((_kx[1] + _kx[2]) * 0.5f, b1 + gap, lim - gap);
            float b3 = Mathf.Clamp((_kx[2] + _kx[3]) * 0.5f, b2 + gap, lim);
            float aT1 = TopAngle(b1 / W), aT2 = TopAngle(b2 / W), aT3 = TopAngle(b3 / W);
            Arc(KLittle, OuterArc, -aT3, aT3);
            Arc(KRingTop, NA, aT3, aT2);
            Arc(KMidTop, NA, aT2, aT1);
            Arc(KIndex, OuterArc, aT1, Tau - aT1);
            Arc(KMidBot, NA, Tau - aT1, Tau - aT2);
            Arc(KRingBot, NA, Tau - aT2, Tau - aT3);
            for (int k = 0; k <= S; k++)
            {
                _ax[k] = SgnPow(Mathf.Cos(_ang[k]), 2f / PalmSquare);
                _ay[k] = SgnPow(Mathf.Sin(_ang[k]), 2f / PalmSquare);
            }

            // ---- cuff (0-6: ribbed, flared, a stripe; ring 6 steps back in to the wrist) and palm (7-12)
            float wristHalfW = W * 0.74f, wristHalfH = h * 0.5f * 0.9f;
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
                else
                {
                    float t = PalmAt[r - CuffRings];
                    c = palmBase + f * (palmLen * t);
                    w = W * Mathf.Lerp(0.78f, 1f, Mathf.SmoothStep(0f, 1f, t / PalmAt[PalmRings - 1]));
                    hb = h * 0.5f; hp = h * 0.5f * 0.85f;   // a little flatter on the palm side
                    col = glove;
                }
                if (r > 0) v += Vector3.Distance(c, _pc[r - 1]);
                _pc[r] = c;
                for (int k = 0; k <= S; k++)
                {
                    int i = PV(r, k);
                    _wp[i] = c + xA * (w * _ax[k]) + up * (_ay[k] * (_ay[k] > 0f ? hb : hp));
                    _uvs[i] = new Vector2(k / (float)S * PalmStitches, v);
                    _colors[i] = col;
                }
            }
            {
                // the cuff's opening: recessed and dark, so it reads as the inside of the glove
                Color inside = CuffColor * 0.3f; inside.a = 0f;
                _wp[PalmCap] = _pc[0] + f * 0.018f;
                _uvs[PalmCap] = new Vector2(PalmStitches * 0.5f, 0f);
                _colors[PalmCap] = inside;
            }

            // ---- the webs between neighbouring fingers: through the hand, bowed forward into a soft crotch
            float crotch = FingerRadius * Bulk * 0.45f;
            for (int j = 0; j < 3; j++)
            {
                Vector3 top = _wp[PV(KR, WebTop[j])], bot = _wp[PV(KR, WebBot[j])];
                for (int i = 1; i < NW; i++)
                {
                    float t = i / (float)NW;
                    _wp[FV(j, 0, i)] = Vector3.Lerp(bot, top, t) + f * (crotch * Mathf.Sin(Mathf.PI * t));
                }
            }
            for (int fi = 0; fi < 4; fi++)
                for (int m = 0; m <= F; m++) { int i = FV(fi, 0, m); _wp[i] = _wp[_weld[i]]; }

            // ---- fingers
            for (int fi = 0; fi < 4; fi++) BuildFinger(fi, f, xA, up, glove);
            BuildThumb(up, f, palmBase, glove);

            // welded copies take their twin's position exactly
            for (int i = 0; i < VertexCount; i++) { int w = _weld[i]; if (w != i) _wp[i] = _wp[w]; }
        }

        /// <summary>One finger: ring 0 is its slice of the knuckle outline; the rings after it follow a spline through
        /// the finger's joints, rounding from that slice into a plump, slightly squared tube, and end in a dome.</summary>
        void BuildFinger(int fi, Vector3 f, Vector3 xA, Vector3 up, Color glove)
        {
            int jf = 4 + 4 * fi;
            float r0 = FingerRadius * Bulk * FingerRadiusMul[fi + 1];
            Vector3 k0 = _joints[jf], j1 = _joints[jf + 1], j2 = _joints[jf + 2], j3 = _joints[jf + 3];
            Vector3 tipDir = Dir(j2, j3, f);

            // ring 0's shape round its own centre, in the palm's cross-section
            Vector3 c0 = Vector3.zero;
            for (int m = 0; m < F; m++) c0 += _wp[FV(fi, 0, m)];
            c0 /= F;
            for (int m = 0; m <= F; m++)
            {
                var o = _wp[FV(fi, 0, m)] - c0;
                _x0[m] = Vector3.Dot(o, xA);
                _y0[m] = Vector3.Dot(o, up);
                float phi = Mathf.Atan2(_y0[m], _x0[m]);
                _fphi[m] = m == 0 ? phi : m == F ? _fphi[0] + Tau : _fphi[m - 1] + Mathf.Repeat(phi - _fphi[m - 1], Tau);
            }

            // the centre line: out of the knuckle slice, through the joints, to just short of the tip
            float rTip = r0 * 0.86f;
            _ctrl[1] = c0;
            _ctrl[2] = Vector3.Lerp(k0, j1, 0.35f);
            _ctrl[3] = j1;
            _ctrl[4] = j2;
            _ctrl[5] = j3 - tipDir * rTip * 0.55f;
            _ctrl[0] = c0 - f * Vector3.Distance(c0, _ctrl[2]);
            _ctrl[6] = _ctrl[5] + (_ctrl[5] - j2);
            for (int r = 0; r < FR; r++)
            {
                float u = r / (float)(FR - 1) * 4f;
                int seg = Mathf.Min(3, (int)u);
                _fc[r] = CatmullRom(_ctrl[seg], _ctrl[seg + 1], _ctrl[seg + 2], _ctrl[seg + 3], u - seg);
                _fr[r] = r0 * Mathf.Lerp(1.1f, 0.88f, Mathf.SmoothStep(0f, 1f, u / 4f));   // stays full near the knuckle
            }
            _ft[0] = f;
            for (int r = 1; r < FR; r++) _ft[r] = Dir(_fc[r - 1], _fc[Mathf.Min(FR - 1, r + 1)], tipDir);
            // ring frames carried along the finger from the palm's own axes: no twist however far it curls
            _fx[0] = xA; _fy[0] = up;
            for (int r = 1; r < FR; r++)
            {
                Vector3 t = _ft[r], x = _fx[r - 1], y = _fy[r - 1];
                x -= t * Vector3.Dot(x, t);
                x = x.sqrMagnitude > 1e-10f ? x.normalized : _fx[r - 1];
                y -= t * Vector3.Dot(y, t);
                y -= x * Vector3.Dot(y, x);
                y = y.sqrMagnitude > 1e-10f ? y.normalized : _fy[r - 1];
                _fx[r] = x; _fy[r] = y;
            }
            float domeLen = _fr[FR - 1] * 1.05f;
            for (int d = 0; d < FDome; d++)
            {
                float beta = FingerDomeDeg[d] * Mathf.Deg2Rad;
                int r = FR + d;
                _ft[r] = _ft[FR - 1]; _fx[r] = _fx[FR - 1]; _fy[r] = _fy[FR - 1];
                _fc[r] = _fc[FR - 1] + _ft[r] * (domeLen * Mathf.Sin(beta));
                _fr[r] = _fr[FR - 1] * Mathf.Pow(Mathf.Cos(beta), 0.85f);
            }

            float fv = 0f, e = 2f / FingerSquare;
            for (int r = 0; r < FRings; r++)
            {
                if (r > 0) fv += Vector3.Distance(_fc[r - 1], _fc[r]);
                _fcen[fi * FRings + r] = _fc[r];
                float blend = r >= 5 ? 1f : Mathf.SmoothStep(0f, 1f, r / 5f);   // knuckle slice → round finger, gradually
                for (int m = 0; m <= F; m++)
                {
                    int i = FV(fi, r, m);
                    if (r > 0)
                    {
                        float cs = Mathf.Cos(_fphi[m]), sn = Mathf.Sin(_fphi[m]);
                        float cx = _fr[r] * SgnPow(cs, e), cy = _fr[r] * SgnPow(sn, e) * (sn > 0f ? 1f : 0.92f);
                        _wp[i] = _fc[r] + _fx[r] * Mathf.Lerp(_x0[m], cx, blend) + _fy[r] * Mathf.Lerp(_y0[m], cy, blend);
                    }
                    _uvs[i] = new Vector2(m / (float)F * FingerStitches, fv);
                    _colors[i] = glove;
                }
            }
            int cap = FCap(fi);
            _wp[cap] = _fc[FR - 1] + _ft[FR - 1] * domeLen;
            _uvs[cap] = new Vector2(FingerStitches * 0.5f, fv + domeLen * 0.3f);
            _colors[cap] = glove;
        }

        /// The thumb: ring 0 is the palm opening's edge loop, later rings follow the thumb joints and end in a dome.
        void BuildThumb(Vector3 back, Vector3 f, Vector3 palmBase, Color glove)
        {
            Vector3 p0 = Vector3.zero;
            for (int m = 0; m < Loop; m++) p0 += _wp[PV(_loopR[m], _loopK[m])];
            p0 /= Loop;
            Vector3 n0 = p0 - (palmBase + f * Vector3.Dot(p0 - palmBase, f));   // out of the palm's side
            n0 = n0.sqrMagnitude > 1e-10f ? n0.normalized : back;

            float rT = ThumbRadius * Bulk;
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

            // angle of each loop vertex round the opening, so every ring lines up with it
            Vector3 up1 = back - n0 * Vector3.Dot(back, n0);
            up1 = up1.sqrMagnitude > 1e-8f ? up1.normalized : Vector3.Cross(n0, Vector3.right).normalized;
            Vector3 right1 = Vector3.Cross(up1, n0);
            for (int m = 0; m < Loop; m++)
            {
                Vector3 w = _wp[PV(_loopR[m], _loopK[m])] - p0;
                float phi = Mathf.Atan2(Vector3.Dot(w, up1), Vector3.Dot(w, right1));
                _phi[m] = m == 0 ? phi : _phi[m - 1] + Mathf.Repeat(phi - _phi[m - 1], Tau);
            }
            _phi[Loop] = _phi[0] + Tau;

            float v = 0f;
            for (int r = 0; r < ThumbRings; r++)
            {
                if (r > 0) v += Vector3.Distance(_tc[r - 1], _tc[r]);
                FrameAt(r, back, out var right, out var up);
                for (int m = 0; m <= Loop; m++)
                {
                    int i = TV(r, m);
                    if (r == 0) _wp[i] = _wp[_weld[i]];
                    else
                    {
                        float cs = Mathf.Cos(_phi[m]), sn = Mathf.Sin(_phi[m]);
                        _wp[i] = _tc[r] + right * (cs * _tr[r]) + up * (sn * _tr[r] * 0.9f);
                    }
                    _uvs[i] = new Vector2((_phi[m] - _phi[0]) / Tau * ThumbStitches, v);
                    _colors[i] = glove;
                }
            }
            _wp[ThumbCap] = _tc[5] + d2 * dome;
            _uvs[ThumbCap] = new Vector2(ThumbStitches * 0.5f, v + dome * 0.5f);
            _colors[ThumbCap] = glove;
        }

        void FrameAt(int r, Vector3 back, out Vector3 right, out Vector3 up)
        {
            Vector3 t = _tt[r];
            up = back - t * Vector3.Dot(back, t);
            up = up.sqrMagnitude > 1e-8f ? up.normalized : Vector3.Cross(t, Vector3.right).normalized;
            right = Vector3.Cross(up, t);
        }

        /// <summary>Once, on the first pose: wind each part's triangles so they face outward (mirroring the left hand
        /// flips the winding, and the knuckle-slice rings run their own way round).</summary>
        void Orient(Vector3 palmBase, Vector3 f)
        {
            bool changed = false;
            for (int p = 0; p < 6; p++)
            {
                float s = 0f;
                for (int t = _partStart[p]; t < _partStart[p + 1]; t += 3)
                {
                    Vector3 a = _wp[_tris[t]], b = _wp[_tris[t + 1]], c = _wp[_tris[t + 2]];
                    Vector3 cen = (a + b + c) / 3f;
                    s += Vector3.Dot(Vector3.Cross(b - a, c - a), cen - Inside(p, _tris[t], cen, palmBase, f));
                }
                if (s >= 0f) continue;
                for (int t = _partStart[p]; t < _partStart[p + 1]; t += 3) { int x = _tris[t + 1]; _tris[t + 1] = _tris[t + 2]; _tris[t + 2] = x; }
                changed = true;
            }
            if (changed) m_Mesh.SetTriangles(_tris, 0);
            m_Oriented = true;
        }

        /// <summary>A point inside the part near a triangle: the palm's axis, or the centre of the finger/thumb ring.</summary>
        Vector3 Inside(int part, int vi, Vector3 cen, Vector3 palmBase, Vector3 f)
        {
            if (part == 0) return palmBase + f * Vector3.Dot(cen - palmBase, f);
            if (part <= 4)
            {
                int fi = part - 1;
                return _fcen[fi * FRings + Mathf.Min((vi - FV(fi, 0, 0)) / (F + 1), FRings - 1)];
            }
            return _tc[Mathf.Min((vi - ThumbStart) / (Loop + 1), ThumbRings - 1)];
        }

        void Arc(int k0, int n, float from, float to)
        {
            for (int i = 0; i <= n; i++) _ang[k0 + i] = Mathf.Lerp(from, to, i / (float)n);
        }

        static float SgnPow(float x, float e) => (x < 0f ? -1f : 1f) * Mathf.Pow(Mathf.Abs(x), e);

        /// <summary>The angle round the palm's superellipse outline where its back surface sits at across-position u (-1..1).</summary>
        static float TopAngle(float u) => Mathf.Acos(Mathf.Clamp(SgnPow(Mathf.Clamp(u, -1f, 1f), PalmSquare * 0.5f), -1f, 1f));

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
