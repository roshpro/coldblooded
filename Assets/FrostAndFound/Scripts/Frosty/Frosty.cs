using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// The victim. Frosty sits slumped at the centre of the room inside a chalk outline: base ball upright with a
    /// red scrape low on its front, middle ball knocked askew with a huge brown cocoa splash, head fallen beside
    /// him with purple glitter on the face and his nose missing. Clue details are oversized so they read in photos.
    /// </summary>
    public class Frosty : MonoBehaviour, ISnowballTarget
    {
        public static Frosty Instance { get; private set; }
        public Clue Stain { get; private set; }
        public Clue Glitter { get; private set; }
        public Clue Scrape { get; private set; }
        public DripTrail Drips { get; private set; }
        Transform m_Visual;
        float m_LastHitLine;

        public Vector3 AimPoint => transform.position + Vector3.up * 0.5f;
        public Vector3 Velocity => Vector3.zero;
        public bool AssistEligible => false;

        const float BaseR = 0.3f, MidR = 0.23f, HeadR = 0.17f;

        public static Frosty Create(Vector3 pos, Quaternion facing)
        {
            var go = new GameObject("Frosty (the victim)");
            go.layer = Layers.Kid;
            go.transform.SetPositionAndRotation(pos, facing);
            var f = go.AddComponent<Frosty>();
            f.Build();
            Instance = f;
            FrostyMarker.Position = pos;
            return f;
        }

        /// <summary>A look-alike for key art: no clues registered, not a snowball target.</summary>
        public static Frosty CreateProp(Vector3 pos, Quaternion facing)
        {
            var go = new GameObject("Frosty (prop)");
            go.layer = Layers.Default;
            go.SetActive(false);
            go.transform.SetPositionAndRotation(pos, facing);
            var f = go.AddComponent<Frosty>();
            f.m_IsProp = true;
            f.Build();
            foreach (var c in go.GetComponentsInChildren<Clue>(true)) DestroyImmediate(c);
            go.SetActive(true);
            return f;
        }

        bool m_IsProp;

        void OnEnable() { if (!m_IsProp) SnowballTargets.Register(this); }
        void OnDisable() { SnowballTargets.Unregister(this); if (Instance == this) Instance = null; }

        void Build()
        {
            m_Visual = new GameObject("Visual").transform;
            m_Visual.SetParent(transform, false);
            m_Visual.gameObject.layer = Layers.Kid;
            var custom = FrostAssets.Instance ? FrostAssets.Instance.frostyModel : null;
            var snow = Palette.Toon(Palette.Snow, true, 0.35f);
            var coal = Palette.Toon(Palette.Coal, true, 0.5f);

            Transform baseBall, midBall, headBall;
            if (custom)
            {
                var m = Instantiate(custom, m_Visual, false);
                m.SetLayerRecursive(Layers.Kid);
                baseBall = Find(m.transform, "Base") ?? m.transform;
                midBall = Find(m.transform, "Middle") ?? m.transform;
                headBall = Find(m.transform, "Head") ?? m.transform;
            }
            else
            {
                // base ball sits upright
                baseBall = ProcMesh.Part("Base", m_Visual, ProcMesh.Sphere(20, 14, 0.035f, 1), snow, new Vector3(0, BaseR * 0.9f, 0), Vector3.one * BaseR * 2f).transform;
                // middle ball knocked askew, slumping to one side
                midBall = ProcMesh.Part("Middle", m_Visual, ProcMesh.Sphere(18, 12, 0.04f, 2), snow, new Vector3(0.1f, BaseR * 1.75f + MidR * 0.55f, 0.03f), Vector3.one * MidR * 2f, new Vector3(0, 0, -24)).transform;
                // head fell off and rests in the snow beside him, face up toward you
                headBall = ProcMesh.Part("Head", m_Visual, ProcMesh.Sphere(16, 12, 0.03f, 3), snow, new Vector3(0.46f, HeadR * 0.92f, 0.2f), Vector3.one * HeadR * 2f, new Vector3(-58, 20, 25)).transform;
                // coal buttons on the middle ball
                for (int i = 0; i < 3; i++)
                    ProcMesh.Part("Button", midBall, ProcMesh.Sphere(8, 6), coal, Quaternion.Euler(-28 + i * 22, 0, 0) * new Vector3(0, 0, 0.5f), new Vector3(0.1f, 0.1f, 0.06f));
                // coal eyes and a coal smile on the fallen head ("X" eyes for the deceased)
                foreach (var side in new[] { -1f, 1f })
                {
                    var eye = ProcMesh.Pivot("Eye", headBall, Quaternion.Euler(-14, 22 * side, 0) * new Vector3(0, 0, 0.49f));
                    ProcMesh.Part("X1", eye, ProcMesh.RoundedBox(0.3f, 1), coal, Vector3.zero, new Vector3(0.16f, 0.04f, 0.04f), new Vector3(0, 0, 45));
                    ProcMesh.Part("X2", eye, ProcMesh.RoundedBox(0.3f, 1), coal, Vector3.zero, new Vector3(0.16f, 0.04f, 0.04f), new Vector3(0, 0, -45));
                    eye.localRotation = Quaternion.LookRotation(eye.localPosition.normalized);
                }
                for (int i = 0; i < 5; i++)
                {
                    float a = (i - 2) * 16f;
                    ProcMesh.Part("Smile", headBall, ProcMesh.Sphere(6, 4), coal, Quaternion.Euler(24 - Mathf.Abs(i - 2) * 5, a, 0) * new Vector3(0, 0, 0.5f), Vector3.one * 0.065f);
                }
                // where the nose used to be
                ProcMesh.Part("Nose Hole", headBall, ProcMesh.Sphere(8, 6), Palette.Toon(Palette.SnowShadow, false, 0f), new Vector3(0, 0.02f, 0.47f), new Vector3(0.11f, 0.11f, 0.06f));
                // stick arm still in, one fallen in the snow
                var wood = Palette.Toon(Palette.Wood, true, 0.2f);
                var arm = ProcMesh.Pivot("Arm", midBall, new Vector3(-0.45f, 0.05f, 0), new Vector3(0, 0, 55));
                ProcMesh.Part("Stick", arm, ProcMesh.Cylinder(6, 0.7f), wood, new Vector3(0, 0.6f, 0), new Vector3(0.06f, 1.2f, 0.06f));
                ProcMesh.Part("Twig", arm, ProcMesh.Cylinder(6, 0.5f), wood, new Vector3(0.1f, 0.95f, 0), new Vector3(0.04f, 0.4f, 0.04f), new Vector3(0, 0, -40));
                ProcMesh.Part("Fallen Stick", m_Visual, ProcMesh.Cylinder(6, 0.7f), wood, new Vector3(-0.35f, 0.04f, 0.42f), new Vector3(0.028f, 0.4f, 0.028f), new Vector3(88, 40, 0));
                // scarf around the neck, tail flopped
                var scarf = Palette.Toon(Palette.Books1, true, 0.3f);
                ProcMesh.Part("Scarf", m_Visual, ProcMesh.Torus(0.42f, 0.12f, 18, 8), scarf, new Vector3(0.08f, BaseR * 1.72f, 0.02f), new Vector3(0.5f, 0.42f, 0.5f), new Vector3(0, 0, -14));
                ProcMesh.Part("Scarf Tail", m_Visual, ProcMesh.RoundedBox(0.3f, 2), scarf, new Vector3(0.22f, BaseR * 1.35f, 0.2f), new Vector3(0.08f, 0.24f, 0.03f), new Vector3(10, 0, 20));
                // top hat knocked off, lying on its side
                var hat = ProcMesh.Pivot("Top Hat", m_Visual, new Vector3(-0.45f, 0.08f, -0.2f), new Vector3(0, 30, 82));
                ProcMesh.Part("Crown", hat, ProcMesh.Cylinder(16), coal, new Vector3(0, 0.08f, 0), new Vector3(0.2f, 0.2f, 0.2f));
                ProcMesh.Part("Brim", hat, ProcMesh.Cylinder(18), coal, new Vector3(0, -0.02f, 0), new Vector3(0.3f, 0.015f, 0.3f));
                ProcMesh.Part("Band", hat, ProcMesh.Cylinder(16), Palette.Toon(Palette.StampRed, false), new Vector3(0, 0.0f, 0), new Vector3(0.205f, 0.04f, 0.205f));
            }

            // ---- clue 1: a huge brown cocoa splash on Frosty (middle ball, facing you)
            var stainRoot = ProcMesh.Pivot("Cocoa Splash", midBall, new Vector3(0.05f, 0.08f, 0.47f));
            stainRoot.localRotation = Quaternion.LookRotation(stainRoot.localPosition.normalized);
            var cocoa = Palette.Toon(Palette.Cocoa, false, 0.25f);
            var cocoaLight = Palette.Toon(Palette.CocoaLight, false, 0.25f);
            // an irregular splash: a broad wet patch, radial splatter streaks, flung droplets, and drips running down
            ProcMesh.Part("Splash", stainRoot, ProcMesh.Sphere(14, 8), cocoa, new Vector3(0, -0.02f, 0), new Vector3(0.5f, 0.3f, 0.07f), new Vector3(0, 0, -8));
            ProcMesh.Part("Splash 2", stainRoot, ProcMesh.Sphere(12, 8), cocoaLight, new Vector3(0.1f, 0.04f, -0.008f), new Vector3(0.26f, 0.16f, 0.06f), new Vector3(0, 0, 25));
            var rngS = new System.Random(21);
            for (int i = 0; i < 11; i++)
            {
                float a = i / 11f * Mathf.PI * 2f + (float)rngS.NextDouble() * 0.4f;
                float r = 0.24f + (float)rngS.NextDouble() * 0.12f;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.7f, 0f);
                // a streak pointing outward from the impact, then a droplet beyond it
                ProcMesh.Part("Streak", stainRoot, ProcMesh.Capsule(0.25f, 1f), i % 3 == 0 ? cocoaLight : cocoa,
                    dir * r * 0.75f + Vector3.back * 0.005f, new Vector3(0.035f, 0.13f + (float)rngS.NextDouble() * 0.08f, 0.03f),
                    new Vector3(0, 0, a * Mathf.Rad2Deg - 90f));
                ProcMesh.Part("Droplet", stainRoot, ProcMesh.Sphere(8, 6), cocoa, dir * (r + 0.07f) + Vector3.back * 0.005f,
                    Vector3.one * (0.025f + (float)rngS.NextDouble() * 0.025f) + new Vector3(0, 0, -0.01f));
            }
            for (int i = 0; i < 4; i++)
                ProcMesh.Part("Drip", stainRoot, ProcMesh.Capsule(0.25f, 1f), cocoa, new Vector3(-0.16f + i * 0.1f, -0.22f - (i % 2) * 0.06f, -0.005f), new Vector3(0.045f, 0.2f + (i % 3) * 0.07f, 0.035f));
            Stain = AddClue(stainRoot, ClueId.CocoaStain, 0.12f, true);

            // ---- clue 3: purple glitter on Frosty's face
            var glitterRoot = ProcMesh.Pivot("Glitter", headBall, new Vector3(0.0f, -0.08f, 0.42f));
            glitterRoot.localRotation = Quaternion.LookRotation(glitterRoot.localPosition.normalized);
            var glitter = new Material(Palette.Toon(Palette.Glitter, false, 0.5f)) { name = "FaceGlitter" };
            glitter.SetFloat("_Sparkle", 1f); glitter.SetFloat("_SparkleScale", 220f); glitter.SetColor("_SparkleColor", Palette.GlintWhite);
            ProcMesh.Part("Glitter Patch", glitterRoot, ProcMesh.Sphere(12, 8), glitter, new Vector3(-0.2f, 0.05f, 0), new Vector3(0.36f, 0.26f, 0.08f));
            ProcMesh.Part("Glitter Patch", glitterRoot, ProcMesh.Sphere(12, 8), glitter, new Vector3(0.24f, 0.08f, 0), new Vector3(0.3f, 0.22f, 0.08f));
            ProcMesh.Part("Glitter Blush", glitterRoot, ProcMesh.Sphere(10, 6), Palette.Toon(Palette.PinkBag, false, 0.4f), new Vector3(0.0f, -0.12f, 0.01f), new Vector3(0.2f, 0.06f, 0.06f));
            Glitter = AddClue(glitterRoot, ClueId.PurpleGlitter, 0.09f, true);
            Glitter.glintTint = Palette.Glitter;

            // ---- clue 4: a red scrape low on Frosty's base (crouch to see it)
            var scrapeRoot = ProcMesh.Pivot("Red Scrape", baseBall, new Vector3(-0.08f, -0.2f, 0.45f));
            scrapeRoot.localRotation = Quaternion.LookRotation(scrapeRoot.localPosition.normalized);
            var paint = Palette.Toon(Palette.SledRed, false, 0.3f);
            for (int i = 0; i < 4; i++)
                ProcMesh.Part("Scrape", scrapeRoot, ProcMesh.Capsule(0.25f, 1f), paint, new Vector3(-0.04f + i * 0.04f, i * 0.035f - 0.05f, 0), new Vector3(0.035f, 0.42f - i * 0.06f, 0.03f), new Vector3(0, 0, 65 + i * 4));
            ProcMesh.Part("Gouge", scrapeRoot, ProcMesh.Capsule(0.25f, 1f), Palette.Toon(Palette.SnowShadow, false, 0f), new Vector3(0.02f, 0.06f, -0.005f), new Vector3(0.05f, 0.4f, 0.03f), new Vector3(0, 0, 68));
            Scrape = AddClue(scrapeRoot, ClueId.RedScrape, 0.1f, true);
            Scrape.glintTint = Palette.GlintWhite;

            // colliders (a compound of spheres) so snowballs splat on him and photos can be blocked by him
            AddSphereCollider(baseBall.position, BaseR);
            AddSphereCollider(midBall.position, MidR);
            AddSphereCollider(headBall.position, HeadR);

            ChalkOutline.Create(transform, new[] { (baseBall.position, BaseR), (midBall.position, MidR), (headBall.position, HeadR) });
            if (!custom) MeshMerger.Merge(m_Visual);
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        Clue AddClue(Transform root, ClueId id, float radius, bool facing)
        {
            var c = root.gameObject.AddComponent<Clue>();
            c.id = id;
            c.radius = radius;
            c.useFacing = facing;
            c.localNormal = Vector3.forward;
            c.owner = transform;
            return c;
        }

        void AddSphereCollider(Vector3 world, float r)
        {
            var go = new GameObject("Collider");
            go.layer = Layers.Kid;
            go.transform.SetParent(transform, false);
            go.transform.position = world;
            go.AddComponent<SphereCollider>().radius = r * 0.95f;
        }

        public void AttachDrips(DripTrail drips) => Drips = drips;

        public IEnumerator Reveal()
        {
            m_Visual.localScale = new Vector3(1f, 0.01f, 1f);
            Fx.SnowPuff(transform.position + Vector3.up * 0.1f, Vector3.up, 40, 1f);
            AudioDirector.PlayAt(Sfx.ScoopCrunch, transform.position, 1f, 0.7f);
            yield return Ease.Tween(0.7f, t => m_Visual.localScale = new Vector3(1f, Mathf.Max(0.01f, Ease.OutBack(t)), 1f), Ease.Linear, false);
        }

        public void OnSnowballHit(Snowball ball, Vector3 point, Vector3 velocity, bool headshot)
        {
            SplatDecal.Spawn(point, (point - transform.position).normalized, 0.14f, transform);
            if (Time.time - m_LastHitLine > 10f)
            {
                m_LastHitLine = Time.time;
                var lucy = Kid.Find(CastId.Lucy);
                if (lucy && !DialogueRunner.AnyoneSpeaking) DialogueRunner.Say(Lines.Get("lucy_tamper"), lucy, false);
            }
        }
    }

    /// <summary>The police chalk outline traced in the snow around Frosty's body.</summary>
    public class ChalkOutline : MonoBehaviour
    {
        public static ChalkOutline Create(Transform frosty, (Vector3 center, float radius)[] parts)
        {
            var go = new GameObject("Chalk Outline");
            go.transform.SetParent(frosty, false);
            var c = go.AddComponent<ChalkOutline>();
            c.Build(frosty, parts);
            return c;
        }

        void Build(Transform frosty, (Vector3 center, float radius)[] parts)
        {
            // outline = the boundary of the union of the parts' footprints, padded, traced by marching angles
            var origin = frosty.position;
            var pts = new List<Vector2>();
            var centroid = Vector2.zero;
            foreach (var p in parts) centroid += (p.center - origin).XZ();
            centroid /= parts.Length;
            int n = 96;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float best = 0f;
                foreach (var p in parts)
                {
                    var c = (p.center - origin).XZ() - centroid;
                    float r = p.radius + 0.1f;
                    // ray from centroid along dir against circle (c, r): furthest intersection
                    float b = Vector2.Dot(dir, c);
                    float disc = b * b - (c.sqrMagnitude - r * r);
                    if (disc >= 0f) best = Mathf.Max(best, b + Mathf.Sqrt(disc));
                }
                if (best <= 0f) best = 0.3f;
                // a hand-drawn wobble
                best += Mathf.Sin(a * 7f) * 0.012f + Mathf.Sin(a * 13f + 1f) * 0.008f;
                pts.Add(centroid + dir * best);
            }
            // smooth
            for (int k = 0; k < 2; k++)
            {
                var s = new List<Vector2>(pts);
                for (int i = 0; i < n; i++) pts[i] = (s[(i - 1 + n) % n] + s[i] * 2f + s[(i + 1) % n]) * 0.25f;
            }
            // convert to frosty-local XZ
            var local = new List<Vector2>();
            foreach (var p in pts)
            {
                var w = origin + p.X0Z();
                var l = frosty.InverseTransformPoint(w);
                local.Add(new Vector2(l.x, l.z));
            }
            var mesh = ProcMesh.Ribbon(local, 0.045f, true);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Palette.Unlit(Palette.Chalk);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float y = SnowSurface.Instance ? SnowSurface.Instance.SurfaceHeight - frosty.position.y + 0.004f : 0.04f;
            transform.localPosition = new Vector3(0, y, 0);
        }
    }

    /// <summary>
    /// Step 1's extra cue: a line of brown cocoa drips runs from the stain across the snow straight to Dexter's
    /// feet. The trail sparkles once you've taken the stain photo.
    /// </summary>
    public class DripTrail : MonoBehaviour
    {
        readonly List<Transform> m_Drops = new List<Transform>();
        bool m_Sparkle;
        float m_Next;

        public static DripTrail Create(Vector3 from, Vector3 to, float floorY)
        {
            var go = new GameObject("Cocoa Drip Trail");
            var d = go.AddComponent<DripTrail>();
            d.Build(from, to, floorY);
            return d;
        }

        void Build(Vector3 from, Vector3 to, float floorY)
        {
            var cocoa = Palette.Toon(Palette.Cocoa, false, 0.2f);
            var light = Palette.Toon(Palette.CocoaLight, false, 0.2f);
            var dir = (to - from).Flat();
            float len = dir.magnitude;
            dir /= Mathf.Max(len, 1e-3f);
            var side = Vector3.Cross(Vector3.up, dir);
            float y = (SnowSurface.Instance ? SnowSurface.Instance.SurfaceHeight : floorY) + 0.003f;
            int count = Mathf.Max(6, Mathf.RoundToInt(len / 0.11f));
            var rng = new System.Random(11);
            for (int i = 1; i < count; i++)
            {
                float u = i / (float)count;
                var p = from + dir * (len * u) + side * (Mathf.Sin(u * 7f) * 0.06f + ((float)rng.NextDouble() - 0.5f) * 0.03f);
                p.y = y;
                float s = Mathf.Lerp(0.045f, 0.03f, u) * (0.75f + (float)rng.NextDouble() * 0.5f);
                var drop = ProcMesh.Part("Drip", transform, ProcMesh.Sphere(8, 5), i % 3 == 0 ? light : cocoa, p,
                    new Vector3(s, 0.008f, s * (1.2f + (float)rng.NextDouble() * 0.4f)), new Vector3(0, (float)rng.NextDouble() * 360f, 0));
                drop.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                m_Drops.Add(drop.transform);
            }
        }

        public void SetSparkling(bool on) { m_Sparkle = on; m_Next = Time.time; }

        void Update()
        {
            if (!m_Sparkle || m_Drops.Count == 0 || Time.time < m_Next) return;
            m_Next = Time.time + Random.Range(0.15f, 0.4f);
            var d = m_Drops[Random.Range(0, m_Drops.Count)];
            Fx.GlintAt(d.position + Vector3.up * 0.015f, 0.035f, Palette.Glint);
        }
    }
}
