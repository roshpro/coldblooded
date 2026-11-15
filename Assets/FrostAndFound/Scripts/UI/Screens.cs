using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// The home screen: one big round Play button floating in front of you, with the scoop &amp; throw gesture
    /// looping under it. Throw a snowball at the button to open the case (pressing it works too).
    /// </summary>
    public class TitleScreen : MonoBehaviour, ISnowballTarget
    {
        const float ButtonSize = 0.24f;
        const float HitRadius = ButtonSize * 0.5f * 1.3f;   // a little forgiving around the rim

        public System.Action OnStart;
        Transform m_Content;
        HButton m_Play;
        GestureCard m_Gesture;
        bool m_Dismissing, m_Started;
        readonly Dictionary<Snowball, Vector3> m_Balls = new Dictionary<Snowball, Vector3>();
        readonly List<Snowball> m_Scratch = new List<Snowball>();

        // ---- ISnowballTarget (also gives throws the usual light aim assist toward the button)
        public Vector3 AimPoint => m_Play ? m_Play.transform.position : transform.position;
        public Vector3 Velocity => Vector3.zero;
        public bool AssistEligible => !m_Started;

        public void OnSnowballHit(Snowball ball, Vector3 point, Vector3 velocity, bool headshot) => Begin(point);

        void OnEnable() { SnowballTargets.Register(this); Snowball.Thrown += OnThrown; }
        void OnDisable() { SnowballTargets.Unregister(this); Snowball.Thrown -= OnThrown; }

        void OnThrown(Snowball ball) { if (!ball.Thrower) m_Balls[ball] = ball.transform.position; }

        void Begin(Vector3? hitPoint)
        {
            if (m_Started) return;
            m_Started = true;
            if (hitPoint.HasValue)
            {
                Fx.SnowPuff(hitPoint.Value, -m_Play.transform.forward, 26, 1f);
                PlayerRig.Active?.FreeHand.Haptic(0.6f, 0.1f);
                Debug.Log("[Frost] Title: Play hit by a snowball");
            }
            OnStart?.Invoke();
        }

        /// <summary>Snowballs pass straight through UI colliders (triggers), so check each flight segment against the button's disc.</summary>
        void CheckBalls()
        {
            if (m_Balls.Count == 0 || !m_Play) return;
            var center = m_Play.transform.position;
            var n = m_Play.transform.forward;
            m_Scratch.Clear();
            m_Scratch.AddRange(m_Balls.Keys);
            foreach (var ball in m_Scratch)
            {
                if (!ball || !ball.IsFlying) { m_Balls.Remove(ball); continue; }
                var from = m_Balls[ball];
                var to = ball.transform.position;
                m_Balls[ball] = to;
                float d0 = Vector3.Dot(from - center, n), d1 = Vector3.Dot(to - center, n);
                bool crossed = Mathf.Sign(d0) != Mathf.Sign(d1);
                if (!crossed && Mathf.Abs(d1) > 0.05f) continue;   // hasn't reached the button's plane
                var hit = crossed ? Vector3.Lerp(from, to, d0 / (d0 - d1)) : to;
                if (Vector3.Distance(hit, center) > HitRadius) continue;
                m_Balls.Clear();
                ball.HitTarget(this, hit, -n);
                return;
            }
        }

        public static TitleScreen Create(System.Action onStart)
        {
            var go = new GameObject("Title Screen");
            go.layer = Layers.UI3D;
            var t = go.AddComponent<TitleScreen>();
            t.OnStart = onStart;
            t.Build();
            var f = go.AddComponent<HFollow>();
            f.distance = 0.9f; f.drop = 0.12f; f.maxAngle = 45f;
            return t;
        }

        void Build()
        {
            m_Content = new GameObject("Content").transform;
            m_Content.SetParent(transform, false);
            m_Content.gameObject.layer = Layers.UI3D;
            m_Play = HButton.Create(m_Content, "Play", HButton.Variant.Round, new Vector2(ButtonSize, ButtonSize), UIKit.Icon.Play, () => Begin(null));
            m_Play.transform.localPosition = new Vector3(0, 0, -0.004f);
            // the game's name over the button (a soft dark outline so it reads against any room)
            var title = HUI.Label(m_Content, "Cold Blooded", UIKit.Face.Display, 0.62f, Palette.UiText, new Vector2(0.6f, 0.09f), TextAlignmentOptions.Center, 0f, false);
            title.transform.localPosition = new Vector3(0, ButtonSize * 0.5f + 0.075f, -0.004f);
            title.outlineWidth = 0.14f;
            title.outlineColor = new Color32(18, 28, 58, 190);
            // how to start: scoop snow and throw it at the button
            m_Gesture = GestureCard.Create(m_Content, GestureKind.ScoopThrow, 0.15f);
            m_Gesture.transform.localPosition = new Vector3(0, -ButtonSize * 0.5f - 0.03f - 0.075f, -0.002f);
            m_Gesture.Show();
            m_Content.gameObject.SetLayerRecursive(Layers.UI3D);
            m_Content.localScale = Vector3.one * 0.9f;
            StartCoroutine(Ease.Tween(0.35f, k => m_Content.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, Ease.OutCubic(k)), Ease.Linear, true));
        }

        void Update()
        {
            if (!m_Started) CheckBalls();
            // a slow "breathing" pulse that invites a throw
            if (m_Dismissing || !m_Play) return;
            m_Play.transform.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / 1.6f)));
        }

        public IEnumerator Dismiss()
        {
            m_Dismissing = true;
            var from = m_Content.localScale;
            yield return Ease.Tween(0.22f, k => m_Content.localScale = from * Mathf.Lerp(1f, 0.9f, k), Ease.InCubic, true);
            Destroy(gameObject);
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
                ("Tips", "Short control hints", () => FrostPrefs.Tips, v => FrostPrefs.Tips = v),
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

    /// <summary>"Finding your room…" while the headset's Space Setup loads, with an escape hatch after a few seconds.</summary>
    public class LoadingCard : MonoBehaviour
    {
        Transform m_Spinner;
        HButton m_Skip;
        float m_Born;
        System.Action m_OnSkip;

        public static LoadingCard Show(string text, string sub, System.Action onSkip)
        {
            var go = new GameObject("Loading");
            go.layer = Layers.UI3D;
            var l = go.AddComponent<LoadingCard>();
            l.m_Born = Time.unscaledTime;
            l.m_OnSkip = onSkip;
            const float W = 0.5f, H = 0.2f;
            var glass = HUI.Panel(go.transform, "Panel", new Vector2(W, H), 0.04f);
            HUI.Winterize(glass.Root, new Vector2(W, H), 0.04f, 9, true, true, 0.6f);
            l.m_Spinner = UIKit.Quad(go.transform, "Spinner", UIKit.Arc(0.021f, 0.005f, 270f), Palette.Unlit(Palette.UiAccent), -0.002f);
            l.m_Spinner.localPosition = new Vector3(-W * 0.5f + 0.05f, 0.045f, -0.002f);
            var t = HUI.Label(go.transform, text, UIKit.Face.Body, 0.22f, Palette.UiText, new Vector2(W - 0.12f, 0.04f), TextAlignmentOptions.Left, 0f, false);
            t.transform.localPosition = new Vector3(0.035f, 0.055f, -0.002f);
            var s2 = HUI.Label(go.transform, sub, UIKit.Face.Regular, 0.155f, Palette.UiTextDim, new Vector2(W - 0.12f, 0.05f), TextAlignmentOptions.TopLeft);
            s2.transform.localPosition = new Vector3(0.035f, 0.005f, -0.002f);
            l.m_Skip = HButton.Create(go.transform, "Play without room scan", HButton.Variant.Secondary, new Vector2(W - 0.05f, 0.054f), UIKit.Icon.Play, () => l.m_OnSkip?.Invoke());
            l.m_Skip.transform.localPosition = new Vector3(0, -H * 0.5f + 0.042f, -0.004f);
            l.m_Skip.gameObject.SetActive(false);
            go.SetLayerRecursive(Layers.UI3D);
            var f = go.AddComponent<HFollow>(); f.distance = 1f; f.drop = 0.05f;
            return l;
        }

        void Update()
        {
            if (m_Spinner) m_Spinner.localRotation = Quaternion.Euler(0, 0, -Time.unscaledTime * 360f);
            if (m_Skip && !m_Skip.gameObject.activeSelf && Time.unscaledTime - m_Born > 6f) m_Skip.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// The ending (doc §6.7: "your photos fan out as a Case File collage"): the GUILTY shot as the hero, the verdict,
    /// the evidence chain as a row of photos, your stats as chips, and one primary action.
    /// </summary>
    public class ResultsScreen : MonoBehaviour
    {
        public static ResultsScreen Show(PolaroidPhoto guilty, List<PolaroidPhoto> evidence, string time, int photos, int shows, int wrong, int throws, System.Action again)
        {
            var go = new GameObject("Case File");
            go.layer = Layers.UI3D;
            var r = go.AddComponent<ResultsScreen>();
            const float W = 1.1f, H = 0.66f;
            var c = new GameObject("Content").transform;
            c.SetParent(go.transform, false);
            c.gameObject.layer = Layers.UI3D;
            var glass = HUI.Panel(c, "Panel", new Vector2(W, H));
            HUI.Winterize(glass.Root, new Vector2(W, H), HUI.PanelRadius, 17);

            // hero: the GUILTY photo on a dark stage
            const float heroW = 0.36f, heroH = H - 0.03f;
            var stage = HUI.Card(c, "Stage", new Vector2(heroW, heroH), 0.024f, Palette.UiDeep);
            stage.Root.localPosition = new Vector3(-W * 0.5f + 0.015f + heroW * 0.5f, 0, -0.001f);
            var over = HUI.Overline(stage.Root, "Exhibit A", Palette.UiTextDim, 0.12f);
            over.rectTransform.sizeDelta = new Vector2(heroW - 0.04f, 0.03f);
            over.transform.localPosition = new Vector3(0, heroH * 0.5f - 0.03f, -0.002f);

            // right side
            float rx = -W * 0.5f + 0.015f + heroW + 0.035f, rw = W - heroW - 0.08f;
            float cxr = rx + rw * 0.5f;
            var ov = HUI.Overline(c, "Case #0001 · closed", Palette.UiAccentHover, 0.13f);
            ov.rectTransform.sizeDelta = new Vector2(rw, 0.03f);
            ov.transform.localPosition = new Vector3(cxr, H * 0.5f - 0.05f, -0.002f);
            var head = HUI.Label(c, "It was Tony.", UIKit.Face.Display, 0.6f, Palette.UiText, new Vector2(rw, 0.08f), TextAlignmentOptions.Left, -1f, false);
            head.transform.localPosition = new Vector3(cxr, H * 0.5f - 0.1f, -0.002f);
            var sub = HUI.Label(c, "Tony \"Turbo\" Ramirez used Frosty as a ramp for the sickest jump in Maple Street history. It failed, obviously.",
                UIKit.Face.Regular, 0.17f, Palette.UiTextDim, new Vector2(rw, 0.06f), TextAlignmentOptions.TopLeft);
            sub.transform.localPosition = new Vector3(cxr, H * 0.5f - 0.175f, -0.002f);

            // stats chips
            float sx = rx;
            foreach (var (icon, val, label) in new[] { (UIKit.Icon.Clock, time, "time"), (UIKit.Icon.Camera, photos.ToString(), photos == 1 ? "photo" : "photos"),
                         (UIKit.Icon.Hand, shows.ToString(), "shown"), (UIKit.Icon.Snowball, throws.ToString(), throws == 1 ? "snowball" : "snowballs") })
            {
                var chip = Stat(c, icon, val, label, out float w);
                chip.localPosition = new Vector3(sx + w * 0.5f, H * 0.5f - 0.255f, -0.002f);
                sx += w + 0.012f;
            }

            // the evidence chain
            var ev = HUI.Overline(c, "The evidence chain", Palette.UiTextDim, 0.12f);
            ev.rectTransform.sizeDelta = new Vector2(rw, 0.03f);
            ev.transform.localPosition = new Vector3(cxr, H * 0.5f - 0.315f, -0.002f);
            string[] labels = { "Cocoa splash", "Carrot bits", "Purple glitter", "Red scrape", "Dented sled" };
            float slotW = rw / 5f;
            var slots = new List<Transform>();
            for (int i = 0; i < 5; i++)
            {
                var slot = new GameObject("Slot " + i).transform;
                slot.SetParent(c, false);
                slot.gameObject.layer = Layers.UI3D;
                slot.localPosition = new Vector3(rx + slotW * (i + 0.5f), H * 0.5f - 0.415f, -0.004f);
                var cap = HUI.Label(slot, labels[i], UIKit.Face.Body, 0.12f, Palette.UiTextDim, new Vector2(slotW, 0.025f), TextAlignmentOptions.Center, 0f, false);
                cap.transform.localPosition = new Vector3(0, -0.085f, -0.002f);
                if (i < 4)
                {
                    var arrow = UIKit.IconQuad(c, UIKit.Icon.ArrowRight, 0.014f, Palette.UiTextFaint, -0.003f);
                    arrow.localPosition = new Vector3(rx + slotW * (i + 1), H * 0.5f - 0.415f, -0.003f);
                }
                slots.Add(slot);
            }

            var againB = HButton.Create(c, "Play again", HButton.Variant.Primary, new Vector2(0.26f, 0.062f), UIKit.Icon.Restart, again);
            againB.transform.localPosition = new Vector3(rx + 0.13f, -H * 0.5f + 0.055f, -0.004f);
            var wrongL = HUI.Label(c, wrong == 0 ? "Flawless: no wrong suspects." : $"{wrong} photo{(wrong == 1 ? "" : "s")} shown to the wrong kid.", UIKit.Face.Regular, 0.15f, Palette.UiTextFaint,
                new Vector2(rw - 0.3f, 0.03f), TextAlignmentOptions.Left, 0f, false);
            wrongL.transform.localPosition = new Vector3(rx + 0.28f + (rw - 0.3f) * 0.5f, -H * 0.5f + 0.055f, -0.002f);

            c.gameObject.SetLayerRecursive(Layers.UI3D);
            var f = go.AddComponent<HFollow>();
            f.distance = 1.15f; f.drop = 0.1f; f.maxAngle = 45f;
            f.SnapNow();
            r.StartCoroutine(r.Animate(c, stage.Root, guilty, evidence, slots));
            return r;
        }

        static Transform Stat(Transform parent, UIKit.Icon icon, string value, string label, out float width)
        {
            var root = new GameObject("Stat · " + label).transform;
            root.SetParent(parent, false);
            root.gameObject.layer = Layers.UI3D;
            var v = HUI.Label(root, value, UIKit.Face.Body, 0.2f, Palette.UiText, new Vector2(0.2f, 0.03f), TextAlignmentOptions.Left, 0f, false);
            var l = HUI.Label(root, label, UIKit.Face.Regular, 0.15f, Palette.UiTextDim, new Vector2(0.2f, 0.03f), TextAlignmentOptions.Left, 0f, false);
            float vw = UIKit.Measure(v, value, 1f).x, lw = UIKit.Measure(l, label, 1f).x;
            float h = 0.046f, iconS = 0.02f;
            width = 0.014f + iconS + 0.01f + vw + 0.008f + lw + 0.016f;
            UIKit.Panel(root, "Shape", new Vector2(width, h), h * 0.5f, Palette.UiSurface, null, 0f, false);
            float x = -width * 0.5f + 0.014f;
            var ic = UIKit.IconQuad(root, icon, iconS, Palette.UiAccentHover, -0.002f);
            ic.localPosition = new Vector3(x + iconS * 0.5f, 0, -0.002f); x += iconS + 0.01f;
            v.rectTransform.sizeDelta = new Vector2(vw + 0.004f, h); v.transform.localPosition = new Vector3(x + vw * 0.5f, 0, -0.003f); x += vw + 0.008f;
            l.rectTransform.sizeDelta = new Vector2(lw + 0.004f, h); l.transform.localPosition = new Vector3(x + lw * 0.5f, 0, -0.003f);
            return root;
        }

        IEnumerator Animate(Transform content, Transform stage, PolaroidPhoto guilty, List<PolaroidPhoto> evidence, List<Transform> slots)
        {
            yield return Ease.Tween(0.3f, k => content.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, Ease.OutCubic(k)), Ease.Linear, true);
            if (guilty) StartCoroutine(FlyTo(guilty.transform, stage, new Vector3(0, -0.02f, -0.006f), Quaternion.Euler(0, 0, -3f), 3.0f, 0f));
            for (int i = 0; i < evidence.Count && i < slots.Count; i++)
                if (evidence[i]) StartCoroutine(FlyTo(evidence[i].transform, slots[i], new Vector3(0, 0, -0.004f), Quaternion.Euler(0, 0, (i % 2 == 0 ? -1 : 1) * 3f), 1.18f, 0.15f + i * 0.1f));
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
