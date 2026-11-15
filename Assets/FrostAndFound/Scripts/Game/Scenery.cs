using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FrostAndFound
{
    /// <summary>
    /// Nature for the room, scaled to how much open floor there is: snowy coast redwoods (the Stanford tree) by the
    /// walls and, in big spaces, standing together in little groves; snow-laden shrubs; rocks and fallen logs with
    /// snow on top; and big wind-heaped drifts in the corners and banked round the trunks. A small room gets a shrub,
    /// a rock and a couple of drifts; a hall gets a snowy grove.
    /// Everything keeps clear of the middle (where Frosty and the kids go), of where you stand, of the view between,
    /// and of furniture, and it joins room.IsBlocked so the kids walk round it. Built with the snow, before the
    /// case, so the title screen has it too. All the parts share a few materials and are merged into a few meshes.
    /// </summary>
    public static class Scenery
    {
        struct Obstacle { public Vector3 pos; public float radius; }

        static readonly Color Bark = new Color(0.62f, 0.3f, 0.17f);        // redwood: red-brown, fibrous
        static readonly Color LogBark = new Color(0.43f, 0.28f, 0.2f);
        static readonly Color Heartwood = new Color(0.82f, 0.64f, 0.44f);
        static readonly Color Needles = new Color(0.16f, 0.38f, 0.27f);
        static readonly Color Needles2 = new Color(0.21f, 0.45f, 0.31f);
        static readonly Color Leaves = new Color(0.29f, 0.53f, 0.37f);
        static readonly Color Stone = new Color(0.53f, 0.57f, 0.64f);

        class Kit
        {
            public Transform root;
            public RoomInfo room;
            public SnowSurface snow;
            public System.Random rng;
            public Material bark, logBark, wood, needles, needles2, leaves, stone, snowMat;
            public float Range(float a, float b) => Mathf.Lerp(a, b, (float)rng.NextDouble());

            /// <summary>A part in world space under the (identity) scenery root, posed in an item's own frame.</summary>
            public void Part(string name, Mesh mesh, Material mat, Vector3 basePos, Quaternion frame, Vector3 local, Vector3 scale, Quaternion localRot, bool shadow = true)
            {
                var go = ProcMesh.Part(name, root, mesh, mat, basePos + frame * local, scale, (frame * localRot).eulerAngles);
                if (!shadow) go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            public GameObject Holder(string name, Vector3 pos)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                go.transform.position = pos;
                return go;
            }
        }

        public static GameObject Create(RoomInfo room, PlayerRig player, int seed = 11)
        {
            var go = new GameObject("Scenery");
            var tune = FrostTuning.I;
            float density = tune ? tune.sceneryDensity : 1f;
            if (room == null || density <= 0f) return go;

            var snowMat = new Material(Palette.Toon(Palette.Snow, false, 0.35f)) { name = "Scenery Snow" };
            snowMat.SetColor("_ShadeColor", Palette.SnowShade);   // the same luminous blue shade as the snow on the floor
            var k = new Kit
            {
                root = go.transform, room = room, snow = SnowSurface.Instance, rng = new System.Random(seed),
                bark = Palette.Toon(Bark, false, 0.12f), logBark = Palette.Toon(LogBark, false, 0.1f), wood = Palette.Toon(Heartwood, false, 0.1f),
                needles = Palette.Toon(Needles, false, 0.18f), needles2 = Palette.Toon(Needles2, false, 0.18f),
                leaves = Palette.Toon(Leaves, false, 0.2f), stone = Palette.Toon(Stone, false, 0.15f), snowMat = snowMat,
            };

            var feet = player ? player.FeetPosition : room.Center;
            var facing = player ? player.FlatForward : Vector3.forward;
            var centre = room.Center;

            // ---- how much open floor is there?
            var open = new List<Vector3>();
            var b = room.Bounds;
            const float step = 0.25f;
            for (float x = b.xMin + step * 0.5f; x < b.xMax; x += step)
                for (float z = b.yMin + step * 0.5f; z < b.yMax; z += step)
                {
                    var p = new Vector3(x, room.FloorY, z);
                    if (room.IsFree(p, 0.05f, 0.1f)) open.Add(p);
                }
            float area = open.Count * step * step;
            if (open.Count == 0) return go;

            // ---- the play space stays clear: the middle (Frosty, the kids), where you stand, the view of the scene
            // and the title button in front of you (snowballs thrown at it mustn't hit a tree)
            float centreClear = Mathf.Clamp(1.4f + 0.025f * area, 1.6f, 2.6f);
            bool InPlay(Vector3 p, float r) =>
                FrostUtil.FlatDistance(p, centre) < centreClear + r * 0.5f
                || FrostUtil.FlatDistance(p, feet) < 1.2f + r
                || SegmentDistance(p, feet, centre) < 0.7f + r * 0.5f
                || SegmentDistance(p, feet, feet + facing * 3f) < 0.8f + r * 0.5f;

            // ---- the kids walk round everything placed here
            var obstacles = new List<Obstacle>();
            var furniture = room.IsBlocked;
            room.IsBlocked = (p, r) =>
            {
                if (furniture != null && furniture(p, r)) return true;
                for (int i = 0; i < obstacles.Count; i++)
                    if (FrostUtil.FlatDistance(p, obstacles[i].pos) < obstacles[i].radius + r) return true;
                return false;
            };

            int Budget(float n) => Mathf.Max(0, Mathf.RoundToInt(n * density));
            int trees = Budget(area < 11f ? 0f : Mathf.Min(9f, 1f + Mathf.Floor((area - 11f) / 9f)));
            int shrubs = Budget(Mathf.Clamp(area / 7f, 1f, 10f));
            int rocks = Budget(Mathf.Clamp(area / 12f, 1f, 6f));
            int logs = Budget(area < 22f ? 0f : Mathf.Min(3f, area / 28f));
            int drifts = Budget(Mathf.Clamp(1f + area / 9f, 2f, 10f));
            bool big = area > 30f;

            // the best of every open spot (jittered off the grid); false when nothing fits
            bool Place(float clear, float wallMin, System.Func<Vector3, float> score, out Vector3 spot)
            {
                spot = default; float best = float.MinValue;
                for (int i = 0; i < open.Count; i++)
                {
                    var p = open[i] + new Vector3(k.Range(-0.12f, 0.12f), 0f, k.Range(-0.12f, 0.12f));
                    if (!room.IsFree(p, clear, wallMin) || InPlay(p, clear)) continue;
                    float s = score(p) + k.Range(0f, 0.35f);
                    if (s > best) { best = s; spot = p; }
                }
                return best > float.MinValue;
            }
            float WallScore(Vector3 p, float want) => -Mathf.Abs(room.EdgeDistance(new Vector2(p.x, p.z)) - want) * 2f;
            // 0 in the open .. ~0.6 tucked in a corner: how much of the ring around p is wall or furniture
            float Enclosure(Vector3 p, float d)
            {
                int n = 0;
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 0.25f;
                    var q = p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d;
                    if (room.EdgeDistance(new Vector2(q.x, q.z)) < 0f || (furniture != null && furniture(q, 0f))) n++;
                }
                return n / 8f;
            }

            var placed = new int[5];
            // ---- redwoods: by the walls and in the corners; in a big space some stand out in the open in groves
            var treeSpots = new List<Vector3>();
            float maxTree = Mathf.Min(3.4f, room.CeilingY - room.FloorY - 0.2f);
            for (int i = 0; i < trees && maxTree > 1.2f; i++)
            {
                float h = Mathf.Max(1.25f, maxTree * k.Range(0.62f, 1f));
                // most trees spread out round the walls; some join one already standing, as a little grove
                bool grove = i >= 1 && k.rng.NextDouble() < (big ? 0.4 : 0.2);
                Vector3 spot = default; bool ok = false; float crown = 0f;
                // a tight corner gets a smaller tree rather than none
                for (int attempt = 0; attempt < 3 && !ok; attempt++)
                {
                    if (attempt > 0) h = Mathf.Max(1.2f, h * 0.8f);
                    crown = 0.25f * h;
                    float cr = crown;
                    ok = Place(cr * 0.85f, cr * 0.95f, p =>
                    {
                        float s = WallScore(p, cr + 0.15f) * (grove ? 0.3f : 1f) + Enclosure(p, 0.9f) * 1.5f;
                        float nearest = 99f;
                        foreach (var t in treeSpots)
                        {
                            float d = FrostUtil.FlatDistance(p, t);
                            if (d < cr * 1.8f) s -= 4f;             // crowns mustn't run into each other
                            nearest = Mathf.Min(nearest, d);
                        }
                        if (treeSpots.Count > 0)
                            s += grove ? (nearest < 2.2f ? 1.5f : -0.5f) : Mathf.Min(nearest, 4.5f) * 0.7f;
                        return s;
                    }, out spot);
                }
                if (!ok) continue;
                Redwood(k, spot, h, i);
                treeSpots.Add(spot);
                obstacles.Add(new Obstacle { pos = spot, radius = crown * 0.8f });   // the lowest boughs are kid height
                k.snow?.AddDrift(spot, crown * 1.1f, k.Range(0.35f, 0.55f), 1f, 0f, i + 3);   // snow banked round the trunk
            }

            var shrubSpots = new List<Vector3>();
            var rockSpots = new List<Vector3>();
            float Spread(Vector3 p, List<Vector3> others, float cap)
            {
                float m = cap;
                foreach (var o in others) m = Mathf.Min(m, FrostUtil.FlatDistance(p, o));
                return m;
            }
            float NearTrees(Vector3 p)
            {
                float s = 0f;
                foreach (var t in treeSpots) { float d = FrostUtil.FlatDistance(p, t); if (d > 0.7f && d < 1.9f) s += 0.5f; }
                return s;
            }

            // ---- shrubs: by the walls, gathered round the trees
            for (int i = 0; i < shrubs; i++)
            {
                float size = k.Range(0.75f, 1.2f) * (big ? 1.15f : 1f);
                float r = 0.32f * size;
                if (!Place(r, r + 0.05f, p => WallScore(p, r + 0.12f) * 0.8f + Enclosure(p, 0.7f) + NearTrees(p) + Spread(p, shrubSpots, 2.5f) * 0.35f, out var spot)) continue;
                Shrub(k, spot, size, i);
                shrubSpots.Add(spot);
                placed[1]++;
                obstacles.Add(new Obstacle { pos = spot, radius = r });
                k.snow?.AddDrift(spot, r * 1.5f, k.Range(0.22f, 0.38f), 1f, 0f, i + 21);
            }

            // ---- rocks: anywhere out of the way, a few near the trees
            for (int i = 0; i < rocks; i++)
            {
                float size = k.Range(0.65f, 1.35f) * (big ? 1.2f : 1f);
                float r = 0.22f * size;
                if (!Place(r, r, p => WallScore(p, 0.6f) * 0.4f + NearTrees(p) * 0.6f + Spread(p, rockSpots, 2.5f) * 0.4f, out var spot)) continue;
                Rock(k, spot, size, i);
                rockSpots.Add(spot);
                placed[2]++;
                obstacles.Add(new Obstacle { pos = spot, radius = r });
                k.snow?.AddDrift(spot, r * 1.6f, k.Range(0.15f, 0.3f), 1f, 0f, i + 31);
            }

            // ---- fallen logs, along the walls
            for (int i = 0; i < logs; i++)
            {
                if (!Place(0.55f, 0.3f, p => WallScore(p, 0.45f) + NearTrees(p) * 0.5f, out var spot)) continue;
                room.ClosestOnEdge(new Vector2(spot.x, spot.z), out var inward);
                float yaw = Mathf.Atan2(inward.x, inward.y) * Mathf.Rad2Deg + k.Range(-15f, 15f);   // its length runs along the wall
                Log(k, spot, yaw, i);
                placed[3]++;
                obstacles.Add(new Obstacle { pos = spot, radius = 0.55f });
            }

            // ---- big drifts: heaped into the corners and against the walls, and out in the open in big rooms
            for (int i = 0; i < drifts && k.snow; i++)
            {
                float radius = k.Range(0.5f, 0.95f) * (big ? 1.25f : 1f);
                bool inOpen = big && k.rng.NextDouble() < 0.35;
                if (!Place(radius * 0.5f, 0.05f, p => inOpen ? 0f : WallScore(p, radius * 0.35f) + Enclosure(p, 1f) * 2f, out var spot)) continue;
                room.ClosestOnEdge(new Vector2(spot.x, spot.z), out var inward);
                float squash = k.Range(0.55f, 0.85f);
                float h01 = k.Range(0.55f, 1f);
                k.snow.AddDrift(spot, radius, h01, squash, Mathf.Atan2(inward.x, inward.y) * Mathf.Rad2Deg, i + 41);   // long side along the wall
                DriftCollider(k, spot, radius * (1f + squash) * 0.5f, h01);
                placed[4]++;
                obstacles.Add(new Obstacle { pos = spot, radius = radius * 0.5f });
            }

            MeshMerger.Merge(go.transform, null, 0.15f);
            Debug.Log($"[Frost] Scenery: {treeSpots.Count} redwoods, {placed[1]} shrubs, {placed[2]} rocks, {placed[3]} logs, {placed[4]} drifts for {area:0} m² of open floor");
            return go;
        }

        // ------------------------------------------------------------------ the pieces

        /// <summary>A coast redwood, stylised: a straight red-brown trunk with a flared foot, and a narrow cone of
        /// drooping tiers, each with a line of snow along its top edge, and a snowy tip.</summary>
        static void Redwood(Kit k, Vector3 spot, float H, int seed)
        {
            var basePos = new Vector3(spot.x, k.room.FloorY, spot.z);
            var frame = Quaternion.Euler(k.Range(-2f, 2f), k.Range(0f, 360f), k.Range(-2f, 2f));
            float r0 = Mathf.Clamp(0.052f * H, 0.075f, 0.17f);   // a sturdy column of a trunk
            k.Part("Trunk", ProcMesh.Cylinder(10, 0.32f), k.bark, basePos, frame, new Vector3(0f, H * 0.45f, 0f), new Vector3(r0 * 2f, H * 0.9f, r0 * 2f), Quaternion.identity);
            k.Part("Root Flare", ProcMesh.Cylinder(10, 0.45f), k.bark, basePos, frame, new Vector3(0f, 0.07f, 0f), new Vector3(r0 * 3.6f, 0.16f, r0 * 3.6f), Quaternion.identity);

            int n = H < 1.8f ? 6 : 8;
            // a redwood shows a good length of bare trunk below a narrow crown; the tip (leader) ends about at H
            float rb = 0.25f * H, y0 = (H < 1.8f ? 0.3f : 0.36f) * H, y1 = 0.78f * H;
            float gap = (y1 - y0) / (n - 1);
            float top = y1;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float ri = rb * (1f - 0.76f * t) * k.Range(0.9f, 1.08f);
                float hi = gap * 1.75f * (1f - 0.2f * t);
                float yi = y0 + t * (y1 - y0);
                top = yi + hi;
                var off = new Vector3(k.Range(-1f, 1f), 0f, k.Range(-1f, 1f)) * 0.02f * H;
                var tilt = Quaternion.Euler(k.Range(-5f, 5f), k.Range(0f, 360f), k.Range(-5f, 5f));
                k.Part("Tier", ProcMesh.Cylinder(11, 0.12f), i % 2 == 0 ? k.needles : k.needles2, basePos, frame, off + Vector3.up * (yi + hi * 0.5f), new Vector3(ri * 2f, hi, ri * 2f), tilt);
                // snow lying along the top of the bough, just in from its tips (where it shows between the tiers)
                float ring = 0.84f;
                float fy = (1f - ring) / 0.88f;   // height up the cone where its radius is ring·ri
                k.Part("Tier Snow", ProcMesh.Torus(0.5f, 0.1f, 16, 6), k.snowMat, basePos, frame,
                    off + Vector3.up * (yi + hi * fy + ri * 0.035f), new Vector3(ri * 2f * ring, ri * 2f * ring * 0.42f, ri * 2f * ring), tilt);
            }
            k.Part("Leader", ProcMesh.Cylinder(9, 0f), k.needles2, basePos, frame, new Vector3(0f, top + 0.05f * H, 0f), new Vector3(0.08f * H, 0.14f * H, 0.08f * H), Quaternion.identity);
            k.Part("Leader Snow", ProcMesh.Sphere(10, 6, 0.12f, seed % 5), k.snowMat, basePos, frame, new Vector3(0f, top + 0.015f * H, 0f), new Vector3(0.07f * H, 0.035f * H, 0.07f * H), Quaternion.identity);

            var holder = k.Holder("Redwood", basePos);
            var trunk = holder.AddComponent<CapsuleCollider>();
            trunk.radius = r0 * 1.2f; trunk.height = H; trunk.center = new Vector3(0f, H * 0.5f, 0f);
            var crown = holder.AddComponent<CapsuleCollider>();   // snowballs thud into the boughs
            crown.radius = rb * 0.72f; crown.height = Mathf.Max(crown.radius * 2f, H - y0); crown.center = new Vector3(0f, (y0 + H) * 0.5f, 0f);
            PhotoSubject.Tag(holder, SubjectKind.Tree, 0.85f).WithBounds(new Bounds(basePos + Vector3.up * H * 0.55f, new Vector3(rb * 2f, H, rb * 2f)));
        }

        /// <summary>A rounded evergreen bush, a few lumpy clumps each with a cap of snow.</summary>
        static void Shrub(Kit k, Vector3 spot, float size, int seed)
        {
            var basePos = new Vector3(spot.x, k.room.FloorY, spot.z);
            var frame = Quaternion.Euler(0f, k.Range(0f, 360f), 0f);
            int blobs = 4 + k.rng.Next(3);
            float topY = 0f;
            for (int i = 0; i < blobs; i++)
            {
                float a = i / (float)blobs * Mathf.PI * 2f + k.Range(-0.4f, 0.4f);
                float d = i == 0 ? 0f : k.Range(0.1f, 0.2f) * size;
                float r = (i == 0 ? 0.26f : k.Range(0.14f, 0.2f)) * size;
                var c = new Vector3(Mathf.Cos(a) * d, r * 0.62f + 0.02f, Mathf.Sin(a) * d);
                var rot = Quaternion.Euler(0f, k.Range(0f, 360f), 0f);
                k.Part("Shrub", ProcMesh.Sphere(10, 7, 0.2f, (seed + i) % 5), k.leaves, basePos, frame, c, new Vector3(r * 2f, r * 1.75f, r * 2f), rot);
                k.Part("Shrub Snow", ProcMesh.Sphere(10, 6, 0.22f, (seed + i + 2) % 5), k.snowMat, basePos, frame, c + Vector3.up * r * 0.62f, new Vector3(r * 1.55f, r * 0.62f, r * 1.55f), rot);
                topY = Mathf.Max(topY, c.y + r * 0.93f);
            }
            var holder = k.Holder("Shrub", basePos);
            var col = holder.AddComponent<SphereCollider>();
            col.radius = 0.3f * size; col.center = new Vector3(0f, 0.18f * size, 0f);
            PhotoSubject.Tag(holder, SubjectKind.Nature, 0.7f).WithBounds(new Bounds(basePos + Vector3.up * topY * 0.5f, new Vector3(0.7f * size, topY, 0.7f * size)));
        }

        /// <summary>A boulder half sunk in the snow with a snow cap.</summary>
        static void Rock(Kit k, Vector3 spot, float size, int seed)
        {
            var basePos = new Vector3(spot.x, k.room.FloorY, spot.z);
            var frame = Quaternion.Euler(k.Range(-6f, 6f), k.Range(0f, 360f), k.Range(-6f, 6f));
            var dims = new Vector3(0.42f, 0.27f, 0.34f) * size;
            k.Part("Rock", ProcMesh.Sphere(9, 6, 0.3f, seed % 5), k.stone, basePos, frame, new Vector3(0f, dims.y * 0.32f, 0f), dims, Quaternion.identity);
            k.Part("Rock Snow", ProcMesh.Sphere(10, 5, 0.2f, (seed + 1) % 5), k.snowMat, basePos, frame, new Vector3(0f, dims.y * 0.66f, 0f), new Vector3(dims.x * 0.78f, dims.y * 0.34f, dims.z * 0.78f), Quaternion.identity);
            var holder = k.Holder("Rock", basePos);
            var col = holder.AddComponent<SphereCollider>();
            col.radius = dims.x * 0.42f; col.center = new Vector3(0f, dims.y * 0.25f, 0f);
            PhotoSubject.Tag(holder, SubjectKind.Nature, 0.6f).WithBounds(new Bounds(basePos + Vector3.up * dims.y * 0.4f, new Vector3(dims.x, dims.y * 0.85f, dims.z)));
        }

        /// <summary>A fallen log with pale cut ends and a stripe of snow along its top.</summary>
        static void Log(Kit k, Vector3 spot, float yaw, int seed)
        {
            float len = k.Range(0.8f, 1.2f), r = k.Range(0.12f, 0.16f);   // a chunky round of trunk, not a stick
            var basePos = new Vector3(spot.x, k.room.FloorY, spot.z);
            var frame = Quaternion.Euler(0f, yaw, k.Range(-3f, 3f));
            var along = Quaternion.Euler(0f, 0f, 90f);   // the cylinder's axis laid along x
            float axisY = r * 0.85f;
            k.Part("Log", ProcMesh.Cylinder(10), k.logBark, basePos, frame, new Vector3(0f, axisY, 0f), new Vector3(r * 2f, len, r * 2f), along);
            k.Part("Log End", ProcMesh.Cylinder(10), k.wood, basePos, frame, new Vector3(len * 0.5f + 0.004f, axisY, 0f), new Vector3(r * 1.8f, 0.01f, r * 1.8f), along, false);
            k.Part("Log End", ProcMesh.Cylinder(10), k.wood, basePos, frame, new Vector3(-len * 0.5f - 0.004f, axisY, 0f), new Vector3(r * 1.8f, 0.01f, r * 1.8f), along, false);
            k.Part("Log Snow", ProcMesh.Sphere(12, 6, 0.14f, seed % 5), k.snowMat, basePos, frame, new Vector3(0f, axisY + r * 0.77f, 0f), new Vector3(len * 0.94f, r * 0.55f, r * 1.5f), Quaternion.identity);
            var holder = k.Holder("Log", basePos);
            holder.transform.rotation = frame;
            var col = holder.AddComponent<CapsuleCollider>();
            col.direction = 0; col.radius = r; col.height = len + r; col.center = new Vector3(0f, axisY, 0f);
            PhotoSubject.Tag(holder, SubjectKind.Nature, 0.6f).WithBounds(new Bounds(basePos + Vector3.up * r, new Vector3(len, r * 2f, len)));
        }

        /// <summary>A collider matching a drift's dome (a big sphere through its rim and its top), so snowballs and
        /// dropped photos land on it rather than sinking through.</summary>
        static void DriftCollider(Kit k, Vector3 spot, float radius, float h01)
        {
            if (!k.snow) return;
            float hh = h01 * k.snow.bigDriftHeight * 0.85f;
            if (hh < 0.06f) return;
            float re = radius * 0.7f;
            float rs = (re * re + hh * hh) / (2f * hh);
            var go = k.Holder("Drift", new Vector3(spot.x, k.snow.SurfaceHeight + hh - rs, spot.z));
            go.layer = Layers.Snow;
            go.AddComponent<SphereCollider>().radius = rs;
        }

        static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            var p2 = new Vector2(p.x, p.z); var a2 = new Vector2(a.x, a.z); var b2 = new Vector2(b.x, b.z);
            var ab = b2 - a2;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p2 - a2, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p2, a2 + ab * t);
        }
    }
}
