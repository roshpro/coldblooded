using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// The title: your real room, nothing in it, and one chunky blue Play button floating at arm's reach, gently
    /// bobbing. Push it with a fingertip (or the tip of a controller): it clicks in, buzzes, pops, and the intro
    /// begins. (A pointer click works too, for the desktop stand-in.)
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        public System.Action OnStart;
        Transform m_Content;
        PokeButton m_Play;
        bool m_Dismissing, m_Started;

        public static TitleScreen Create(System.Action onStart)
        {
            var go = new GameObject("Title Screen");
            go.layer = Layers.UI3D;
            var t = go.AddComponent<TitleScreen>();
            t.OnStart = onStart;
            t.Build();
            var f = go.AddComponent<HFollow>();
            f.distance = 0.42f; f.drop = 0.17f; f.maxAngle = 40f;
            f.SnapNow();
            return t;
        }

        void Build()
        {
            m_Content = new GameObject("Content").transform;
            m_Content.SetParent(transform, false);
            m_Content.gameObject.layer = Layers.UI3D;
            m_Play = PokeButton.Create(m_Content, Begin);
            m_Content.gameObject.SetLayerRecursive(Layers.UI3D);
            m_Content.localScale = Vector3.zero;
            StartCoroutine(Ease.Tween(0.4f, k => m_Content.localScale = Vector3.one * Ease.OutBack(k), Ease.Linear, true));
        }

        void Begin()
        {
            if (m_Started) return;
            m_Started = true;
            OnStart?.Invoke();
        }

        void Update()
        {
            if (m_Dismissing || !m_Play) return;
            // a gentle bob, and a slow pulse that invites a press
            float t = Time.unscaledTime;
            m_Play.transform.localPosition = new Vector3(0f, 0.008f * Mathf.Sin(t * 2f * Mathf.PI / 2.6f), 0f);
            if (!m_Started) m_Play.Pulse = 0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI / 1.6f);
        }

        /// <summary>Pops and disappears.</summary>
        public IEnumerator Dismiss()
        {
            m_Dismissing = true;
            yield return new WaitForSecondsRealtime(0.12f);
            var from = m_Content.localScale;
            yield return Ease.Tween(0.1f, k => m_Content.localScale = from * Mathf.Lerp(1f, 1.18f, k), Ease.OutCubic, true);
            AudioDirector.Play2D(Sfx.Plop, 0.8f, 1.2f);
            Fx.SnowPuff(m_Play ? m_Play.transform.position : transform.position, -transform.forward, 16, 0.5f);
            yield return Ease.Tween(0.12f, k => m_Content.localScale = from * Mathf.Lerp(1.18f, 0f, k), Ease.InCubic, true);
            Destroy(gameObject);
        }
    }

    /// <summary>A round, palm-sized, bright-blue push button with a white play triangle and a soft blue glow. Pressed
    /// by a fingertip (index tip with hand tracking, the controller's tip otherwise) pushing in from the front, or by
    /// a pointer click.</summary>
    public class PokeButton : HControl
    {
        const float R = 0.05f;          // palm-sized: 10 cm across
        const float Travel = 0.016f;    // how far it pushes in
        const float FireAt = 0.011f;    // pushed this far in: click
        static readonly Color Blue = new Color32(30, 136, 255, 255), BlueHover = new Color32(70, 160, 255, 255), Rim = new Color32(14, 86, 196, 255), Glow = new Color32(80, 170, 255, 255);

        public System.Action onPress;
        /// <summary>0..1 idle pulse (glow and a hint of scale), driven by the title screen.</summary>
        public float Pulse;
        Transform m_Body;
        Material m_Face;
        readonly List<Material> m_Glow = new List<Material>();
        readonly bool[] m_Armed = new bool[2];
        float m_Push, m_PushVis;
        bool m_Fired;

        public static PokeButton Create(Transform parent, System.Action onPress)
        {
            var go = new GameObject("Play Button");
            go.transform.SetParent(parent, false);
            go.layer = Layers.UI3D;
            var b = go.AddComponent<PokeButton>();
            b.Build();
            b.onPress = onPress;
            b.onClick = b.Fire;
            return b;
        }

        void Build()
        {
            // soft glow: a few faint rings behind
            float[] sizes = { 1.3f, 1.55f, 1.85f };
            float[] alphas = { 0.32f, 0.16f, 0.07f };
            for (int i = 0; i < sizes.Length; i++)
            {
                float d = R * 2f * sizes[i];
                var m = Palette.Unlit(Glow.WithAlpha(alphas[i]), true);
                m_Glow.Add(m);
                var q = UIKit.Quad(transform, "Glow", UIKit.RoundedRect(d, d, d * 0.5f, 24), m, 0.02f + i * 0.001f);
                q.GetComponent<MeshRenderer>().sortingOrder = UIKit.Order.Shadow;
            }
            m_Body = new GameObject("Body").transform;
            m_Body.SetParent(transform, false);
            m_Body.gameObject.layer = Layers.UI3D;
            // a chunky puck: dark-blue side, a rim and the bright face
            ProcMesh.Part("Side", m_Body, ProcMesh.Cylinder(40), Palette.Toon(Rim, false, 0.3f), new Vector3(0f, 0f, 0.011f), new Vector3(R * 2f + 0.006f, 0.02f, R * 2f + 0.006f), new Vector3(90f, 0f, 0f))
                .GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            UIKit.Quad(m_Body, "Rim", UIKit.RoundedRect(R * 2f + 0.006f, R * 2f + 0.006f, R + 0.003f, 24), Palette.Unlit(Rim), 0.0006f);
            m_Face = Palette.Unlit(Blue);
            UIKit.Quad(m_Body, "Face", UIKit.RoundedRect(R * 2f, R * 2f, R, 24), m_Face, 0f);
            var shine = UIKit.Quad(m_Body, "Shine", UIKit.RoundedRect(R * 0.9f, R * 0.4f, R * 0.2f, 12), Palette.Unlit(Color.white.WithAlpha(0.22f), true), -0.0006f);
            shine.localPosition = new Vector3(-R * 0.2f, R * 0.55f, -0.0006f);
            shine.localRotation = Quaternion.Euler(0f, 0f, 18f);
            var icon = UIKit.IconQuad(m_Body, UIKit.Icon.Play, R * 0.95f, Color.white, -0.0012f);
            icon.localPosition = new Vector3(R * 0.08f, 0f, -0.0012f);
            MakeCollider(new Vector2(R * 2f + 0.03f, R * 2f + 0.03f), 0.06f);
        }

        protected override void Update()
        {
            base.Update();
            if (m_Fired) return;
            var rig = PlayerRig.Active;
            m_Push = 0f;
            if (rig && rig.IsXR)
            {
                Poke(rig.Left, 0);
                Poke(rig.Right, 1);
            }
        }

        void Poke(HandRig hand, int i)
        {
            if (!hand.IsTracked) { m_Armed[i] = false; return; }
            Vector3 tip;
            if (hand.HasJoints) tip = hand.Joints[7];           // index fingertip
            else if (hand.Tip) tip = hand.Tip.position;        // the controller's tip
            else { m_Armed[i] = false; return; }
            var local = transform.InverseTransformPoint(tip);   // front faces -z: pushing in is +z
            float radial = new Vector2(local.x, local.y).magnitude;
            if (radial > R * 1.35f || local.z < -0.06f || local.z > 0.06f) { m_Armed[i] = false; return; }
            if (local.z < -0.004f) m_Armed[i] = true;          // approaching from the front
            if (!m_Armed[i]) return;
            m_Push = Mathf.Max(m_Push, Mathf.Clamp(local.z + 0.004f, 0f, Travel));
            if (local.z > FireAt)
            {
                hand.Haptic(0.6f, 0.06f);
                Click();
            }
        }

        void Fire()
        {
            if (m_Fired) return;
            m_Fired = true;
            m_Push = Travel;
            onPress?.Invoke();
        }

        protected override void Animate(float hover, float press)
        {
            float target = m_Fired ? Travel * 0.8f : Mathf.Max(m_Push, press * Travel);
            m_PushVis = Mathf.Lerp(m_PushVis, target, 1f - Mathf.Exp(-30f * Time.unscaledDeltaTime));
            if (m_Body)
            {
                m_Body.localPosition = new Vector3(0f, 0f, m_PushVis);
                m_Body.localScale = Vector3.one * (1f + 0.025f * Pulse * (m_Fired ? 0f : 1f));
            }
            if (m_Face) m_Face.SetColor("_BaseColor", Color.Lerp(Blue, BlueHover, Mathf.Max(hover, m_PushVis / Travel)));
            for (int i = 0; i < m_Glow.Count; i++)
            {
                float a = (i == 0 ? 0.32f : i == 1 ? 0.16f : 0.07f) * (0.75f + 0.5f * Pulse + 0.4f * hover);
                m_Glow[i].SetColor("_BaseColor", Glow.WithAlpha(a));
            }
        }
    }

    /// <summary>Pause: a compact panel with your settings, and a floating dock (Resume · Restart · Exit) beneath it.</summary>
    public class MenuScreen : MonoBehaviour
    {
        public static MenuScreen Open(System.Action resume, System.Action restart, string context)
        {
            var go = new GameObject("Menu");
            go.layer = Layers.UI3D;
            var m = go.AddComponent<MenuScreen>();
            const float W = 0.5f, H = 0.4f;
            var c = new GameObject("Content").transform;
            c.SetParent(go.transform, false);
            c.gameObject.layer = Layers.UI3D;
            var glass = HUI.Panel(c, "Panel", new Vector2(W, H));
            HUI.Winterize(glass.Root, new Vector2(W, H), HUI.PanelRadius, 11, true, false);
            var title = HUI.Label(c, "Paused", UIKit.Face.Display, 0.42f, Palette.UiText, new Vector2(W - 0.06f, 0.06f), TextAlignmentOptions.Left, 0f, false);
            title.transform.localPosition = new Vector3(0, H * 0.5f - 0.055f, -0.002f);
            var sub = HUI.Label(c, context, UIKit.Face.Regular, 0.16f, Palette.UiTextDim, new Vector2(W - 0.06f, 0.03f), TextAlignmentOptions.Left, 0f, false);
            sub.transform.localPosition = new Vector3(0, H * 0.5f - 0.098f, -0.002f);
            float y = H * 0.5f - 0.155f;
            foreach (var (label, desc, get, set) in new (string, string, System.Func<bool>, System.Action<bool>)[]
            {
                ("Lefty mode", "Camera in your right hand instead", () => FrostPrefs.LeftHanded, v => FrostPrefs.LeftHanded = v),
                ("Subtitles", "Speech bubbles & captions", () => FrostPrefs.Subtitles, v => FrostPrefs.Subtitles = v),
                ("Gesture hints", "Animated hand hints", () => FrostPrefs.Tips, v => FrostPrefs.Tips = v),
            })
            {
                var t = HToggle.Create(c, label, desc, new Vector2(W - 0.05f, 0.062f), get, set);
                t.transform.localPosition = new Vector3(0, y, -0.002f);
                y -= 0.07f;
            }
            // the dock
            const float DW = 0.34f, DH = 0.085f, B = 0.062f;
            var dock = HUI.Panel(c, "Dock", new Vector2(DW, DH), DH * 0.5f);
            HUI.Winterize(dock.Root, new Vector2(DW, DH), DH * 0.5f, 5, false, true, 0.55f);
            dock.Root.localPosition = new Vector3(0, -H * 0.5f - 0.026f - DH * 0.5f, -0.01f);
            var resumeB = HButton.Create(dock.Root, "Resume", HButton.Variant.Icon, new Vector2(B, B), UIKit.Icon.Play, resume);
            resumeB.transform.localPosition = new Vector3(-0.1f, 0, -0.003f);
            var restartB = HButton.Create(dock.Root, "Restart case", HButton.Variant.Icon, new Vector2(B, B), UIKit.Icon.Restart, restart);
            restartB.transform.localPosition = new Vector3(0, 0, -0.003f);
            var exitB = HButton.Create(dock.Root, "Exit game", HButton.Variant.Icon, new Vector2(B, B), UIKit.Icon.Off, () => Application.Quit());
            exitB.transform.localPosition = new Vector3(0.1f, 0, -0.003f);
            // primary resume as a big pill too (easiest target)
            var big = HButton.Create(c, "Resume", HButton.Variant.Primary, new Vector2(W - 0.05f, 0.058f), UIKit.Icon.Play, resume);
            big.transform.localPosition = new Vector3(0, -H * 0.5f + 0.045f, -0.004f);
            c.gameObject.SetLayerRecursive(Layers.UI3D);
            var f = go.AddComponent<HFollow>();
            f.distance = 0.85f; f.drop = 0.06f; f.maxAngle = 40f;
            m.StartCoroutine(Ease.Tween(0.22f, k => c.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, Ease.OutCubic(k)), Ease.Linear, true));
            return m;
        }
    }

    /// <summary>
    /// The ending (doc §6.7: "your photos fan out as a Case File collage"): the GUILTY shot as the hero, the verdict,
    /// the evidence chain as a row of photos, your stats as chips, and one primary action.
    /// </summary>
    public class ResultsScreen : MonoBehaviour
    {
        // the journal's colours: leather cover, cream pages, blue-black pen, red marker
        static readonly Color Leather = new Color32(110, 70, 44, 255), Page = new Color32(244, 235, 211, 255), PageEdge = new Color32(222, 208, 176, 255);
        static readonly Color Rule = new Color(0.45f, 0.62f, 0.85f, 0.35f), Margin = new Color(0.86f, 0.25f, 0.25f, 0.45f), Tape = new Color(0.95f, 0.9f, 0.74f, 0.82f);
        static Color Pen => Palette.Ink;
        static Color Marker => Palette.EvidenceRed;

        /// <summary>Case closed: an open scrapbook detective journal. Left page: the case number, the photo of Tony
        /// taped in, the CASE CLOSED stamp and a sticky note to play again. Right page: "It was TONY!" in red
        /// handwriting, the story, your stats in pen, and the evidence trail taped along the bottom.</summary>
        public static ResultsScreen Show(PolaroidPhoto guilty, List<PolaroidPhoto> evidence, string time, int photos, int shows, int wrong, int throws, System.Action again)
        {
            var go = new GameObject("Case File");
            go.layer = Layers.UI3D;
            var r = go.AddComponent<ResultsScreen>();
            const float P = 0.55f, H = 0.68f;   // one page
            var c = new GameObject("Content").transform;
            c.SetParent(go.transform, false);
            c.gameObject.layer = Layers.UI3D;

            // the book: cover, two pages, a shadow down the spine
            var cover = UIKit.Panel(c, "Cover", new Vector2(P * 2f + 0.05f, H + 0.05f), 0.02f, Leather, null, 0f, true, null, 0.3f);
            cover.Root.localPosition = new Vector3(0f, 0f, 0.004f);
            for (int side = -1; side <= 1; side += 2)
            {
                var edge = UIKit.Panel(c, "Page Edge", new Vector2(P, H), 0.006f, PageEdge, null, 0f, false);
                edge.Root.localPosition = new Vector3(side * (P * 0.5f + 0.004f), -0.004f, 0.002f);
                var page = UIKit.Panel(c, side < 0 ? "Left Page" : "Right Page", new Vector2(P, H), 0.006f, Page, null, 0f, false);
                page.Root.localPosition = new Vector3(side * P * 0.5f, 0f, 0f);
            }
            Strip(c, "Spine", new Vector2(0.05f, H), new Color(0.25f, 0.16f, 0.08f, 0.22f), new Vector3(0f, 0f, -0.0005f), 0f);
            Strip(c, "Spine Crease", new Vector2(0.008f, H), new Color(0.25f, 0.16f, 0.08f, 0.35f), new Vector3(0f, 0f, -0.0006f), 0f);

            // right page: ruled lines and a red margin
            float rx0 = 0.02f, rx1 = P - 0.02f, mx = rx0 + 0.055f;
            for (float y = H * 0.5f - 0.11f; y > -H * 0.5f + 0.03f; y -= 0.034f)
                Strip(c, "Rule", new Vector2(rx1 - rx0, 0.0014f), Rule, new Vector3((rx0 + rx1) * 0.5f, y, -0.0008f), 0f);
            Strip(c, "Margin", new Vector2(0.0018f, H - 0.02f), Margin, new Vector3(mx, 0f, -0.0009f), 0f);

            // ---------------- left page
            float lc = -P * 0.5f;
            var file = Script(c, "Case #0001", 0.42f, Pen, new Vector2(P - 0.1f, 0.06f), TextAlignmentOptions.Left);
            file.transform.localPosition = new Vector3(lc, H * 0.5f - 0.06f, -0.003f);
            var date = Script(c, "Maple Street, the morning after the snow", 0.26f, Pen.WithAlpha(0.75f), new Vector2(P - 0.1f, 0.04f), TextAlignmentOptions.Left);
            date.transform.localPosition = new Vector3(lc, H * 0.5f - 0.105f, -0.003f);

            // the culprit's photo, taped in at an angle
            var hero = new GameObject("Culprit Photo").transform;
            hero.SetParent(c, false);
            hero.localPosition = new Vector3(lc + 0.015f, 0.015f, -0.004f);
            hero.localRotation = Quaternion.Euler(0f, 0f, -4f);
            const float heroScale = 2.3f;
            float hw = PolaroidPhoto.Width * heroScale, hh = PolaroidPhoto.Height * heroScale;
            TapeOn(hero, new Vector3(-hw * 0.45f, hh * 0.47f, 0f), 35f, 0.075f);
            TapeOn(hero, new Vector3(hw * 0.45f, hh * 0.47f, 0f), -35f, 0.075f);
            var caught = Script(c, "caught red-handed!", 0.3f, Marker, new Vector2(P - 0.1f, 0.04f), TextAlignmentOptions.Center);
            caught.transform.localPosition = new Vector3(lc + 0.02f, 0.015f - hh * 0.5f - 0.035f, -0.003f);
            caught.transform.localRotation = Quaternion.Euler(0f, 0f, 2f);

            // the stamp
            var stamp = new GameObject("Stamp").transform;
            stamp.SetParent(c, false);
            stamp.localPosition = new Vector3(lc + 0.12f, -H * 0.5f + 0.085f, -0.012f);
            stamp.localRotation = Quaternion.Euler(0f, 0f, 9f);
            var stampText = UIKit.Text(stamp, "Stamp Text", "CASE CLOSED", UIKit.Face.Display, 0.36f, Palette.StampRed.WithAlpha(0.85f), new Vector2(0.3f, 0.06f), TextAlignmentOptions.Center, 4f, false);
            stampText.transform.localPosition = new Vector3(0f, 0f, -0.001f);
            UIKit.Quad(stamp, "Stamp Frame", UIKit.RoundedFrame(0.27f, 0.07f, 0.008f, 0.0045f, 3), Palette.Unlit(Palette.StampRed.WithAlpha(0.8f), true), 0f)
                .GetComponent<MeshRenderer>().sortingOrder = UIKit.Order.Text;

            // play again: a sticky note in the corner
            var againB = HButton.Create(c, "Play again", HButton.Variant.Note, new Vector2(0.15f, 0.085f), null, again);
            againB.transform.localPosition = new Vector3(-P + 0.11f, -H * 0.5f + 0.08f, -0.014f);
            againB.transform.localRotation = Quaternion.Euler(0f, 0f, -5f);

            // ---------------- right page
            float tx = mx + 0.02f, tw = rx1 - tx;
            float tcx = tx + tw * 0.5f;
            // a little narrower than the page's writing space: the tilt and the bold script lean past the box otherwise
            var head = Script(c, "It was TONY!", 0.95f, Marker, new Vector2(tw * 0.86f, 0.12f), TextAlignmentOptions.Left);
            head.fontStyle = FontStyles.Bold;
            head.transform.localPosition = new Vector3(tx + tw * 0.43f, H * 0.5f - 0.075f, -0.003f);
            head.transform.localRotation = Quaternion.Euler(0f, 0f, 2.5f);
            // underlined twice, quickly
            Strip(c, "Underline", new Vector2(tw * 0.78f, 0.005f), Marker, new Vector3(tx + tw * 0.4f, H * 0.5f - 0.128f, -0.003f), 1.5f);
            Strip(c, "Underline 2", new Vector2(tw * 0.6f, 0.004f), Marker.WithAlpha(0.85f), new Vector3(tx + tw * 0.34f, H * 0.5f - 0.139f, -0.003f), -1f);

            var story = Script(c, "Tony \"Turbo\" Ramirez used Frosty as a ramp for the sickest jump in Maple Street history. It failed, obviously.",
                0.27f, Pen, new Vector2(tw, 0.11f), TextAlignmentOptions.TopLeft, true);
            story.transform.localPosition = new Vector3(tcx, H * 0.5f - 0.2f, -0.003f);

            string wrongLine = wrong == 0 ? "No wrong suspects - flawless!" : $"Wrong kid shown a photo: {wrong}";
            var lines = new[]
            {
                $"Time on the case:  {time}",
                $"Photos snapped:  {photos}",
                $"Photos shown:  {shows}",
                $"Snowballs thrown:  {throws}",
                wrongLine,
            };
            float ly = H * 0.5f - 0.285f;
            for (int i = 0; i < lines.Length; i++)
            {
                var t = Script(c, "- " + lines[i], 0.27f, i == lines.Length - 1 && wrong == 0 ? Marker : Pen, new Vector2(tw, 0.034f), TextAlignmentOptions.Left);
                t.transform.localPosition = new Vector3(tcx, ly - i * 0.034f, -0.003f);
            }

            // the evidence trail, taped along the bottom
            var trail = Script(c, "The evidence trail:", 0.27f, Pen, new Vector2(tw, 0.034f), TextAlignmentOptions.Left);
            trail.fontStyle = FontStyles.Bold | FontStyles.Underline;
            trail.transform.localPosition = new Vector3(tcx, -H * 0.5f + 0.2f, -0.003f);
            string[] labels = { "cocoa splash", "carrot bits", "purple glitter", "red scrape", "dented sled" };
            float span = rx1 - rx0 - 0.01f, slotW = span / 5f;
            var slots = new List<Transform>();
            for (int i = 0; i < 5; i++)
            {
                var slot = new GameObject("Slot " + i).transform;
                slot.SetParent(c, false);
                float sx = rx0 + 0.005f + slotW * (i + 0.5f);
                slot.localPosition = new Vector3(sx, -H * 0.5f + 0.105f + (i % 2 == 0 ? 0.004f : -0.004f), -0.004f);
                slot.localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? -1f : 1f) * 3f);
                TapeOn(slot, new Vector3(0f, PolaroidPhoto.Height * 0.52f, 0f), (i % 2 == 0 ? 8f : -8f), 0.045f);
                var cap = Script(c, labels[i], 0.2f, Pen, new Vector2(slotW, 0.04f), TextAlignmentOptions.Center);
                cap.transform.localPosition = new Vector3(sx, -H * 0.5f + 0.03f, -0.003f);
                slots.Add(slot);
            }

            c.gameObject.SetLayerRecursive(Layers.UI3D);
            var f = go.AddComponent<HFollow>();
            f.distance = 1.15f; f.drop = 0.1f; f.maxAngle = 45f;
            f.SnapNow();
            r.StartCoroutine(r.Animate(c, hero, guilty, evidence, slots));
            return r;
        }

        static TextMeshPro Script(Transform parent, string text, float size, Color color, Vector2 box, TextAlignmentOptions align, bool wrap = false)
        {
            var t = UIKit.Text(parent, "Handwriting", text, UIKit.Face.Hand, size, color, box, align, 0f, wrap);
            t.font = UIKit.ScriptFont();
            t.fontStyle = FontStyles.Normal;
            // handwriting never runs off the page: a long line (a big number, a long time) shrinks to fit its box
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = size * 0.55f;
            return t;
        }

        /// <summary>A flat strip of colour (ruled lines, underlines, the spine shadow).</summary>
        static Transform Strip(Transform parent, string name, Vector2 size, Color color, Vector3 pos, float angle)
        {
            var q = UIKit.Quad(parent, name, ProcMesh.Quad(), Palette.Unlit(color, color.a < 0.999f), pos.z);
            q.localPosition = pos;
            q.localRotation = Quaternion.Euler(0f, 0f, angle);
            q.localScale = new Vector3(size.x, size.y, 1f);
            q.GetComponent<MeshRenderer>().sortingOrder = UIKit.Order.Decor;
            return q;
        }

        /// <summary>A strip of masking tape over whatever gets stuck here (drawn in front of the photo).</summary>
        static void TapeOn(Transform holder, Vector3 at, float angle, float length)
        {
            var q = UIKit.Quad(holder, "Tape", ProcMesh.Quad(), Palette.Unlit(Tape, true), -0.012f);
            q.localPosition = new Vector3(at.x, at.y, -0.012f);
            q.localRotation = Quaternion.Euler(0f, 0f, angle);
            q.localScale = new Vector3(length, length * 0.32f, 1f);
            q.GetComponent<MeshRenderer>().sortingOrder = UIKit.Order.Overlay;
        }

        IEnumerator Animate(Transform content, Transform hero, PolaroidPhoto guilty, List<PolaroidPhoto> evidence, List<Transform> slots)
        {
            yield return Ease.Tween(0.3f, k => content.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, Ease.OutCubic(k)), Ease.Linear, true);
            if (guilty) StartCoroutine(FlyTo(guilty.transform, hero, new Vector3(0, 0, -0.004f), Quaternion.identity, 2.3f, 0f));
            for (int i = 0; i < evidence.Count && i < slots.Count; i++)
                if (evidence[i]) StartCoroutine(FlyTo(evidence[i].transform, slots[i], new Vector3(0, 0, -0.004f), Quaternion.identity, 1f, 0.15f + i * 0.1f));
        }

        IEnumerator FlyTo(Transform t, Transform parent, Vector3 localPos, Quaternion localRot, float scale, float delay)
        {
            t.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(delay);
            t.SetParent(parent, true);
            var p0 = t.localPosition; var r0 = t.localRotation; var s0 = t.localScale;
            AudioDirector.Play2D(Sfx.Whoosh, 0.3f, 1.5f);
            yield return Ease.Tween(0.45f, k =>
            {
                t.localPosition = Vector3.Lerp(p0, localPos, k);
                t.localRotation = Quaternion.Slerp(r0, localRot, k);
                t.localScale = Vector3.Lerp(s0, Vector3.one * scale, k);
            }, Ease.OutBack, true);
            t.gameObject.SetLayerRecursive(Layers.UI3D);
        }
    }
}
