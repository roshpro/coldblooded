using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// The chunky retro instant camera in your camera hand, with a live viewfinder on its back.
    /// Trigger (or pinch) → flash, ka-CHUNK, and the photo pops out already developed with a quick white fade,
    /// then lands in your other hand. No film limit; a missed shot is just a photo of snow.
    /// </summary>
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

        RenderTexture m_Viewfinder;
        Transform m_Body;
        Renderer m_FlashBulb;
        Material m_FlashMat;
        GameObject m_Backdrop;
        float m_Cooldown;
        Material m_BlurMat;
        bool m_ForceRender;

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
            lensGo.transform.localPosition = new Vector3(0, 0.005f, 0.06f);
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
                new Vector3(0.012f, 0.012f, -0.0496f), new Vector3(0.056f, 0.056f, 1f), new Vector3(0, 0, 0));
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
            ProcMesh.Part("Shell", t, ProcMesh.RoundedBox(0.16f, 3), body, new Vector3(0, -0.005f, 0), new Vector3(0.13f, 0.085f, 0.095f));
            ProcMesh.Part("Top", t, ProcMesh.RoundedBox(0.2f, 3), body, new Vector3(0, 0.045f, -0.01f), new Vector3(0.13f, 0.035f, 0.075f), new Vector3(-12, 0, 0));
            ProcMesh.Part("Base", t, ProcMesh.RoundedBox(0.2f, 2), dark, new Vector3(0, -0.05f, 0.002f), new Vector3(0.133f, 0.012f, 0.098f));
            ProcMesh.Part("Lens Barrel", t, ProcMesh.Cylinder(18), dark, new Vector3(0, 0.0f, 0.052f), new Vector3(0.048f, 0.022f, 0.048f), new Vector3(90, 0, 0));
            ProcMesh.Part("Lens Ring", t, ProcMesh.Torus(0.42f, 0.08f, 20, 8), Palette.Toon(Palette.Coal, false, 0.4f), new Vector3(0, 0.0f, 0.064f), new Vector3(0.055f, 0.055f, 0.055f), new Vector3(90, 0, 0));
            ProcMesh.Part("Glass", t, ProcMesh.Disc(18), Palette.Toon(Palette.Lens, false, 0.9f, 0.15f), new Vector3(0, 0.0f, 0.0635f), new Vector3(0.036f, 0.002f, 0.036f), new Vector3(90, 0, 0));
            m_FlashMat = new Material(FrostAssets.Shader(FrostAssets.ShaderId.Unlit)) { name = "FlashBulb" };
            m_FlashMat.SetColor("_BaseColor", Palette.SkyHorizon);
            var bulb = ProcMesh.Part("Flash", t, ProcMesh.RoundedBox(0.3f, 2), m_FlashMat, new Vector3(0.036f, 0.046f, 0.03f), new Vector3(0.036f, 0.018f, 0.012f), new Vector3(-12, 0, 0));
            m_FlashBulb = bulb.GetComponent<Renderer>();
            ProcMesh.Part("Shutter", t, ProcMesh.Cylinder(12), Palette.Toon(Palette.EvidenceRed, true, 0.4f), new Vector3(0.045f, 0.03f, 0.04f), new Vector3(0.016f, 0.008f, 0.016f), new Vector3(70, 0, 0));
            ProcMesh.Part("Eyepiece", t, ProcMesh.RoundedBox(0.25f, 2), dark, new Vector3(-0.035f, 0.052f, -0.035f), new Vector3(0.026f, 0.018f, 0.02f));
            Color[] rainbow = { Palette.Rainbow1, Palette.Rainbow2, Palette.Rainbow3, Palette.Rainbow4, Palette.Rainbow5 };
            for (int i = 0; i < rainbow.Length; i++)
                ProcMesh.Part("Stripe", t, ProcMesh.RoundedBox(0.1f, 1), Palette.Toon(rainbow[i], false, 0.2f),
                    new Vector3(-0.012f + i * 0.0062f, 0.01f, 0.0482f), new Vector3(0.0058f, 0.06f, 0.002f));
            // the photo slot at the front-bottom
            ProcMesh.Part("Slot", t, ProcMesh.RoundedBox(0.3f, 1), Palette.Toon(Palette.Coal), new Vector3(0, -0.042f, 0.0475f), new Vector3(0.095f, 0.006f, 0.002f));
            // viewfinder bezel on the back
            ProcMesh.Part("Bezel", t, ProcMesh.RoundedBox(0.2f, 2), dark, new Vector3(0.012f, 0.012f, -0.047f), new Vector3(0.064f, 0.064f, 0.004f));
            Slot = ProcMesh.Pivot("PhotoSlot", t, new Vector3(0, -0.044f, 0.05f));
            m_Body.localPosition = new Vector3(0, 0.015f, -0.01f);
            MeshMerger.Merge(m_Body, null, 0f);
        }

        void Update()
        {
            var rig = PlayerRig.Active;
            if (!rig) return;
            var hand = rig.CameraHand;
            if (hand.Anchor)
            {
                // sit in the hand: grip under the body, lens pointing where the hand points
                var target = hand.Anchor.TransformPoint(rig.IsXR ? new Vector3(0, 0.03f, 0.03f) : Vector3.zero);
                transform.SetPositionAndRotation(target, hand.Anchor.rotation * Quaternion.Euler(rig.IsXR ? 15f : 0f, 0, 0));
            }
            if (m_Cooldown > 0f) m_Cooldown -= Time.deltaTime;

            // the viewfinder renders at a third of the frame rate in XR (~24–30 fps) to save the Quest's draw calls; every frame while shooting
            if (Lens) Lens.enabled = !rig.IsXR || m_ForceRender || (Time.frameCount % 3) == 0;
            m_ForceRender = false;

            if (Enabled && hand.TriggerDown && m_Cooldown <= 0f) TakePhoto();
        }

        public void TakePhoto()
        {
            if (m_Cooldown > 0f) return;
            m_Cooldown = 0.55f;
            StartCoroutine(Shoot());
        }

        IEnumerator Shoot()
        {
            var tune = FrostTuning.I;
            m_ForceRender = true;
            AudioDirector.PlayAt(Sfx.Shutter, transform.position, 1f, Random.Range(0.97f, 1.03f));
            PlayerRig.Active?.CameraHand.Haptic(0.7f, 0.07f);
            StartCoroutine(FlashBulb());
            // let the lens render this frame so the photo matches what the viewfinder shows
            yield return new WaitForEndOfFrame();

            PhotoCount++;
            var data = new PhotoData { Number = PhotoCount, TakenAt = Time.time };
            ClueDetector.Detect(Lens, data);
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
            PhotoTaken?.Invoke(data);
            yield return Eject(photo, tune.developFadeSeconds);
        }

        IEnumerator FlashBulb()
        {
            if (!m_FlashMat) yield break;
            Fx.GlintAt(m_FlashBulb ? m_FlashBulb.transform.position : transform.position, 0.12f, Palette.GlintWhite);
            m_FlashMat.SetColor("_BaseColor", Color.white * 1.6f);
            yield return Ease.Tween(0.25f, t => m_FlashMat.SetColor("_BaseColor", Color.Lerp(Color.white * 1.6f, Palette.SkyHorizon, t)), Ease.OutCubic, false);
        }

        IEnumerator Eject(PolaroidPhoto photo, float developSeconds)
        {
            var holder = PhotoHolder.Instance;
            holder?.Receive(photo, false);
            var p = photo.transform;
            p.localScale = Vector3.one;
            AudioDirector.PlayAt(Sfx.Whirr, transform.position, 0.6f, 1f);
            // slide out of the slot (picture side up/toward the lens), developing as it goes
            float slide = 0.32f;
            var develop = StartCoroutine(photo.Develop(developSeconds));
            for (float t = 0; t < 1f; t += Time.deltaTime / slide)
            {
                if (!Slot) break;
                var rot = Slot.rotation * Quaternion.Euler(-90f, 0f, 0f) * Quaternion.Euler(0, 180f, 0);
                var pos = Slot.position + Slot.forward * (PolaroidPhoto.Height * 0.5f * Ease.OutCubic(t) - PolaroidPhoto.Height * 0.45f) + Slot.up * -0.004f;
                p.SetPositionAndRotation(pos, rot);
                yield return null;
            }
            yield return new WaitForSeconds(0.08f);
            // pops out and lands in your free hand
            var rig = PlayerRig.Active;
            if (rig && holder)
            {
                var from = p.position; var fromRot = p.rotation;
                for (float t = 0; t < 1f; t += Time.deltaTime / 0.26f)
                {
                    var hand = rig.FreeHand;
                    if (!hand.Palm) break;
                    var to = hand.Palm.position + hand.Anchor.up * 0.035f;
                    var toRot = Quaternion.LookRotation(-(rig.Head.position - to).normalized, rig.Head.up);
                    var k = Ease.InOutCubic(t);
                    p.SetPositionAndRotation(Vector3.Lerp(from, to, k) + Vector3.up * Ease.Bump(t) * 0.06f, Quaternion.Slerp(fromRot, toRot, k));
                    yield return null;
                }
                holder.MarkArrived();
                rig.FreeHand.Haptic(0.25f, 0.04f);
            }
            yield return develop;
        }

        public void SetVisible(bool on) { if (m_Body) m_Body.gameObject.SetActive(on); }

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
