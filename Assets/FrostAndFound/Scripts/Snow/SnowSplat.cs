using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// The snow a snowball leaves stuck to whatever it hit: the real room's walls and furniture (through the room
    /// scan's colliders), the trees and props, the kids. It sits flat on the surface, stays about 25 s, then fades
    /// over 5 s. On a kid it rides along on their head or chest. At most 40 at once (the oldest go first) for Quest.
    /// The snow floor gets a crater in the snow instead (SnowSurface).
    /// </summary>
    public class SnowSplat : MonoBehaviour
    {
        const int MaxLive = 40;
        const float Hold = 25f, FadeTime = 5f;
        static readonly List<SnowSplat> s_Live = new List<SnowSplat>();
        static Material s_Mat;
        static MaterialPropertyBlock s_Block;
        static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");

        MeshRenderer m_Renderer;
        float m_Born;
        bool m_Fading;

        public static void Spawn(Vector3 point, Vector3 normal, Collider hit, float size = 0.24f)
        {
            if (!hit || normal.sqrMagnitude < 1e-6f) return;
            int layer = hit.gameObject.layer;
            if (layer != Layers.Room && layer != Layers.Default && layer != Layers.Kid) return;   // not UI, hands, snow
            while (s_Live.Count >= MaxLive)
            {
                var oldest = s_Live[0];
                s_Live.RemoveAt(0);
                if (oldest) Destroy(oldest.gameObject);
            }
            if (!s_Mat)
            {
                s_Mat = Palette.UnlitTextured(FxTextures.WallSplat, Palette.Snow, true, "SnowSplat");
                s_Block = new MaterialPropertyBlock();
            }

            normal.Normalize();
            var go = new GameObject("Snow Splat");
            go.AddComponent<MeshFilter>().sharedMesh = ProcMesh.Quad();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = s_Mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            // the quad faces its own -z: turn that out along the surface normal, with a random spin about it
            var up = Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up;
            go.transform.SetPositionAndRotation(point + normal * 0.006f,
                Quaternion.AngleAxis(Random.Range(0f, 360f), normal) * Quaternion.LookRotation(-normal, up));
            go.transform.localScale = Vector3.one * size * Random.Range(0.85f, 1.2f);

            // on a kid (or anything that moves), ride along with the bit that was hit
            var kid = hit.GetComponentInParent<Kid>();
            Transform follow = null;
            if (kid) follow = hit.name.Contains("Head") && kid.Rig.Head ? kid.Rig.Head : (kid.Rig.Spine ? kid.Rig.Spine : kid.transform);
            else if (hit.attachedRigidbody) follow = hit.attachedRigidbody.transform;
            else if (hit.GetComponentInParent<Sled>()) follow = hit.GetComponentInParent<Sled>().transform;
            if (follow) go.transform.SetParent(follow, true);

            var s = go.AddComponent<SnowSplat>();
            s.m_Renderer = mr;
            s.m_Born = Time.time;
            s_Live.Add(s);
        }

        void Update()
        {
            float age = Time.time - m_Born;
            if (age < Hold) return;
            float t = (age - Hold) / FadeTime;
            if (t >= 1f) { Destroy(gameObject); return; }
            if (!m_Fading) m_Fading = true;
            var c = Palette.Snow;
            c.a = 1f - t * t;
            s_Block.SetColor(s_BaseColor, c);
            m_Renderer.SetPropertyBlock(s_Block);
        }

        void OnDestroy() => s_Live.Remove(this);
    }
}
