using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// System UI in the style of Meta Horizon OS: dark rounded panels with a hairline stroke and soft shadow,
    /// pill buttons with one blue primary action, switch toggles, a floating dock, toasts, and Quest-standard
    /// input (controller/hand ray + cursor, trigger/pinch to select, poke to press, hover ticks + haptics).
    /// Sizes follow comfortable-reading rules for ~1 m: body ≥ 2.3 cm line height, targets ≥ 5 cm tall.
    /// </summary>
    public static class HUI
    {
        public const float PanelRadius = 0.034f;
        public const float CardRadius = 0.022f;
        public const float ButtonHeight = 0.056f;
        public const float Stroke = 0.0018f;

        public static readonly Color StrokeSolid = Color.Lerp(Palette.UiPanelSolid, Palette.UiIce, 0.4f);
        /// <summary>The colour at the bottom of a solid glass panel (speech-bubble tails match it).</summary>
        public static Color GlassBottom => (Palette.UiPanelSolid * (1f - 0.22f)).WithAlpha(1f);

        /// <summary>A winter ice-glass panel: deep blue, slightly see-through, frosted edges, hairline icy stroke.</summary>
        public static UIKit.PanelParts Panel(Transform parent, string name, Vector2 size, float radius = PanelRadius, bool shadow = true, bool translucent = true)
        {
            var p = UIKit.Panel(parent, name, size, radius, Palette.UiPanelSolid, null, 0f, shadow, null, 0.3f);
            p.FillMat = Glass(size, radius, translucent ? Palette.UiPanel : Palette.UiPanelSolid, translucent);
            var mr = p.Fill.GetComponent<MeshRenderer>();
            mr.sharedMaterial = p.FillMat;
            if (translucent) mr.sortingOrder = UIKit.Order.Glass;   // under every other UI layer
            return p;
        }

        public static UIKit.PanelParts Card(Transform parent, string name, Vector2 size, float radius = CardRadius, Color? fill = null)
        {
            var p = UIKit.Panel(parent, name, size, radius, fill ?? Palette.UiSurface, null, 0f, false);
            p.FillMat = Glass(size, radius, fill ?? Palette.UiSurface, false, 0.12f, 0f, 0f, 0.3f);
            p.FillMat.SetColor("_StrokeColor", Palette.UiStroke.WithAlpha(0.2f));
            p.Fill.GetComponent<MeshRenderer>().sharedMaterial = p.FillMat;
            return p;
        }

        /// <summary>An ice-glass material for one rounded rect (the shader draws its stroke, bevel, frost and sheen).</summary>
        public static Material Glass(Vector2 size, float radius, Color color, bool translucent,
            float gradient = 0.22f, float frost = 0.55f, float sheen = 0.35f, float highlight = 0.5f)
        {
            var m = new Material(FrostAssets.Shader(FrostAssets.ShaderId.UIGlass)) { name = translucent ? "UI Glass" : "UI Glass (solid)" };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Gradient", gradient);
            m.SetVector("_Size", new Vector4(size.x, size.y, 0f, 0f));
            m.SetFloat("_Radius", Mathf.Clamp(radius, 0f, Mathf.Min(size.x, size.y) * 0.5f));
            m.SetFloat("_Stroke", Stroke);
            m.SetColor("_StrokeColor", Palette.UiStroke);
            m.SetFloat("_Highlight", highlight);
            m.SetFloat("_Frost", frost);
            m.SetTexture("_FrostMap", FxTextures.FrostCrystals);
            m.SetFloat("_Sheen", sheen);
            if (!translucent)
            {
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                m.SetFloat("_SrcBlendA", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_DstBlendA", (float)UnityEngine.Rendering.BlendMode.Zero);
                m.SetFloat("_ZWrite", 1f);
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            }
            return m;
        }

        static Material s_Trim;
        /// <summary>
        /// Winter trim for the big panels: snow heaped on the top edge, a frozen rim with icicles along the bottom.
        /// Kept small (about a centimetre of snow, icicles mostly under 2 cm) so the layout still reads as clean UI.
        /// </summary>
        public static void Winterize(Transform panelRoot, Vector2 size, float radius, int seed, bool snow = true, bool icicles = true, float scale = 1f)
        {
            if (!s_Trim)
            {
                s_Trim = new Material(FrostAssets.Shader(FrostAssets.ShaderId.UIGlass)) { name = "UI Winter Trim" };
                s_Trim.SetColor("_BaseColor", Color.white);
                s_Trim.SetFloat("_VertexColor", 1f);
                s_Trim.SetVector("_Size", Vector4.zero);
            }
            if (snow)
                UIKit.Quad(panelRoot, "Snow Cap", UIKit.SnowCap(size.x, size.y, radius, seed, 0.011f * scale), s_Trim, -0.0016f)
                    .GetComponent<MeshRenderer>().sortingOrder = UIKit.Order.Decor;
            if (icicles)
                UIKit.Quad(panelRoot, "Icicles", UIKit.Icicles(size.x, size.y, radius, seed + 1, scale), s_Trim, -0.0016f)
                    .GetComponent<MeshRenderer>().sortingOrder = UIKit.Order.Decor;
        }

        public static TextMeshPro Label(Transform parent, string text, UIKit.Face face, float size, Color color, Vector2 box,
            TextAlignmentOptions align = TextAlignmentOptions.Left, float spacing = 0f, bool wrap = true)
        {
            var t = UIKit.Text(parent, "Text", text, face, size, color, box, align, spacing, wrap);
            t.fontStyle = FontStyles.Normal;
            t.transform.localPosition = new Vector3(0, 0, -0.002f);
            return t;
        }

        /// <summary>Uppercase, letter-spaced section label ("HOW TO PLAY").</summary>
        public static TextMeshPro Overline(Transform parent, string text, Color? color = null, float size = 0.13f)
        {
            var t = UIKit.Text(parent, "Overline", text.ToUpperInvariant(), UIKit.Face.Heading, size, color ?? Palette.UiIce, new Vector2(0.6f, 0.03f), TextAlignmentOptions.Left, 10f, false);
            t.transform.localPosition = new Vector3(0, 0, -0.002f);
            return t;
        }

        public static void Place(Transform t, Vector3 local) => t.localPosition = new Vector3(local.x, local.y, local.z == 0 ? t.localPosition.z : local.z);
    }

    // =============================================================================================== controls
    /// <summary>Base for anything the pointer can hover and press.</summary>
    public abstract class HControl : MonoBehaviour
    {
        public static readonly List<HControl> All = new List<HControl>();
        public System.Action onClick;
        public bool interactable = true;
        public string tooltip;
        protected BoxCollider m_Col;
        protected float m_Hover, m_Press;
        bool m_Hovered, m_Pressed;
        public bool Hovered => m_Hovered;

        protected virtual void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); m_Hovered = false; m_Pressed = false; }

        protected void MakeCollider(Vector2 size, float depth = 0.03f)
        {
            m_Col = gameObject.GetOrAdd<BoxCollider>();
            m_Col.isTrigger = true;
            m_Col.size = new Vector3(size.x, size.y, depth);
            m_Col.center = new Vector3(0, 0, -0.004f);
            gameObject.layer = Layers.UI3D;
        }

        public Bounds LocalBounds => new Bounds(m_Col ? m_Col.center : Vector3.zero, m_Col ? m_Col.size : Vector3.one * 0.05f);

        public void SetHovered(bool on)
        {
            if (on == m_Hovered) return;
            m_Hovered = on;
            if (on && interactable) AudioDirector.Play2D(Sfx.UiHover, 0.35f, 1f);
        }

        public void SetPressed(bool on) => m_Pressed = on;

        public void Click()
        {
            if (!interactable) return;
            AudioDirector.Play2D(Sfx.UiClick, 0.75f, 1f);
            m_Press = 1f;
            onClick?.Invoke();
            OnClicked();
        }

        protected virtual void OnClicked() { }

        protected virtual void Update()
        {
            float dt = Time.unscaledDeltaTime;
            m_Hover = Ease.Damp(m_Hover, m_Hovered && interactable ? 1f : 0f, 18f, dt);
            m_Press = Ease.Damp(m_Press, m_Pressed ? 1f : 0f, m_Pressed ? 30f : 10f, dt);
            Animate(m_Hover, m_Press);
        }

        protected abstract void Animate(float hover, float press);
    }

    /// <summary>Pill button: Primary (blue), Secondary (grey), Icon (circle with a hover tooltip).</summary>
    public class HButton : HControl
    {
        public enum Variant { Primary, Secondary, Icon, Ghost }
        Variant m_Variant;
        Material m_Fill;
        Transform m_Content, m_Ring;
        Material m_RingMat;
        TextMeshPro m_Label;
        Transform m_Tip;
        Vector2 m_Size;

        public static HButton Create(Transform parent, string label, Variant variant, Vector2 size, UIKit.Icon? icon = null, System.Action onClick = null)
        {
            var go = new GameObject("Button · " + label);
            go.transform.SetParent(parent, false);
            go.layer = Layers.UI3D;
            var b = go.AddComponent<HButton>();
            b.Build(label, variant, size, icon);
            b.onClick = onClick;
            return b;
        }

        void Build(string label, Variant variant, Vector2 size, UIKit.Icon? icon)
        {
            m_Variant = variant;
            m_Size = size;
            m_Content = new GameObject("Content").transform;
            m_Content.SetParent(transform, false);
            m_Content.gameObject.layer = Layers.UI3D;
            float r = variant == Variant.Icon ? size.y * 0.5f : size.y * 0.5f;
            bool primary = variant == Variant.Primary;
            m_Fill = HUI.Glass(size, r, IdleColor, false, primary ? 0.2f : 0.12f, 0f, primary ? 0.25f : 0f, primary ? 0.7f : 0.35f);
            m_Fill.SetColor("_BaseColor", IdleColor);
            UIKit.Quad(m_Content, "Fill", UIKit.RoundedRect(size.x, size.y, r), m_Fill, 0f);
            m_RingMat = Palette.UnlitTextured(Texture2D.whiteTexture, Color.white.WithAlpha(0f), true, "HoverRing");
            m_Ring = UIKit.Quad(m_Content, "Hover Ring", UIKit.RoundedFrame(size.x + 0.008f, size.y + 0.008f, r + 0.004f, 0.0028f), m_RingMat, 0.0005f);
            m_Ring.GetComponent<MeshRenderer>().sortingOrder = UIKit.Order.Overlay;
            var textColor = variant == Variant.Primary ? Color.white : Palette.UiText;
            float iconSize = Mathf.Min(size.y * 0.46f, 0.03f);
            if (variant == Variant.Icon)
            {
                if (icon.HasValue) UIKit.IconQuad(m_Content, icon.Value, size.y * 0.46f, textColor, -0.002f);
                tooltip = label;
            }
            else
            {
                m_Label = UIKit.Text(m_Content, "Label", label, UIKit.Face.Body, Mathf.Clamp(size.y * 3.6f, 0.17f, 0.24f), textColor, new Vector2(size.x, size.y), TextAlignmentOptions.Center, 1f, false);
                m_Label.fontStyle = FontStyles.Normal;
                m_Label.transform.localPosition = new Vector3(0, 0, -0.002f);
                if (icon.HasValue)
                {
                    float w = UIKit.Measure(m_Label, label, 2f).x;
                    float total = w + iconSize + 0.012f;
                    var ic = UIKit.IconQuad(m_Content, icon.Value, iconSize, textColor, -0.002f);
                    ic.localPosition = new Vector3(-total * 0.5f + iconSize * 0.5f, 0, -0.002f);
                    m_Label.rectTransform.sizeDelta = new Vector2(w + 0.01f, size.y);
                    m_Label.transform.localPosition = new Vector3(total * 0.5f - w * 0.5f, 0, -0.002f);
                }
            }
            MakeCollider(size);
        }

        Color IdleColor => m_Variant switch
        {
            Variant.Primary => Palette.UiAccent,
            Variant.Ghost => Palette.UiPanelSolid,
            _ => Palette.UiSurface,
        };
        Color HoverColor => m_Variant == Variant.Primary ? Palette.UiAccentHover : Palette.UiSurfaceHover;
        Color PressColor => m_Variant == Variant.Primary ? Palette.UiAccentPressed : Palette.UiSurfacePressed;

        public void SetLabel(string text) { if (m_Label) m_Label.text = text; tooltip = text; }

        protected override void Animate(float hover, float press)
        {
            var c = Color.Lerp(Color.Lerp(IdleColor, HoverColor, hover), PressColor, press);
            if (!interactable) c = Color.Lerp(IdleColor, Palette.UiPanelSolid, 0.5f);
            m_Fill.SetColor("_BaseColor", c);
            m_RingMat.SetColor("_BaseColor", Palette.UiIce.WithAlpha(0.5f * hover));
            m_Content.localScale = Vector3.one * (1f + 0.035f * hover - 0.05f * press);
            m_Content.localPosition = new Vector3(0, 0, 0.004f * press);
            if (m_Variant == Variant.Icon) UpdateTooltip(hover);
        }

        void UpdateTooltip(float hover)
        {
            if (string.IsNullOrEmpty(tooltip)) return;
            if (!m_Tip && hover > 0.05f)
            {
                m_Tip = UIKit.Pill(transform, "Tooltip", tooltip, UIKit.Face.Body, 0.15f, Palette.UiSurface, Palette.UiText, out _, 0.036f, 0.014f, HUI.StrokeSolid, true);
                m_Tip.localPosition = new Vector3(0, m_Size.y * 0.5f + 0.034f, -0.01f);
                foreach (var tmp in m_Tip.GetComponentsInChildren<TextMeshPro>()) tmp.fontStyle = FontStyles.Normal;
            }
            if (m_Tip)
            {
                m_Tip.gameObject.SetActive(hover > 0.05f);
                m_Tip.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, hover);
            }
        }
    }

    /// <summary>A settings row with an iOS/Horizon-style switch.</summary>
    public class HToggle : HControl
    {
        Transform m_Knob;
        Material m_Track, m_RowMat;
        bool m_On;
        float m_Anim;
        System.Func<bool> m_Get;
        System.Action<bool> m_Set;
        Vector2 m_Size;
        const float TrackW = 0.066f, TrackH = 0.038f, Knob = 0.03f;

        public static HToggle Create(Transform parent, string label, string sub, Vector2 size, System.Func<bool> get, System.Action<bool> set)
        {
            var go = new GameObject("Toggle · " + label);
            go.transform.SetParent(parent, false);
            go.layer = Layers.UI3D;
            var t = go.AddComponent<HToggle>();
            t.m_Get = get; t.m_Set = set;
            t.Build(label, sub, size);
            return t;
        }

        void Build(string label, string sub, Vector2 size)
        {
            m_Size = size;
            m_RowMat = HUI.Glass(size, HUI.CardRadius, Palette.UiSurface, false, 0.1f, 0f, 0f, 0.25f);
            m_RowMat.SetColor("_StrokeColor", Palette.UiStroke.WithAlpha(0.18f));
            m_RowMat.SetColor("_BaseColor", Palette.UiSurface);
            UIKit.Quad(transform, "Row", UIKit.RoundedRect(size.x, size.y, HUI.CardRadius), m_RowMat, 0f);
            float textW = size.x - TrackW - 0.05f;
            var l = HUI.Label(transform, label, UIKit.Face.Body, 0.19f, Palette.UiText, new Vector2(textW, 0.03f), TextAlignmentOptions.Left, 0f, false);
            l.transform.localPosition = new Vector3(-size.x * 0.5f + 0.02f + textW * 0.5f, string.IsNullOrEmpty(sub) ? 0f : 0.011f, -0.002f);
            if (!string.IsNullOrEmpty(sub))
            {
                var s = HUI.Label(transform, sub, UIKit.Face.Regular, 0.152f, Palette.UiTextDim, new Vector2(textW, 0.025f), TextAlignmentOptions.Left, 0f, false);
                s.transform.localPosition = new Vector3(-size.x * 0.5f + 0.02f + textW * 0.5f, -0.013f, -0.002f);
            }
            var track = new GameObject("Switch").transform;
            track.SetParent(transform, false);
            track.gameObject.layer = Layers.UI3D;
            track.localPosition = new Vector3(size.x * 0.5f - 0.02f - TrackW * 0.5f, 0, -0.002f);
            m_Track = new Material(FrostAssets.Shader(FrostAssets.ShaderId.Unlit)) { name = "Track" };
            UIKit.Quad(track, "Track", UIKit.RoundedRect(TrackW, TrackH, TrackH * 0.5f), m_Track, 0f);
            m_Knob = UIKit.Quad(track, "Knob", UIKit.RoundedRect(Knob, Knob, Knob * 0.5f), Palette.Unlit(Color.white), -0.002f);
            m_On = m_Get();
            m_Anim = m_On ? 1f : 0f;
            onClick = () => { m_On = !m_On; m_Set(m_On); };
            MakeCollider(size);
        }

        protected override void Animate(float hover, float press)
        {
            m_Anim = Ease.Damp(m_Anim, m_On ? 1f : 0f, 16f, Time.unscaledDeltaTime);
            m_Track.SetColor("_BaseColor", Color.Lerp(Palette.UiTrackOff, Palette.UiAccent, m_Anim));
            float travel = (TrackW - Knob) * 0.5f - 0.004f;
            m_Knob.localPosition = new Vector3(Mathf.Lerp(-travel, travel, m_Anim), 0, -0.002f);
            m_Knob.localScale = Vector3.one * (1f - 0.08f * press);
            m_RowMat.SetColor("_BaseColor", Color.Lerp(Palette.UiSurface, Palette.UiSurfaceHover, hover));
        }
    }

    // =============================================================================================== pointer
    /// <summary>
    /// Ray + cursor + poke for every HControl. Controllers aim from the grip; tracked hands aim from the shoulder
    /// through the palm (Meta-style hand ray) and select with an index pinch; any fingertip can poke a button.
    /// Desktop uses the mouse (or the screen centre while the cursor is captured).
    /// </summary>
    public class HPointer : MonoBehaviour
    {
        public static HPointer Instance { get; private set; }
        /// <summary>Tests / autopilot: aim the desktop pointer with the view instead of the mouse.</summary>
        public static bool ForceGazeRay;
        public static bool IsOverUI => Instance && Instance.m_Over;
        static int s_ConsumedFrame = -1;
        public static bool ConsumedThisFrame => s_ConsumedFrame == Time.frameCount;

        class RayVis { public LineRenderer line; public Transform dot, ring; public HControl hover, pressed; public float pokeDepthPrev = 1f; public bool pokeArmed = true; }
        readonly RayVis[] m_Rays = new RayVis[2];
        bool m_Over;
        readonly RaycastHit[] m_Hits = new RaycastHit[8];

        public static void Ensure()
        {
            if (Instance) return;
            var go = new GameObject("[Frost Pointer]");
            Instance = go.AddComponent<HPointer>();
        }

        void Awake()
        {
            Instance = this;
            for (int i = 0; i < 2; i++) m_Rays[i] = MakeVis(i);
        }

        RayVis MakeVis(int i)
        {
            var v = new RayVis();
            var lgo = new GameObject("Ray " + i);
            lgo.transform.SetParent(transform, false);
            lgo.layer = Layers.UI3D;
            v.line = lgo.AddComponent<LineRenderer>();
            v.line.positionCount = 2;
            v.line.useWorldSpace = true;
            v.line.widthCurve = new AnimationCurve(new Keyframe(0, 0.0032f), new Keyframe(1, 0.0012f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0.0f, 0), new GradientAlphaKey(0.55f, 0.25f), new GradientAlphaKey(0.25f, 1f) });
            v.line.colorGradient = g;
            v.line.sharedMaterial = Palette.Particle(FxTextures.SoftDot, false, "PointerRay");
            v.line.textureMode = LineTextureMode.Stretch;
            v.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            v.line.enabled = false;
            v.dot = UIKit.Quad(transform, "Cursor Dot", UIKit.RoundedRect(0.011f, 0.011f, 0.0055f), Palette.Unlit(Color.white), 0f);
            v.ring = UIKit.Quad(transform, "Cursor Ring", UIKit.RoundedFrame(0.026f, 0.026f, 0.013f, 0.0022f), Palette.Unlit(Color.white.WithAlpha(0.55f), true), 0f);
            v.dot.GetComponent<MeshRenderer>().sortingOrder = UIKit.Order.Cursor;
            v.ring.GetComponent<MeshRenderer>().sortingOrder = UIKit.Order.Cursor;
            v.dot.GetComponent<MeshRenderer>().sharedMaterial = Palette.Unlit(Color.white, true);
            v.line.sortingOrder = UIKit.Order.Cursor - 1;
            v.dot.gameObject.SetActive(false); v.ring.gameObject.SetActive(false);
            return v;
        }

        bool AnyUI()
        {
            foreach (var c in HControl.All) if (c && c.isActiveAndEnabled) return true;
            return false;
        }

        void Update()
        {
            var rig = PlayerRig.Active;
            m_Over = false;
            if (!rig || !AnyUI())
            {
                foreach (var v in m_Rays) Hide(v);
                return;
            }
            // menus open while time is stopped (pause) and panels lazy-follow: make sure physics sees the
            // colliders where they are now, not where they were at the last simulation step
            Physics.SyncTransforms();
            if (rig.IsXR)
            {
                UpdateHand(rig, rig.Right, m_Rays[1]);
                UpdateHand(rig, rig.Left, m_Rays[0]);
            }
            else
            {
                UpdateDesktop(rig, m_Rays[1]);
                Hide(m_Rays[0]);
            }
        }

        void UpdateHand(PlayerRig rig, HandRig hand, RayVis v)
        {
            if (!hand.IsTracked || !hand.Anchor) { Release(v, false); Hide(v); return; }
            Vector3 origin, dir;
            if (hand.IsOpticalHand)
            {
                // Meta-style hand ray: from an estimated shoulder through the palm
                var head = rig.Head;
                var side = hand.Side == HandSide.Right ? 1f : -1f;
                var shoulder = head.position + Vector3.down * 0.17f + Vector3.Cross(Vector3.up, rig.FlatForward) * (0.17f * side);
                origin = hand.Palm.position;
                dir = (origin - shoulder).normalized;
            }
            else
            {
                origin = hand.Anchor.position;
                dir = hand.Anchor.forward;
            }
            // poke first: a fingertip pushing into a control's face presses it
            if (Poke(hand, v)) { Hide(v); return; }
            var target = Cast(origin, dir, out var hit);
            SetHover(v, target);
            if (target)
            {
                m_Over = true;
                ShowRay(v, origin, hit.point, hit.normal, true);
                if (hand.TriggerDown) { v.pressed = target; target.SetPressed(true); hand.Haptic(0.25f, 0.03f); s_ConsumedFrame = Time.frameCount; }
            }
            else ShowRay(v, origin, origin + dir * 0.45f, -dir, false);
            if (v.pressed && !hand.TriggerHeld) Release(v, v.pressed == target);
        }

        void UpdateDesktop(PlayerRig rig, RayVis v)
        {
            var cam = rig.ViewCamera;
            if (!cam) return;
            Ray ray = new Ray(rig.Head.position, rig.Head.forward);
            bool down = false, up = false;
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (mouse != null)
            {
                if (Cursor.lockState != CursorLockMode.Locked && !ForceGazeRay) ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                down = mouse.leftButton.wasPressedThisFrame; up = mouse.leftButton.wasReleasedThisFrame;
            }
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) { down = true; up = true; }
#endif
            if (rig.CameraHand.TriggerDown) { down = true; up = true; } // scripted presses (autopilot / tests)
            var target = Cast(ray.origin, ray.direction, out var hit);
            SetHover(v, target);
            v.line.enabled = false;
            if (target)
            {
                m_Over = true;
                PlaceCursor(v, hit.point, hit.normal, Vector3.Distance(ray.origin, hit.point));
                if (down) { v.pressed = target; target.SetPressed(true); s_ConsumedFrame = Time.frameCount; }
            }
            else { v.dot.gameObject.SetActive(false); v.ring.gameObject.SetActive(false); }
            if (v.pressed && up) Release(v, v.pressed == target);
        }

        bool Poke(HandRig hand, RayVis v)
        {
            if (!hand.Tip) return false;
            foreach (var c in HControl.All)
            {
                if (!c || !c.isActiveAndEnabled || !c.interactable) continue;
                var local = c.transform.InverseTransformPoint(hand.Tip.position);
                var b = c.LocalBounds;
                if (Mathf.Abs(local.x - b.center.x) > b.extents.x || Mathf.Abs(local.y - b.center.y) > b.extents.y) continue;
                float depth = local.z; // front face is at -z; negative = in front
                if (depth < -0.06f || depth > 0.03f) continue;
                SetHover(v, c);
                m_Over = true;
                if (v.pokeArmed && depth > -0.004f && v.pokeDepthPrev <= -0.004f)
                {
                    v.pokeArmed = false;
                    c.SetPressed(true);
                    c.Click();
                    c.SetPressed(false);
                    hand.Haptic(0.4f, 0.04f);
                    s_ConsumedFrame = Time.frameCount;
                }
                if (depth < -0.02f) v.pokeArmed = true;
                v.pokeDepthPrev = depth;
                return true;
            }
            v.pokeDepthPrev = -1f;
            v.pokeArmed = true;
            return false;
        }

        HControl Cast(Vector3 origin, Vector3 dir, out RaycastHit best)
        {
            best = default;
            int n = Physics.RaycastNonAlloc(origin, dir, m_Hits, 8f, Layers.Mask(Layers.UI3D), QueryTriggerInteraction.Collide);
            HControl bestC = null; float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = m_Hits[i].collider.GetComponent<HControl>();
                if (!c || !c.isActiveAndEnabled) continue;
                if (m_Hits[i].distance < bestD) { bestD = m_Hits[i].distance; bestC = c; best = m_Hits[i]; }
            }
            return bestC;
        }

        void SetHover(RayVis v, HControl target)
        {
            if (v.hover == target) return;
            if (v.hover) v.hover.SetHovered(false);
            v.hover = target;
            if (target)
            {
                target.SetHovered(true);
                var rig = PlayerRig.Active;
                if (rig) (v == m_Rays[1] ? rig.Right : rig.Left).Haptic(0.08f, 0.012f);
            }
        }

        void Release(RayVis v, bool inside)
        {
            if (!v.pressed) return;
            var p = v.pressed;
            v.pressed = null;
            p.SetPressed(false);
            if (inside) p.Click();
        }

        void ShowRay(RayVis v, Vector3 from, Vector3 to, Vector3 normal, bool hit)
        {
            v.line.enabled = true;
            v.line.SetPosition(0, from);
            v.line.SetPosition(1, to);
            if (hit) PlaceCursor(v, to, normal, Vector3.Distance(from, to));
            else { v.dot.gameObject.SetActive(false); v.ring.gameObject.SetActive(false); }
        }

        void PlaceCursor(RayVis v, Vector3 p, Vector3 n, float dist)
        {
            v.dot.gameObject.SetActive(true); v.ring.gameObject.SetActive(true);
            var rot = Quaternion.LookRotation(-n, Vector3.up);
            float s = Mathf.Clamp(dist, 0.4f, 3f);
            v.dot.SetPositionAndRotation(p + n * 0.006f, rot);
            v.ring.SetPositionAndRotation(p + n * 0.006f, rot);
            bool pressing = v.pressed != null;
            v.dot.localScale = Vector3.one * s * (pressing ? 0.8f : 1f);
            v.ring.localScale = Vector3.one * s * (pressing ? 0.7f : 1f);
        }

        void Hide(RayVis v)
        {
            if (v.hover) { v.hover.SetHovered(false); v.hover = null; }
            v.line.enabled = false;
            v.dot.gameObject.SetActive(false);
            v.ring.gameObject.SetActive(false);
        }
    }

    // =============================================================================================== placement
    /// <summary>World-locked panel that gently re-centres in front of you when you look away (Horizon-style lazy follow).</summary>
    public class HFollow : MonoBehaviour
    {
        public float distance = 1.05f;
        public float drop = 0.08f;          // below eye height
        public float maxAngle = 38f;        // look this far away and it glides back
        public float tilt = 8f;             // tilted back toward you
        bool m_Moving;
        Vector3 m_TargetPos;
        Quaternion m_TargetRot;

        public void SnapNow()
        {
            Compute(out m_TargetPos, out m_TargetRot);
            transform.SetPositionAndRotation(m_TargetPos, m_TargetRot);
        }

        void Start() => SnapNow();

        void Compute(out Vector3 pos, out Quaternion rot)
        {
            var rig = PlayerRig.Active;
            var head = rig ? rig.Head : Camera.main ? Camera.main.transform : transform;
            var fwd = rig ? rig.FlatForward : head.forward.Flat().normalized;
            pos = head.position + fwd * distance + Vector3.down * drop;
            rot = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(tilt, 0, 0);
        }

        void LateUpdate()
        {
            var rig = PlayerRig.Active;
            if (!rig) return;
            var toPanel = (transform.position - rig.Head.position).Flat();
            float angle = Vector3.Angle(rig.FlatForward, toPanel);
            float dist = toPanel.magnitude;
            if (!m_Moving && (angle > maxAngle || dist > distance * 1.8f || dist < distance * 0.45f))
            {
                m_Moving = true;
                Compute(out m_TargetPos, out m_TargetRot);
            }
            if (m_Moving)
            {
                Compute(out m_TargetPos, out m_TargetRot);
                float dt = Time.unscaledDeltaTime;
                transform.position = Ease.Damp(transform.position, m_TargetPos, 5f, dt);
                transform.rotation = Ease.Damp(transform.rotation, m_TargetRot, 5f, dt);
                if (Vector3.Distance(transform.position, m_TargetPos) < 0.01f) m_Moving = false;
            }
        }
    }

    // =============================================================================================== toasts
    /// <summary>Small Horizon-style toast pill under your gaze: an icon and one line. One at a time, queued.</summary>
    public class HToast : MonoBehaviour
    {
        static HToast s_I;
        readonly Queue<(string text, UIKit.Icon icon, float secs)> m_Queue = new Queue<(string, UIKit.Icon, float)>();
        Transform m_Root;
        bool m_Showing;

        public static void Show(string text, UIKit.Icon icon, float seconds = 4f)
        {
            if (!FrostPrefs.Tips) return;
            if (!s_I) s_I = new GameObject("[Frost Toasts]").AddComponent<HToast>();
            s_I.m_Queue.Enqueue((text, icon, seconds));
        }

        void Update()
        {
            if (!m_Showing && m_Queue.Count > 0) StartCoroutine(Run(m_Queue.Dequeue()));
            var rig = PlayerRig.Active;
            if (m_Root && rig)
            {
                var target = GazeAnchor(rig, 0.95f, 17f);
                m_Root.position = Ease.Damp(m_Root.position, target, 6f, Time.unscaledDeltaTime);
                m_Root.rotation = Quaternion.LookRotation((m_Root.position - rig.Head.position).normalized, Vector3.up);
            }
        }

        /// <summary>A point a little below where you're looking (body-locked, like Horizon's toasts).</summary>
        public static Vector3 GazeAnchor(PlayerRig rig, float distance, float degreesBelow)
        {
            var head = rig.Head;
            var fwd = head.forward;
            // keep it readable when looking far up/down: clamp the gaze pitch
            var flat = fwd.Flat().sqrMagnitude > 1e-4f ? fwd.Flat().normalized : rig.FlatForward;
            float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f)) * Mathf.Rad2Deg, -45f, 35f) - degreesBelow;
            var dir = Quaternion.AngleAxis(-pitch, Vector3.Cross(Vector3.up, flat)) * flat;
            return head.position + dir.normalized * distance;
        }

        IEnumerator Run((string text, UIKit.Icon icon, float secs) t)
        {
            m_Showing = true;
            m_Root = new GameObject("Toast").transform;
            m_Root.gameObject.layer = Layers.UI3D;
            var rig = PlayerRig.Active;
            if (rig) m_Root.position = GazeAnchor(rig, 0.95f, 19f);
            var label = HUI.Label(m_Root, t.text, UIKit.Face.Body, 0.19f, Palette.UiText, new Vector2(0.6f, 0.04f), TextAlignmentOptions.Left, 0f, false);
            float w = UIKit.Measure(label, t.text, 0.9f).x;
            float h = 0.058f, iconBox = 0.036f;
            float total = 0.018f + iconBox + 0.012f + w + 0.024f;
            HUI.Panel(m_Root, "Pill", new Vector2(total, h), h * 0.5f);
            var ib = HUI.Card(m_Root, "Icon Bubble", new Vector2(iconBox, iconBox), iconBox * 0.5f, Palette.UiAccent);
            ib.Root.localPosition = new Vector3(-total * 0.5f + 0.012f + iconBox * 0.5f, 0, -0.002f);
            UIKit.IconQuad(ib.Root, t.icon, iconBox * 0.58f, Color.white, -0.002f);
            label.rectTransform.sizeDelta = new Vector2(w + 0.01f, h);
            label.transform.localPosition = new Vector3(-total * 0.5f + 0.018f + iconBox + 0.012f + w * 0.5f, 0, -0.003f);
            m_Root.gameObject.SetLayerRecursive(Layers.UI3D);
            AudioDirector.Play2D(Sfx.UiHover, 0.5f, 0.8f);
            yield return Ease.Tween(0.25f, k => m_Root.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, Ease.OutBack(k)), Ease.Linear, true);
            float end = Time.unscaledTime + t.secs;
            while (Time.unscaledTime < end) yield return null;
            yield return Ease.Tween(0.2f, k => m_Root.localScale = Vector3.one * (1f - k), Ease.InCubic, true);
            Destroy(m_Root.gameObject);
            m_Root = null;
            m_Showing = false;
        }
    }
}
