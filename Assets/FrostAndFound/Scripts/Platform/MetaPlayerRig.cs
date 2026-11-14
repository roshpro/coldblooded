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
        Coroutine[] m_Haptics = new Coroutine[2];

        public override Transform Head => cameraRig ? cameraRig.centerEyeAnchor : transform;
        public override Camera ViewCamera => m_Camera;
        public override bool IsXR => true;
        public override bool MenuDown => m_MenuDown;

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

        void LateUpdate() => SampleTrackers();

        void UpdateHand(HandRig h, OVRInput.Controller ctrl, Transform controllerAnchor, Transform handAnchor, OVRHand ovrHand, OVRSkeleton skel)
        {
            bool handTracked = ovrHand && ovrHand.IsTracked && ovrHand.IsDataValid;
            bool controllerTracked = OVRInput.IsControllerConnected(ctrl) && (OVRInput.GetControllerPositionTracked(ctrl) || OVRInput.GetControllerOrientationTracked(ctrl));
            h.IsTracked = handTracked || controllerTracked;

            if (controllerTracked && !handTracked)
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
            }
            else
            {
                h.UpdateTrigger(0f);
                h.PrimaryDown = false;
            }
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

        protected override void OnDisable()
        {
            base.OnDisable();
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
        }
    }
}
#endif
