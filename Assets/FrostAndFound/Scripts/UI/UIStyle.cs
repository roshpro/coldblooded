using TMPro;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>One place that styles every TextMeshPro element so type always reads as chosen, not default.</summary>
    public static class UIStyle
    {
        public enum Role { Speech, Name, Title, Subtitle, Marker, Stamp, Caption, Button, Apron, Small }

        public static TMP_FontAsset Font(Role role)
        {
            switch (role)
            {
                case Role.Title:
                case Role.Stamp: return UIKit.Font(UIKit.Face.Display);
                case Role.Name:
                case Role.Button:
                case Role.Apron:
                case Role.Subtitle: return UIKit.Font(UIKit.Face.Heading);
                case Role.Marker: return UIKit.Font(UIKit.Face.Hand);
                case Role.Small: return UIKit.Font(UIKit.Face.Mono);
                default: return UIKit.Font(UIKit.Face.Body);
            }
        }

        public static void Apply(TMP_Text t, Role role, Color? colorOverride = null)
        {
            var f = Font(role);
            if (f) t.font = f;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true;
            t.extraPadding = true;
            switch (role)
            {
                case Role.Speech:
                    t.fontStyle = FontStyles.Bold;
                    t.color = Palette.BubbleText;
                    t.characterSpacing = 2f;
                    t.lineSpacing = -8f;
                    break;
                case Role.Name:
                    t.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                    t.characterSpacing = 8f;
                    t.color = colorOverride ?? Palette.Ink;
                    break;
                case Role.Title:
                    t.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                    t.characterSpacing = 6f;
                    t.color = Palette.Snow;
                    SetOutline(t, 0.22f, Palette.Ink);
                    SetUnderlay(t, Palette.Ink, 0.6f);
                    break;
                case Role.Subtitle:
                    t.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                    t.characterSpacing = 14f;
                    t.color = Palette.Glint;
                    SetOutline(t, 0.18f, Palette.Ink);
                    break;
                case Role.Marker:
                    t.fontStyle = FontStyles.Bold | FontStyles.Italic;
                    t.characterSpacing = 3f;
                    t.color = Palette.EvidenceRed;
                    break;
                case Role.Stamp:
                    t.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                    t.characterSpacing = 10f;
                    t.color = Palette.StampRed;
                    SetOutline(t, 0.12f, Palette.StampRed);
                    break;
                case Role.Caption:
                    t.fontStyle = FontStyles.Bold;
                    t.characterSpacing = 2f;
                    t.color = Palette.Snow;
                    SetOutline(t, 0.25f, Palette.Ink);
                    break;
                case Role.Button:
                    t.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                    t.characterSpacing = 8f;
                    t.color = Palette.Ink;
                    break;
                case Role.Apron:
                    t.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                    t.characterSpacing = 4f;
                    t.color = colorOverride ?? Palette.Cocoa;
                    break;
                case Role.Small:
                    t.fontStyle = FontStyles.Bold;
                    t.characterSpacing = 4f;
                    t.color = colorOverride ?? Palette.InkSoft;
                    break;
            }
            if (colorOverride.HasValue && role != Role.Name && role != Role.Apron && role != Role.Small) t.color = colorOverride.Value;
            var r = t.GetComponent<MeshRenderer>();
            if (r) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
        }

        static void SetOutline(TMP_Text t, float width, Color c)
        {
            t.outlineWidth = width;
            t.outlineColor = c;
        }

        static void SetUnderlay(TMP_Text t, Color c, float softness)
        {
            var m = t.fontMaterial;
            if (!m) return;
            if (m.HasProperty(ShaderUtilities.ID_UnderlayColor))
            {
                m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                m.SetColor(ShaderUtilities.ID_UnderlayColor, c.WithAlpha(0.7f));
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
                m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);
            }
        }

        public static TextMeshPro WorldText(string name, Transform parent, string text, Role role, float size, Vector2 box, Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (parent) go.layer = parent.gameObject.layer;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            Apply(tmp, role, color);
            tmp.fontSize = size;
            tmp.rectTransform.sizeDelta = box;
            tmp.alignment = TextAlignmentOptions.Center;
            return tmp;
        }
    }
}
