using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Voiced lines with subtitles. Each line plays at the speaker (spatial), shows a speech bubble above their head,
    /// and — if the speaker is outside your view — also a caption under your gaze so you never miss a line.
    /// </summary>
    public class DialogueRunner : MonoBehaviour
    {
        static DialogueRunner s_I;
        readonly Dictionary<CastId, float> m_SpeakingUntil = new Dictionary<CastId, float>();
        readonly Dictionary<CastId, SpeechBubble> m_Bubbles = new Dictionary<CastId, SpeechBubble>();
        CaptionBar m_Caption;

        public static bool AnyoneSpeaking
        {
            get
            {
                if (!s_I) return false;
                foreach (var kv in s_I.m_SpeakingUntil) if (kv.Value > Time.time) return true;
                return false;
            }
        }

        /// <summary>A kid starts a line (faces use it to pick the expression they settle into afterwards).</summary>
        public static event System.Action<Kid, Line> LineStarted;

        public static bool IsSpeaking(CastId id) => s_I && s_I.m_SpeakingUntil.TryGetValue(id, out var t) && t > Time.time;

        public static void Ensure()
        {
            if (s_I) return;
            var go = new GameObject("[Frost Dialogue]");
            s_I = go.AddComponent<DialogueRunner>();
            s_I.m_Caption = CaptionBar.Create();
        }

        /// <summary>Fire-and-forget line.</summary>
        public static void Say(Line line, Kid speaker, bool interrupt)
        {
            if (line == null || !speaker) return;
            Ensure();
            s_I.StartCoroutine(s_I.Speak(line, speaker, interrupt));
        }

        public static IEnumerator SayAndWait(Line line, Kid speaker, bool interrupt = true)
        {
            if (line == null || !speaker) yield break;
            Ensure();
            yield return s_I.StartCoroutine(s_I.Speak(line, speaker, interrupt));
        }

        IEnumerator Speak(Line line, Kid speaker, bool interrupt)
        {
            var id = speaker.id;
            if (IsSpeaking(id) && !interrupt) yield break;
            if (interrupt && IsSpeaking(id)) AudioDirector.StopAllVoices();
            LineStarted?.Invoke(speaker, line);
            float voiceLen = AudioDirector.PlayVoice(line, speaker.Rig.Head ? speaker.Rig.Head : speaker.transform);
            // subtitles stay up at least long enough to read
            float readLen = Mathf.Max(voiceLen, 0.9f + line.Text.Length * 0.045f);
            readLen = Mathf.Min(readLen, voiceLen + 1.6f);
            m_SpeakingUntil[id] = Time.time + voiceLen;
            if (FrostPrefs.Subtitles)
            {
                if (!m_Bubbles.TryGetValue(id, out var bubble) || !bubble)
                    m_Bubbles[id] = bubble = SpeechBubble.Create(speaker);
                bubble.Show(line.Text, readLen);
                m_Caption.Offer(speaker, line.Text, readLen);
            }
            float end = Time.time + readLen;
            while (Time.time < end) yield return null;
        }
    }

    /// <summary>
    /// A Horizon-style dark tooltip that floats above a kid and turns to face you: a colour-coded name chip,
    /// crisp white text that types on, and a little tail pointing at the speaker.
    /// </summary>
    public class SpeechBubble : MonoBehaviour
    {
        const float MaxTextW = 0.4f, PadX = 0.022f, PadY = 0.018f, TextSize = 0.205f;
        Kid m_Kid;
        TextMeshPro m_Text;
        Transform m_Shape;
        float m_Until, m_Shown, m_H;
        string m_Full;

        public static SpeechBubble Create(Kid kid)
        {
            var go = new GameObject("Speech Bubble · " + kid.DisplayName);
            go.layer = Layers.UI3D;
            var b = go.AddComponent<SpeechBubble>();
            b.m_Kid = kid;
            b.m_Text = HUI.Label(go.transform, "", UIKit.Face.Body, TextSize, Palette.UiText, new Vector2(MaxTextW, 0.1f), TextAlignmentOptions.TopLeft);
            b.m_Text.lineSpacing = -4f;
            go.transform.localScale = Vector3.zero;
            return b;
        }

        public void Show(string text, float seconds)
        {
            m_Full = text;
            m_Text.text = text;
            m_Text.maxVisibleCharacters = 0;
            var size = UIKit.Measure(m_Text, text, MaxTextW);
            float w = Mathf.Max(size.x, 0.14f) + PadX * 2f, h = size.y + PadY * 2f + 0.022f;
            m_H = h;
            m_Text.rectTransform.sizeDelta = new Vector2(size.x + 0.004f, size.y + 0.01f);
            m_Text.transform.localPosition = new Vector3(-w * 0.5f + PadX + size.x * 0.5f + 0.002f, -0.002f, -0.003f);
            if (m_Shape) Destroy(m_Shape.gameObject);
            m_Shape = new GameObject("Shape").transform;
            m_Shape.SetParent(transform, false);
            m_Shape.gameObject.layer = Layers.UI3D;
            HUI.Panel(m_Shape, "Body", new Vector2(w, h), 0.026f, true, false);
            var tail = UIKit.Quad(m_Shape, "Tail", UIKit.Tail(0.034f, 0.026f, -0.004f), Palette.Unlit(HUI.GlassBottom), -0.0004f);
            tail.localPosition = new Vector3(-w * 0.5f + 0.05f, -h * 0.5f + 0.003f, -0.0004f);
            var tailStroke = UIKit.Quad(m_Shape, "Tail Stroke", UIKit.Tail(0.039f, 0.03f, -0.004f), Palette.Unlit(HUI.StrokeSolid), 0.0012f);
            tailStroke.localPosition = new Vector3(-w * 0.5f + 0.05f, -h * 0.5f + 0.0035f, 0.0012f);
            var chip = UIKit.Pill(m_Shape, "Name", m_Kid.DisplayName.ToUpperInvariant(), UIKit.Face.Heading, 0.14f, m_Kid.Color, Color.white, out float cw, 0.034f, 0.014f);
            chip.localPosition = new Vector3(-w * 0.5f + PadX + cw * 0.5f - 0.006f, h * 0.5f, -0.004f);
            m_Shape.gameObject.SetLayerRecursive(Layers.UI3D);
            m_Until = Time.time + seconds;
            m_Shown = Time.time;
            gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            if (!m_Kid) { Destroy(gameObject); return; }
            var rig = PlayerRig.Active;
            var anchor = m_Kid.BubbleAnchor.position;
            float s = transform.localScale.x;
            transform.position = anchor + Vector3.up * ((m_H * 0.5f + 0.03f) * Mathf.Max(s, 0.5f));
            if (rig)
            {
                var toHead = rig.Head.position - transform.position;
                if (toHead.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(-toHead.normalized, Vector3.up);
                // constant-ish angular size so it reads from across the room
                float target = Time.time < m_Until ? Mathf.Clamp(toHead.magnitude / 1.3f, 0.85f, 1.8f) : 0f;
                float pop = Mathf.Clamp01((Time.time - m_Shown) / 0.22f);
                transform.localScale = Vector3.one * (Time.time < m_Until ? target * Mathf.Lerp(0.85f, 1f, Ease.OutBack(pop)) : Ease.Damp(s, 0f, 16f, Time.deltaTime));
            }
            if (m_Full != null) m_Text.maxVisibleCharacters = Mathf.Clamp(Mathf.FloorToInt((Time.time - m_Shown) * 50f), 0, m_Full.Length);
        }
    }

    /// <summary>When the speaker is outside your view: a caption pill under your gaze with an arrow toward them.</summary>
    public class CaptionBar : MonoBehaviour
    {
        TextMeshPro m_Text;
        Transform m_Shape, m_ArrowL, m_ArrowR;
        float m_Until;
        Kid m_Speaker;
        bool m_Visible;

        public static CaptionBar Create()
        {
            var go = new GameObject("Caption Bar");
            go.layer = Layers.UI3D;
            var c = go.AddComponent<CaptionBar>();
            c.m_Text = HUI.Label(go.transform, "", UIKit.Face.Body, 0.19f, Palette.UiText, new Vector2(0.6f, 0.1f), TextAlignmentOptions.Left);
            go.SetActive(true);
            return c;
        }

        public void Offer(Kid speaker, string text, float seconds)
        {
            m_Speaker = speaker;
            string name = "<color=#" + ColorUtility.ToHtmlStringRGB(Color.Lerp(speaker.Color, Color.white, 0.45f)) + "><b>" + speaker.DisplayName + "</b></color>   ";
            m_Text.text = name + text;
            m_Until = Time.time + seconds;
            var size = UIKit.Measure(m_Text, m_Text.text, 0.62f);
            float w = size.x + 0.09f, h = size.y + 0.03f;
            m_Text.rectTransform.sizeDelta = new Vector2(size.x + 0.004f, size.y + 0.01f);
            m_Text.transform.localPosition = new Vector3(0.012f, 0, -0.003f);
            if (m_Shape) Destroy(m_Shape.gameObject);
            m_Shape = new GameObject("Shape").transform;
            m_Shape.SetParent(transform, false);
            HUI.Panel(m_Shape, "Pill", new Vector2(w, h), Mathf.Min(h * 0.5f, 0.03f));
            m_ArrowL = UIKit.IconQuad(m_Shape, UIKit.Icon.ArrowLeft, 0.016f, Palette.UiTextDim, -0.003f);
            m_ArrowL.localPosition = new Vector3(-w * 0.5f + 0.022f, 0, -0.003f);
            m_ArrowR = UIKit.IconQuad(m_Shape, UIKit.Icon.ArrowRight, 0.016f, Palette.UiTextDim, -0.003f);
            m_ArrowR.localPosition = new Vector3(w * 0.5f - 0.022f, 0, -0.003f);
            gameObject.SetLayerRecursive(Layers.UI3D);
            m_Shape.gameObject.SetActive(m_Visible);
            m_Text.enabled = m_Visible;
        }

        void LateUpdate()
        {
            var rig = PlayerRig.Active;
            if (!rig) return;
            bool paused = GameManager.I && GameManager.I.IsPaused;
            bool show = Time.time < m_Until && m_Speaker && !paused && !SpeakerVisible(rig);
            if (show != m_Visible) { m_Visible = show; m_Text.enabled = show; if (m_Shape) m_Shape.gameObject.SetActive(show); }
            if (!show) return;
            var head = rig.Head;
            var fwd = rig.FlatForward;
            var target = HToast.GazeAnchor(rig, 1.1f, 27f);
            transform.position = Ease.Damp(transform.position, target, 5f, Time.deltaTime);
            transform.rotation = Quaternion.LookRotation((transform.position - head.position).normalized, Vector3.up);
            // which side is the speaker on?
            var to = (m_Speaker.HeadPoint - head.position).Flat();
            float side = Vector3.Dot(Vector3.Cross(fwd, to.normalized), Vector3.up);
            if (m_ArrowL) m_ArrowL.gameObject.SetActive(side < -0.05f);
            if (m_ArrowR) m_ArrowR.gameObject.SetActive(side > 0.05f);
        }

        bool SpeakerVisible(PlayerRig rig)
        {
            var cam = rig.ViewCamera;
            if (!cam) return true;
            var vp = cam.WorldToViewportPoint(m_Speaker.BubbleAnchor.position);
            return vp.z > 0f && vp.x > 0.1f && vp.x < 0.9f && vp.y > 0.05f && vp.y < 0.95f;
        }
    }
}
