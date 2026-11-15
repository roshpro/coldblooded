using UnityEngine;
using UnityEngine.Rendering;

namespace FrostAndFound
{
    /// <summary>
    /// Real-world occlusion (Meta Depth API, see FrostOcclusion.hlsl): a real person or piece of furniture in front of
    /// a kid hides the kid. It applies only while the player's own eyes render, so the UI overlay and the Polaroid's
    /// lens always see everything. <see cref="Exempt"/> opts a hierarchy out: the player's own kit (the hand holding
    /// it would cut it out) and things that deliberately live beyond the real walls, like the aurora.
    /// </summary>
    public static class DepthOcclusion
    {
        /// <summary>Must match FROST_NO_OCCLUSION_LAYER in FrostOcclusion.hlsl.</summary>
        public const uint ExemptLayerBit = 1u << 30;

        static readonly int k_Off = Shader.PropertyToID("_FrostOcclusionOff");
        static bool s_Hooked;

        public static void Init()
        {
            Shader.SetGlobalFloat(k_Off, 1f);
            if (s_Hooked) return;
            s_Hooked = true;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        static void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            var rig = PlayerRig.Active;
            Shader.SetGlobalFloat(k_Off, rig != null && cam == rig.ViewCamera ? 0f : 1f);
        }

        /// <summary>Never occlude anything under <paramref name="root"/> (renderers added later need another call).</summary>
        public static void Exempt(GameObject root)
        {
            if (!root) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.renderingLayerMask |= ExemptLayerBit;
        }

        public static void Exempt(Renderer r)
        {
            if (r) r.renderingLayerMask |= ExemptLayerBit;
        }
    }
}
