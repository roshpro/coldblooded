using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Art bible — the single source of every colour in the game.
    /// Style:   Chunky cartoon noir-in-the-snow: flat toon bands, palette-tinted shadows,
    ///          oversized clue props, everything rounded and slightly squashed.
    /// Palette: snow #F4F8FF · snow shadow #AFC3E6 · ink #1E2238 · chalk #FFF3C9 ·
    ///          suspect colours (Tony red, Serena purple, Dexter brown, Sherman blue, Lucy bright blue) ·
    ///          accent glint #FFE27A · evidence red #E0203A.
    /// Mood:    Overcast winter afternoon in a living room turned crime scene: soft warm key,
    ///          cool periwinkle shadow bounce, snow that sparkles.
    /// </summary>
    public static class Palette
    {
        // --- Environment ---------------------------------------------------------------
        public static readonly Color Snow = Hex("#F4F8FF");
        public static readonly Color SnowShadow = Hex("#AFC3E6");
        public static readonly Color SnowDeep = Hex("#8EA6D6");
        public static readonly Color SnowShade = Hex("#93B4EA");   // luminous sky-blue shade on the snow layer
        public static readonly Color SnowPrint = Hex("#7C9EDB");   // soft occlusion in footprints
        public static readonly Color Chalk = Hex("#FFF3C9");
        public static readonly Color Ink = Hex("#1E2238");
        public static readonly Color InkSoft = Hex("#3A3F63");
        public static readonly Color Shadow = Hex("#6E7BB3");     // generic cool toon shadow tint
        public static readonly Color SkyTop = Hex("#9FB6E8");
        public static readonly Color SkyHorizon = Hex("#E9EEFB");
        public static readonly Color SkyGround = Hex("#C9D3EE");
        public static readonly Color Wall = Hex("#D9CFC1");       // simulated room (desktop only)
        public static readonly Color WallTrim = Hex("#A8977F");
        public static readonly Color Floorboards = Hex("#9C7A5A");
        public static readonly Color Wood = Hex("#7A5537");
        public static readonly Color Sofa = Hex("#6F8A7A");
        public static readonly Color LampGlow = Hex("#FFD89A");

        // --- Accents --------------------------------------------------------------------
        public static readonly Color Glint = Hex("#FFE27A");
        public static readonly Color GlintWhite = Hex("#FFFBEA");
        public static readonly Color EvidenceRed = Hex("#E0203A");
        public static readonly Color StampRed = Hex("#D7263D");
        public static readonly Color Manila = Hex("#E9D8A6");
        public static readonly Color ManilaDark = Hex("#C9B27A");
        public static readonly Color Paper = Hex("#FBF8F0");
        public static readonly Color Coal = Hex("#2B2B36");

        // --- Suspects (colour-coded to their clue) --------------------------------------
        public static readonly Color TonyRed = Hex("#E5383B");
        public static readonly Color TonyRedDark = Hex("#A4161A");
        public static readonly Color SledRed = Hex("#D62828");
        public static readonly Color SerenaPurple = Hex("#8E4FD6");
        public static readonly Color SerenaPurpleDark = Hex("#5E2FA0");
        public static readonly Color Glitter = Hex("#C77DFF");
        public static readonly Color PinkBag = Hex("#FF7EB6");
        public static readonly Color DexterBrown = Hex("#8B5A2B");
        public static readonly Color Cocoa = Hex("#6B3E1F");
        public static readonly Color CocoaLight = Hex("#A06A3F");
        public static readonly Color Apron = Hex("#F3E3C3");
        public static readonly Color ShermanBlue = Hex("#4A6FD8");
        public static readonly Color ShermanSuit = Hex("#F2F4FA");
        public static readonly Color Carrot = Hex("#FF8C1A");
        public static readonly Color CarrotLeaf = Hex("#4CAF50");
        public static readonly Color LucyBlue = Hex("#1FA4FF");
        public static readonly Color LucyBlueDark = Hex("#0B6FC2");
        public static readonly Color Deerstalker = Hex("#B08A5B");
        public static readonly Color Tights = Hex("#F7F7FB");
        public static readonly Color Hair = Hex("#1B1A22");
        public static readonly Color HairBrown = Hex("#4A2E1F");
        public static readonly Color Goggles = Hex("#FFB703");
        public static readonly Color Lens = Hex("#3A86FF");
        public static readonly Color Gold = Hex("#E8B23A");
        public static readonly Color Books1 = Hex("#2A9D8F");
        public static readonly Color Books2 = Hex("#E76F51");
        public static readonly Color Books3 = Hex("#F4A261");
        public static readonly Color Marshmallow = Hex("#FFF7F0");
        public static readonly Color Mitten = Hex("#2EC4B6");       // the player's mittens
        public static readonly Color MittenCuff = Hex("#F4F8FF");
        public static readonly Color Boot = Hex("#3B2F2F");
        public static readonly Color BirdieYellow = Hex("#FFD23F");

        // --- Skin tones (a varied cast) -------------------------------------------------
        public static readonly Color SkinA = Hex("#F2C9A0");
        public static readonly Color SkinB = Hex("#C68B59");
        public static readonly Color SkinC = Hex("#8D5524");
        public static readonly Color SkinD = Hex("#E8B98F");
        public static readonly Color SkinE = Hex("#FFD7B5");
        public static readonly Color Cheek = Hex("#FF9AA2");

        // --- Polaroid -------------------------------------------------------------------
        public static readonly Color CameraBody = Hex("#F1EDE4");
        public static readonly Color CameraDark = Hex("#2C2C34");
        public static readonly Color Rainbow1 = Hex("#E63946");
        public static readonly Color Rainbow2 = Hex("#F4A261");
        public static readonly Color Rainbow3 = Hex("#E9C46A");
        public static readonly Color Rainbow4 = Hex("#2A9D8F");
        public static readonly Color Rainbow5 = Hex("#3A86FF");

        // --- System UI: Horizon OS layout, winter "ice glass" skin -------------------------
        // Deep winter-blue glass panels (slightly see-through), lighter ice cards, a frosty hairline stroke,
        // one icy-blue primary action. Contrast: UiText on UiPanel ≈ 12:1, UiTextDim on UiSurface ≈ 5.8:1.
        public static readonly Color UiPanel = Hex("#12305A").WithAlpha(0.9f);
        public static readonly Color UiPanelSolid = Hex("#12305A");
        public static readonly Color UiStroke = Hex("#CFEFFF").WithAlpha(0.34f);
        public static readonly Color UiSurface = Hex("#1D4478");
        public static readonly Color UiSurfaceHover = Hex("#285894");
        public static readonly Color UiSurfacePressed = Hex("#336BAE");
        public static readonly Color UiText = Hex("#F3F9FF");
        public static readonly Color UiTextDim = Hex("#AFCBEB");
        public static readonly Color UiTextFaint = Hex("#7F9EC6");
        public static readonly Color UiAccent = Hex("#2389F5");
        public static readonly Color UiAccentHover = Hex("#45A2FF");
        public static readonly Color UiAccentPressed = Hex("#176ED2");
        public static readonly Color UiTrackOff = Hex("#3B5C88");
        public static readonly Color UiIce = Hex("#9FDBFF");          // overlines, hover glow, icicle tint
        public static readonly Color UiDeep = Hex("#0A1C38");         // stages behind imagery
        public static readonly Color UiShadow = Hex("#020B1A").WithAlpha(0.32f);
        public static readonly Color UiScrimTop = Hex("#0A1E3C").WithAlpha(0f);
        public static readonly Color UiScrimBottom = Hex("#0A1E3C").WithAlpha(0.92f);

        // --- In-world UI ----------------------------------------------------------------
        public static readonly Color BubbleFill = Hex("#FFFDF7");
        public static readonly Color BubbleText = Hex("#1E2238");
        public static readonly Color PanelInk = new Color(0.118f, 0.133f, 0.22f, 0.86f);

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static Color WithAlpha(this Color c, float a) { c.a = a; return c; }

        /// <summary>A darker, cooler sibling of a base colour used for the toon shadow band.</summary>
        /// <summary>A soft edge line: a deeper, slightly cooler tone of the object's own colour.</summary>
        public static Color OutlineOf(Color c)
        {
            var s = ShadeOf(c);
            return new Color(s.r * 0.72f, s.g * 0.74f, s.b * 0.8f, 1f);
        }

        public static Color ShadeOf(Color c)
        {
            var cool = Color.Lerp(c, Shadow, 0.38f);
            return new Color(cool.r * 0.78f, cool.g * 0.8f, cool.b * 0.9f, 1f);
        }

        // ------------------------------------------------------------------------------------
        // Material cache — every renderer pulls its material from here so that colours always
        // trace back to the palette and SRP-batcher-friendly materials are shared.
        // ------------------------------------------------------------------------------------
        static readonly Dictionary<string, Material> s_Cache = new Dictionary<string, Material>();

        public static Material Toon(Color c, bool outline = false, float rim = 0.25f, float emission = 0f)
        {
            string key = "toon" + ColorUtility.ToHtmlStringRGBA(c) + outline + rim.ToString("0.00") + emission.ToString("0.00");
            if (s_Cache.TryGetValue(key, out var m) && m) return m;
            m = new Material(FrostAssets.ToonShader(outline)) { name = "Toon_" + ColorUtility.ToHtmlStringRGB(c) + (outline ? "_OL" : "") };
            m.SetColor("_BaseColor", c);
            m.SetColor("_ShadeColor", ShadeOf(c));
            m.SetColor("_RimColor", Color.Lerp(c, Color.white, 0.6f));
            m.SetFloat("_RimStrength", rim * 0.75f);
            m.SetFloat("_AmbientStrength", 0.1f);
            m.SetColor("_EmissionColor", c * emission);
            // soft, painterly shading: a wide gradient instead of a hard cel band, and where an object keeps an
            // outline it's a thin line in a deeper tone of its own colour, never black ink
            m.SetFloat("_BandThreshold", 0.46f);
            m.SetFloat("_BandSoftness", 0.26f);
            m.SetColor("_OutlineColor", OutlineOf(c));
            m.SetFloat("_OutlineWidth", outline ? 0.45f : 0f);
            m.enableInstancing = true;
            s_Cache[key] = m;
            return m;
        }

        public static Material ToonTextured(Texture tex, Color tint, bool outline = false, string name = "ToonTex")
        {
            var m = new Material(FrostAssets.ToonShader(outline)) { name = name };
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            m.SetColor("_ShadeColor", ShadeOf(tint));
            m.SetColor("_RimColor", Color.Lerp(tint, Color.white, 0.6f));
            m.SetFloat("_RimStrength", 0.2f);
            m.SetFloat("_BandThreshold", 0.46f);
            m.SetFloat("_BandSoftness", 0.26f);
            m.SetColor("_OutlineColor", OutlineOf(tint));
            m.SetFloat("_OutlineWidth", outline ? 0.45f : 0f);
            return m;
        }

        /// <summary>A painted character texture with soft, gradual shading and no ink outline.</summary>
        public static Material SoftTextured(Texture tex, string name)
        {
            var m = new Material(FrostAssets.ToonShader(false)) { name = name };
            if (tex) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetColor("_ShadeColor", new Color(0.8f, 0.83f, 0.93f));   // a cool, light shade (multiplied by the texture)
            m.SetColor("_RimColor", new Color(0.92f, 0.96f, 1f));
            m.SetFloat("_RimStrength", 0.08f);
            // characters ignore cast shadows entirely: their own hats (one mesh with the body) would otherwise drop a
            // grey-blue shade over face and coat and wash out the colours. URP doesn't pass the renderer's
            // receiveShadows flag to our toon shader, so this has to be switched off in the material.
            m.SetFloat("_ShadowStrength", 0f);
            m.SetFloat("_RimPower", 3f);
            m.SetFloat("_BandThreshold", 0.42f);
            m.SetFloat("_BandSoftness", 0.34f);
            m.SetFloat("_AmbientStrength", 0.22f);
            // no light from below: undersides (chin, hat brim, coat hem) sit in shade instead of looking lit by the snow
            m.SetFloat("_UnderShade", 1f);
            m.SetFloat("_OutlineWidth", 0f);
            return m;
        }

        public static Material Unlit(Color c, bool transparent = false)
        {
            string key = "unlit" + ColorUtility.ToHtmlStringRGBA(c) + transparent;
            if (s_Cache.TryGetValue(key, out var m) && m) return m;
            m = new Material(FrostAssets.Shader(transparent ? FrostAssets.ShaderId.UnlitTransparent : FrostAssets.ShaderId.Unlit)) { name = "Unlit_" + ColorUtility.ToHtmlStringRGBA(c) };
            m.SetColor("_BaseColor", c);
            s_Cache[key] = m;
            return m;
        }

        /// <summary>Invisible depth-only material: real objects (MR furniture) hide virtual things behind them.</summary>
        public static Material DepthOccluder()
        {
            const string key = "depthOccluder";
            if (s_Cache.TryGetValue(key, out var m) && m) return m;
            m = new Material(FrostAssets.Shader(FrostAssets.ShaderId.DepthOccluder)) { name = "Depth Occluder", enableInstancing = true };
            s_Cache[key] = m;
            return m;
        }

        public static Material UnlitTextured(Texture tex, Color tint, bool transparent = true, string name = "UnlitTex")
        {
            var m = new Material(FrostAssets.Shader(transparent ? FrostAssets.ShaderId.UnlitTransparent : FrostAssets.ShaderId.Unlit)) { name = name };
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            return m;
        }

        public static Material Particle(Texture tex, bool additive = false, string name = "Particle")
        {
            string key = "particle" + (tex ? tex.name : "none") + additive;
            if (s_Cache.TryGetValue(key, out var m) && m) return m;
            m = new Material(FrostAssets.Shader(FrostAssets.ShaderId.Particle)) { name = name };
            m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Additive", additive ? 1f : 0f);
            if (additive)
            {
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            }
            s_Cache[key] = m;
            return m;
        }
    }
}
