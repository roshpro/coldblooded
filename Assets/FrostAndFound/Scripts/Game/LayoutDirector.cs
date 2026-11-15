using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Lays the crime scene out in whatever room you're in (doc §7: scale the layout to the detected bounds):
    /// Frosty at the centre in his chalk outline, the kids idling around the room's edges clear of furniture,
    /// Tony spawned farthest from you, Lucy close by (she's your guide), Dexter at the end of the cocoa drip trail.
    /// </summary>
    public static class LayoutDirector
    {
        public class Result
        {
            public Frosty Frosty;
            public readonly Dictionary<CastId, Kid> Kids = new Dictionary<CastId, Kid>();
        }

        public static Result Build(RoomInfo room, PlayerRig player)
        {
            var tune = FrostTuning.I;
            var res = new Result();
            var feet = player.FeetPosition;
            var fwd = player.FlatForward;
            var rng = new System.Random(Random.Range(1, int.MaxValue));   // a different arrangement every game
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // ---------------- Frosty: near the centre of the floor (somewhere a little different each time), nudged
            // off the player and off furniture
            var center = room.Center;
            float wander = Mathf.Min(0.5f, Mathf.Min(room.Width, room.Depth) * 0.12f);
            float wa = R(0f, Mathf.PI * 2f), wr = wander * Mathf.Sqrt(R(0f, 1f));
            center += new Vector3(Mathf.Cos(wa), 0f, Mathf.Sin(wa)) * wr;
            float dPlayer = FrostUtil.FlatDistance(center, feet);
            if (dPlayer < 1.3f) center += fwd * (1.3f - dPlayer);
            center = room.ClampInside(center, 0.8f);
            float fr = Frosty.FootprintRadius * 0.7f;
            if (!room.IsFree(center, fr, 0.7f))
            {
                for (int i = 0; i < 40; i++)
                {
                    var c = room.RandomFreePoint(fr, 0.7f, rng, 1);
                    if (room.IsFree(c, fr, 0.7f) && FrostUtil.FlatDistance(c, feet) > 1.1f) { center = c; break; }
                }
            }
            center.y = room.FloorY;
            var frostyFacing = FrostUtil.FlatLookRotation(center, feet) * Quaternion.Euler(0f, R(-20f, 20f), 0f);
            res.Frosty = Frosty.Create(center, frostyFacing);
            PhotoSubject.Tag(res.Frosty.gameObject, SubjectKind.Frosty, 1.1f);   // so a photo of him gets a reaction

            // ---------------- things left in the snow: Tony's sled planted upright somewhere (not obviously his) and a
            // sand bucket with a little spade, each by a wall where the drifts are, clear of Frosty, you and each other
            var rngP = rng;
            Vector3 PropSpot(float minFrom, params Vector3[] avoid)
            {
                Vector3 best = default; float bestScore = float.MinValue;
                for (int i = 0; i < 80; i++)
                {
                    var p = room.RandomFreePoint(0.25f, 0.35f, rngP, 20);
                    float edge = room.EdgeDistance(new Vector2(p.x, p.z));
                    if (edge > 0.95f || FrostUtil.FlatDistance(p, center) < Frosty.FootprintRadius + 0.45f || FrostUtil.FlatDistance(p, feet) < 1.2f) continue;
                    bool near = false;
                    foreach (var a in avoid) if (FrostUtil.FlatDistance(p, a) < minFrom) near = true;
                    if (near) continue;
                    float score = -Mathf.Abs(edge - 0.5f) + (float)rngP.NextDouble() * 1.2f;
                    foreach (var a in avoid) score += Mathf.Min(FrostUtil.FlatDistance(p, a), 3f) * 0.6f;   // well apart, for variety
                    if (score > bestScore) { bestScore = score; best = p; }
                }
                return bestScore > float.MinValue ? best : room.ClampInside(center - fwd * 1.5f, 0.4f);
            }
            var sledSpot = PropSpot(0f);
            var toysSpot = PropSpot(1.4f, sledSpot);
            sledSpot.y = toysSpot.y = room.FloorY;
            var furniture = room.IsBlocked;
            room.IsBlocked = (p, r) => (furniture != null && furniture(p, r))
                || FrostUtil.FlatDistance(p, sledSpot) < 0.3f + r || FrostUtil.FlatDistance(p, toysSpot) < 0.25f + r;
            SnowToys.Create(toysSpot, Quaternion.Euler(0f, R(-70f, 70f), 0f) * (center - toysSpot).Flat());

            // ---------------- kid spots: every free bit of floor is a candidate (not just a ring by the walls), so
            // even in a small room the five of them spread out instead of bunching in whatever corner is left
            var slots = new List<Vector3>();
            var b = room.Bounds;
            for (float x = b.xMin + 0.2f; x < b.xMax - 0.2f; x += 0.15f)
                for (float z = b.yMin + 0.2f; z < b.yMax - 0.2f; z += 0.15f)
                {
                    var p = new Vector3(x, room.FloorY, z);
                    if (!room.IsFree(p, 0.26f, 0.3f)) continue;
                    if (FrostUtil.FlatDistance(p, center) < Frosty.FootprintRadius + 0.25f) continue;   // clear of Frosty
                    if (FrostUtil.FlatDistance(p, feet) < 0.85f) continue;
                    slots.Add(p);
                }
            // tiny or cluttered rooms: fall back to a circle around Frosty
            if (slots.Count < 5)
            {
                float r = Mathf.Clamp(Mathf.Min(room.Width, room.Depth) * 0.38f, 0.9f, 2.2f);
                for (int i = 0; i < 16; i++)
                {
                    float a = i / 16f * Mathf.PI * 2f;
                    var p = room.ClampInside(center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r, 0.35f);
                    if (FrostUtil.FlatDistance(p, feet) > 0.7f) slots.Add(p);
                }
            }
            var placed = new Dictionary<CastId, Vector3>();
            float MinToPlaced(Vector3 s)
            {
                float m = 10f;
                foreach (var kv in placed) m = Mathf.Min(m, FrostUtil.FlatDistance(kv.Value, s));
                return m;
            }
            // each kid takes the best spot by its story role, but always as far from the others as the room allows
            Vector3 Pick(System.Func<Vector3, float> score)
            {
                foreach (float spacing in new[] { 1.5f, 1.2f, tune.minKidSpacing, 0.6f, 0f })
                {
                    Vector3 best = default; float bestScore = float.MinValue; bool any = false;
                    foreach (var s in slots)
                    {
                        float gap = MinToPlaced(s);
                        if (gap < spacing) continue;
                        float sc = score(s) + Mathf.Min(gap, 2.6f) * 2f + R(0f, 1.6f);   // spreading out matters most; a dash of chance
                        if (sc > bestScore) { bestScore = sc; best = s; any = true; }
                    }
                    if (any) return best;
                }
                return center + fwd;
            }
            // Tony spawns farthest from you
            placed[CastId.Tony] = Pick(s => FrostUtil.FlatDistance(s, feet) - (FrostUtil.FlatDistance(s, sledSpot) < 1.6f ? 5f : 0f));   // not next to his own sled
            // Lucy close, a little to the side (she's your guide and opens the scene)
            placed[CastId.Lucy] = Pick(s =>
            {
                float d = FrostUtil.FlatDistance(s, feet);
                var dir = (s - feet).Flat().normalized;
                float side = 1f - Mathf.Abs(Vector3.Dot(dir, fwd) - 0.55f);
                return -Mathf.Abs(d - 1.4f) * 1.2f + side;
            });
            // Dexter: visible from where you stand, in front of Frosty's stain, so the drip trail reads
            placed[CastId.Dexter] = Pick(s =>
            {
                var dir = (s - center).Flat().normalized;
                float inFront = Vector3.Dot(dir, frostyFacing * Vector3.forward);
                return inFront * 0.8f - Mathf.Abs(FrostUtil.FlatDistance(s, center) - 1.3f);
            });
            placed[CastId.Sherman] = Pick(s => Mathf.Min(FrostUtil.FlatDistance(s, feet), 2.5f) * 0.3f);
            placed[CastId.Serena] = Pick(s => Mathf.Min(FrostUtil.FlatDistance(s, feet), 2.5f) * 0.3f);

            // ---------------- spawn
            foreach (var kv in placed)
            {
                var pos = kv.Value; pos.y = room.FloorY;
                // face the room (between Frosty and you)
                var look = Vector3.Lerp(center, feet, 0.4f);
                var rot = FrostUtil.FlatLookRotation(pos, look) * Quaternion.Euler(0f, R(-25f, 25f), 0f);
                Kid kid = kv.Key switch
                {
                    CastId.Dexter => Kid.Spawn<DexterKid>(kv.Key, pos, rot),
                    CastId.Sherman => Kid.Spawn<ShermanKid>(kv.Key, pos, rot),
                    CastId.Serena => Kid.Spawn<SerenaKid>(kv.Key, pos, rot),
                    CastId.Lucy => Kid.Spawn<LucyKid>(kv.Key, pos, rot),
                    _ => Kid.Spawn<TonyKid>(kv.Key, pos, rot),
                };
                res.Kids[kv.Key] = kid;
            }

            if (res.Kids.TryGetValue(CastId.Tony, out var tonyKid) && tonyKid is TonyKid tk && tk.Sled)
            {
                tk.Sled.Plant(sledSpot, Quaternion.Euler(0f, R(-35f, 35f), 0f) * (center - sledSpot).Flat());
                PhotoSubject.Tag(tk.Sled.gameObject, SubjectKind.Sled);
            }

            // ---------------- clue 2 lives on Sherman: bits on the carrot (+ orange stains glint with it)
            if (res.Kids.TryGetValue(CastId.Sherman, out var sherman))
            {
                var carrot = FindDeep(sherman.transform, "Carrot");
                if (carrot)
                {
                    var clue = carrot.gameObject.AddComponent<Clue>();
                    clue.id = ClueId.CarrotBits;
                    clue.radius = 0.08f;
                    clue.owner = sherman.transform;
                    clue.glintTint = Palette.Glint;
                    foreach (var t in sherman.GetComponentsInChildren<Transform>()) if (t.name == "Stain") clue.extraGlints.Add(t);
                }
            }

            return res;
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
    }

    /// <summary>A kid's sand bucket and spade left out in the snow: a bit of life in the scene (set dressing only).</summary>
    public static class SnowToys
    {
        public static GameObject Create(Vector3 pos, Vector3 face)
        {
            var root = new GameObject("Sand Bucket and Spade");
            float snowY = SnowSurface.Instance ? SnowSurface.Instance.SurfaceHeightAt(pos) : pos.y;
            var facing = face.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(face.normalized) : Quaternion.identity;
            root.transform.SetPositionAndRotation(new Vector3(pos.x, snowY - 0.025f, pos.z), facing * Quaternion.Euler(0f, 25f, 0f));
            var t = root.transform;
            var pail = Palette.Toon(new Color(0.98f, 0.74f, 0.2f), false, 0.3f);
            var rim = Palette.Toon(new Color(0.93f, 0.4f, 0.22f), false, 0.3f);
            var blue = Palette.Toon(new Color(0.22f, 0.56f, 0.92f), false, 0.3f);
            // the bucket, set down a little crooked and half full of snow
            var bucket = ProcMesh.Pivot("Bucket", t, Vector3.zero, new Vector3(5f, 0f, -4f));
            ProcMesh.Part("Pail", bucket, ProcMesh.Cylinder(18, 0.8f), pail, new Vector3(0f, 0.07f, 0f), new Vector3(0.17f, 0.14f, 0.17f), new Vector3(180f, 0f, 0f));
            ProcMesh.Part("Rim", bucket, ProcMesh.Torus(0.47f, 0.06f, 20, 6), rim, new Vector3(0f, 0.14f, 0f), new Vector3(0.18f, 0.18f, 0.18f));
            ProcMesh.Part("Snow Inside", bucket, ProcMesh.Disc(16), Palette.Toon(Palette.Snow, false, 0.3f), new Vector3(0f, 0.12f, 0f), new Vector3(0.155f, 0.002f, 0.155f));
            ProcMesh.Part("Handle", bucket, ProcMesh.Torus(0.45f, 0.04f, 20, 5), rim, new Vector3(0f, 0.14f, 0f), new Vector3(0.18f, 0.18f, 0.18f), new Vector3(90f, 0f, 0f));
            // the spade stuck in the snow beside it
            var spade = ProcMesh.Pivot("Spade", t, new Vector3(0.2f, 0f, 0.05f), new Vector3(-22f, 0f, 12f));
            ProcMesh.Part("Shaft", spade, ProcMesh.Cylinder(8), blue, new Vector3(0f, 0.16f, 0f), new Vector3(0.022f, 0.24f, 0.022f));
            ProcMesh.Part("Grip", spade, ProcMesh.RoundedBox(0.4f, 2), blue, new Vector3(0f, 0.29f, 0f), new Vector3(0.07f, 0.025f, 0.025f));
            ProcMesh.Part("Blade", spade, ProcMesh.RoundedBox(0.3f, 2), blue, new Vector3(0f, 0.02f, 0f), new Vector3(0.08f, 0.1f, 0.012f));
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var col = root.AddComponent<BoxCollider>();   // snowballs splat on it
            col.center = new Vector3(0.08f, 0.1f, 0.02f);
            col.size = new Vector3(0.36f, 0.22f, 0.22f);
            MeshMerger.Merge(t);
            PhotoSubject.Tag(root, SubjectKind.Toys);
            return root;
        }
    }
}
