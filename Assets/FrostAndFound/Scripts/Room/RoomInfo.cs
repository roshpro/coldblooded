using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// The play space as the game understands it: a floor polygon (world XZ), floor height,
    /// ceiling height and an optional "is something in the way here?" test (furniture from the scene model).
    /// </summary>
    public class RoomInfo
    {
        public readonly List<Vector2> Polygon = new List<Vector2>();   // world XZ, any winding
        public float FloorY;
        public float CeilingY = 2.4f;
        public string Source = "fallback";
        /// <summary>Returns true when the world position (at floor level) is inside furniture/obstacles.</summary>
        public System.Func<Vector3, float, bool> IsBlocked;

        Rect m_Bounds;
        Vector2 m_Centroid;

        public Rect Bounds => m_Bounds;
        public Vector2 Centroid => m_Centroid;
        public Vector3 Center => new Vector3(m_Centroid.x, FloorY, m_Centroid.y);
        public float Width => m_Bounds.width;
        public float Depth => m_Bounds.height;

        public static RoomInfo Rectangle(Vector3 center, Vector2 size, float yawDegrees, float floorY)
        {
            var r = new RoomInfo { FloorY = floorY };
            var rot = Quaternion.Euler(0, yawDegrees, 0);
            var hx = size.x * 0.5f; var hz = size.y * 0.5f;
            foreach (var c in new[] { new Vector3(-hx, 0, -hz), new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz), new Vector3(hx, 0, -hz) })
                r.Polygon.Add((center + rot * c).XZ());
            r.Recalculate();
            return r;
        }

        public void Recalculate()
        {
            if (Polygon.Count == 0) { m_Bounds = new Rect(-2, -2, 4, 4); m_Centroid = Vector2.zero; return; }
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in Polygon)
            {
                minX = Mathf.Min(minX, p.x); minY = Mathf.Min(minY, p.y);
                maxX = Mathf.Max(maxX, p.x); maxY = Mathf.Max(maxY, p.y);
            }
            m_Bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
            // area-weighted centroid
            float a = 0f; Vector2 c = Vector2.zero;
            for (int i = 0; i < Polygon.Count; i++)
            {
                var p0 = Polygon[i]; var p1 = Polygon[(i + 1) % Polygon.Count];
                float cross = p0.x * p1.y - p1.x * p0.y;
                a += cross; c += (p0 + p1) * cross;
            }
            m_Centroid = Mathf.Abs(a) > 1e-5f ? c / (3f * a) : m_Bounds.center;
            if (!Contains(m_Centroid)) m_Centroid = m_Bounds.center;
        }

        public bool Contains(Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = Polygon.Count - 1; i < Polygon.Count; j = i++)
            {
                var a = Polygon[i]; var b = Polygon[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y + 1e-9f) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        public bool Contains(Vector3 p) => Contains(p.XZ());

        /// <summary>Distance from p to the nearest polygon edge (positive inside).</summary>
        public float EdgeDistance(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0, j = Polygon.Count - 1; i < Polygon.Count; j = i++)
                best = Mathf.Min(best, DistToSegment(p, Polygon[j], Polygon[i]));
            return Contains(p) ? best : -best;
        }

        public Vector2 ClosestOnEdge(Vector2 p, out Vector2 inwardNormal)
        {
            float best = float.MaxValue; Vector2 bestPt = p; inwardNormal = Vector2.zero;
            for (int i = 0, j = Polygon.Count - 1; i < Polygon.Count; j = i++)
            {
                var q = ClosestOnSegment(p, Polygon[j], Polygon[i]);
                float d = (q - p).sqrMagnitude;
                if (d < best) { best = d; bestPt = q; }
            }
            var toCenter = (m_Centroid - bestPt);
            inwardNormal = toCenter.sqrMagnitude > 1e-6f ? toCenter.normalized : Vector2.up;
            return bestPt;
        }

        /// <summary>Keeps a world point inside the room by at least inset metres.</summary>
        public Vector3 ClampInside(Vector3 p, float inset)
        {
            var q = p.XZ();
            if (Contains(q) && EdgeDistance(q) >= inset) return p;
            // walk toward the centroid until valid
            for (int i = 0; i < 24; i++)
            {
                q = Vector2.Lerp(q, m_Centroid, 0.15f);
                if (Contains(q) && EdgeDistance(q) >= inset) break;
            }
            return new Vector3(q.x, p.y, q.y);
        }

        public bool IsFree(Vector3 p, float radius, float wallInset)
        {
            var q = p.XZ();
            if (!Contains(q) || EdgeDistance(q) < wallInset) return false;
            if (IsBlocked != null && IsBlocked(new Vector3(p.x, FloorY, p.z), radius)) return false;
            return true;
        }

        public Vector3 RandomFreePoint(float radius, float wallInset, System.Random rng = null, int tries = 60)
        {
            rng ??= new System.Random();
            for (int i = 0; i < tries; i++)
            {
                var q = new Vector2(
                    Mathf.Lerp(m_Bounds.xMin, m_Bounds.xMax, (float)rng.NextDouble()),
                    Mathf.Lerp(m_Bounds.yMin, m_Bounds.yMax, (float)rng.NextDouble()));
                var p = new Vector3(q.x, FloorY, q.y);
                if (IsFree(p, radius, wallInset)) return p;
            }
            return Center;
        }

        /// <summary>Points on a ring inset from the walls, sampled evenly by perimeter length.</summary>
        public List<Vector3> PerimeterRing(float inset, int samples)
        {
            var pts = new List<Vector3>();
            float perimeter = 0f;
            for (int i = 0; i < Polygon.Count; i++) perimeter += Vector2.Distance(Polygon[i], Polygon[(i + 1) % Polygon.Count]);
            if (perimeter <= 0f) return pts;
            for (int s = 0; s < samples; s++)
            {
                float target = perimeter * s / samples;
                float acc = 0f;
                for (int i = 0; i < Polygon.Count; i++)
                {
                    var a = Polygon[i]; var b = Polygon[(i + 1) % Polygon.Count];
                    float len = Vector2.Distance(a, b);
                    if (acc + len >= target)
                    {
                        var e = Vector2.Lerp(a, b, (target - acc) / Mathf.Max(len, 1e-5f));
                        var inward = (m_Centroid - e).normalized;
                        var p = e + inward * inset;
                        // pull further in if the inset point is still outside or too near a wall (concave rooms)
                        for (int k = 0; k < 10 && (!Contains(p) || EdgeDistance(p) < inset * 0.8f); k++) p = Vector2.Lerp(p, m_Centroid, 0.12f);
                        pts.Add(new Vector3(p.x, FloorY, p.y));
                        break;
                    }
                    acc += len;
                }
            }
            return pts;
        }

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b) => Vector2.Distance(p, ClosestOnSegment(p, a, b));

        static Vector2 ClosestOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-9f));
            return a + ab * t;
        }
    }

    /// <summary>Global access to the current play space and an event when it changes.</summary>
    public static class RoomService
    {
        public static RoomInfo Current { get; private set; }
        public static event System.Action<RoomInfo> Changed;

        public static void Set(RoomInfo room)
        {
            room.Recalculate();
            Current = room;
            Debug.Log($"[Frost] Room ready ({room.Source}): {room.Width:0.0} × {room.Depth:0.0} m, floor y={room.FloorY:0.00}");
            Changed?.Invoke(room);
        }

        public static void Clear() { Current = null; }
    }
}
