using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// The chunky retro instant camera, with a live viewfinder on its back. It only appears in your camera hand
    /// (left by default) when you make the camera gesture — a slight pinch, or a light squeeze of the trigger —
    /// otherwise you just see mittens. Pinch fully (pull the trigger) → flash, ka-CHUNK, and the print slides out
    /// of the slot and waits there, blank, until your other hand pulls it free; it develops in your hand over ~2 s.
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

        const float RaiseAt = 0.22f;        // pinch strength / trigger travel that brings the camera up
        const float LowerDelay = 0.6f;      // the camera lingers this long after you relax your fingers
        const float EjectSeconds = 0.9f;
        const float DevelopSeconds = 2f;
        const float GrabReach = 0.085f;     // how close your free hand has to get to the print
        /// <summary>The camera body's size (full size).</summary>
        public const float BodyScale = 1f;

        RenderTexture m_Viewfinder;
        Transform m_Body;
        Renderer m_FlashBulb;
        Material m_FlashMat;
        GameObject m_Backdrop;
        float m_Cooldown;
        Material m_BlurMat;
        bool m_ForceRender;
        float m_Raise, m_PoseTime, m_LastPose = -10f, m_FreeOpenTime = -10f;
        bool m_QueuedShot, m_Busy, m_Hidden, m_InReach;
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
            lensGo.transform.localPosition = new Vector3(0, 0.001f, 0.06f) * BodyScale;
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

            // viewfinder screen on the back of the camera
            var screen = ProcMesh.Part("Viewfinder", m_Body, ProcMesh.Quad(), Palette.UnlitTextured(m_Viewfinder, Color.white, false, "ViewfinderScreen"),
                new Vector3(0.012f, 0.006f, -0.0496f), new Vector3(0.056f, 0.056f, 1f), new Vector3(0, 0, 0));
            screen.transform.localRotation = Quaternion.Euler(0, 180f, 0) * Quaternion.Euler(0, 180f, 0); // quad faces -Z = toward you
            screen.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            m_BlurMat = new Material(FrostAssets.Shader(FrostAssets.ShaderId.Blur)) { name = "PhotoBlur" };
        }

        void BuildMock()
        {
            var body = Palette.Toon(Palette.CameraBody, true, 0.2f);
            var dark = Palette.Toon(Palette.CameraDark, true, 0.3f);
            var t = m_Body;
            // classic wedge body: main block + sloped top + chunky lens + flash window + red button + rainbow stripe
            // (a little less tall than it was: the shell is 1.2 cm shorter and everything above it sits lower)
            ProcMesh.Part("Shell", t, ProcMesh.RoundedBox(0.16f, 3), body, new Vector3(0, -0.011f, 0), new Vector3(0.13f, 0.073f, 0.095f));
            ProcMesh.Part("Top", t, ProcMesh.RoundedBox(0.2f, 3), body, new Vector3(0, 0.033f, -0.01f), new Vector3(0.13f, 0.035f, 0.075f), new Vector3(-12, 0, 0));
            ProcMesh.Part("Base", t, ProcMesh.RoundedBox(0.2f, 2), dark, new Vector3(0, -0.05f, 0.002f), new Vector3(0.133f, 0.012f, 0.098f));
            ProcMesh.Part("Lens Barrel", t, ProcMesh.Cylinder(18), dark, new Vector3(0, -0.004f, 0.052f), new Vector3(0.048f, 0.022f, 0.048f), new Vector3(90, 0, 0));
            ProcMesh.Part("Lens Ring", t, ProcMesh.Torus(0.42f, 0.08f, 20, 8), Palette.Toon(Palette.Coal, false, 0.4f), new Vector3(0, -0.004f, 0.064f), new Vector3(0.055f, 0.055f, 0.055f), new Vector3(90, 0, 0));
            ProcMesh.Part("Glass", t, ProcMesh.Disc(18), Palette.Toon(Palette.Lens, false, 0.9f, 0.15f), new Vector3(0, -0.004f, 0.0635f), new Vector3(0.036f, 0.002f, 0.036f), new Vector3(90, 0, 0));
            m_FlashMat = new Material(FrostAssets.Shader(FrostAssets.ShaderId.Unlit)) { name = "FlashBulb" };
            m_FlashMat.SetColor("_BaseColor", Palette.SkyHorizon);
            var bulb = ProcMesh.Part("Flash", t, ProcMesh.RoundedBox(0.3f, 2), m_FlashMat, new Vector3(0.036f, 0.034f, 0.03f), new Vector3(0.036f, 0.018f, 0.012f), new Vector3(-12, 0, 0));
            m_FlashBulb = bulb.GetComponent<Renderer>();
            // the shutter sits on top at the holding hand's end, right under the mitten's tip (left end for the left hand)
            float side = FrostPrefs.CameraHand == HandSide.Left ? -1f : 1f;
            ProcMesh.Part("Shutter", t, ProcMesh.Cylinder(12), Palette.Toon(Palette.EvidenceRed, true, 0.4f), new Vector3(0.046f * side, 0.0525f, -0.004f), new Vector3(0.018f, 0.008f, 0.018f), new Vector3(-12f, 0, 0));
            ProcMesh.Part("Eyepiece", t, ProcMesh.RoundedBox(0.25f, 2), dark, new Vector3(-0.035f * side, 0.040f, -0.035f), new Vector3(0.026f, 0.018f, 0.02f));
            Color[] rainbow = { Palette.Rainbow1, Palette.Rainbow2, Palette.Rainbow3, Palette.Rainbow4, Palette.Rainbow5 };
            for (int i = 0; i < rainbow.Length; i++)
                ProcMesh.Part("Stripe", t, ProcMesh.RoundedBox(0.1f, 1), Palette.Toon(rainbow[i], false, 0.2f),
                    new Vector3(-0.012f + i * 0.0062f, 0.0f, 0.0482f), new Vector3(0.0058f, 0.05f, 0.002f));
            // the photo slot at the front-bottom
            ProcMesh.Part("Slot", t, ProcMesh.RoundedBox(0.3f, 1), Palette.Toon(Palette.Coal), new Vector3(0, -0.042f, 0.0475f), new Vector3(0.095f, 0.006f, 0.002f));
            // viewfinder bezel on the back
            ProcMesh.Part("Bezel", t, ProcMesh.RoundedBox(0.2f, 2), dark, new Vector3(0.012f, 0.006f, -0.047f), new Vector3(0.064f, 0.064f, 0.004f));
            Slot = ProcMesh.Pivot("PhotoSlot", t, new Vector3(0, -0.044f, 0.05f));
            m_Body.localPosition = new Vector3(0, 0.015f, -0.01f);
            MeshMerger.Merge(m_Body, null, 0f);
        }

        void Update()
        {
            var rig = PlayerRig.Active;
            if (!rig) return;
            var hand = rig.CameraHand;
            float dt = Time.deltaTime;
            if (m_Cooldown > 0f) m_Cooldown -= dt;

            // ---- raise: the camera gesture brings it up; otherwise your hand is just a mitten
            bool wantsUp;
            if (!rig.IsXR) wantsUp = true;   // desktop stand-in: always in hand
            else
            {
                // a light pinch with the other fingers relaxed (not a fist), the hand up off the snow and empty
                var snow = SnowSurface.Instance;
                bool low = snow && hand.Palm && hand.Palm.position.y - snow.SurfaceHeightAt(hand.Palm.position) < FrostTuning.I.scoopHeight + 0.12f;
                bool busy = SnowballHand.Instance && SnowballHand.Instance.IsHoldingIn(hand);
                bool pose = Enabled && hand.IsTracked && hand.PhotoPose >= RaiseAt && hand.Grip < 0.55f && !busy && (!low || m_Raise > 0.5f);
                m_PoseTime = pose ? m_PoseTime + dt : 0f;
                if (pose && m_PoseTime >= 0.06f) m_LastPose = Time.time;
                wantsUp = Time.time - m_LastPose < LowerDelay;
            }
            if (m_Waiting || m_Busy) wantsUp = true;    // mid-shot, or a print still in the slot
            if (m_Hidden || !hand.IsTracked) wantsUp = false;
            m_Raise = Mathf.MoveTowards(m_Raise, wantsUp ? 1f : 0f, dt / (wantsUp ? 0.16f : 0.2f));
            float scale = wantsUp ? Ease.OutBack(m_Raise) : Mathf.SmoothStep(0f, 1f, m_Raise);
            if (m_Body)
            {
                bool show = scale > 0.01f;
                if (m_Body.gameObject.activeSelf != show) m_Body.gameObject.SetActive(show);
                m_Body.localScale = Vector3.one * Mathf.Max(scale * BodyScale, 0.001f);
            }


            // the viewfinder renders at a third of the frame rate in XR (~24–30 fps) to save the Quest's draw calls;
            // every frame while shooting, and not at all while the camera is down
            if (Lens) Lens.enabled = m_Raise > 0.5f && (!rig.IsXR || m_ForceRender || (Time.frameCount % 3) == 0);
            m_ForceRender = false;

            // ---- shutter: a full pinch / trigger pull. Pressed while the camera is still coming up, it fires once it's there.
            if (Enabled && hand.TriggerDown && m_Cooldown <= 0f)
            {
                if (m_Waiting) Jammed(hand);
                else if (m_Raise >= 0.95f) TakePhoto();
                else if (wantsUp || hand.PhotoPose >= RaiseAt) { m_QueuedShot = true; m_LastPose = Time.time; }
            }
            if (m_QueuedShot && (!Enabled || !hand.IsTracked)) m_QueuedShot = false;
            if (m_QueuedShot && m_Raise >= 0.95f) { m_QueuedShot = false; if (!m_Waiting) TakePhoto(); }
        }

        void LateUpdate()
        {
            var rig = PlayerRig.Active;
            if (!rig) return;
            var hand = rig.CameraHand;
            if (!hand.Anchor) return;
            // sit in the mitten's grip: hanging under the hand, index on the shutter
            if (hand.HasCameraGrip) transform.SetPositionAndRotation(hand.CameraGrip.position, hand.CameraGrip.rotation);
            else
            {
                HoldPose(rig, hand, out var pos, out var rot);
                transform.SetPositionAndRotation(pos, rot);
            }
            if (m_Waiting) PlaceInSlot(m_Waiting.transform, m_SlotOut);
        }

        /// <summary>Fallback when no mitten grip is available (no hand this frame).</summary>
        void HoldPose(PlayerRig rig, HandRig hand, out Vector3 pos, out Quaternion rot)
        {
            if (!rig.IsXR)
            {
                pos = hand.Anchor.position;
                rot = hand.Anchor.rotation;
                return;
            }
            if (hand.HasJoints)
            {
                var j = hand.Joints;
                Vector3 fwd = (j[8] - hand.Wrist);
                if (fwd.sqrMagnitude < 1e-6f) fwd = hand.Anchor.forward;
                // aim halfway between where the hand points and where you look, levelled so shots aren't tilted
                var aim = Vector3.Slerp(fwd.normalized, rig.Head.forward, 0.5f);
                rot = Quaternion.LookRotation(aim, Vector3.up);
                float side = hand.Side == HandSide.Left ? 1f : -1f;   // the body extends toward your middle
                pos = hand.PinchPoint + rot * new Vector3(0.05f * side, -0.032f, 0.012f);
                return;
            }
            pos = hand.Anchor.TransformPoint(new Vector3(0, 0.03f, 0.03f));
            rot = hand.Anchor.rotation * Quaternion.Euler(15f, 0, 0);
        }

        void Jammed(HandRig hand)
        {
            m_Cooldown = 0.4f;
            AudioDirector.PlayAt(Sfx.Whirr, transform.position, 0.25f, 0.6f);
            hand.Haptic(0.2f, 0.03f);
            HToast.Show("Pull the photo out first, with your other hand", UIKit.Icon.Camera, 3f);
        }

        public void TakePhoto()
        {
            if (m_Cooldown > 0f || m_Busy || m_Waiting) return;
            m_Cooldown = 0.55f;
            m_LastPose = Time.time;
            StartCoroutine(Shoot());
        }

        IEnumerator Shoot()
        {
            m_Busy = true;
            m_ForceRender = true;
            AudioDirector.PlayAt(Sfx.Shutter, transform.position, 1f, Random.Range(0.97f, 1.03f));
            PlayerRig.Active?.CameraHand.Haptic(0.7f, 0.07f);
            StartCoroutine(FlashBulb());
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

        /// <summary>The print slides out of the slot and waits there, blank, until your free hand pulls it free.</summary>
        IEnumerator EjectAndWait(PolaroidPhoto photo)
        {
            m_Waiting = photo;
            m_Busy = false;
            var p = photo.transform;
            p.localScale = Vector3.one * BodyScale;   // sized to the slot; it grows to full size as you pull it out
            AudioDirector.PlayAt(Sfx.Whirr, transform.position, 0.6f, 1f);
            for (float t = 0; t < 1f; t += Time.deltaTime / EjectSeconds)
            {
                m_SlotOut = Ease.OutCubic(t);
                PlaceInSlot(p, m_SlotOut);
                yield return null;
            }
            m_SlotOut = 1f;
            float waitStart = Time.time;
            bool tipped = false;
            m_InReach = false;
            while (photo)
            {
                var rig = PlayerRig.Active;
                bool closing = CaseDirector.I != null && CaseDirector.I.CaseClosed;
                if (closing || (rig && !rig.IsXR && Time.time - waitStart > 0.7f)) break;   // finale / desktop: handed over
                if (rig && TryGrab(rig)) break;
                if (!tipped && Time.time - waitStart > 4f && rig && rig.IsXR)
                {
                    tipped = true;
                    HToast.Show("Pull the photo out of the camera with your other hand", UIKit.Icon.Camera, 3.5f);
                }
                yield return null;
            }
            m_Waiting = null;
            if (photo) yield return TakeOut(photo);
        }

        void PlaceInSlot(Transform p, float out01)
        {
            if (!Slot) return;
            var rot = Slot.rotation * Quaternion.Euler(-90f, 0f, 0f) * Quaternion.Euler(0, 180f, 0);
            float along = Mathf.Lerp(-PolaroidPhoto.Height * 0.45f, PolaroidPhoto.Height * 0.2f, out01);
            p.SetPositionAndRotation(Slot.position + Slot.forward * along + Slot.up * -0.004f, rot);
        }

        /// <summary>Your free hand pulls the print: reach the bit sticking out and close your fingers on it.</summary>
        bool TryGrab(PlayerRig rig)
        {
            var hand = rig.FreeHand;
            if (!hand.IsTracked || !Slot || (SnowballHand.Instance && SnowballHand.Instance.IsHoldingIn(hand))) return false;
            bool closed = hand.Grip > 0.6f || hand.Trigger > 0.6f;
            if (!closed) m_FreeOpenTime = Time.time;
            var grabAt = hand.HasJoints ? hand.PinchPoint : (hand.Palm ? hand.Palm.position : hand.Anchor.position);
            var edge = Slot.position + Slot.forward * (PolaroidPhoto.Height * 0.62f);
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
            bool fast = CaseDirector.I != null && CaseDirector.I.CaseClosed;
            yield return photo.Develop(fast ? 0.6f : DevelopSeconds);
        }

        public void SetVisible(bool on) { m_Hidden = !on; if (!on) m_Raise = 0f; }

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
