using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>Renders the key art for the title panel: Frosty slumped in his chalk outline, in the snow.</summary>
    public static class TitleArt
    {
        static RenderTexture s_Art;

        public static Texture Get()
        {
            if (s_Art) return s_Art;
            s_Art = new RenderTexture(1344, 400, 24, RenderTextureFormat.ARGB32) { name = "Title Art", antiAliasing = 4 };
            s_Art.Create();
            CoroutineHost.Run(Render(s_Art));
            return s_Art;
        }

        static IEnumerator Render(RenderTexture target)
        {
            var origin = new Vector3(0, -200f, 0);
            var studio = new GameObject("Title Art Studio");
            studio.transform.position = origin;
            ProcMesh.Part("Snow", studio.transform, ProcMesh.Disc(48), Palette.Toon(Palette.Snow, false, 0.2f), origin + Vector3.down * 0.01f, new Vector3(9f, 0.02f, 9f));
            var sky = new GameObject("Sky");
            sky.transform.SetParent(studio.transform, false);
            sky.transform.position = origin;
            sky.transform.localScale = Vector3.one * 30f;
            sky.AddComponent<MeshFilter>().sharedMesh = ProcMesh.Sphere(24, 16);
            var skyMat = new Material(FrostAssets.Shader(FrostAssets.ShaderId.Sky));
            skyMat.SetColor("_TopColor", Palette.SkyTop); skyMat.SetColor("_HorizonColor", Palette.SkyHorizon); skyMat.SetColor("_BottomColor", Palette.SkyGround);
            skyMat.SetFloat("_Flakes", 0.9f);
            sky.AddComponent<MeshRenderer>().sharedMaterial = skyMat;
            var frosty = Frosty.CreateProp(origin, Quaternion.Euler(0, 200f, 0));
            frosty.transform.SetParent(studio.transform, true);
            // drips leading out of frame, like the real scene
            for (int i = 0; i < 9; i++)
                ProcMesh.Part("Drip", studio.transform, ProcMesh.Sphere(8, 5), Palette.Toon(i % 3 == 0 ? Palette.CocoaLight : Palette.Cocoa, false), origin + new Vector3(-0.4f - i * 0.13f, 0.004f, -0.35f - i * 0.05f + Mathf.Sin(i) * 0.04f), new Vector3(0.05f, 0.01f, 0.06f));
            var camGo = new GameObject("Art Camera");
            camGo.transform.SetParent(studio.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 17f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.SkyHorizon;
            cam.cullingMask = Layers.Mask(Layers.Default, Layers.Kid);
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 40f;
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            cam.depth = -20;
            var data = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            // store-style hero framing: Frosty sits in the right third, the left stays calm for the title
            camGo.transform.position = origin + new Vector3(-1.1f, 1.3f, -3.1f);
            camGo.transform.LookAt(origin + new Vector3(0f, 0.34f, 0f));
            camGo.transform.Rotate(Vector3.up, -12.5f, Space.World);
            cam.targetTexture = target;
            cam.aspect = target.width / (float)target.height;
            // render for a couple of real frames (shaders warm up), then tear the studio down
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();
            cam.enabled = false;
            cam.targetTexture = null;   // destroying a camera releases its target texture — detach first
            Object.Destroy(studio);
        }

        static Texture2D s_Scrim;
        /// <summary>Hero scrim: dark on the left (under the title) and along the bottom, clear over the art.</summary>
        public static Texture2D Scrim
        {
            get
            {
                if (s_Scrim) return s_Scrim;
                const int W = 128, H = 48;
                s_Scrim = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Scrim" };
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float u = x / (W - 1f), v = y / (H - 1f);
                        float left = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((0.68f - u) / 0.68f)) * 0.93f;
                        float bottom = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((0.5f - v) / 0.5f)) * 0.55f;
                        float a = 1f - (1f - left) * (1f - bottom);
                        s_Scrim.SetPixel(x, y, Palette.UiScrimBottom.WithAlpha(a));
                    }
                s_Scrim.Apply();
                return s_Scrim;
            }
        }
    }

    /// <summary>The home panel: key art, title, one big primary action, how-to-play, and settings.</summary>
    public class TitleScreen : MonoBehaviour
    {
        public System.Action OnStart;
        Transform m_Content;

        public static TitleScreen Create(System.Action onStart)
        {
            var go = new GameObject("Title Screen");
            go.layer = Layers.UI3D;
            var t = go.AddComponent<TitleScreen>();
            t.OnStart = onStart;
            t.Build();
            var f = go.AddComponent<HFollow>();
            f.distance = 1.1f; f.drop = 0.12f; f.maxAngle = 45f;
            return t;
        }

        void Build()
        {
            const float W = 1.04f, H = 0.7f;
            m_Content = new GameObject("Content").transform;
            m_Content.SetParent(transform, false);
            m_Content.gameObject.layer = Layers.UI3D;
            var glass = HUI.Panel(m_Content, "Panel", new Vector2(W, H));
            HUI.Winterize(glass.Root, new Vector2(W, H), HUI.PanelRadius, 3);

            // ---- hero
            const float heroW = W - 0.03f, heroH = 0.3f;
            float heroY = H * 0.5f - 0.015f - heroH * 0.5f;
            var hero = UIKit.Quad(m_Content, "Hero", UIKit.RoundedRect(heroW, heroH, 0.024f), Palette.UnlitTextured(TitleArt.Get(), Color.white, false, "Hero"), -0.001f);
            hero.localPosition = new Vector3(0, heroY, -0.001f);
            var scrim = UIKit.Quad(m_Content, "Scrim", UIKit.RoundedRect(heroW, heroH, 0.024f), Palette.UnlitTextured(TitleArt.Scrim, Color.white, true, "Scrim"), -0.0015f);
            scrim.localPosition = new Vector3(0, heroY, -0.0015f);
            scrim.GetComponent<MeshRenderer>().sortingOrder = UIKit.Order.Scrim;
            var chip = UIKit.Pill(m_Content, "Genre", "MURDER MYSTERY  ·  MIXED REALITY", UIKit.Face.Heading, 0.135f, Palette.UiDeep, Palette.UiText, out float chipW, 0.032f, 0.014f);
            chip.localPosition = new Vector3(-heroW * 0.5f + 0.03f + chipW * 0.5f, heroY + heroH * 0.5f - 0.034f, -0.004f);
            var title = HUI.Label(m_Content, "Frost & Found", UIKit.Face.Display, 0.86f, Color.white, new Vector2(heroW - 0.06f, 0.1f), TextAlignmentOptions.BottomLeft, -1f, false);
            title.fontStyle = FontStyles.Normal;
            title.enableVertexGradient = true;   // snow-white fading to ice at the base of the letters
            title.colorGradient = new VertexGradient(Color.white, Color.white, Palette.Hex("#C9EAFF"), Palette.Hex("#C9EAFF"));
            UIKit.Shadowed(title);
            title.transform.localPosition = new Vector3(0, heroY - heroH * 0.5f + 0.098f, -0.004f);
            var tagline = HUI.Label(m_Content, "A murder mystery where the victim is a snowman — and the suspects are knee-high.", UIKit.Face.Regular, 0.19f, Palette.UiTextDim, new Vector2(heroW - 0.06f, 0.03f), TextAlignmentOptions.TopLeft, 0f, false);
            tagline.transform.localPosition = new Vector3(0, heroY - heroH * 0.5f + 0.038f, -0.004f);

            // ---- left column: pitch, facts, the primary action
            float top = heroY - heroH * 0.5f - 0.032f;
            float leftX = -W * 0.5f + 0.035f, leftW = 0.48f;
            var desc = HUI.Label(m_Content, "Snap the clues with your Polaroid, show each photo to the kid it points to, and follow the chain to the culprit. Your only tools: a camera and a snowball.",
                UIKit.Face.Regular, 0.18f, Palette.UiTextDim, new Vector2(leftW, 0.1f), TextAlignmentOptions.TopLeft);
            desc.transform.localPosition = new Vector3(leftX + leftW * 0.5f, top - 0.045f, -0.002f);
            float cx = leftX;
            foreach (var (icon, text) in new[] { (UIKit.Icon.Clock, "2 min"), (UIKit.Icon.Snowflake, "Room-scale"), (UIKit.Icon.Hand, "Hands or controllers") })
            {
                var p = MetaChip(m_Content, icon, text, out float w);
                p.localPosition = new Vector3(cx + w * 0.5f, top - 0.122f, -0.002f);
                cx += w + 0.01f;
            }
            var start = HButton.Create(m_Content, "Open the case", HButton.Variant.Primary, new Vector2(0.3f, 0.066f), UIKit.Icon.Play, () => OnStart?.Invoke());
            start.transform.localPosition = new Vector3(leftX + 0.15f, top - 0.2f, -0.004f);
            var hint = HUI.Label(m_Content, "Trigger, pinch or poke", UIKit.Face.Regular, 0.14f, Palette.UiTextFaint, new Vector2(0.17f, 0.03f), TextAlignmentOptions.Left, 0f, false);
            hint.transform.localPosition = new Vector3(leftX + 0.3f + 0.02f + 0.085f, top - 0.2f, -0.002f);

            // ---- right column: how to play
            float rightX = 0.045f, rightW = W * 0.5f - 0.035f - rightX;
            var ov = HUI.Overline(m_Content, "How to play");
            ov.rectTransform.sizeDelta = new Vector2(rightW, 0.03f);
            ov.transform.localPosition = new Vector3(rightX + rightW * 0.5f, top - 0.012f, -0.002f);
            (UIKit.Icon, string, string)[] rows =
            {
                (UIKit.Icon.Camera, "Snap the clue", "Trigger or pinch. Get within arm's reach."),
                (UIKit.Icon.Hand, "Show the evidence", "Hold your newest photo out to a kid."),
                (UIKit.Icon.Snowball, "Play in the snow", "Reach down, squeeze, and throw."),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                var row = HowToRow(m_Content, rows[i].Item1, rows[i].Item2, rows[i].Item3, rightW);
                row.localPosition = new Vector3(rightX + rightW * 0.5f, top - 0.06f - i * 0.064f, -0.002f);
            }

            // ---- footer: settings
            float footY = -H * 0.5f + 0.045f;
            float tw = (W - 0.07f - 0.02f) / 3f;
            var t1 = HToggle.Create(m_Content, "Lefty mode", "Camera in left hand", new Vector2(tw, 0.062f), () => FrostPrefs.LeftHanded, v => FrostPrefs.LeftHanded = v);
            var t2 = HToggle.Create(m_Content, "Subtitles", "Speech bubbles & captions", new Vector2(tw, 0.062f), () => FrostPrefs.Subtitles, v => FrostPrefs.Subtitles = v);
            var t3 = HToggle.Create(m_Content, "Tips", "Short control hints", new Vector2(tw, 0.062f), () => FrostPrefs.Tips, v => FrostPrefs.Tips = v);
            t1.transform.localPosition = new Vector3(-tw - 0.01f, footY, -0.002f);
            t2.transform.localPosition = new Vector3(0, footY, -0.002f);
            t3.transform.localPosition = new Vector3(tw + 0.01f, footY, -0.002f);

            m_Content.gameObject.SetLayerRecursive(Layers.UI3D);
            m_Content.localScale = Vector3.one * 0.9f;
            StartCoroutine(Ease.Tween(0.35f, k => m_Content.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, Ease.OutCubic(k)), Ease.Linear, true));
        }

        public static Transform MetaChip(Transform parent, UIKit.Icon icon, string text, out float width)
        {
            var root = new GameObject("Chip · " + text).transform;
            root.SetParent(parent, false);
            root.gameObject.layer = Layers.UI3D;
            var label = HUI.Label(root, text, UIKit.Face.Body, 0.16f, Palette.UiText, new Vector2(1f, 0.03f), TextAlignmentOptions.Left, 0f, false);
            float tw = UIKit.Measure(label, text, 1f).x;
            float h = 0.038f, iconS = 0.019f;
            width = 0.012f + iconS + 0.008f + tw + 0.014f;
            UIKit.Panel(root, "Shape", new Vector2(width, h), h * 0.5f, Palette.UiSurface, null, 0f, false);
            var ic = UIKit.IconQuad(root, icon, iconS, Palette.UiTextDim, -0.002f);
            ic.localPosition = new Vector3(-width * 0.5f + 0.012f + iconS * 0.5f, 0, -0.002f);
            label.rectTransform.sizeDelta = new Vector2(tw + 0.005f, h);
            label.transform.localPosition = new Vector3(-width * 0.5f + 0.012f + iconS + 0.008f + tw * 0.5f, 0, -0.003f);
            return root;
        }

        static Transform HowToRow(Transform parent, UIKit.Icon icon, string title, string desc, float width)
        {
            var root = new GameObject("How · " + title).transform;
            root.SetParent(parent, false);
            root.gameObject.layer = Layers.UI3D;
            const float tile = 0.05f;
            var t = HUI.Card(root, "Tile", new Vector2(tile, tile), 0.014f, Palette.UiSurface);
            t.Root.localPosition = new Vector3(-width * 0.5f + tile * 0.5f, 0, 0);
            UIKit.IconQuad(t.Root, icon, tile * 0.56f, Palette.UiAccentHover, -0.002f);
            float tx = -width * 0.5f + tile + 0.014f, tW = width - tile - 0.014f;
            var a = HUI.Label(root, title, UIKit.Face.Body, 0.18f, Palette.UiText, new Vector2(tW, 0.028f), TextAlignmentOptions.Left, 0f, false);
            a.transform.localPosition = new Vector3(tx + tW * 0.5f, 0.011f, -0.002f);
            var b = HUI.Label(root, desc, UIKit.Face.Regular, 0.158f, Palette.UiTextDim, new Vector2(tW, 0.025f), TextAlignmentOptions.Left, 0f, false);
            b.transform.localPosition = new Vector3(tx + tW * 0.5f, -0.013f, -0.002f);
            return root;
        }

        public IEnumerator Dismiss()
        {
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
                ("Lefty mode", "Camera in left hand", () => FrostPrefs.LeftHanded, v => FrostPrefs.LeftHanded = v),
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
