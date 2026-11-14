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

            // ---------------- Frosty: centre of the floor, nudged off the player and off furniture
            var center = room.Center;
            float dPlayer = FrostUtil.FlatDistance(center, feet);
            if (dPlayer < 1.3f) center += fwd * (1.3f - dPlayer);
            center = room.ClampInside(center, 0.8f);
            if (!room.IsFree(center, 0.55f, 0.7f))
            {
                var rng = new System.Random(3);
                for (int i = 0; i < 40; i++)
                {
                    var c = room.RandomFreePoint(0.55f, 0.7f, rng, 1);
                    if (room.IsFree(c, 0.55f, 0.7f) && FrostUtil.FlatDistance(c, feet) > 1.1f) { center = c; break; }
                }
            }
            center.y = room.FloorY;
            var frostyFacing = FrostUtil.FlatLookRotation(center, feet);
            res.Frosty = Frosty.Create(center, frostyFacing);

            // ---------------- kid slots around the edges
            var ring = room.PerimeterRing(tune.kidWallInset, 48);
            var slots = new List<Vector3>();
            foreach (var p in ring)
            {
                if (!room.IsFree(p, 0.28f, tune.kidWallInset * 0.7f)) continue;
                if (FrostUtil.FlatDistance(p, center) < 1.0f) continue;
                if (FrostUtil.FlatDistance(p, feet) < 0.9f) continue;
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
            float minSpacing = tune.minKidSpacing;

            Vector3 Pick(System.Func<Vector3, float> score)
            {
                Vector3 best = slots.Count > 0 ? slots[0] : center + fwd;
                float bestScore = float.MinValue;
                foreach (var s in slots)
                {
                    bool tooClose = false;
                    foreach (var kv in placed) if (FrostUtil.FlatDistance(kv.Value, s) < minSpacing) tooClose = true;
                    float sc = score(s) - (tooClose ? 100f : 0f);
                    if (sc > bestScore) { bestScore = sc; best = s; }
                }
                return best;
            }

            float MinToPlaced(Vector3 s)
            {
                float m = 10f;
                foreach (var kv in placed) m = Mathf.Min(m, FrostUtil.FlatDistance(kv.Value, s));
                return m;
            }

            // Tony spawns farthest from you
            placed[CastId.Tony] = Pick(s => FrostUtil.FlatDistance(s, feet));
            // Lucy close, a little to the side (she's your guide and opens the scene)
            placed[CastId.Lucy] = Pick(s =>
            {
                float d = FrostUtil.FlatDistance(s, feet);
                var dir = (s - feet).Flat().normalized;
                float side = 1f - Mathf.Abs(Vector3.Dot(dir, fwd) - 0.55f);
                return -Mathf.Abs(d - 1.5f) * 2f + side;
            });
            // Dexter: visible from where you stand, in front of Frosty's stain, so the drip trail reads
            placed[CastId.Dexter] = Pick(s =>
            {
                var dir = (s - center).Flat().normalized;
                float inFront = Vector3.Dot(dir, frostyFacing * Vector3.forward);
                return inFront * 0.8f + MinToPlaced(s) * 0.6f - Mathf.Abs(FrostUtil.FlatDistance(s, center) - 1.5f);
            });
            placed[CastId.Sherman] = Pick(s => MinToPlaced(s));
            placed[CastId.Serena] = Pick(s => MinToPlaced(s));

            // ---------------- spawn
            foreach (var kv in placed)
            {
                var pos = kv.Value; pos.y = room.FloorY;
                // face the room (between Frosty and you)
                var look = Vector3.Lerp(center, feet, 0.4f);
                var rot = FrostUtil.FlatLookRotation(pos, look);
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

            // ---------------- the cocoa drip trail from the stain to Dexter's feet
            if (res.Kids.TryGetValue(CastId.Dexter, out var dexter))
            {
                var from = center + frostyFacing * Vector3.forward * 0.34f;
                var to = dexter.transform.position + (center - dexter.transform.position).Flat().normalized * 0.22f;
                var drips = DripTrail.Create(from, to, room.FloorY);
                res.Frosty.AttachDrips(drips);
            }
            return res;
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
    }
}
