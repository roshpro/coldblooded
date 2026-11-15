using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// The chunky retro instant camera, with a live viewfinder on its back. It fades into your camera hand (left by
    /// default) as your thumb and index line up (or the trigger starts to travel) and settles once they're
    /// parallel, pads facing (hand tracking); otherwise you just see gloves. Held like a phone: screen flush between
    /// thumb and index. A quick pinch (it needn't close all the way) shoots, from the pose just before the pinch.
    /// Pinch fully (pull the trigger) → flash, ka-CHUNK, and the print slides out of the slot and waits there, blank,
    /// until your other hand pulls it free; it develops in your hand over ~2 s.
    /// No film limit; a missed shot is just a photo of snow.
    /// </summary>
    [DefaultExecutionOrder(150)] // places itself after the mittens have taken their grip this frame
    public class PolaroidCamera : MonoBehaviour
    {
        public static PolaroidCamera Instance { get; private set; }
        public static event System.Action<PhotoData> PhotoTaken;
        /// <summary>Lets the story layer inspect / modify a photo before it prints (Tony's blur, GUILTY).</summary>
        public static System.Action<PhotoData, Camera> Evaluate;

        public Camera Lens { get; private set; }
        public Transform Slot { get; private set; }
        public int PhotoCount { get; private set; }
        public bool Enabled { get; set; } = true;
        /// <summary>True while the camera is up in your hand (the mittens wrap it).</summary>
        public bool IsRaised => m_Raise > 0.5f;
        /// <summary>A print is sticking out of the slot, waiting for your other hand.</summary>
        public bool PrintWaiting => m_Waiting != null;

        const float UpAtHand = 0.25f;       // camera-gesture progress that summons it (hand tracking: thumb–index gap ≈ 8 cm)
        const float UpAtController = 0.3f;  // the same for a controller's trigger travel
        const float GrowTime = 0.18f;       // how long it takes to grow in once summoned
        const float StayAbove = 0.02f;      // once there, it stays while the gesture is held at least this much
        const float LowerDelay = 0.6f;      // the camera lingers this long after you relax your fingers
        const float EjectSeconds = 0.9f;
        const float GrabReach = 0.085f;     // how close your free hand has to get to the print
        const float DevelopSeconds = 2f;
        /// <summary>The camera body's size (full size).</summary>
        public const float BodyScale = 1f;
        /// <summary>Where a tracked hand pinches the camera, in the body's space (right camera hand; x mirrors for the
        /// left): held like a phone you're taking a picture with, the index tip on the top edge and the thumb tip on
        /// the bottom edge of the back, at the hand's end, so the screen lies flush between your fingertips.</summary>
        static readonly Vector3 ContactTop = new Vector3(0.028f, 0.0355f, -0.034f), ContactBottom = new Vector3(0.028f, -0.032f, -0.034f);
        /// <summary>Thumb-to-index span across the camera at full size (top contact to bottom contact).</summary>
        const float ContactSpan = 0.0675f;
        /// <summary>The camera is sized to the gap between your thumb and index as it comes up (so it fits the
        /// pinch you're making), then keeps that size while it's in your hand.</summary>
        public float GripScale { get; private set; } = 1f;
        /// <summary>This frame, a tracked hand is pinching the camera: the glove pins its index tip to
        /// <see cref="TopContact"/> and its thumb tip to <see cref="BottomContact"/>, by <see cref="PinWeight"/>.</summary>
        public bool Pinched => m_PlacedFrame == Time.frameCount && m_Body && m_Body.gameObject.activeSelf;
        public Vector3 TopContact => m_Body.TransformPoint(Mirror(ContactTop));
        public Vector3 BottomContact => m_Body.TransformPoint(Mirror(ContactBottom));
        public Vector3 ContactUp => m_Body.up;
        public float PinWeight => Pinched ? Mathf.SmoothStep(0f, 1f, m_Raise) : 0f;

        RenderTexture m_Viewfinder;
        Transform m_Body;
        Renderer m_FlashBulb;
        Material m_FlashMat;
        GameObject m_Backdrop;
        float m_Cooldown;
        Material m_BlurMat;
        bool m_ForceRender;
        float m_Raise, m_LastPose = -10f, m_Pop = 1f, m_FreeOpenTime = -10f;
        bool m_Up, m_QueuedShot, m_Busy, m_Hidden, m_InReach;
        // the tracked-hand hold, smoothed
        Vector3 m_HoldPos; Quaternion m_HoldRot = Quaternion.identity;
        bool m_HoldValid;
        // the last ~0.5 s of holds and pinch gaps: a shot rewinds to the pose from just before the pinch started,
        // and holds it there while it shoots, so the pinch itself never jolts the framing
        const int HistoryN = 48;
        readonly float[] m_HistT = new float[HistoryN], m_HistGap = new float[HistoryN];
        readonly Vector3[] m_HistPos = new Vector3[HistoryN];
        readonly Quaternion[] m_HistRot = new Quaternion[HistoryN], m_HistHandRot = new Quaternion[HistoryN];
        readonly Vector3[] m_HistHandPos = new Vector3[HistoryN];
        int m_HistHead = -1, m_HistCount;
        bool m_Frozen, m_QuickArmed = true;
        float m_FreezeUntil, m_QuickOpenGap, m_UpSince;
        // while shooting, the camera's pose relative to your hand (wrist) is held: it moves with your hand, but the
        // fingers closing can't tip it
        Vector3 m_FrozenPos; Quaternion m_FrozenRot = Quaternion.identity;
        int m_PlacedFrame = -1;

        static Vector3 Mirror(Vector3 v) => FrostPrefs.CameraHand == HandSide.Left ? new Vector3(-v.x, v.y, v.z) : v;
        PolaroidPhoto m_Waiting;
        float m_SlotOut;

        public static PolaroidCamera Create()
        {
            var go = new GameObject("Polaroid Camera");
            go.layer = Layers.PlayerHands;
            var cam = go.AddComponent<PolaroidCamera>();
            cam.Build();
            return cam;
        }

        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        void Build()
        {
            var tune = FrostTuning.I;
            m_Body = new GameObject("Body").transform;
            m_Body.SetParent(transform, false);
            m_Body.gameObject.layer = Layers.PlayerHands;
            // held so the lens points forward (+Z) and the viewfinder faces you (-Z)
            var custom = FrostAssets.Instance ? FrostAssets.Instance.polaroidModel : null;
            if (custom) Instantiate(custom, m_Body, false).SetLayerRecursive(Layers.PlayerHands);
            else BuildMock();

            // the lens camera
            var lensGo = new GameObject("Lens Camera");
            lensGo.transform.SetParent(transform, false);
            float handSide = FrostPrefs.CameraHand == HandSide.Left ? -1f : 1f;
            lensGo.transform.localPosition = new Vector3(0.008f * handSide, 0.009f, 0.05f) * BodyScale;
            Lens = lensGo.AddComponent<Camera>();
            Lens.fieldOfView = tune.photoFov;
            Lens.nearClipPlane = 0.04f;
            Lens.farClipPlane = 40f;
            Lens.clearFlags = CameraClearFlags.SolidColor;
            Lens.backgroundColor = Palette.SkyHorizon;
            Lens.cullingMask = ~Layers.Mask(Layers.PlayerHands, Layers.UI3D);
            Lens.stereoTargetEye = StereoTargetEyeMask.None;
            Lens.depth = -10;
            Lens.allowHDR = false;
            Lens.allowMSAA = true;
            var data = lensGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = true;
            data.requiresDepthTexture = false;
            data.requiresColorTexture = false;
            int res = Mathf.Clamp(tune.photoResolution, 128, 1024);
            m_Viewfinder = new RenderTexture(res, res, 24, RenderTextureFormat.ARGB32) { name = "Viewfinder", antiAliasing = 2 };
            m_Viewfinder.Create();
            Lens.targetTexture = m_Viewfinder;

            // viewfinder screen on the back of the camera, set into its bezel, clear of your thumb
            var screen = ProcMesh.Part("Viewfinder", m_Body, ProcMesh.Quad(), Palette.UnlitTextured(m_Viewfinder, Color.white, false, "ViewfinderScreen"),
                new Vector3(-0.006f * handSide, 0f, -0.0358f), new Vector3(0.034f, 0.034f, 1f), new Vector3(0, 0, 0));
            screen.transform.localRotation = Quaternion.Euler(0, 180f, 0) * Quaternion.Euler(0, 180f, 0); // quad faces -Z = toward you
            screen.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            m_BlurMat = new Material(FrostAssets.Shader(FrostAssets.ShaderId.Blur)) { name = "PhotoBlur" };
            DepthOcclusion.Exempt(m_Body.gameObject);   // in your hand: the real hand holding it mustn't cut it out
        }

        void BuildMock()
        {
            var body = Palette.Toon(Palette.CameraBody, true, 0.2f);
            var dark = Palette.Toon(Palette.CameraDark, true, 0.3f);
            var t = m_Body;
            // A compact retro instant camera, 10.5 × 5.4 × 7 cm plus a low sloped top: small enough that the hand holds
            // it in a light pinch (index on the shutter, thumb on the back's top corner). Everything on its faces is
            // kept inside that face's flat area and sunk partly into it, so nothing hangs off a rounded edge.
            //   Shell flat areas: front/back x ±0.0378, y ±0.0194 (faces at z ±0.035); top hump flat x ±0.0364.
            // The holding hand's end (left for the left hand) gets the shutter and the lens leans that way;
            // the rainbow stripes and flash sit on the open side, where your fingers don't cover them.
            float side = FrostPrefs.CameraHand == HandSide.Left ? -1f : 1f;
            ProcMesh.Part("Shell", t, ProcMesh.RoundedBox(0.14f, 3), body, Vector3.zero, new Vector3(0.105f, 0.054f, 0.07f));
            ProcMesh.Part("Top", t, ProcMesh.RoundedBox(0.15f, 3), body, new Vector3(0, 0.031f, -0.008f), new Vector3(0.104f, 0.016f, 0.05f), new Vector3(-10f, 0, 0));
            ProcMesh.Part("Base", t, ProcMesh.RoundedBox(0.3f, 2), dark, new Vector3(0, -0.0285f, 0.001f), new Vector3(0.107f, 0.008f, 0.072f));
            float lensX = 0.008f * side;
            ProcMesh.Part("Lens Barrel", t, ProcMesh.Cylinder(18), dark, new Vector3(lensX, -0.001f, 0.039f), new Vector3(0.034f, 0.014f, 0.034f), new Vector3(90, 0, 0));
            ProcMesh.Part("Lens Ring", t, ProcMesh.Torus(0.42f, 0.08f, 20, 8), Palette.Toon(Palette.Coal, false, 0.4f), new Vector3(lensX, -0.001f, 0.046f), new Vector3(0.038f, 0.038f, 0.038f), new Vector3(90, 0, 0));
            ProcMesh.Part("Glass", t, ProcMesh.Disc(18), Palette.Toon(Palette.Lens, false, 0.9f, 0.15f), new Vector3(lensX, -0.001f, 0.0458f), new Vector3(0.026f, 0.002f, 0.026f), new Vector3(90, 0, 0));
            // flash window, set into the sloped top's front face
            m_FlashMat = new Material(FrostAssets.Shader(FrostAssets.ShaderId.Unlit)) { name = "FlashBulb" };
            m_FlashMat.SetColor("_BaseColor", Palette.SkyHorizon);
            var bulb = ProcMesh.Part("Flash", t, ProcMesh.RoundedBox(0.3f, 2), m_FlashMat, new Vector3(-0.016f * side, 0.0353f, 0.0166f), new Vector3(0.022f, 0.009f, 0.004f), new Vector3(-10f, 0, 0));
            m_FlashBulb = bulb.GetComponent<Renderer>();
            // the shutter, seated on the sloped top at the holding hand's end, right under the glove's index tip
            ProcMesh.Part("Shutter", t, ProcMesh.Cylinder(12), Palette.Toon(Palette.EvidenceRed, true, 0.4f), new Vector3(0.028f * side, 0.0396f, -0.017f), new Vector3(0.014f, 0.006f, 0.014f), new Vector3(-10f, 0, 0));
            // rainbow stripes: flush on the front, mostly sunk into it (only 0.6 mm proud)
            Color[] rainbow = { Palette.Rainbow1, Palette.Rainbow2, Palette.Rainbow3, Palette.Rainbow4, Palette.Rainbow5 };
            for (int i = 0; i < rainbow.Length; i++)
                ProcMesh.Part("Stripe", t, ProcMesh.RoundedBox(0.1f, 1), Palette.Toon(rainbow[i], false, 0.2f),
                    new Vector3(-side * (0.0151f + i * 0.0042f), 0f, 0.0341f), new Vector3(0.0042f, 0.032f, 0.003f));
            // the photo slot along the front-bottom edge, just above the base
            // the photo slot: a slit across the bottom, so prints hang down out of it facing you
            ProcMesh.Part("Slot", t, ProcMesh.RoundedBox(0.3f, 1), Palette.Toon(Palette.Coal), new Vector3(0, -0.0326f, 0.004f), new Vector3(0.094f, 0.002f, 0.005f));
            // viewfinder bezel on the back (the screen itself is added in Build)
            // a thin square frame (2.5 mm all round, ~2.3 mm corners) mostly sunk into the back: only 0.6 mm proud
            ProcMesh.Part("Bezel", t, ProcMesh.RoundedBox(0.06f, 2), dark, new Vector3(-0.006f * side, 0f, -0.0331f), new Vector3(0.039f, 0.039f, 0.005f));
            Slot = ProcMesh.Pivot("PhotoSlot", t, new Vector3(0, -0.0327f, 0.004f));
            m_Body.localPosition = new Vector3(0, 0.01f, -0.005f);
            MeshMerger.Merge(m_Body, null, 0f);
        }

        void Update()
        {
            var rig = PlayerRig.Active;
            if (!rig) return;
            var hand = rig.CameraHand;
            float dt = Time.deltaTime;
            if (m_Cooldown > 0f) m_Cooldown -= dt;

            // ---- raise: the camera fades in as you go into the camera gesture, and settles once you're there
            float target;
            bool gate = true;
            // sized to the pinch you're making when it's summoned; once it's up it keeps that size
            if (!m_Up) GripScale = rig.IsXR && hand.IsOpticalHand && hand.HasJoints ? Mathf.Clamp(hand.PinchGap / ContactSpan, 0.8f, 1.1f) : 1f;
            if (!rig.IsXR) { target = 1f; m_Up = true; }   // desktop stand-in: always in hand
            else
            {
                // other fingers relaxed (not a fist), the hand up off the snow, empty, and out in front of you
                // (a relaxed hand at your side has its thumb and index a few cm apart too)
                var snow = SnowSurface.Instance;
                bool low = snow && hand.Palm && hand.Palm.position.y - snow.SurfaceHeightAt(hand.Palm.position) < FrostTuning.I.scoopHeight + 0.12f;
                bool busy = SnowballHand.Instance && SnowballHand.Instance.IsHoldingIn(hand);
                // (controllers: holding the side grip is the gesture, so a fist and the view cone don't apply)
                bool optical = hand.IsOpticalHand;
                gate = Enabled && hand.IsTracked && (!optical || hand.Grip < 0.55f) && !busy && !low && (m_Up || !optical || InView(rig, hand));
                float pose = gate ? Mathf.Clamp01(hand.PhotoPose) : 0f;
                float upAt = hand.IsOpticalHand ? UpAtHand : UpAtController;
                if (!m_Up && pose >= upAt) Settle(hand);
                if (m_Up && pose > StayAbove) m_LastPose = Time.time;
                if (m_Up && Time.time - m_LastPose >= LowerDelay && !m_Busy && !m_QueuedShot) m_Up = false;
                // no half-way camera: below the threshold it's away, past it it grows all the way in and is usable
                target = m_Up ? 1f : 0f;
            }
            if (m_Busy) { target = 1f; m_Up = true; }    // mid-shot
            if (m_Hidden || !hand.IsTracked) { target = 0f; m_Up = false; m_QueuedShot = false; }
            // put away with a print still hanging out of it: the print falls to the snow
            if (!m_Up && m_Waiting && (!rig.IsXR || target < 0.9f)) DropWaiting();
            m_Raise = Mathf.MoveTowards(m_Raise, target, dt / (target > m_Raise ? GrowTime : 0.2f));
            // a little bump as it settles into your hand
            m_Pop = Mathf.Min(1f, m_Pop + dt / 0.2f);
            float scale = Mathf.SmoothStep(0f, 1f, m_Raise) * (1f + 0.07f * Mathf.Sin(m_Pop * Mathf.PI));
            if (m_Body)
            {
                bool show = scale > 0.01f;
                if (m_Body.gameObject.activeSelf != show) m_Body.gameObject.SetActive(show);
                m_Body.localScale = Vector3.one * Mathf.Max(scale * BodyScale * GripScale, 0.001f);
            }
            if (m_Raise <= 0.001f) { m_HoldValid = false; m_Frozen = false; m_HistCount = 0; }


            // the viewfinder renders at a third of the frame rate in XR (~24–30 fps) to save the Quest's draw calls;
            // every frame while shooting, and not at all while the camera is down
            if (Lens) Lens.enabled = m_Raise > 0.5f && (!rig.IsXR || m_ForceRender || (Time.frameCount % 3) == 0);
            m_ForceRender = false;

            // ---- shutter. Hand tracking: a quick pinch, not necessarily all the way (or a full pinch). Controllers: the
            // trigger. Pressed while the camera is still coming up, it fires once it's there.
            bool tracked = rig.IsXR && hand.IsOpticalHand && hand.HasJoints;
            // hand tracking: only a real pinch shoots (the system's own pinch signal fires on shaky, blurred hands)
            if (tracked) { if (QuickPinch(hand, out int before)) { ShootSteady(before); m_LastPose = Time.time; } }
            else if (Enabled && hand.TriggerDown && m_Cooldown <= 0f)
            {
                if (m_Raise >= 0.95f) { if (tracked) ShootSteady(WidestRecent(Time.time - FrostTuning.I.quickPinchWindow)); else TakePhoto(); }
                else if (gate) { if (!m_Up) Settle(hand); m_QueuedShot = true; m_LastPose = Time.time; }
            }
            if (m_QueuedShot && (!Enabled || !hand.IsTracked)) m_QueuedShot = false;
            if (m_QueuedShot && m_Raise >= 0.95f) { m_QueuedShot = false; TakePhoto(); }
        }

        /// <summary>
        /// A quick pinch: within the last <c>quickPinchWindow</c> seconds the gap has closed by a good fraction of how
        /// open it was. <paramref name="before"/> is the history entry from when it was widest (just before the pinch).
        /// One shot per pinch: open your fingers again to arm the next.
        /// </summary>
        bool QuickPinch(HandRig hand, out int before)
        {
            before = -1;
            float gap = hand.PinchGap;
            // a shaking or blurred hand: tracking jitters the fingertips by centimetres, so nothing counts until it's
            // steady again (and it has to stay steady a moment)
            bool steady = hand.HighConfidence && hand.Tracker.Instant.magnitude < MaxShakeSpeed;
            if (!steady) { m_SteadySince = -1f; return false; }
            if (m_SteadySince < 0f) m_SteadySince = Time.time;
            if (Time.time - m_SteadySince < SteadyFor) return false;
            if (!m_QuickArmed && gap > PinchOpen) m_QuickArmed = true;   // fingers properly apart again: the next pinch counts
            if (!Enabled || !m_Up || m_Raise < 0.95f || m_Busy || m_Cooldown > 0f || !m_QuickArmed) return false;
            if (gap > PinchClosed) return false;   // thumb and index actually have to meet
            // ...coming from apart (not just held shut), since the camera settled so reaching into the gesture doesn't count
            before = WidestRecent(Mathf.Max(Time.time - 0.6f, m_UpSince + 0.15f));
            if (before < 0 || m_HistGap[before] < PinchOpen) return false;
            m_QuickArmed = false;
            m_QuickOpenGap = m_HistGap[before];
            return true;
        }

        // a real pinch: thumb and index within 2.2 cm, from at least 4 cm apart; ignored while the hand's moving fast
        const float PinchClosed = 0.022f, PinchOpen = 0.04f, MaxShakeSpeed = 1.1f, SteadyFor = 0.12f;
        float m_SteadySince = -1f;

        /// <summary>The history entry with the widest pinch since <paramref name="since"/> (-1 if none).</summary>
        int WidestRecent(float since)
        {
            int best = -1; float widest = -1f;
            for (int k = 0; k < m_HistCount; k++)
            {
                int i = (m_HistHead - k + HistoryN) % HistoryN;
                if (m_HistT[i] < since) break;
                if (m_HistGap[i] > widest) { widest = m_HistGap[i]; best = i; }
            }
            return best;
        }

        /// <summary>Shoot with the camera at its pose relative to your hand from history entry <paramref name="at"/>
        /// (or as it is now), and keep it at that pose relative to your hand while it shoots.</summary>
        void ShootSteady(int at)
        {
            var a = PlayerRig.Active ? PlayerRig.Active.CameraHand.Anchor : null;
            if (m_Cooldown > 0f || m_Busy || !a) return;
            Vector3 hp = at >= 0 ? m_HistHandPos[at] : a.position, cp = at >= 0 ? m_HistPos[at] : transform.position;
            Quaternion hr = at >= 0 ? m_HistHandRot[at] : a.rotation, cr = at >= 0 ? m_HistRot[at] : transform.rotation;
            var inv = Quaternion.Inverse(hr);
            m_FrozenPos = inv * (cp - hp);
            m_FrozenRot = inv * cr;
            m_Frozen = true;
            m_FreezeUntil = Time.time + 0.45f;
            transform.SetPositionAndRotation(a.position + a.rotation * m_FrozenPos, a.rotation * m_FrozenRot);
            TakePhoto();
        }

        /// <summary>The camera has come all the way into your hand.</summary>
        void Settle(HandRig hand)
        {
            m_Up = true;
            m_UpSince = Time.time;
            m_LastPose = Time.time;
            m_Pop = 0f;
            hand.Haptic(0.15f, 0.025f);
        }

        /// <summary>Your camera hand is out in front of you, roughly where you're looking.</summary>
        static bool InView(PlayerRig rig, HandRig hand)
        {
            var head = rig.Head;
            var p = hand.HasJoints ? hand.PinchPoint : (hand.Palm ? hand.Palm.position : hand.Anchor.position);
            var to = p - head.position;
            return Vector3.Dot(to, head.forward) > 0.2f && Vector3.Angle(to, head.forward) < FrostTuning.I.cameraViewCone;
        }

        /// <summary>A tracked hand pinches the camera between thumb and index. Placed before the gloves pose (they
        /// pin their fingertips to its faces), so it's called by <see cref="Mittens"/>.</summary>
        public void PlaceTracked()
        {
            var rig = PlayerRig.Active;
            if (!rig || !rig.IsXR) return;
            var hand = rig.CameraHand;
            if (!hand.Anchor || !hand.IsOpticalHand || !hand.HasJoints) return;
            HoldPose(rig, hand, out var pos, out var rot);
            transform.SetPositionAndRotation(pos, rot);
            m_PlacedFrame = Time.frameCount;
        }

        void LateUpdate()
        {
            var rig = PlayerRig.Active;
            if (!rig) return;
            var hand = rig.CameraHand;
            if (!hand.Anchor) return;
            // controllers: sit in the glove's grip, hanging under the hand, index on the shutter
            if (m_PlacedFrame == Time.frameCount) { }   // a tracked hand already placed it this frame
            else if (hand.HasCameraGrip) transform.SetPositionAndRotation(hand.CameraGrip.position, hand.CameraGrip.rotation);
            else
            {
                HoldPose(rig, hand, out var pos, out var rot);
                transform.SetPositionAndRotation(pos, rot);
            }
            if (m_Waiting) PlaceInSlot(m_Waiting.transform, m_SlotOut);
        }

        /// <summary>Tracked hands: pinched between your thumb and index, pointing where your hand points.
        /// Also the fallback when there's no glove grip.</summary>
        void HoldPose(PlayerRig rig, HandRig hand, out Vector3 pos, out Quaternion rot)
        {
            if (!rig.IsXR)
            {
                pos = hand.Anchor.position;
                rot = hand.Anchor.rotation;
                return;
            }
            if (hand.HasJoints && hand.IsOpticalHand)
            {
                var j = hand.Joints;
                // held like a phone taking a picture: the screen lies flush between your thumb (bottom edge) and
                // index (top edge) and faces your eyes, so the lens looks out along the line from your eye through
                // the camera (a touch of where your hand points). Its up runs from thumb tip to index tip, eased
                // toward true up so a slightly rolled pinch doesn't tilt the shot much
                var axis = j[7] - j[3];
                var up = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.up;
                up = (up + Vector3.up * 0.6f).normalized;
                var tips = (j[3] + j[7]) * 0.5f;
                var fromEye = tips - rig.Head.position;
                if (fromEye.sqrMagnitude < 1e-6f) fromEye = rig.Head.forward;
                Vector3 fwd = (j[8] - hand.Wrist);
                if (fwd.sqrMagnitude < 1e-6f) fwd = hand.Anchor.forward;
                var aim = Vector3.ProjectOnPlane(Vector3.Slerp(fromEye.normalized, fwd.normalized, 0.15f), up);
                if (aim.sqrMagnitude < 1e-4f) aim = Vector3.ProjectOnPlane(rig.Head.forward, up);
                if (aim.sqrMagnitude < 1e-6f) aim = Vector3.forward;
                var wantRot = Quaternion.LookRotation(aim, up);
                float dt = Time.deltaTime;
                m_HoldRot = m_HoldValid ? Quaternion.Slerp(m_HoldRot, wantRot, 1f - Mathf.Exp(-dt / 0.06f)) : wantRot;
                // the middle of the two contacts sits on the middle of your fingertips (the body grows out from there)
                float s = m_Body ? m_Body.localScale.x : 1f;
                var mid = (Mirror(ContactTop) + Mirror(ContactBottom)) * 0.5f;
                var wantPos = tips - m_HoldRot * ((m_Body ? m_Body.localPosition : Vector3.zero) + mid * s);
                m_HoldPos = m_HoldValid ? Vector3.Lerp(m_HoldPos, wantPos, 1f - Mathf.Exp(-dt / 0.03f)) : wantPos;
                m_HoldValid = true;
                // remember it (with how open the pinch was) so a shot can rewind to just before the pinch
                m_HistHead = (m_HistHead + 1) % HistoryN;
                m_HistT[m_HistHead] = Time.time; m_HistGap[m_HistHead] = hand.PinchGap;
                m_HistPos[m_HistHead] = m_HoldPos; m_HistRot[m_HistHead] = m_HoldRot;
                m_HistHandPos[m_HistHead] = hand.Anchor.position; m_HistHandRot[m_HistHead] = hand.Anchor.rotation;
                if (m_HistCount < HistoryN) m_HistCount++;
                if (m_Frozen)
                {
                    if (Time.time < m_FreezeUntil || m_Busy)
                    {
                        // held at the shot's pose relative to your hand: it follows the hand, not the closing fingers
                        pos = hand.Anchor.position + hand.Anchor.rotation * m_FrozenPos;
                        rot = hand.Anchor.rotation * m_FrozenRot;
                        m_HoldPos = pos; m_HoldRot = rot;   // so it glides on from here once released
                        return;
                    }
                    m_Frozen = false;
                }
                pos = m_HoldPos;
                rot = m_HoldRot;
                return;
            }
            // controller: held up just above the controller (clear of the hand wrapped round it), aimed where it points
            pos = hand.Anchor.TransformPoint(new Vector3(0, 0.075f, 0.035f));
            rot = hand.Anchor.rotation * Quaternion.Euler(15f, 0, 0);
        }

        /// <summary>The print hanging out of the slot falls off into the snow (you can pick it up from there).</summary>
        void DropWaiting()
        {
            var p = m_Waiting;
            m_Waiting = null;
            if (!p) return;
            p.transform.localScale = Vector3.one * BodyScale;
            p.Drop(Vector3.down * 0.3f + (Slot ? -Slot.forward * 0.15f : Vector3.zero));
            PhotoHolder.Instance?.Dropped.Add(p);
            if (!SatieSound.Fire("photo_drop", p.transform.position)) AudioDirector.PlayAt(Sfx.Whirr, p.transform.position, 0.2f, 1.9f);
        }

        public void TakePhoto()
        {
            if (m_Cooldown > 0f || m_Busy) return;
            if (m_Waiting) DropWaiting();   // the last print gets pushed out by the next
            m_Cooldown = 0.55f;
            m_LastPose = Time.time;
            StartCoroutine(Shoot());
        }

        IEnumerator Shoot()
        {
            m_Busy = true;
            m_ForceRender = true;
            if (!SatieSound.Fire("shutter", transform.position)) AudioDirector.PlayAt(Sfx.Shutter, transform.position, 1f, Random.Range(0.97f, 1.03f));
            PlayerRig.Active?.CameraHand.Haptic(0.7f, 0.07f);
            StartCoroutine(FlashBulb());
            PassthroughMood.Flash();   // the flash lights up your real room for a blink
            // let the lens render this frame so the photo matches what the viewfinder shows
            yield return new WaitForEndOfFrame();

            PhotoCount++;
            var data = new PhotoData { Number = PhotoCount, TakenAt = Time.time };
            ClueDetector.Detect(Lens, data);
            SubjectDetector.Detect(Lens, data);
            Evaluate?.Invoke(data, Lens);

            var rt = new RenderTexture(m_Viewfinder.width, m_Viewfinder.height, 0, RenderTextureFormat.ARGB32) { name = "Photo " + data.Number };
            rt.Create();
            if (data.Blurred)
            {
                m_BlurMat.SetVector("_Direction", new Vector4(Random.Range(0.6f, 1f), Random.Range(-0.3f, 0.3f), 0, 0));
                m_BlurMat.SetFloat("_Amount", 0.09f);
                Graphics.Blit(m_Viewfinder, rt, m_BlurMat);
            }
            else Graphics.Blit(m_Viewfinder, rt);
            data.Image = rt;

            var photo = PolaroidPhoto.Create(data);
            PhotoHolder.Instance?.AddToAlbum(photo);
            PhotoTaken?.Invoke(data);
            yield return EjectAndWait(photo);
        }

        IEnumerator FlashBulb()
        {
            if (!m_FlashMat) yield break;
            Fx.GlintAt(m_FlashBulb ? m_FlashBulb.transform.position : transform.position, 0.12f, Palette.GlintWhite);
            m_FlashMat.SetColor("_BaseColor", Color.white * 1.6f);
            yield return Ease.Tween(0.25f, t => m_FlashMat.SetColor("_BaseColor", Color.Lerp(Color.white * 1.6f, Palette.SkyHorizon, t)), Ease.OutCubic, false);
        }

        /// <summary>The print slides down out of the bottom slot facing you and develops as it hangs there, until your
        /// free hand pulls it free (or the next shot or putting the camera away knocks it into the snow).</summary>
        IEnumerator EjectAndWait(PolaroidPhoto photo)
        {
            m_Waiting = photo;
            m_Busy = false;
            var p = photo.transform;
            if (!SatieSound.Fire("print_eject", transform.position)) AudioDirector.PlayAt(Sfx.Whirr, transform.position, 0.6f, 1f);
            bool fastDev = CaseDirector.I != null && CaseDirector.I.CaseClosed;
            StartCoroutine(photo.Develop(fastDev ? 0.6f : DevelopSeconds));
            for (float t = 0; t < 1f && m_Waiting == photo; t += Time.deltaTime / EjectSeconds)
            {
                m_SlotOut = Ease.OutCubic(t);
                PlaceInSlot(p, m_SlotOut);
                yield return null;
            }
            if (m_Waiting != photo) yield break;   // knocked out mid-eject
            m_SlotOut = 1f;
            float waitStart = Time.time;
            bool tipped = false;
            m_InReach = false;
            while (photo && m_Waiting == photo)
            {
                var rig = PlayerRig.Active;
                bool closing = CaseDirector.I != null && CaseDirector.I.CaseClosed;
                if (closing || (rig && !rig.IsXR && Time.time - waitStart > 0.7f)) break;   // finale / desktop: handed over
                if (rig && TryGrab(rig)) { Hints.Done("pull-photo"); break; }
                if (!tipped && Time.time - waitStart > 4f && rig && rig.IsXR)
                {
                    tipped = true;
                    if (Hints.Allow("pull-photo")) HToast.Show("Pull the photo out of the camera with your other hand", UIKit.Icon.Camera, 3.5f);
                }
                yield return null;
            }
            if (m_Waiting != photo) yield break;   // it fell off
            m_Waiting = null;
            if (photo) yield return TakeOut(photo);
        }

        /// <summary>Hanging down out of the bottom slot, picture toward you (its front is -z, the same way as the screen).
        /// It grows downward out of the slit as it ejects, so it never pokes up through the body.</summary>
        void PlaceInSlot(Transform p, float out01)
        {
            if (!Slot) return;
            float s = BodyScale, shown = Mathf.Lerp(0.04f, 1f, out01);
            p.localScale = new Vector3(s, s * shown, s);
            var top = Slot.position + Slot.up * (0.004f * s);
            p.SetPositionAndRotation(top - Slot.up * (PolaroidPhoto.Height * s * shown * 0.5f), Slot.rotation);
        }

        /// <summary>Your free hand pulls the print: reach the bit sticking out and close your fingers on it.</summary>
        bool TryGrab(PlayerRig rig)
        {
            var hand = rig.FreeHand;
            if (!hand.IsTracked || !Slot || (SnowballHand.Instance && SnowballHand.Instance.IsHoldingIn(hand))) return false;
            bool closed = hand.Grip > 0.6f || hand.Trigger > 0.6f;
            if (!closed) m_FreeOpenTime = Time.time;
            var grabAt = hand.HasJoints ? hand.PinchPoint : (hand.Palm ? hand.Palm.position : hand.Anchor.position);
            var edge = Slot.position - Slot.up * (PolaroidPhoto.Height * BodyScale * 0.7f);   // the hanging print's lower half
            bool reach = Vector3.Distance(grabAt, edge) < GrabReach;
            if (reach && !m_InReach) hand.Haptic(0.12f, 0.02f);   // a little tick: you're touching it
            m_InReach = reach;
            return reach && closed && Time.time - m_FreeOpenTime < 0.8f;
        }

        IEnumerator TakeOut(PolaroidPhoto photo)
        {
            var holder = PhotoHolder.Instance;
            var rig = PlayerRig.Active;
            var p = photo.transform;
            AudioDirector.PlayAt(Sfx.Whirr, p.position, 0.35f, 1.7f);
            if (rig && holder)
            {
                holder.Receive(photo, false);
                var from = p.position; var fromRot = p.rotation;
                for (float t = 0; t < 1f; t += Time.deltaTime / 0.16f)
                {
                    var hand = rig.FreeHand;
                    if (!hand.Anchor) break;
                    PhotoHolder.HeldPose(rig, hand, null, out var to, out var toRot);
                    var k = Ease.OutCubic(t);
                    p.SetPositionAndRotation(Vector3.Lerp(from, to, k), Quaternion.Slerp(fromRot, toRot, k));
                    p.localScale = Vector3.one * Mathf.Lerp(BodyScale, 1f, k);
                    yield return null;
                }
                holder.MarkArrived();
                rig.FreeHand.Haptic(0.3f, 0.05f);
                if (!PhotoHolder.DropTipShown && rig.IsXR)
                {
                    PhotoHolder.DropTipShown = true;
                    HToast.Show(rig.FreeHand.IsOpticalHand ? "Open your hand flat to drop a photo. Pinch it to pick it back up" : "Press A/X to drop a photo. Grab it to pick it back up", UIKit.Icon.Camera, 4.5f);
                }
            }
        }

        public void SetVisible(bool on) { m_Hidden = !on; if (!on) { m_Raise = 0f; m_Up = false; } }

        void OnDestroy()
        {
            if (m_Viewfinder) { m_Viewfinder.Release(); Destroy(m_Viewfinder); }
            if (m_BlurMat) Destroy(m_BlurMat);
            if (m_FlashMat) Destroy(m_FlashMat);
        }
    }

    /// <summary>
    /// Is the sparkling clue in the viewport and within range? (The doc's exact rule.) Previously solved clues
    /// still count so re-shown evidence gets a "we've been over this", but clues that haven't started sparkling don't.
    /// </summary>
    public static class ClueDetector
    {
        public static void Detect(Camera lens, PhotoData data)
        {
            var tune = FrostTuning.I;
            float f = tune.frameForgiveness;
            Clue best = null; float bestScore = float.MaxValue; Vector3 bestVp = default;
            foreach (var clue in Clue.All)
            {
                if (clue == null || !clue.CountsAsEvidence) continue;
                var pos = clue.FocusPoint;
                var vp = lens.WorldToViewportPoint(pos);
                if (vp.z <= 0.02f) continue;
                if (vp.x < -f || vp.x > 1f + f || vp.y < -f || vp.y > 1f + f) continue;
                float dist = Vector3.Distance(lens.transform.position, pos);
                if (dist > tune.clueRange + clue.radius * 0.5f) { data.TooFar = true; continue; }
                if (clue.useFacing && Vector3.Dot(clue.FacingNormal, (lens.transform.position - pos).normalized) < -0.15f) continue;
                if (Occluded(lens.transform.position, pos, clue)) continue;
                float score = Vector2.Distance(new Vector2(vp.x, vp.y), new Vector2(0.5f, 0.5f)) + (clue.IsActive ? 0f : 0.5f);
                if (score < bestScore) { bestScore = score; best = clue; bestVp = vp; }
            }
            if (best != null)
            {
                data.Clue = best.id;
                data.TooFar = false;
                data.ClueUV = new Vector2(Mathf.Clamp(bestVp.x, 0.08f, 0.92f), Mathf.Clamp(bestVp.y, 0.08f, 0.92f));
                float halfH = Mathf.Tan(lens.fieldOfView * 0.5f * Mathf.Deg2Rad) * bestVp.z;
                data.ClueRadiusUV = best.radius / Mathf.Max(halfH * 2f, 1e-3f) * 1.25f;
            }
            // who is in the frame (for poses)
            foreach (var kid in Kid.All)
            {
                if (!kid) continue;
                var vp = lens.WorldToViewportPoint(kid.ChestPoint);
                if (vp.z > 0.1f && vp.z < 3.2f && vp.x > 0.05f && vp.x < 0.95f && vp.y > 0f && vp.y < 1f)
                    data.KidsInFrame.Add(kid.id);
            }
        }

        static readonly RaycastHit[] s_Hits = new RaycastHit[8];

        static bool Occluded(Vector3 from, Vector3 to, Clue clue)
        {
            var dir = to - from;
            float len = dir.magnitude - clue.radius * 0.8f;
            if (len <= 0.02f) return false;
            int n = Physics.RaycastNonAlloc(from, dir.normalized, s_Hits, len, Layers.Mask(Layers.Kid, Layers.Room, Layers.Default), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var root = s_Hits[i].collider.transform;
                if (clue.owner && root.IsChildOf(clue.owner)) continue;
                return true;
            }
            return false;
        }
    }
}
