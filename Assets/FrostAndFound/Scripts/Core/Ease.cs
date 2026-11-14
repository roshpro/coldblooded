using System;
using System.Collections;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>Three curves plus a coroutine tween helper (unscaled by default so UI keeps moving while paused).</summary>
    public static class Ease
    {
        public static float Linear(float t) => t;
        public static float OutCubic(float t) { t = 1f - Mathf.Clamp01(t); return 1f - t * t * t; }
        public static float InCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }
        public static float InOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }
        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }
        public static float OutElastic(float t)
        {
            t = Mathf.Clamp01(t);
            if (t == 0f || t == 1f) return t;
            const float c4 = 2f * Mathf.PI / 3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }
        public static float OutBounce(float t)
        {
            t = Mathf.Clamp01(t);
            const float n1 = 7.5625f, d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1; return n1 * t * t + 0.984375f;
        }
        /// <summary>0→1→0 bump, handy for pops.</summary>
        public static float Bump(float t) => Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);

        public static IEnumerator Tween(float duration, Action<float> apply, Func<float, float> ease = null, bool unscaled = true)
        {
            ease ??= OutCubic;
            if (duration <= 0f) { apply(ease(1f)); yield break; }
            for (float t = 0f; t < 1f; t += (unscaled ? Time.unscaledDeltaTime : Time.deltaTime) / duration)
            {
                apply(ease(t));
                yield return null;
            }
            apply(ease(1f));
        }

        /// <summary>Frame-rate independent exponential damping.</summary>
        public static float Damp(float current, float target, float lambda, float dt) =>
            Mathf.Lerp(current, target, 1f - Mathf.Exp(-lambda * dt));
        public static Vector3 Damp(Vector3 current, Vector3 target, float lambda, float dt) =>
            Vector3.Lerp(current, target, 1f - Mathf.Exp(-lambda * dt));
        public static Quaternion Damp(Quaternion current, Quaternion target, float lambda, float dt) =>
            Quaternion.Slerp(current, target, 1f - Mathf.Exp(-lambda * dt));
    }

    /// <summary>A hidden MonoBehaviour that runs coroutines for plain classes and static helpers.</summary>
    public class CoroutineHost : MonoBehaviour
    {
        static CoroutineHost s_Host;
        public static CoroutineHost Host
        {
            get
            {
                if (!s_Host)
                {
                    var go = new GameObject("[Frost Coroutines]");
                    go.hideFlags = HideFlags.HideInHierarchy;
                    s_Host = go.AddComponent<CoroutineHost>();
                }
                return s_Host;
            }
        }
        public static Coroutine Run(IEnumerator routine) => Host.StartCoroutine(routine);
        public static void Stop(Coroutine c) { if (c != null && s_Host) s_Host.StopCoroutine(c); }
    }

    public static class FrostUtil
    {
        public static T GetOrAdd<T>(this GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c ? c : go.AddComponent<T>();
        }

        public static void SetLayerRecursive(this GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) t.gameObject.SetLayerRecursive(layer);
        }

        public static Vector3 Flat(this Vector3 v) => new Vector3(v.x, 0f, v.z);
        public static Vector2 XZ(this Vector3 v) => new Vector2(v.x, v.z);
        public static Vector3 X0Z(this Vector2 v, float y = 0f) => new Vector3(v.x, y, v.y);

        public static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(a.XZ(), b.XZ());

        public static Quaternion FlatLookRotation(Vector3 from, Vector3 to)
        {
            var d = (to - from).Flat();
            return d.sqrMagnitude < 1e-6f ? Quaternion.identity : Quaternion.LookRotation(d.normalized, Vector3.up);
        }

        public static void DestroySafe(UnityEngine.Object o)
        {
            if (!o) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o); else UnityEngine.Object.DestroyImmediate(o);
        }

        public static float Hash01(int seed)
        {
            unchecked
            {
                uint x = (uint)seed * 747796405u + 2891336453u;
                x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
                x = (x >> 22) ^ x;
                return (x & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }

    /// <summary>Layer indices configured by the project setup (see Editor/FrostProjectSetup.cs).</summary>
    public static class Layers
    {
        public const int Default = 0;
        public const int Snow = 6;          // the snow surface
        public const int Kid = 7;           // suspects + Frosty (snowball targets)
        public const int PlayerHands = 8;   // mittens, held camera/photo/snowball — never photographed
        public const int PhotoOnly = 9;     // snowy photo backdrop, only the Polaroid lens sees it
        public const int Snowball = 10;
        public const int Room = 11;         // MRUK / simulated room colliders
        public const int UI3D = 12;         // world-space UI

        public static readonly string[] Names =
        {
            null, null, null, null, null, null,
            "Snow", "Kid", "PlayerHands", "PhotoOnly", "Snowball", "Room", "UI3D"
        };

        public static int Mask(params int[] layers)
        {
            int m = 0;
            foreach (var l in layers) m |= 1 << l;
            return m;
        }
    }
}
