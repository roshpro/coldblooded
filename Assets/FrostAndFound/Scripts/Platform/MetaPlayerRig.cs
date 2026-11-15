#if FROST_META
using System.Collections;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Quest player built on the Meta XR SDK: OVRCameraRig for head and hands, OVRInput for Touch controllers,
    /// OVRHand / OVRSkeleton for hand tracking (pinch = shutter, fist = scoop), passthrough via OVRPassthroughLayer.
    /// Controllers and hands can be mixed freely; each side picks whichever is currently tracked.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class MetaPlayerRig : PlayerRig
    {
        public OVRCameraRig cameraRig;
        public OVRHand leftHand, rightHand;
        public OVRSkeleton leftSkeleton, rightSkeleton;

        Camera m_Camera;
        bool m_MenuDown;
        NativeGlove m_LeftGlove, m_RightGlove;
        Material m_GloveMat;
        Coroutine[] m_Haptics = new Coroutine[2];

        public override Transform Head => cameraRig ? cameraRig.centerEyeAnchor : transform;
        public override Camera ViewCamera => m_Camera;
        public override bool IsXR => true;
        public override bool MenuDown => m_MenuDown;
        public override bool MenuHeld => OVRInput.Get(OVRInput.Button.Start);

        void Awake()
        {
            if (!cameraRig) cameraRig = GetComponent<OVRCameraRig>();
            SetupHand(Left, "Frost Left Hand");
            SetupHand(Right, "Frost Right Hand");
            Left.HapticImpl = (a, s) => Buzz(0, a, s);
            Right.HapticImpl = (a, s) => Buzz(1, a, s);
        }

        void Start()
        {
            EnsureCamera();
            // tracked hands wear Meta's own hand mesh as the glove, so every joint shows exactly as tracked
            m_GloveMat = new Material(FrostAssets.Shader(FrostAssets.ShaderId.Wool)) { name = "Wool (tracked hand)" };
            m_GloveMat.SetFloat("_Inflate", 0.0035f);
            m_LeftGlove = NativeGlove.Create(leftHand, leftSkeleton, m_GloveMat);
            m_RightGlove = NativeGlove.Create(rightHand, rightSkeleton, m_GloveMat);
            // holding controllers: the headset still poses your hands (wrapped round the controllers) so the same
            // knitted hand shows, and keeps the hand data valid while a controller is in it
            if (OVRManager.instance) OVRManager.instance.controllerDrivenHandPosesType = OVRManager.ControllerDrivenHandPosesType.ConformingToController;
            if (leftHand) leftHand.m_showState = OVRInput.InputDeviceShowState.Always;
            if (rightHand) rightHand.m_showState = OVRInput.InputDeviceShowState.Always;
            // passthrough shows your real room, so the guardian lines aren't needed on top of it
            if (OVRManager.instance) OVRManager.instance.shouldBoundaryVisibilityBeSuppressed = true;
        }

        /// <summary>OVRCameraRig creates its anchors in its own Awake, so look the eye camera up lazily.</summary>
        void EnsureCamera()
        {
            if (m_Camera || !cameraRig || !cameraRig.centerEyeAnchor) return;
            m_Camera = cameraRig.centerEyeAnchor.GetComponent<Camera>();
            if (!m_Camera) return;
            // Passthrough needs a transparent clear; the virtual Polaroid never sees PhotoOnly backdrops.
            m_Camera.clearFlags = CameraClearFlags.SolidColor;
            m_Camera.backgroundColor = new Color(0, 0, 0, 0);
            m_Camera.cullingMask &= ~Layers.Mask(Layers.PhotoOnly);
            m_Camera.nearClipPlane = 0.03f;
            var data = m_Camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (data) { data.renderPostProcessing = false; data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None; }
            FloorY = cameraRig.trackingSpace ? cameraRig.trackingSpace.position.y : 0f;
        }

        void SetupHand(HandRig h, string name)
        {
            h.Anchor = new GameObject(name).transform;
            h.Anchor.SetParent(transform, false);
            h.Palm = new GameObject("Palm").transform;
            h.Palm.SetParent(h.Anchor, false);
            h.Tip = new GameObject("Tip").transform;
            h.Tip.SetParent(h.Anchor, false);
        }

        void Update()
        {
            if (!cameraRig) return;
            EnsureCamera();
            m_MenuDown = OVRInput.GetDown(OVRInput.Button.Start);
            UpdateHand(Left, OVRInput.Controller.LTouch, cameraRig.leftControllerAnchor, cameraRig.leftHandAnchor, leftHand, leftSkeleton);
            UpdateHand(Right, OVRInput.Controller.RTouch, cameraRig.rightControllerAnchor, cameraRig.rightHandAnchor, rightHand, rightSkeleton);
        }

        void LateUpdate()
        {
            SampleJoints(Left, leftSkeleton);
            SampleJoints(Right, rightSkeleton);
            SampleTrackers();
            Left.HasNativeMesh = m_LeftGlove != null && Left.HasJoints && m_LeftGlove.Showing();
            Right.HasNativeMesh = m_RightGlove != null && Right.HasJoints && m_RightGlove.Showing();
        }

        /// <summary>
        /// The glove on a tracked hand: Meta's own hand mesh (OVRMesh + OVRMeshRenderer), skinned to the tracked
        /// skeleton so every finger joint shows exactly as tracked, dressed in the wool shader. The mesh is puffed out
        /// a few mm (_Inflate) so it reads as a glove; it gets vertex colours (red, with a ribbed cuff at the wrist)
        /// and its own rest pose as UV1/UV2, which the knit is laid out on so it never stretches.
        /// </summary>
        sealed class NativeGlove
        {
            OVRHand m_Hand;
            OVRMesh m_Mesh;
            SkinnedMeshRenderer m_Smr;
            Mesh m_Dressed;

            public static NativeGlove Create(OVRHand hand, OVRSkeleton skel, Material mat)
            {
                if (!hand || !skel || skel.gameObject != hand.gameObject)
                {
                    Debug.LogWarning("[Frost] Tracked-hand glove needs OVRHand and OVRSkeleton on one object; using the procedural glove");
                    return null;
                }
                var go = hand.gameObject;
                var g = new NativeGlove { m_Hand = hand };
                // the renderer first, with our material, so OVRMeshRenderer keeps it as its own
                g.m_Smr = go.GetComponent<SkinnedMeshRenderer>();
                if (!g.m_Smr) g.m_Smr = go.AddComponent<SkinnedMeshRenderer>();
                g.m_Smr.sharedMaterial = mat;
                g.m_Smr.enabled = false;
                g.m_Smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                g.m_Smr.receiveShadows = false;
                g.m_Mesh = go.GetComponent<OVRMesh>();
                if (!g.m_Mesh) g.m_Mesh = go.AddComponent<OVRMesh>();
                if (!go.GetComponent<OVRMeshRenderer>()) go.AddComponent<OVRMeshRenderer>();
                go.layer = Layers.PlayerHands;   // the Polaroid never photographs your hands
                return g;
            }

            /// <summary>Drawing this frame (the mesh has loaded and is bound, and the hand is confidently tracked).</summary>
            public bool Showing()
            {
                if (!m_Smr || !m_Mesh || !m_Mesh.IsInitialized || !m_Hand) return false;
                var mesh = m_Smr.sharedMesh;
                if (!mesh || mesh.bindposes == null || mesh.bindposes.Length == 0) return false;
                if (mesh != m_Dressed) { Dress(mesh); m_Dressed = mesh; }
                return m_Smr.enabled && m_Hand.IsTracked && m_Hand.IsDataValid;
            }

            static void Dress(Mesh mesh)
            {
                var v = mesh.vertices;
                var n = mesh.normals;
                int count = v.Length;
                var b = mesh.bounds;
                var size = b.size;
                // the hand's own axes in its bind pose: the longest extent runs along the fingers, the thinnest
                // through the palm; the fingers are at the end where most of the skeleton's bones are
                int fa = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
                int ua = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
                if (ua == fa) ua = (fa + 1) % 3;
                var fwd = Vector3.zero; fwd[fa] = 1f;
                var up = Vector3.zero; up[ua] = 1f;
                var bones = Vector3.zero;
                var bp = mesh.bindposes;
                for (int i = 0; i < bp.Length; i++) bones += (Vector3)bp[i].inverse.GetColumn(3);
                bones /= bp.Length;
                if (Vector3.Dot(bones - b.center, fwd) < 0f) fwd = -fwd;
                var toRest = Quaternion.Inverse(Quaternion.LookRotation(fwd, up));
                float wrist = float.MaxValue;
                for (int i = 0; i < count; i++) wrist = Mathf.Min(wrist, Vector3.Dot(v[i] - b.center, fwd));

                var rest = new Vector3[count];
                var restN = new Vector3[count];
                var col = new Color[count];
                Color glove = Mittens.GloveColor; glove.a = 1f;   // stocking stitch
                Color cuff = Mittens.CuffColor; cuff.a = 0f;      // ribbing
                for (int i = 0; i < count; i++)
                {
                    var p = v[i] - b.center;
                    rest[i] = toRest * p;
                    restN[i] = n != null && n.Length == count ? toRest * n[i] : Vector3.up;
                    col[i] = Vector3.Dot(p, fwd) - wrist < 0.022f ? cuff : glove;
                }
                mesh.SetUVs(1, rest);
                mesh.SetUVs(2, restN);
                mesh.colors = col;
            }
        }

        void UpdateHand(HandRig h, OVRInput.Controller ctrl, Transform controllerAnchor, Transform handAnchor, OVRHand ovrHand, OVRSkeleton skel)
        {
            bool handTracked = ovrHand && ovrHand.IsTracked && ovrHand.IsDataValid;
            bool controllerTracked = OVRInput.IsControllerConnected(ctrl) && (OVRInput.GetControllerPositionTracked(ctrl) || OVRInput.GetControllerOrientationTracked(ctrl));
            h.IsTracked = handTracked || controllerTracked;

            // a controller in this hand drives it (the hand pose then comes from the controller too)
            bool inHand = controllerTracked && (!handTracked || OVRInput.GetControllerIsInHandState(h.Side == HandSide.Left ? OVRInput.Hand.HandLeft : OVRInput.Hand.HandRight) == OVRInput.ControllerInHandState.ControllerInHand);
            if (inHand)
            {
                h.IsOpticalHand = false;
                var src = controllerAnchor ? controllerAnchor : handAnchor;
                h.Anchor.SetPositionAndRotation(src.position, src.rotation);
                h.Palm.localPosition = new Vector3(h.Side == HandSide.Left ? 0.01f : -0.01f, -0.025f, -0.035f);
                h.Palm.localRotation = Quaternion.identity;
                h.Tip.localPosition = new Vector3(0, -0.01f, 0.06f);
                h.Grip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, ctrl);
                h.UpdateTrigger(OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, ctrl));
                h.PrimaryDown = OVRInput.GetDown(OVRInput.Button.One, ctrl);
                // the camera comes up on the side grip; the index (top) trigger is the shutter
                h.PhotoPose = h.Grip;
            }
            else if (handTracked)
            {
                h.IsOpticalHand = true;
                h.Anchor.SetPositionAndRotation(handAnchor.position, handAnchor.rotation);
                Transform palm = Bone(skel, OVRSkeleton.BoneId.XRHand_Palm, OVRSkeleton.BoneId.Hand_WristRoot);
                Transform tip = Bone(skel, OVRSkeleton.BoneId.XRHand_IndexTip, OVRSkeleton.BoneId.Hand_IndexTip);
                if (palm)
                {
                    // the palm bone's "down" side holds things; push the hold point a touch into the cupped hand
                    h.Palm.SetPositionAndRotation(palm.position + palm.up * -0.015f, palm.rotation);
                }
                else { h.Palm.localPosition = new Vector3(0, -0.02f, 0.06f); h.Palm.localRotation = Quaternion.identity; }
                if (tip) h.Tip.position = tip.position; else h.Tip.localPosition = new Vector3(0, 0, 0.15f);
                h.Grip = FistCurl(skel);
                float pinch = ovrHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
                h.UpdateTrigger(pinch, 0.85f, 0.55f);
                h.PrimaryDown = false;
                h.PhotoPose = CameraPose(h);
            }
            else
            {
                h.UpdateTrigger(0f);
                h.PrimaryDown = false;
                h.PhotoPose = 0f;
                h.HasJoints = false;
            }
        }

        /// <summary>0..1, how much the hand is making the camera gesture: just how close thumb and index are (any angle),
        /// starting at <c>cameraAppearGap</c> and full by <c>cameraFullGap</c> (or once OVR reports a pinch).</summary>
        static float CameraPose(HandRig h)
        {
            if (!h.HasJoints) return 0f;
            var tune = FrostTuning.I;
            return Mathf.InverseLerp(tune.cameraAppearGap, tune.cameraFullGap, h.PinchGap);
        }

        static readonly OVRSkeleton.BoneId[][] s_XrChains =
        {
            new[] { OVRSkeleton.BoneId.XRHand_ThumbMetacarpal, OVRSkeleton.BoneId.XRHand_ThumbProximal, OVRSkeleton.BoneId.XRHand_ThumbDistal, OVRSkeleton.BoneId.XRHand_ThumbTip },
            new[] { OVRSkeleton.BoneId.XRHand_IndexProximal, OVRSkeleton.BoneId.XRHand_IndexIntermediate, OVRSkeleton.BoneId.XRHand_IndexDistal, OVRSkeleton.BoneId.XRHand_IndexTip },
            new[] { OVRSkeleton.BoneId.XRHand_MiddleProximal, OVRSkeleton.BoneId.XRHand_MiddleIntermediate, OVRSkeleton.BoneId.XRHand_MiddleDistal, OVRSkeleton.BoneId.XRHand_MiddleTip },
            new[] { OVRSkeleton.BoneId.XRHand_RingProximal, OVRSkeleton.BoneId.XRHand_RingIntermediate, OVRSkeleton.BoneId.XRHand_RingDistal, OVRSkeleton.BoneId.XRHand_RingTip },
            new[] { OVRSkeleton.BoneId.XRHand_LittleProximal, OVRSkeleton.BoneId.XRHand_LittleIntermediate, OVRSkeleton.BoneId.XRHand_LittleDistal, OVRSkeleton.BoneId.XRHand_LittleTip },
        };
        static readonly OVRSkeleton.BoneId[][] s_OvrChains =
        {
            new[] { OVRSkeleton.BoneId.Hand_Thumb1, OVRSkeleton.BoneId.Hand_Thumb2, OVRSkeleton.BoneId.Hand_Thumb3, OVRSkeleton.BoneId.Hand_ThumbTip },
            new[] { OVRSkeleton.BoneId.Hand_Index1, OVRSkeleton.BoneId.Hand_Index2, OVRSkeleton.BoneId.Hand_Index3, OVRSkeleton.BoneId.Hand_IndexTip },
            new[] { OVRSkeleton.BoneId.Hand_Middle1, OVRSkeleton.BoneId.Hand_Middle2, OVRSkeleton.BoneId.Hand_Middle3, OVRSkeleton.BoneId.Hand_MiddleTip },
            new[] { OVRSkeleton.BoneId.Hand_Ring1, OVRSkeleton.BoneId.Hand_Ring2, OVRSkeleton.BoneId.Hand_Ring3, OVRSkeleton.BoneId.Hand_RingTip },
            new[] { OVRSkeleton.BoneId.Hand_Pinky1, OVRSkeleton.BoneId.Hand_Pinky2, OVRSkeleton.BoneId.Hand_Pinky3, OVRSkeleton.BoneId.Hand_PinkyTip },
        };

        /// <summary>Copies this frame's tracked joints into the HandRig for the knitted mittens (runs in LateUpdate,
        /// after OVRSkeleton has moved its bones).</summary>
        static void SampleJoints(HandRig h, OVRSkeleton skel)
        {
            h.HasJoints = false;
            if (!h.IsTracked || !skel || !skel.IsInitialized || !skel.IsDataValid || skel.Bones == null || skel.Bones.Count == 0) return;
            var t = skel.GetSkeletonType();
            bool xr = t == OVRSkeleton.SkeletonType.XRHandLeft || t == OVRSkeleton.SkeletonType.XRHandRight;
            var chains = xr ? s_XrChains : s_OvrChains;
            var wristId = xr ? OVRSkeleton.BoneId.XRHand_Wrist : OVRSkeleton.BoneId.Hand_WristRoot;
            bool wrist = false; int found = 0;
            foreach (var b in skel.Bones)
            {
                if (b == null || !b.Transform) continue;
                if (b.Id == wristId) { h.Wrist = b.Transform.position; wrist = true; continue; }
                for (int f = 0; f < 5; f++)
                    for (int j = 0; j < 4; j++)
                        if (chains[f][j] == b.Id) { h.Joints[f * 4 + j] = b.Transform.position; found++; }
            }
            h.HasJoints = wrist && found == 20;
        }

        static Transform Bone(OVRSkeleton skel, OVRSkeleton.BoneId a, OVRSkeleton.BoneId b)
        {
            if (!skel || !skel.IsInitialized || skel.Bones == null) return null;
            Transform fallback = null;
            foreach (var bone in skel.Bones)
            {
                if (bone == null || !bone.Transform) continue;
                if (bone.Id == a) return bone.Transform;
                if (bone.Id == b) fallback = bone.Transform;
            }
            return fallback;
        }

        /// <summary>0 = open hand, 1 = fist. Angle between each finger's proximal direction and its tip direction.</summary>
        static float FistCurl(OVRSkeleton skel)
        {
            if (!skel || !skel.IsInitialized || skel.Bones == null) return 0f;
            float sum = 0f; int n = 0;
            sum += FingerCurl(skel, OVRSkeleton.BoneId.XRHand_MiddleMetacarpal, OVRSkeleton.BoneId.XRHand_MiddleProximal, OVRSkeleton.BoneId.XRHand_MiddleTip, ref n);
            sum += FingerCurl(skel, OVRSkeleton.BoneId.XRHand_RingMetacarpal, OVRSkeleton.BoneId.XRHand_RingProximal, OVRSkeleton.BoneId.XRHand_RingTip, ref n);
            sum += FingerCurl(skel, OVRSkeleton.BoneId.XRHand_IndexMetacarpal, OVRSkeleton.BoneId.XRHand_IndexProximal, OVRSkeleton.BoneId.XRHand_IndexTip, ref n);
            if (n == 0)
            {
                sum += FingerCurl(skel, OVRSkeleton.BoneId.Hand_WristRoot, OVRSkeleton.BoneId.Hand_Middle1, OVRSkeleton.BoneId.Hand_MiddleTip, ref n);
                sum += FingerCurl(skel, OVRSkeleton.BoneId.Hand_WristRoot, OVRSkeleton.BoneId.Hand_Ring1, OVRSkeleton.BoneId.Hand_RingTip, ref n);
            }
            return n > 0 ? sum / n : 0f;
        }

        static float FingerCurl(OVRSkeleton skel, OVRSkeleton.BoneId baseId, OVRSkeleton.BoneId knuckleId, OVRSkeleton.BoneId tipId, ref int n)
        {
            Transform b = null, k = null, t = null;
            foreach (var bone in skel.Bones)
            {
                if (bone == null || !bone.Transform) continue;
                if (bone.Id == baseId) b = bone.Transform;
                else if (bone.Id == knuckleId) k = bone.Transform;
                else if (bone.Id == tipId) t = bone.Transform;
            }
            if (!b || !k || !t) return 0f;
            var palmDir = (k.position - b.position).normalized;
            var fingerDir = (t.position - k.position).normalized;
            float angle = Vector3.Angle(palmDir, fingerDir); // ~0 open, ~150+ fist
            n++;
            return Mathf.InverseLerp(35f, 140f, angle);
        }

        void Buzz(int side, float amplitude, float seconds)
        {
            if (m_Haptics[side] != null) StopCoroutine(m_Haptics[side]);
            m_Haptics[side] = StartCoroutine(BuzzRoutine(side == 0 ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch, amplitude, seconds));
        }

        IEnumerator BuzzRoutine(OVRInput.Controller c, float amplitude, float seconds)
        {
            OVRInput.SetControllerVibration(0.6f, Mathf.Clamp01(amplitude), c);
            yield return new WaitForSeconds(seconds);
            OVRInput.SetControllerVibration(0f, 0f, c);
        }

        void OnDestroy() { if (m_GloveMat) Destroy(m_GloveMat); }

        protected override void OnDisable()
        {
            base.OnDisable();
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
        }
    }
}
#endif
