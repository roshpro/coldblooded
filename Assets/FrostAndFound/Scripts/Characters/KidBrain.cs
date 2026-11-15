using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// How each kid likes to spend the time while you investigate (0..1 each): how restless they are, how much they
    /// seek out the others, Frosty, you, and how far and fast they roam.
    /// </summary>
    public struct KidPersona
    {
        public float energy, social, frosty, player, explore, roam, pace, dash;
        /// <summary>Stay within this distance of you (0 = free to go anywhere).</summary>
        public float leash;

        public static KidPersona For(CastId id) => id switch
        {
            // whistles, strolls slowly, keeps drifting back to look at Frosty
            CastId.Dexter => new KidPersona { energy = 0.5f, social = 0.3f, frosty = 0.75f, player = 0.25f, explore = 0.45f, roam = 0.55f, pace = 0.35f, dash = 0.05f },
            // can't keep still; loves the camera; the most likely to break into a run
            CastId.Sherman => new KidPersona { energy = 0.95f, social = 0.6f, frosty = 0.35f, player = 0.7f, explore = 0.6f, roam = 0.9f, pace = 0.85f, dash = 0.4f },
            // goes from friend to friend
            CastId.Serena => new KidPersona { energy = 0.65f, social = 0.95f, frosty = 0.3f, player = 0.35f, explore = 0.35f, roam = 0.7f, pace = 0.6f, dash = 0.1f },
            // your guide: never far from you
            CastId.Lucy => new KidPersona { energy = 0.6f, social = 0.35f, frosty = 0.45f, player = 0.8f, explore = 0.4f, roam = 0.5f, pace = 0.6f, dash = 0.05f, leash = 2.8f },
            // restless and shifty: keeps moving, steers clear of Frosty (and his sled, see TonyKid)
            _ => new KidPersona { energy = 0.85f, social = 0.4f, frosty = 0.08f, player = 0.2f, explore = 0.7f, roam = 0.85f, pace = 0.7f, dash = 0.3f },
        };
    }

    /// <summary>
    /// The kids' idle-time AI. A small utility brain picks what to do next (wander somewhere new, go and stand with a
    /// friend, go and look at Frosty, come and see what you're up to, poke about by the trees, run, or just stand about),
    /// weighted by personality and what's around, never the same thing twice in a row if it can help it. Walks are
    /// planned on <see cref="KidNav"/>'s grid round the furniture, trees and props, and steered round each other and you
    /// on the way (<see cref="Kid.WalkTo"/>).
    /// </summary>
    public class KidBrain
    {
        enum Act { None, Idle, Roam, Chat, Frosty, Player, Explore, Dash, Retreat, Furniture }
        enum Phase { Deciding, Walking, Lingering }

        readonly Kid m_Kid;
        readonly KidPersona m_P;
        Act m_Act, m_LastAct;
        Phase m_Phase;
        float m_Until, m_NextPlayerVisit, m_NextChat;
        bool m_Arrived;
        Kid m_Friend;
        Vector3 m_LookAt;
        readonly Queue<Vector3> m_Visited = new Queue<Vector3>();

        public KidBrain(Kid kid)
        {
            m_Kid = kid;
            m_P = KidPersona.For(kid.id);
            m_Until = Time.time + Random.Range(1f, 4f) * (1.4f - m_P.energy);   // don't all set off at once
            m_NextPlayerVisit = Time.time + Random.Range(8f, 20f);
        }

        public string Doing => m_Act + " / " + m_Phase;

        float Speed(Act a) => a == Act.Dash ? Random.Range(1.3f, 1.7f) : Mathf.Lerp(0.5f, 0.85f, m_P.pace) * (a == Act.Player ? 1.1f : 1f) * Random.Range(0.92f, 1.08f);

        /// <summary>Called every frame while the kid is free to do as it likes.</summary>
        public void Tick(PlayerRig player)
        {
            var me = m_Kid.transform.position;
            var head = player.Head.position;

            // you've walked right up to me: take a step back (unless we're the one who came over)
            if (m_Phase != Phase.Walking && m_Act != Act.Retreat && FrostUtil.FlatDistance(me, head) < 0.55f)
            {
                var away = (me - head).Flat();
                away = away.sqrMagnitude > 1e-4f ? away.normalized : -m_Kid.transform.forward;
                for (int i = 0; i < 5; i++)
                {
                    var p = me + Quaternion.Euler(0f, (i - 2) * 30f, 0f) * away * 0.6f;
                    if (KidNav.Walkable(p) && Go(Act.Retreat, p, 0.75f)) return;
                }
            }

            switch (m_Phase)
            {
                case Phase.Walking:
                    if (m_Kid.IsMoving) return;
                    if (m_Arrived) Linger(player);
                    else { m_Phase = Phase.Deciding; m_Until = Time.time + Random.Range(0.4f, 1.2f); }   // interrupted or stuck
                    return;
                case Phase.Lingering:
                    // a friend we're chatting with wandered off: no point standing there
                    if (m_Act == Act.Chat && (!m_Friend || FrostUtil.FlatDistance(m_Friend.transform.position, me) > 1.8f)) m_Until = Mathf.Min(m_Until, Time.time + 0.5f);
                    if (Time.time < m_Until) return;
                    m_Phase = Phase.Deciding;
                    // energetic kids often go straight on to the next thing
                    m_Until = Time.time + (Random.value < m_P.energy * 0.5f ? 0f : Random.Range(0.6f, 3.5f) * (1.4f - m_P.energy));
                    return;
                default:
                    if (Time.time < m_Until) return;
                    Decide(player);
                    return;
            }
        }

        /// <summary>The kid was taken over (dialogue, a snowball fight, being questioned): start afresh afterwards.</summary>
        public void Reset()
        {
            if (m_Phase == Phase.Walking && !m_Kid.IsMoving) m_Phase = Phase.Deciding;
        }

        void Decide(PlayerRig player)
        {
            var w = new Dictionary<Act, float>
            {
                [Act.Idle] = 0.55f * (1.3f - m_P.energy),
                [Act.Roam] = 1.1f * m_P.energy,
                [Act.Dash] = m_P.dash * m_P.energy,
                [Act.Chat] = Time.time > m_NextChat ? 1.1f * m_P.social : 0f,
                [Act.Frosty] = Frosty.Instance ? 0.8f * m_P.frosty : 0f,
                [Act.Player] = Time.time > m_NextPlayerVisit ? 0.9f * m_P.player : 0f,
                [Act.Explore] = 0.6f * m_P.explore,
                // your real sofa, table and cabinets (only when Space Setup marked some)
                [Act.Furniture] = RoomService.Current != null && RoomService.Current.Furniture.Count > 0 ? 0.55f * m_P.explore + 0.15f : 0f,
            };
            // Lucy: drifting too far from you pulls her back
            if (m_P.leash > 0f && FrostUtil.FlatDistance(m_Kid.transform.position, player.Head.position) > m_P.leash) w[Act.Player] += 3f;
            if (w.ContainsKey(m_LastAct)) w[m_LastAct] *= 0.35f;   // variety

            for (int attempt = 0; attempt < 4; attempt++)
            {
                float total = 0f;
                foreach (var kv in w) total += kv.Value;
                if (total <= 0f) break;
                float r = Random.value * total;
                Act pick = Act.Idle;
                foreach (var kv in w) { r -= kv.Value; if (r <= 0f) { pick = kv.Key; break; } }
                if (Try(pick, player)) return;
                w[pick] = 0f;   // nowhere to do that: pick something else
            }
            // stand about for a bit
            Begin(Act.Idle);
            m_Phase = Phase.Lingering;
            m_Until = Time.time + Random.Range(1.5f, 4f);
            m_LookAt = Vector3.zero;
        }

        bool Try(Act a, PlayerRig player)
        {
            var room = RoomService.Current;
            if (room == null) return false;
            var me = m_Kid.transform.position;
            var head = player.Head.position;
            switch (a)
            {
                case Act.Idle:
                    Begin(Act.Idle);
                    m_Phase = Phase.Lingering;
                    m_Until = Time.time + Random.Range(2f, 6f) * (1.5f - m_P.energy);
                    m_LookAt = Vector3.zero;
                    if (m_Kid.Anim && m_Kid.Anim.IsHumanoid && Random.value < 0.6f) m_Kid.Anim.Fidget(head);
                    return true;

                case Act.Roam:
                case Act.Dash:
                {
                    // somewhere new: away from where we've just been and from the crowd, within our range
                    var b = room.Bounds;
                    float range = Mathf.Lerp(1.8f, Mathf.Max(b.width, b.height), m_P.roam);
                    float minWalk = a == Act.Dash ? 2.2f : 1.0f;
                    Vector3 best = default; float bestScore = float.MinValue;
                    for (int i = 0; i < 24; i++)
                    {
                        var p = new Vector3(Random.Range(b.xMin, b.xMax), room.FloorY, Random.Range(b.yMin, b.yMax));
                        float d = FrostUtil.FlatDistance(p, me);
                        if (d < minWalk || d > range || !SpotOk(p, player, 0.95f)) continue;
                        float s = Mathf.Min(Crowd(p), 2.5f) + Novelty(p) * 0.8f + Random.Range(0f, 1.2f);
                        if (s > bestScore) { bestScore = s; best = p; }
                    }
                    if (bestScore == float.MinValue) return false;
                    m_LookAt = Vector3.zero;
                    return Go(a, best, Speed(a));
                }

                case Act.Chat:
                {
                    // over to someone who's standing about, and stand facing them
                    var friends = new List<Kid>();
                    foreach (var k in Kid.All)
                        if (k && k != m_Kid && !k.IsMoving && !k.Busy && !k.IsStunned && FrostUtil.FlatDistance(k.transform.position, head) > 0.9f) friends.Add(k);
                    if (friends.Count == 0) return false;
                    var f = friends[Random.Range(0, friends.Count)];
                    var fp = f.transform.position;
                    var from = (me - fp).Flat();
                    if (from.sqrMagnitude < 1e-4f) from = Vector3.forward;
                    for (int i = 0; i < 10; i++)
                    {
                        var p = fp + Quaternion.Euler(0f, Random.Range(-80f, 80f), 0f) * from.normalized * Random.Range(0.85f, 1.05f);
                        if (!SpotOk(p, player, 0.8f, f)) continue;
                        m_Friend = f;
                        m_LookAt = f.HeadPoint;
                        m_NextChat = Time.time + Random.Range(6f, 14f);
                        return Go(a, p, Speed(a));
                    }
                    return false;
                }

                case Act.Frosty:
                {
                    var fr = Frosty.Instance;
                    if (!fr) return false;
                    var c = fr.transform.position;
                    for (int i = 0; i < 12; i++)
                    {
                        float ang = Random.Range(0f, Mathf.PI * 2f);
                        var p = c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (Frosty.FootprintRadius + Random.Range(0.3f, 0.6f));
                        if (!SpotOk(p, player, 0.9f)) continue;
                        m_LookAt = c + Vector3.up * 0.15f;
                        return Go(a, p, Speed(a));
                    }
                    return false;
                }

                case Act.Player:
                {
                    // come and see what you're doing: stop a little way off, where you can see us
                    var fwd = player.FlatForward;
                    for (int i = 0; i < 12; i++)
                    {
                        var p = head.Flat() + Quaternion.Euler(0f, Random.Range(-60f, 60f), 0f) * fwd * Random.Range(1.2f, 1.7f);
                        p.y = room.FloorY;
                        if (!SpotOk(p, player, 0.9f)) continue;
                        m_LookAt = Vector3.zero;   // (looking at you is the default when you're near)
                        m_NextPlayerVisit = Time.time + Random.Range(20f, 40f);
                        return Go(a, p, Speed(a));
                    }
                    return false;
                }

                case Act.Furniture:
                {
                    // go and stand by a piece of your real furniture and have a look at it (Dexter puts his cocoa down on a table)
                    var list = room.Furniture;
                    if (list.Count == 0) return false;
                    for (int i = 0; i < 10; i++)
                    {
                        var f = list[Random.Range(0, list.Count)];
                        if (f.Kind == FurnitureKind.Lamp || f.Kind == FurnitureKind.Plant) continue;
                        var toward = (me - f.Center).Flat();
                        if (toward.sqrMagnitude < 1e-3f) toward = f.Forward;
                        var near = f.Center + Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * toward.normalized * 3f;
                        var p = f.EdgePoint(near, 0.32f, room.FloorY);
                        if (FrostUtil.FlatDistance(p, me) < 0.6f || !SpotOk(p, player, 0.9f)) continue;
                        m_LookAt = new Vector3(f.Center.x, Mathf.Min(f.TopY, room.FloorY + 1.1f), f.Center.z);
                        return Go(a, p, Speed(a));
                    }
                    return false;
                }

                case Act.Explore:
                {
                    // poke about by the walls, where the trees and drifts are, and have a look
                    for (int i = 0; i < 16; i++)
                    {
                        var p = room.RandomFreePoint(0.22f, 0.35f, null, 4);
                        float e = room.EdgeDistance(p.XZ());
                        if (e > 1.1f || FrostUtil.FlatDistance(p, me) < 0.8f || !SpotOk(p, player, 0.95f)) continue;
                        var edge = room.ClosestOnEdge(p.XZ(), out _);
                        var outward = (new Vector3(edge.x, room.FloorY, edge.y) - p).Flat();
                        var side = Quaternion.Euler(0f, Random.Range(-50f, 50f), 0f) * (outward.sqrMagnitude > 1e-4f ? outward.normalized : Vector3.forward);
                        m_LookAt = p + side * 1.5f + Vector3.up * Random.Range(0.2f, 1.4f);
                        return Go(a, p, Speed(a));
                    }
                    return false;
                }
            }
            return false;
        }

        void Begin(Act a)
        {
            m_LastAct = m_Act = a;
            if (a != Act.Chat) m_Friend = null;
        }

        bool Go(Act a, Vector3 p, float speed)
        {
            m_Arrived = false;
            if (!m_Kid.WalkTo(p, speed, () => m_Arrived = true)) return false;
            Begin(a);
            m_Phase = Phase.Walking;
            Remember(p);
            return true;
        }

        void Linger(PlayerRig player)
        {
            m_Phase = Phase.Lingering;
            float t = m_Act switch
            {
                Act.Chat => Random.Range(4f, 8f),
                Act.Frosty => Random.Range(2.5f, 5.5f),
                Act.Player => Random.Range(2.5f, 4.5f),
                Act.Explore => Random.Range(2f, 4f),
                Act.Furniture => Random.Range(4f, 8f),
                Act.Retreat => Random.Range(0.8f, 1.6f),
                Act.Dash => Random.Range(0.6f, 1.5f),
                _ => Random.Range(0.4f, 2.2f) * (1.4f - m_P.energy),
            };
            m_Until = Time.time + t;
            if (m_Act == Act.Chat && m_Friend)
            {
                m_LookAt = m_Friend.HeadPoint;
                // they turn to us too, if they're just standing about
                if (!m_Friend.IsMoving && !m_Friend.Busy) m_Friend.Attend(m_Kid.HeadPoint, t);
            }
            if (m_LookAt != Vector3.zero) m_Kid.Attend(m_LookAt, t);
            m_Kid.OnLinger(t);
            var anim = m_Kid.Anim;
            if (anim && anim.IsHumanoid)
            {
                if (m_Act == Act.Player && Random.value < 0.45f) anim.Fidget(player.Head.position);
                else if (m_Act != Act.Retreat && Random.value < 0.35f) anim.Fidget(m_LookAt != Vector3.zero ? m_LookAt : player.Head.position);
            }
        }

        /// <summary>A place we could stand: walkable, clear of you and of where the others are and are heading.</summary>
        bool SpotOk(Vector3 p, PlayerRig player, float gap, Kid except = null)
        {
            if (!KidNav.Walkable(p) || !m_Kid.SpotOk(p)) return false;
            var head = player.Head.position;
            if (FrostUtil.FlatDistance(p, head) < 0.9f) return false;
            if (m_P.leash > 0f && FrostUtil.FlatDistance(p, head) > m_P.leash) return false;
            foreach (var k in Kid.All)
            {
                if (!k || k == m_Kid) continue;
                float need = k == except ? 0.7f : gap;
                if (FrostUtil.FlatDistance(p, k.transform.position) < need || FrostUtil.FlatDistance(p, k.Destination) < need) return false;
            }
            return true;
        }

        static float Crowd(Vector3 p)
        {
            float m = 10f;
            foreach (var k in Kid.All) if (k) m = Mathf.Min(m, Mathf.Min(FrostUtil.FlatDistance(p, k.transform.position), FrostUtil.FlatDistance(p, k.Destination)));
            return m;
        }

        float Novelty(Vector3 p)
        {
            float m = 3f;
            foreach (var v in m_Visited) m = Mathf.Min(m, FrostUtil.FlatDistance(p, v));
            return m;
        }

        void Remember(Vector3 p)
        {
            m_Visited.Enqueue(p);
            while (m_Visited.Count > 5) m_Visited.Dequeue();
        }
    }

    /// <summary>
    /// A walk grid over the room for the kids: free floor (inside the walls, off the furniture, trees, rocks, props and
    /// Frosty), A* across it, and the path pulled tight so they walk in straight lines between the corners.
    /// </summary>
    public static class KidNav
    {
        const float Cell = 0.2f, Clearance = 0.2f, WallInset = 0.25f;
        static RoomInfo s_Room;
        static bool[] s_Free;
        static int s_W, s_H;
        static Vector2 s_Origin;
        static float s_BuiltAt = -100f;
        static Vector3 s_FrostyAt;

        static void Ensure()
        {
            var room = RoomService.Current;
            if (room == null) { s_Room = null; return; }
            var fr = Frosty.Instance ? Frosty.Instance.transform.position : new Vector3(1e4f, 0f, 1e4f);
            // the room's obstacles are set once the scene is laid out; refresh now and then in case anything moved
            if (room == s_Room && s_Free != null && Time.time - s_BuiltAt < 10f && (fr - s_FrostyAt).sqrMagnitude < 0.01f) return;
            s_Room = room; s_BuiltAt = Time.time; s_FrostyAt = fr;
            var b = room.Bounds;
            s_Origin = new Vector2(b.xMin, b.yMin);
            s_W = Mathf.Max(1, Mathf.CeilToInt(b.width / Cell));
            s_H = Mathf.Max(1, Mathf.CeilToInt(b.height / Cell));
            s_Free = new bool[s_W * s_H];
            float frostyR = Frosty.FootprintRadius + 0.1f;
            for (int y = 0; y < s_H; y++)
                for (int x = 0; x < s_W; x++)
                {
                    var p = CellPos(x, y);
                    s_Free[y * s_W + x] = room.IsFree(p, Clearance, WallInset) && FrostUtil.FlatDistance(p, fr) > frostyR;
                }
        }

        static Vector3 CellPos(int x, int y) => new Vector3(s_Origin.x + (x + 0.5f) * Cell, s_Room != null ? s_Room.FloorY : 0f, s_Origin.y + (y + 0.5f) * Cell);
        static bool ToCell(Vector3 p, out int x, out int y)
        {
            x = Mathf.FloorToInt((p.x - s_Origin.x) / Cell);
            y = Mathf.FloorToInt((p.z - s_Origin.y) / Cell);
            return x >= 0 && y >= 0 && x < s_W && y < s_H;
        }
        static bool FreeCell(int x, int y) => x >= 0 && y >= 0 && x < s_W && y < s_H && s_Free[y * s_W + x];

        /// <summary>Can a kid stand here?</summary>
        public static bool Walkable(Vector3 p)
        {
            Ensure();
            return s_Room != null && ToCell(p, out int x, out int y) && s_Free[y * s_W + x];
        }

        /// <summary>A straight walk from a to b stays on free floor.</summary>
        public static bool LineClear(Vector3 a, Vector3 b)
        {
            Ensure();
            if (s_Room == null) return false;
            float len = FrostUtil.FlatDistance(a, b);
            int n = Mathf.Max(1, Mathf.CeilToInt(len / (Cell * 0.5f)));
            for (int i = 1; i <= n; i++)
            {
                var q = Vector3.Lerp(a, b, i / (float)n);
                if (!ToCell(q, out int x, out int y) || !s_Free[y * s_W + x]) return false;
            }
            return true;
        }

        /// <summary>Corners to walk through from a to b (ending at b), or null if there's no way there.</summary>
        public static List<Vector3> FindPath(Vector3 from, Vector3 to)
        {
            Ensure();
            if (s_Room == null) return null;
            to.y = s_Room.FloorY;
            if (!Walkable(to)) return null;
            if (LineClear(from, to)) return new List<Vector3> { to };
            if (!Nearest(from, out int sx, out int sy) || !ToCell(to, out int gx, out int gy)) return null;

            int n = s_W * s_H, start = sy * s_W + sx, goal = gy * s_W + gx;
            var g = new float[n];
            var came = new int[n];
            var closed = new bool[n];
            for (int i = 0; i < n; i++) { g[i] = float.MaxValue; came[i] = -1; }
            var open = new Heap();
            g[start] = 0f;
            open.Push(start, H(sx, sy, gx, gy));
            bool found = false;
            int expanded = 0;
            while (open.Count > 0 && expanded < 8000)
            {
                int cur = open.Pop();
                if (closed[cur]) continue;
                if (cur == goal) { found = true; break; }
                closed[cur] = true; expanded++;
                int cx = cur % s_W, cy = cur / s_W;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx, ny = cy + dy;
                        if (!FreeCell(nx, ny)) continue;
                        if (dx != 0 && dy != 0 && (!FreeCell(cx + dx, cy) || !FreeCell(cx, cy + dy))) continue;   // no cutting corners
                        int ni = ny * s_W + nx;
                        if (closed[ni]) continue;
                        float ng = g[cur] + (dx != 0 && dy != 0 ? 1.4142f : 1f);
                        if (ng >= g[ni]) continue;
                        g[ni] = ng; came[ni] = cur;
                        open.Push(ni, ng + H(nx, ny, gx, gy));
                    }
            }
            if (!found) return null;

            var cells = new List<Vector3>();
            for (int c = goal; c != -1; c = came[c]) cells.Add(CellPos(c % s_W, c / s_W));
            cells.Reverse();
            cells[cells.Count - 1] = to;
            // pull the path tight: from each corner, head for the farthest point still in a straight line
            var path = new List<Vector3>();
            var at = from;
            int k = 0;
            while (k < cells.Count)
            {
                int far = k;
                for (int j = cells.Count - 1; j > k; j--) if (LineClear(at, cells[j])) { far = j; break; }
                path.Add(cells[far]);
                at = cells[far];
                k = far + 1;
            }
            return path;
        }

        static float H(int x, int y, int gx, int gy)
        {
            int dx = Mathf.Abs(x - gx), dy = Mathf.Abs(y - gy);
            return Mathf.Max(dx, dy) + 0.4142f * Mathf.Min(dx, dy);
        }

        /// <summary>The free cell nearest p (kids can end up standing just inside a blocked cell).</summary>
        static bool Nearest(Vector3 p, out int x, out int y)
        {
            ToCell(p, out int px, out int py);
            for (int r = 0; r <= 4; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        if (FreeCell(px + dx, py + dy)) { x = px + dx; y = py + dy; return true; }
                    }
            x = y = 0;
            return false;
        }

        /// <summary>A tiny binary min-heap of cell indices.</summary>
        class Heap
        {
            readonly List<(int i, float f)> m_A = new List<(int, float)>();
            public int Count => m_A.Count;
            public void Push(int i, float f)
            {
                m_A.Add((i, f));
                int c = m_A.Count - 1;
                while (c > 0) { int p = (c - 1) / 2; if (m_A[p].f <= m_A[c].f) break; (m_A[p], m_A[c]) = (m_A[c], m_A[p]); c = p; }
            }
            public int Pop()
            {
                int top = m_A[0].i;
                int last = m_A.Count - 1;
                m_A[0] = m_A[last]; m_A.RemoveAt(last);
                int c = 0;
                while (true)
                {
                    int l = c * 2 + 1, r = l + 1, s = c;
                    if (l < m_A.Count && m_A[l].f < m_A[s].f) s = l;
                    if (r < m_A.Count && m_A[r].f < m_A[s].f) s = r;
                    if (s == c) break;
                    (m_A[s], m_A[c]) = (m_A[c], m_A[s]); c = s;
                }
                return top;
            }
        }
    }
}
