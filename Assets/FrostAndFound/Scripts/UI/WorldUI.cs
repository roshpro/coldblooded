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
                root.position = rig.Head.position + fwd * 1.25f + Vector3.up * 0.1f;
                root.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
            var stamp = UIKit.Stamp(root, "CASE CLOSED", 1.25f, Palette.StampRed, -7f, 0.95f);
            root.gameObject.SetLayerRecursive(Layers.UI3D);
            yield return Ease.Tween(0.2f, t => root.localScale = Vector3.one * Mathf.Lerp(3f, 1f, t), Ease.InCubic, false);
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
