using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Project-wide asset hub, loaded from Resources/FrostAssets. It keeps the custom shaders referenced
    /// so they ship in builds (materials are created at runtime), and it is where the team plugs in
    /// final Blender models and ElevenLabs audio without touching code.
    /// Create / refresh it with the menu  Frost & Found ▸ Setup ▸ Refresh Asset Hub.
    /// </summary>
    [CreateAssetMenu(menuName = "Frost & Found/Asset Hub", fileName = "FrostAssets")]
    public class FrostAssets : ScriptableObject
    {
        public enum ShaderId { Toon, ToonOutlined, Snow, Unlit, UnlitTransparent, Particle, Sky, Photo, Blur, UIGlass, Wool, DepthOccluder }

        [Header("Shaders (auto-filled by Setup)")]
        public Shader toon;
        public Shader toonOutlined;
        public Shader snow;
        public Shader unlit;
        public Shader unlitTransparent;
        public Shader particle;
        public Shader sky;
        public Shader photo;
        public Shader blur;
        public Shader uiGlass;
        public Shader wool;
        public Shader depthOccluder;

        [Header("Fonts (auto-filled by Setup from Assets/FrostAndFound/Fonts)")]
        public TMPro.TMP_FontAsset displayFont;   // Roboto Black
        public TMPro.TMP_FontAsset headingFont;   // Roboto Bold
        public TMPro.TMP_FontAsset bodyFont;      // Inter SemiBold
        public TMPro.TMP_FontAsset regularFont;   // Inter Regular
        public TMPro.TMP_FontAsset monoFont;      // Roboto Mono Bold
        public TMPro.TMP_FontAsset handFont;      // Inter SemiBold Italic
        [Tooltip("Legacy fallbacks")] public TMPro.TMP_FontAsset speechFont;
        public TMPro.TMP_FontAsset titleFont;

        [Header("Final art — drop Blender prefabs here to replace the mockups")]
        [Tooltip("Optional model per character. Leave empty to use the procedural mockup.")]
        public CharacterModelSlot[] characterModels = new CharacterModelSlot[0];
        public GameObject frostyModel;
        [Tooltip("Colour texture for the custom snowman (embedded in its FBX; extracted by Setup).")]
        public Texture2D frostyTexture;
        public GameObject polaroidModel;
        public GameObject sledModel;

        [Header("Audio overrides (optional; Resources/SFX/<id> and Resources/Voice/<lineId> also work)")]
        public AudioClip music;

        static FrostAssets s_Instance;
        public static FrostAssets Instance
        {
            get
            {
                if (!s_Instance) s_Instance = Resources.Load<FrostAssets>("FrostAssets");
                return s_Instance;
            }
        }

        public static Shader Shader(ShaderId id)
        {
            var a = Instance;
            Shader s = null;
            if (a)
            {
                switch (id)
                {
                    case ShaderId.Toon: s = a.toon; break;
                    case ShaderId.ToonOutlined: s = a.toonOutlined; break;
                    case ShaderId.Snow: s = a.snow; break;
                    case ShaderId.Unlit: s = a.unlit; break;
                    case ShaderId.UnlitTransparent: s = a.unlitTransparent; break;
                    case ShaderId.Particle: s = a.particle; break;
                    case ShaderId.Sky: s = a.sky; break;
                    case ShaderId.Photo: s = a.photo; break;
                    case ShaderId.Blur: s = a.blur; break;
                    case ShaderId.UIGlass: s = a.uiGlass; break;
                    case ShaderId.Wool: s = a.wool; break;
                    case ShaderId.DepthOccluder: s = a.depthOccluder; break;
                }
            }
            if (s) return s;
            string name = "FrostAndFound/" + id;
            s = UnityEngine.Shader.Find(name);
            if (!s)
            {
                Debug.LogError("[Frost] Shader '" + name + "' missing. Run Frost & Found ▸ Setup ▸ Refresh Asset Hub.");
                s = UnityEngine.Shader.Find("Universal Render Pipeline/Unlit");
            }
            return s;
        }

        public static Shader ToonShader(bool outlined) => Shader(outlined ? ShaderId.ToonOutlined : ShaderId.Toon);

        public static GameObject ModelFor(CastId id) => SlotFor(id)?.prefab;

        public static CharacterModelSlot SlotFor(CastId id)
        {
            var a = Instance;
            if (!a || a.characterModels == null) return null;
            foreach (var slot in a.characterModels)
                if (slot != null && slot.character == id && slot.prefab) return slot;
            return null;
        }
    }

    [System.Serializable]
    public class CharacterModelSlot
    {
        public CastId character;
        [Tooltip("Prefab with the rigged model. Child transforms named Head, Hand_L, Hand_R and (optional) ClueAnchor are used as attachment points.")]
        public GameObject prefab;
        [Tooltip("Optional Animator controller with the parameters documented in README (Speed, Talk, Point, Hit, Dodge, Dazed, Pose).")]
        public RuntimeAnimatorController animator;
        public float scale = 1f;
        [Tooltip("Colour texture for the soft character material (Meshy texture_0).")]
        public Texture2D texture;
    }
}
