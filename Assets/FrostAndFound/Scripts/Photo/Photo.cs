using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

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
            m_Label.text = data.Guilty ? "GOT HIM!" : (data.IsEvidence ? "EXHIBIT " + data.Number : "#" + data.Number);
            UIStyle.Apply(m_Label, UIStyle.Role.Marker);
            m_Label.fontSize = 0.11f;
            m_Label.rectTransform.sizeDelta = new Vector2(Width * 0.9f, Height * 0.5f);
            m_Label.alignment = TextAlignmentOptions.Center;
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
    public class PhotoHolder : MonoBehaviour
    {
        public static PhotoHolder Instance { get; private set; }
        public readonly List<PolaroidPhoto> Album = new List<PolaroidPhoto>();
        public PolaroidPhoto Current { get; private set; }
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
            if (SnowballHand.Instance && SnowballHand.Instance.IsHolding) IsTucked = true;
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
            if (SnowballHand.Instance && SnowballHand.Instance.IsHolding) yield break;
            IsTucked = false;
            if (Current)
            {
                Current.gameObject.SetActive(true);
                yield return Ease.Tween(0.2f, t => Current.transform.localScale = Vector3.one * Ease.OutBack(t), Ease.Linear, false);
            }
            m_Restore = null;
        }

        void LateUpdate()
        {
            var rig = PlayerRig.Active;
            if (!rig || !Current || !m_Arrived || IsTucked) return;
            var hand = rig.FreeHand;
            if (!hand.Palm) return;
            var head = rig.Head;
            // held like a card between thumb and fingers, picture toward your eyes
            var pos = hand.Palm.position + hand.Anchor.up * 0.035f + hand.Anchor.forward * 0.02f;
            var toHead = (head.position - pos).normalized;
            var faceSelf = Quaternion.LookRotation(-toHead, head.up);
            m_Flip = Ease.Damp(m_Flip, FaceTarget ? 1f : 0f, 10f, Time.deltaTime);
            var rot = faceSelf;
            if (FaceTarget)
            {
                var toKid = (FaceTarget.position - pos).normalized;
                var faceKid = Quaternion.LookRotation(-toKid, Vector3.up);
                rot = Quaternion.Slerp(faceSelf, faceKid, m_Flip);
            }
            Current.transform.SetPositionAndRotation(pos, rot);
        }

        public void ClearAll()
        {
            foreach (var p in Album) if (p) Destroy(p.gameObject);
            Album.Clear();
            Current = null;
        }
    }
}
