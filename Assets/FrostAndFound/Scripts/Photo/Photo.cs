using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FrostAndFound
{
    /// <summary>What a photograph shows, as the game sees it.</summary>
    public class PhotoData
    {
        public int Number;
        public Texture Image;
        public ClueId Clue = ClueId.None;
        public Vector2 ClueUV = new Vector2(0.5f, 0.5f);
        public float ClueRadiusUV = 0.25f;
        public bool Blurred;
        public bool Guilty;
        public bool TooFar;          // a clue was in frame but beyond range (for Lucy's hint)
        public float TakenAt;
        public readonly List<CastId> KidsInFrame = new List<CastId>();
        /// <summary>Kids you can actually make out in the picture (unblocked, big enough, face in shot).</summary>
        public readonly List<CastId> KidsSeen = new List<CastId>();
        /// <summary>Kids who are a main part of the shot (not just someone small in the background).</summary>
        public readonly List<CastId> KidsFeatured = new List<CastId>();
        /// <summary>What the picture is "of": its biggest, most central subject (see SubjectDetector).</summary>
        public SubjectKind Subject = SubjectKind.None;
        public CastId SubjectKid;            // valid when Subject == Kid
        public bool MostlySnow;
        public bool IsEvidence => Clue != ClueId.None && !Blurred;
    }

    /// <summary>
    /// The physical instant photo: white paper frame, the picture, a hand-drawn red circle on good shots, and a
    /// GUILTY stamp for the finale. Animates out of the camera, develops with a white flash, and flies to your hand.
    /// </summary>
    public class PolaroidPhoto : MonoBehaviour
    {
        public const float Width = 0.088f, Height = 0.107f, ImageSize = 0.078f;
        public PhotoData Data { get; private set; }
        /// <summary>The picture has fully come through (only then can you show it to someone).</summary>
        public bool IsDeveloped { get; private set; }

        Material m_ImageMat;
        Transform m_Circle, m_Stamp, m_Content;
        Material m_CircleMat;
        TextMeshPro m_Label;

        public static PolaroidPhoto Create(PhotoData data)
        {
            var go = new GameObject("Polaroid #" + data.Number);
            go.layer = Layers.PlayerHands;
            var p = go.AddComponent<PolaroidPhoto>();
            p.Build(data);
            return p;
        }

        void Build(PhotoData data)
        {
            Data = data;
            m_Content = new GameObject("Content").transform;
            m_Content.SetParent(transform, false);
            m_Content.gameObject.layer = gameObject.layer;
            // paper (thin rounded slab); front faces -Z like a Quad so it looks at whoever it is "facing"
            ProcMesh.Part("Paper", m_Content, ProcMesh.RoundedBox(0.08f, 2), Palette.Toon(Palette.Paper, false, 0.1f),
                Vector3.zero, new Vector3(Width, Height, 0.0025f));
            // image (sits in the upper part like a real instant photo)
            m_ImageMat = new Material(FrostAssets.Shader(FrostAssets.ShaderId.Photo)) { name = "PhotoImage" };
            m_ImageMat.SetTexture("_BaseMap", data.Image);
            m_ImageMat.SetFloat("_Flash", 0f);
            m_ImageMat.SetFloat("_Develop", 0f);   // a blank print until it's pulled out and develops
            var img = ProcMesh.Part("Image", m_Content, ProcMesh.Quad(), m_ImageMat,
                new Vector3(0, (Height - ImageSize) * 0.5f - 0.005f, -0.0014f), new Vector3(ImageSize, ImageSize, 1f));
            img.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // red marker circle (hidden until drawn)
            if (data.IsEvidence && !data.Guilty)
            {
                m_CircleMat = Palette.UnlitTextured(FxTextures.EvidenceCircle, Palette.EvidenceRed.WithAlpha(0f), true, "EvidenceCircle");
                var c = ProcMesh.Part("Circle", m_Content, ProcMesh.Quad(), m_CircleMat, Vector3.zero, Vector3.one);
                m_Circle = c.transform;
                var uv = data.ClueUV;
                float r = Mathf.Clamp(data.ClueRadiusUV, 0.12f, 0.42f);
                var imgCenter = img.transform.localPosition;
                m_Circle.localPosition = imgCenter + new Vector3((uv.x - 0.5f) * ImageSize, (uv.y - 0.5f) * ImageSize, -0.0006f);
                m_Circle.localScale = new Vector3(ImageSize * r * 2.2f, ImageSize * r * 2.2f, 1f);
                m_Circle.localRotation = Quaternion.Euler(0, 0, Random.Range(-25f, 25f));
                c.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // label on the back, in marker
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(m_Content, false);
            labelGo.layer = gameObject.layer;
            labelGo.transform.localPosition = new Vector3(0, 0, 0.0016f);
            labelGo.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            m_Label = labelGo.AddComponent<TextMeshPro>();
            m_Label.text = data.Guilty ? "GOT HIM!" : "#" + data.Number;
            UIStyle.Apply(m_Label, UIStyle.Role.Marker);
            m_Label.fontSize = data.Guilty ? 0.11f : 0.075f;
            m_Label.rectTransform.sizeDelta = new Vector2(Width * 0.9f, Height * 0.5f);
            m_Label.alignment = TextAlignmentOptions.Center;
            DepthOcclusion.Exempt(gameObject);   // held in your hand: never cut out by it
        }

        /// <summary>Instant film developing in your hand: blank → the picture surfaces → colour → (circle draws on).</summary>
        public IEnumerator Develop(float seconds)
        {
            yield return Ease.Tween(seconds, t => m_ImageMat.SetFloat("_Develop", t), Ease.InOutCubic, false);
            m_ImageMat.SetFloat("_Develop", 1f);
            IsDeveloped = true;
            if (m_Circle)
            {
                AudioDirector.PlayAt(Sfx.Marker, transform.position, 0.5f, 1f);
                var baseScale = m_Circle.localScale;
                yield return Ease.Tween(0.28f, t =>
                {
                    m_CircleMat.SetColor("_BaseColor", Palette.EvidenceRed.WithAlpha(t));
                    float s = Mathf.Lerp(1.25f, 1f, t);
                    m_Circle.localScale = new Vector3(baseScale.x * s, baseScale.y * s, 1f);
                }, Ease.OutCubic, false);
            }
            if (Data.Guilty) yield return SlamGuilty();
        }

        IEnumerator SlamGuilty()
        {
            // a giant red GUILTY rubber stamp slammed across the picture
            var stamp = UIKit.Stamp(m_Content, "GUILTY", 0.2f, Palette.StampRed, 14f, 0.92f);
            stamp.gameObject.SetLayerRecursive(gameObject.layer);
            m_Stamp = stamp;
            m_Stamp.localPosition = new Vector3(0, (Height - ImageSize) * 0.5f - 0.005f, -0.003f);
            AudioDirector.PlayAt(Sfx.StampSlam, transform.position, 1f, 1f);
            yield return Ease.Tween(0.18f, t => m_Stamp.localScale = Vector3.one * Mathf.Lerp(3.2f, 1f, t), Ease.InCubic, false);
            PlayerRig.Active?.CameraHand.Haptic(0.9f, 0.12f);
            yield return Ease.Tween(0.12f, t => m_Content.localScale = Vector3.one * (1f + 0.08f * Ease.Bump(t)), Ease.Linear, false);
        }

        public void SetFlash(float f) => m_ImageMat.SetFloat("_Flash", f);

        // ---- dropped in the snow
        Rigidbody m_Rb;
        float m_DroppedAt;
        public bool IsDropped => m_Rb;

        /// <summary>Let go: it flutters down into the snow and lies there until someone picks it up.</summary>
        public void Drop(Vector3 velocity)
        {
            transform.SetParent(null, true);
            gameObject.layer = 0;   // Default: lands on the snow and furniture (the picture itself stays hidden from the lens)
            var col = gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(Width, Height, 0.004f);
            m_Rb = gameObject.AddComponent<Rigidbody>();
            m_Rb.mass = 0.02f;
            m_Rb.linearDamping = 3f;
            m_Rb.angularDamping = 1.5f;
            m_Rb.interpolation = RigidbodyInterpolation.Interpolate;
            m_Rb.linearVelocity = Vector3.ClampMagnitude(velocity, 2f);
            m_Rb.angularVelocity = Random.insideUnitSphere * 3f;
            m_DroppedAt = Time.time;
        }

        public void PickUp()
        {
            SetHighlight(false);
            if (m_Rb) Destroy(m_Rb);
            var col = GetComponent<BoxCollider>();
            if (col) Destroy(col);
            m_Rb = null;
            gameObject.layer = Layers.PlayerHands;
        }

        // ---- the "this is the one you'll pick up" glow
        Transform m_Glow;
        Material m_GlowMat;
        float m_GlowOn, m_GlowWant;
        static Texture2D s_GlowTex;

        /// <summary>Light it up softly round its edges (it's the one your hand will pick up), or not.</summary>
        public void SetHighlight(bool on)
        {
            m_GlowWant = on ? 1f : 0f;
            if (on && !m_Glow) BuildGlow();
        }

        void BuildGlow()
        {
            if (!s_GlowTex) s_GlowTex = MakeGlowTexture();
            m_GlowMat = new Material(Palette.Particle(s_GlowTex, true, "Photo Glow")) { name = "Photo Glow" };
            m_GlowMat.SetColor("_BaseColor", new Color(1f, 0.93f, 0.7f, 0f));
            // in the paper's own plane: the photo hides the middle, so only a halo round its edges shows (either side up)
            var g = ProcMesh.Part("Pick-up Glow", m_Content, ProcMesh.Quad(), m_GlowMat, Vector3.zero, new Vector3(Width * 1.3f, Height * 1.25f, 1f));
            g.layer = gameObject.layer;
            var mr = g.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            m_Glow = g.transform;
        }

        void LateUpdate()
        {
            if (!m_Glow) return;
            m_GlowOn = Mathf.MoveTowards(m_GlowOn, m_GlowWant, Time.deltaTime * 5f);
            m_Glow.gameObject.SetActive(m_GlowOn > 0.001f);
            float pulse = 0.9f + 0.1f * Mathf.Sin(Time.time * 3f);   // a slow, faint breathing
            m_GlowMat.SetColor("_BaseColor", new Color(1f, 0.95f, 0.82f, 0.3f * m_GlowOn * pulse));
        }

        // a soft rounded-rectangle halo: full inside the photo's outline, fading out a couple of centimetres beyond it
        static Texture2D MakeGlowTexture()
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Photo Glow", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float dx = Mathf.Max(Mathf.Abs(u) - 0.74f, 0f), dy = Mathf.Max(Mathf.Abs(v) - 0.76f, 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d / 0.24f);
                    px[y * n + x] = new Color(1f, 1f, 1f, a * a);
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>Stop simulating once it has settled (or when something else wants to move it, e.g. the case file).</summary>
        public void Freeze() { if (m_Rb) m_Rb.isKinematic = true; }

        void FixedUpdate()
        {
            if (m_Rb && !m_Rb.isKinematic && Time.time - m_DroppedAt > 1.5f && m_Rb.IsSleeping()) m_Rb.isKinematic = true;
        }

        void OnDestroy()
        {
            if (m_ImageMat) Destroy(m_ImageMat);
            if (m_CircleMat) Destroy(m_CircleMat);
        }
    }

    /// <summary>
    /// The free hand's photo slot. You always show your newest photo (the chain is linear, so there's no
    /// inventory). Scooping snow tucks it away; throwing brings it back.
    /// </summary>
    [DefaultExecutionOrder(150)] // after the mittens have taken their pinch this frame
    public class PhotoHolder : MonoBehaviour
    {
        public static PhotoHolder Instance { get; private set; }
        public readonly List<PolaroidPhoto> Album = new List<PolaroidPhoto>();
        public PolaroidPhoto Current { get; private set; }
        /// <summary>Photos you've let go of, lying in the snow (still part of the album).</summary>
        public readonly List<PolaroidPhoto> Dropped = new List<PolaroidPhoto>();
        public static bool DropTipShown;
        float m_FlatSince = -1f, m_OpenTime = -10f;
        public bool IsTucked { get; private set; }
        public bool Visible => Current && !IsTucked && m_Arrived;
        /// <summary>Set by ShowPhotoDetector while the player holds the photo out to a kid.</summary>
        public Transform FaceTarget { get; set; }

        bool m_Arrived;
        float m_Flip; // 0 = facing player, 1 = facing target
        Coroutine m_Restore;

        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        public void Receive(PolaroidPhoto photo, bool arrived)
        {
            if (Current && Current != photo) Current.gameObject.SetActive(false);
            Current = photo;
            m_Arrived = arrived;
            if (!Album.Contains(photo)) Album.Add(photo);
            var rigNow = PlayerRig.Active;
            if (SnowballHand.Instance && rigNow && SnowballHand.Instance.IsHoldingIn(rigNow.FreeHand)) IsTucked = true;
            photo.gameObject.SetActive(!IsTucked || !arrived);
        }

        public void MarkArrived() { m_Arrived = true; if (IsTucked && Current) Current.gameObject.SetActive(false); }
        /// <summary>Photos join the album the moment they're taken (the case file needs them even if a print is still in the slot).</summary>
        public void AddToAlbum(PolaroidPhoto photo) { if (photo && !Album.Contains(photo)) Album.Add(photo); }

        public void TuckAway()
        {
            if (m_Restore != null) { StopCoroutine(m_Restore); m_Restore = null; }
            IsTucked = true;
            if (Current && m_Arrived) Current.gameObject.SetActive(false);
        }

        public void Restore(float delay)
        {
            if (m_Restore != null) StopCoroutine(m_Restore);
            m_Restore = StartCoroutine(RestoreRoutine(delay));
        }

        IEnumerator RestoreRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (SnowballHand.Instance && PlayerRig.Active && SnowballHand.Instance.IsHoldingIn(PlayerRig.Active.FreeHand)) yield break;
            IsTucked = false;
            if (Current)
            {
                Current.gameObject.SetActive(true);
                yield return Ease.Tween(0.2f, t => Current.transform.localScale = Vector3.one * Ease.OutBack(t), Ease.Linear, false);
            }
            m_Restore = null;
        }

        void Update()
        {
            var rig = PlayerRig.Active;
            if (!rig) return;
            if (CaseDirector.I != null && CaseDirector.I.CaseClosed) { foreach (var p in Dropped) if (p) p.Freeze(); return; }
            var hand = rig.FreeHand;
            if (!hand.IsTracked) return;
            // let go: open your hand flat for a moment (hand tracking), press A/X (controllers), or G (desktop)
            if (Visible)
            {
                bool drop;
                if (hand.IsOpticalHand)
                {
                    bool flat = hand.Grip < 0.12f && hand.Trigger < 0.1f;
                    if (!flat) m_FlatSince = -1f;
                    else if (m_FlatSince < 0f) m_FlatSince = Time.time;
                    drop = m_FlatSince > 0f && Time.time - m_FlatSince > 0.1f;
                }
                else if (rig.IsXR) drop = hand.PrimaryDown;
                else drop = Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame;
                if (drop) { DropCurrent(hand); return; }
            }
            // pick one back up: reach down to it and grab (desktop: G picks up the nearest). The one your hand would
            // take glows softly, so with a few lying close together you can see which.
            Dropped.RemoveAll(p => !p);
            var holding = SnowballHand.Instance && SnowballHand.Instance.IsHoldingIn(hand);
            var near = holding ? null : NearestDropped(rig, hand, rig.IsXR ? HighlightReach : 2.5f);
            if (near != m_Highlighted)
            {
                if (m_Highlighted) m_Highlighted.SetHighlight(false);
                m_Highlighted = near;
                if (near) near.SetHighlight(true);
            }
            if (!near) { m_OpenTime = Time.time; return; }
            if (!rig.IsXR)
            {
                if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame) PickUp(near, hand);
                return;
            }
            bool closed = hand.Grip > 0.6f || hand.Trigger > 0.6f;
            if (!closed) { m_OpenTime = Time.time; return; }
            if (Time.time - m_OpenTime > 0.8f) return;
            if (Vector3.Distance(near.transform.position, ReachPoint(hand)) < PickReach) PickUp(near, hand);
        }

        const float PickReach = 0.2f, HighlightReach = 0.32f;
        PolaroidPhoto m_Highlighted;

        static Vector3 ReachPoint(HandRig hand) => hand.HasJoints ? hand.PinchPoint : (hand.Palm ? hand.Palm.position : hand.Anchor.position);

        PolaroidPhoto NearestDropped(PlayerRig rig, HandRig hand, float within)
        {
            PolaroidPhoto best = null;
            float bestD = within;
            var at = rig.IsXR ? ReachPoint(hand) : rig.Head.position;
            foreach (var p in Dropped)
            {
                if (!p) continue;
                float d = rig.IsXR ? Vector3.Distance(p.transform.position, at) : FrostUtil.FlatDistance(p.transform.position, at);
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        /// <summary>A dropped photo is within this hand's grab: picking it up wins over scooping snow.</summary>
        public bool PhotoInReach(HandRig hand)
        {
            var rig = PlayerRig.Active;
            if (!rig || hand != rig.FreeHand || Dropped.Count == 0 || (CaseDirector.I != null && CaseDirector.I.CaseClosed)) return false;
            var at = ReachPoint(hand);
            foreach (var p in Dropped) if (p && Vector3.Distance(p.transform.position, at) < PickReach + 0.05f) return true;
            return false;
        }

        void DropCurrent(HandRig hand)
        {
            var p = Current;
            Current = null;
            m_Arrived = false;
            m_FlatSince = -1f;
            FaceTarget = null;
            p.Drop(hand.ReleaseVelocity() * 0.5f);
            Dropped.Add(p);
            if (!SatieSound.Fire("photo_drop", p.transform.position)) AudioDirector.PlayAt(Sfx.Whirr, p.transform.position, 0.2f, 1.9f);
            hand.Haptic(0.15f, 0.03f);
        }

        void PickUp(PolaroidPhoto p, HandRig hand)
        {
            Dropped.Remove(p);
            p.PickUp();
            p.transform.localScale = Vector3.one;
            if (Current && Current != p) Current.gameObject.SetActive(false);
            Current = p;
            m_Arrived = true;
            IsTucked = false;
            p.gameObject.SetActive(true);
            AudioDirector.PlayAt(Sfx.Whirr, p.transform.position, 0.3f, 1.7f);
            hand.Haptic(0.25f, 0.04f);
        }

        void LateUpdate()
        {
            var rig = PlayerRig.Active;
            if (!rig || !Current || !m_Arrived || IsTucked) return;
            var hand = rig.FreeHand;
            if (!hand.Anchor) return;
            m_Flip = Ease.Damp(m_Flip, FaceTarget ? 1f : 0f, 10f, Time.deltaTime);
            HeldPose(rig, hand, FaceTarget, out var pos, out var rot, m_Flip);
            Current.transform.SetPositionAndRotation(pos, rot);
        }

        /// <summary>A photo pinched by its bottom border between the mitten's thumb and finger, picture toward your
        /// eyes (or turned toward the kid you're showing it to).</summary>
        public static void HeldPose(PlayerRig rig, HandRig hand, Transform faceTarget, out Vector3 pos, out Quaternion rot, float flip = 0f)
        {
            var head = rig.Head;
            if (!hand.HasPhotoGrip && !hand.HasJoints && hand.Palm)
            {
                // no mitten grip (desktop stand-in): held up like a card above the hand
                pos = hand.Palm.position + hand.Anchor.up * 0.035f + hand.Anchor.forward * 0.02f;
                rot = Quaternion.LookRotation(-(head.position - pos).normalized, head.up);
                if (faceTarget && flip > 0f) rot = Quaternion.Slerp(rot, Quaternion.LookRotation(-(faceTarget.position - pos).normalized, Vector3.up), flip);
                return;
            }
            var pinch = hand.HasPhotoGrip ? hand.PhotoGrip : hand.PinchPoint;
            var toHead = (head.position - pinch).normalized;
            rot = Quaternion.LookRotation(-toHead, head.up);
            if (faceTarget && flip > 0f)
            {
                var toKid = (faceTarget.position - pinch).normalized;
                rot = Quaternion.Slerp(rot, Quaternion.LookRotation(-toKid, Vector3.up), flip);
            }
            // the pinch sits on the white border below the picture; the paper stands just in front of the mitten
            pos = pinch + rot * new Vector3(0f, PolaroidPhoto.Height * 0.5f - 0.011f, -0.006f);
        }

        public void ClearAll()
        {
            foreach (var p in Album) if (p) Destroy(p.gameObject);
            Album.Clear();
            Dropped.Clear();
            Current = null;
        }
    }
}
