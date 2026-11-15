using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>What a photo is "of", so a kid shown a picture that isn't evidence can react to what's in it.</summary>
    public enum SubjectKind { None, Kid, Frosty, Sled, Toys, Tree, Nature, Snow }

    /// <summary>
    /// Tags something a kid can recognise in a photo: Frosty, the sled, the bucket and spade, the scenery's trees,
    /// shrubs and rocks. (Kids themselves are found through Kid.All.) Bounds come from the renderers underneath,
    /// or are given explicitly for scenery whose meshes are merged into one shared mesh.
    /// </summary>
    public class PhotoSubject : MonoBehaviour
    {
        public static readonly List<PhotoSubject> All = new List<PhotoSubject>();

        public SubjectKind kind;
        [Tooltip("How eye-catching it is next to everything else in the frame (kids count 1.3).")]
        public float salience = 1f;
        bool m_HasBounds;
        Bounds m_Bounds;   // world space, when given explicitly

        public static PhotoSubject Tag(GameObject go, SubjectKind kind, float salience = 1f)
        {
            if (!go) return null;
            var s = go.GetComponent<PhotoSubject>();
            if (!s) s = go.AddComponent<PhotoSubject>();
            s.kind = kind;
            s.salience = salience;
            return s;
        }

        public PhotoSubject WithBounds(Bounds worldBounds)
        {
            m_Bounds = worldBounds;
            m_HasBounds = true;
            return this;
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        public bool TryGetBounds(out Bounds b)
        {
            if (m_HasBounds) { b = m_Bounds; return true; }
            b = default;
            bool any = false;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                int l = r.gameObject.layer;
                if (l == Layers.PhotoOnly || l == Layers.UI3D || l == Layers.PlayerHands) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any;
        }
    }

    /// <summary>
    /// Works out who and what a photo shows, the way a person would read it: which kids are recognisable in it
    /// (in frame, not hidden behind a wall or furniture or someone else, and big enough to make out), what the
    /// main subject is (the biggest, most central thing — people catch the eye more than scenery), and whether the
    /// rest of the frame is mostly snow. Runs once per shot, after the clue check.
    /// </summary>
    public static class SubjectDetector
    {
        const float MinKidFrac = 0.12f;      // a kid filling under ~12% of the frame height is too small to make out
        const float MinThingFrac = 0.09f;
        const float MinScore = 0.08f;
        static readonly RaycastHit[] s_Hits = new RaycastHit[12];
        static readonly Vector3[] s_Pts = new Vector3[4];
        static readonly List<(CastId id, float score)> s_KidScores = new List<(CastId, float)>();

        public static void Detect(Camera lens, PhotoData data)
        {
            if (!lens || data == null) return;
            var eye = lens.transform.position;
            float tanHalf = Mathf.Tan(lens.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float best = MinScore;
            s_KidScores.Clear();

            // ---- the kids
            foreach (var kid in Kid.All)
            {
                if (!kid || !kid.isActiveAndEnabled) continue;
                float h = Mathf.Max(0.6f, kid.Rig.Height);
                var feet = kid.transform.position;
                s_Pts[0] = kid.HeadPoint;
                s_Pts[1] = kid.ChestPoint;
                s_Pts[2] = feet + Vector3.up * h * 0.32f;
                s_Pts[3] = feet + Vector3.up * h * 0.1f;
                if (!Seen(lens, eye, s_Pts, 4, kid.transform, out float centre, out int visible)) continue;
                float d = Vector3.Distance(eye, feet + Vector3.up * h * 0.5f);
                float frac = h / Mathf.Max(0.2f, d) / (2f * tanHalf);
                if (frac < MinKidFrac) continue;
                // the face matters most: a photo of just their boots doesn't count as a photo of them
                bool face = InFrame(lens.WorldToViewportPoint(s_Pts[0]), 0.02f) && !Blocked(eye, s_Pts[0], kid.transform);
                if (!face && visible < 3) continue;
                data.KidsSeen.Add(kid.id);
                float score = Mathf.Min(frac, 1.1f) * centre * (0.6f + 0.1f * visible) * 1.3f;
                s_KidScores.Add((kid.id, score));
                if (score > best) { best = score; data.Subject = SubjectKind.Kid; data.SubjectKid = kid.id; }
            }

            // ---- Frosty, the sled, the toys, the scenery
            foreach (var s in PhotoSubject.All)
            {
                if (!s || !s.isActiveAndEnabled || !s.TryGetBounds(out var b)) continue;
                var c = b.center;
                var ext = b.extents;
                s_Pts[0] = c;
                s_Pts[1] = c + Vector3.up * ext.y * 0.6f;
                s_Pts[2] = c + lens.transform.right * ext.x * 0.5f;
                s_Pts[3] = c - lens.transform.right * ext.x * 0.5f;
                if (!Seen(lens, eye, s_Pts, 4, s.transform, out float centre, out int visible)) continue;
                float d = Vector3.Distance(eye, c);
                float size = Mathf.Max(ext.y * 2f, Mathf.Max(ext.x, ext.z) * 1.4f);
                float frac = size / Mathf.Max(0.2f, d) / (2f * tanHalf);
                if (frac < MinThingFrac) continue;
                float score = Mathf.Min(frac, 1.1f) * centre * (0.6f + 0.1f * visible) * s.salience;
                if (score > best) { best = score; data.Subject = s.kind; }
            }

            // featured: the main subject, or near enough as prominent (a group shot), not a face in the background
            foreach (var ks in s_KidScores)
                if ((data.Subject == SubjectKind.Kid && data.SubjectKid == ks.id) || ks.score >= best * 0.55f)
                    data.KidsFeatured.Add(ks.id);

            // ---- the backdrop: is the frame mostly snow? (3×3 rays)
            int snowHits = 0; bool centreSnow = false;
            var snow = SnowSurface.Instance;
            int mask = Layers.Mask(Layers.Snow, Layers.Room, Layers.Default, Layers.Kid);
            for (int iy = 0; iy < 3; iy++)
                for (int ix = 0; ix < 3; ix++)
                {
                    var ray = lens.ViewportPointToRay(new Vector3(0.2f + 0.3f * ix, 0.2f + 0.3f * iy, 0f));
                    bool isSnow = false;
                    if (Physics.Raycast(ray, out var hit, 30f, mask, QueryTriggerInteraction.Ignore))
                        isSnow = hit.collider.gameObject.layer == Layers.Snow && (!snow || snow.HasSnowAt(hit.point));
                    else if (snow && ray.direction.y < -0.05f && snow.Room != null)
                    {
                        float t = (snow.Room.FloorY - ray.origin.y) / ray.direction.y;
                        isSnow = t > 0f && snow.HasSnowAt(ray.origin + ray.direction * t);
                    }
                    if (isSnow) { snowHits++; if (ix == 1 && iy == 1) centreSnow = true; }
                }
            data.MostlySnow = snowHits >= 5 || (centreSnow && snowHits >= 3);
            if (data.Subject == SubjectKind.None && data.MostlySnow) data.Subject = SubjectKind.Snow;
        }

        /// <summary>Any of the sample points in frame and in plain view? Returns how central the best one is
        /// (1 dead centre .. ~0.35 in a corner) and how many were visible.</summary>
        static bool Seen(Camera lens, Vector3 eye, Vector3[] pts, int n, Transform self, out float centre, out int visible)
        {
            centre = 0f; visible = 0;
            for (int i = 0; i < n; i++)
            {
                var vp = lens.WorldToViewportPoint(pts[i]);
                if (!InFrame(vp, 0.02f) || Blocked(eye, pts[i], self)) continue;
                visible++;
                float off = Vector2.Distance(new Vector2(vp.x, vp.y), new Vector2(0.5f, 0.5f)) / 0.7071f;
                centre = Mathf.Max(centre, 1f - 0.65f * Mathf.Clamp01(off));
            }
            return visible > 0;
        }

        static bool InFrame(Vector3 vp, float margin) =>
            vp.z > 0.15f && vp.x > -margin && vp.x < 1f + margin && vp.y > -margin && vp.y < 1f + margin;

        /// <summary>Something solid (a wall, furniture, a tree, Frosty, another kid) between the lens and the point?</summary>
        static bool Blocked(Vector3 from, Vector3 to, Transform self)
        {
            var dir = to - from;
            float len = dir.magnitude - 0.06f;
            if (len <= 0.05f) return false;
            int n = Physics.RaycastNonAlloc(from, dir / (len + 0.06f), s_Hits, len,
                Layers.Mask(Layers.Room, Layers.Default, Layers.Kid), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var t = s_Hits[i].collider.transform;
                if (self && t.IsChildOf(self)) continue;
                return true;
            }
            return false;
        }
    }
}
