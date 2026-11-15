using System.Collections;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// "Lucy stamps CASE CLOSED in the air" (doc §6.7) — an in-world content moment, so it keeps the game's
    /// rubber-stamp look rather than the system-UI style.
    /// </summary>
    public static class CaseClosedStamp
    {
        public static IEnumerator Slam()
        {
            var root = new GameObject("CASE CLOSED").transform;
            root.gameObject.layer = Layers.UI3D;
            var rig = PlayerRig.Active;
            if (rig)
            {
                var fwd = rig.FlatForward;
                root.position = rig.Head.position + fwd * 1.5f + Vector3.up * 0.12f;
                root.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
            var stamp = UIKit.Stamp(root, "CASE CLOSED", 1.25f, Palette.StampRed, -7f, 0.95f);
            root.gameObject.SetLayerRecursive(Layers.UI3D);
            yield return Ease.Tween(0.2f, t => root.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, t), Ease.InCubic, false);
            AudioDirector.Play2D(Sfx.StampSlam, 1f);
            AudioDirector.Play2D(Sfx.Fanfare, 0.7f);
            rig?.CameraHand.Haptic(1f, 0.15f);
            rig?.FreeHand.Haptic(1f, 0.15f);
            Fx.ConfettiBurst(root.position + Vector3.down * 0.2f, 60);
            yield return Ease.Tween(0.15f, t => root.localScale = Vector3.one * (1f + 0.06f * Ease.Bump(t)), Ease.Linear, false);
            yield return new WaitForSeconds(1.5f);
            var from = root.position;
            yield return Ease.Tween(0.45f, t => { root.position = from + Vector3.up * 0.35f * t; root.localScale = Vector3.one * Mathf.Lerp(1f, 0.0f, Ease.InCubic(t)); }, Ease.Linear, false);
            Object.Destroy(root.gameObject);
        }
    }
}

namespace FrostAndFound
{
    /// <summary>
    /// Draws the world-space UI (layer UI3D) after everything else, with a fresh depth buffer: a URP overlay camera
    /// stacked on the view camera. Messages, notices and menus are then always legible, never hidden behind the snow,
    /// the kids or anything else in the room.
    /// </summary>
    public static class UIOverlayCamera
    {
        public static Camera Attach(Camera view)
        {
            if (!view) return null;
            var baseData = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(view);
            if (baseData == null || baseData.renderType != UnityEngine.Rendering.Universal.CameraRenderType.Base) return null;
            foreach (var c in baseData.cameraStack) if (c && c.name == "UI Overlay Camera") return c;   // already attached
            var go = new GameObject("UI Overlay Camera");
            go.transform.SetParent(view.transform, false);
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(view);
            cam.cullingMask = Layers.Mask(Layers.UI3D);
            cam.clearFlags = CameraClearFlags.Depth;
            var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
            data.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Overlay;
            data.renderShadows = false;
            data.renderPostProcessing = false;
            baseData.cameraStack.Add(cam);
            view.cullingMask &= ~Layers.Mask(Layers.UI3D);
            Debug.Log("[Frost] UI drawn on top (overlay camera).");
            return cam;
        }
    }
}
