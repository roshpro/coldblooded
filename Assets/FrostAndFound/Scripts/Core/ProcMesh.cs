using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Chunky low-poly meshes for the mockups (spheres, capsules, cylinders, cones, rounded boxes, discs, tori).
    /// Every mesh is cached by its parameters so dozens of parts share a handful of meshes.
    /// </summary>
    public static class ProcMesh
    {
        static readonly Dictionary<string, Mesh> s_Cache = new Dictionary<string, Mesh>();

        static Mesh Cached(string key, System.Func<Mesh> build)
        {
            if (s_Cache.TryGetValue(key, out var m) && m) return m;
            m = build();
            m.name = key;
            s_Cache[key] = m;
            return m;
        }

        /// <summary>UV sphere of radius 0.5 (matches Unity's primitive) — low segment counts read as chunky.</summary>
        public static Mesh Sphere(int lon = 14, int lat = 10, float lumpiness = 0f, int seed = 0)
        {
            return Cached($"sphere_{lon}_{lat}_{lumpiness:0.000}_{seed}", () =>
            {
                var verts = new List<Vector3>();
                var norms = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();
                for (int y = 0; y <= lat; y++)
                {
                    float v = y / (float)lat;
                    float phi = v * Mathf.PI;
                    for (int x = 0; x <= lon; x++)
                    {
                        float u = x / (float)lon;
                        float theta = u * Mathf.PI * 2f;
                        var n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                        float r = 0.5f;
                        if (lumpiness > 0f)
                        {
                            int xi = x % lon;
                            float h = Mathf.Sin(xi * 1.7f + seed) * Mathf.Cos(y * 2.3f + seed * 0.7f) + Mathf.Sin(xi * 0.9f + y * 1.3f + seed * 1.9f) * 0.6f;
                            if (y == 0 || y == lat) h = 0f;
                            r *= 1f + h * lumpiness;
                        }
                        verts.Add(n * r);
                        norms.Add(n);
                        uvs.Add(new Vector2(u, 1f - v));
                    }
                }
                for (int y = 0; y < lat; y++)
                    for (int x = 0; x < lon; x++)
                    {
                        int a = y * (lon + 1) + x, b = a + lon + 1;
                        tris.Add(a); tris.Add(a + 1); tris.Add(b);
                        tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
                    }
                var m = new Mesh();
                m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
                if (lumpiness > 0f) m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            });
        }

        /// <summary>Capsule along Y, total height 1 and radius 0.25 at scale 1 (like Unity's primitive, but halved radius control).</summary>
        public static Mesh Capsule(float radius = 0.25f, float height = 1f, int lon = 12, int lat = 8)
        {
            return Cached($"capsule_{radius:0.000}_{height:0.000}_{lon}_{lat}", () =>
            {
                var verts = new List<Vector3>();
                var norms = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();
                float half = Mathf.Max(0f, height * 0.5f - radius);
                int rings = lat + 1; // includes duplicated equator for the cylinder section
                var ringData = new List<(float y, float r, float ny)>();
                int hemi = lat / 2;
                for (int i = 0; i <= hemi; i++)
                {
                    float a = Mathf.PI * 0.5f * (1f - i / (float)hemi);
                    ringData.Add((half + Mathf.Sin(a) * radius, Mathf.Cos(a) * radius, Mathf.Sin(a)));
                }
                for (int i = 0; i <= hemi; i++)
                {
                    float a = -Mathf.PI * 0.5f * (i / (float)hemi);
                    ringData.Add((-half + Mathf.Sin(a) * radius, Mathf.Cos(a) * radius, Mathf.Sin(a)));
                }
                rings = ringData.Count;
                for (int y = 0; y < rings; y++)
                {
                    var (ry, rr, ny) = ringData[y];
                    float nxz = Mathf.Sqrt(Mathf.Max(0f, 1f - ny * ny));
                    for (int x = 0; x <= lon; x++)
                    {
                        float t = x / (float)lon * Mathf.PI * 2f;
                        float cx = Mathf.Cos(t), cz = Mathf.Sin(t);
                        verts.Add(new Vector3(cx * rr, ry, cz * rr));
                        norms.Add(new Vector3(cx * nxz, ny, cz * nxz).normalized);
                        uvs.Add(new Vector2(x / (float)lon, 1f - y / (float)(rings - 1)));
                    }
                }
                for (int y = 0; y < rings - 1; y++)
                    for (int x = 0; x < lon; x++)
                    {
                        int a = y * (lon + 1) + x, b = a + lon + 1;
                        tris.Add(a); tris.Add(a + 1); tris.Add(b);
                        tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
                    }
                var m = new Mesh();
                m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
                m.RecalculateBounds();
                return m;
            });
        }

        /// <summary>Cylinder along Y with height 1 and radius 0.5 (top/bottom radius scale for cones/tapers).</summary>
        public static Mesh Cylinder(int sides = 14, float topScale = 1f, bool caps = true)
        {
            return Cached($"cyl_{sides}_{topScale:0.000}_{caps}", () =>
            {
                var verts = new List<Vector3>();
                var norms = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();
                float rb = 0.5f, rt = 0.5f * topScale;
                float slope = (rb - rt); // over height 1
                for (int i = 0; i <= sides; i++)
                {
                    float t = i / (float)sides * Mathf.PI * 2f;
                    float cx = Mathf.Cos(t), cz = Mathf.Sin(t);
                    var n = new Vector3(cx, slope, cz).normalized;
                    verts.Add(new Vector3(cx * rb, -0.5f, cz * rb)); norms.Add(n); uvs.Add(new Vector2(i / (float)sides, 0));
                    verts.Add(new Vector3(cx * rt, 0.5f, cz * rt)); norms.Add(n); uvs.Add(new Vector2(i / (float)sides, 1));
                }
                for (int i = 0; i < sides; i++)
                {
                    int a = i * 2;
                    tris.Add(a); tris.Add(a + 1); tris.Add(a + 2);
                    tris.Add(a + 1); tris.Add(a + 3); tris.Add(a + 2);
                }
                if (caps)
                {
                    for (int side = 0; side < 2; side++)
                    {
                        float y = side == 0 ? -0.5f : 0.5f;
                        float r = side == 0 ? rb : rt;
                        if (r < 1e-4f) continue;
                        var n = side == 0 ? Vector3.down : Vector3.up;
                        int center = verts.Count;
                        verts.Add(new Vector3(0, y, 0)); norms.Add(n); uvs.Add(new Vector2(0.5f, 0.5f));
                        for (int i = 0; i <= sides; i++)
                        {
                            float t = i / (float)sides * Mathf.PI * 2f;
                            verts.Add(new Vector3(Mathf.Cos(t) * r, y, Mathf.Sin(t) * r)); norms.Add(n);
                            uvs.Add(new Vector2(Mathf.Cos(t) * 0.5f + 0.5f, Mathf.Sin(t) * 0.5f + 0.5f));
                        }
                        for (int i = 0; i < sides; i++)
                        {
                            if (side == 0) { tris.Add(center); tris.Add(center + 1 + i); tris.Add(center + 2 + i); }
                            else { tris.Add(center); tris.Add(center + 2 + i); tris.Add(center + 1 + i); }
                        }
                    }
                }
                var m = new Mesh();
                m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
                m.RecalculateBounds();
                return m;
            });
        }

        public static Mesh Cone(int sides = 12) => Cylinder(sides, 0f, true);

        /// <summary>Box of size 1 with rounded edges (radius in local units, ≤ 0.5).</summary>
        public static Mesh RoundedBox(float radius = 0.15f, int seg = 3)
        {
            return Cached($"rbox_{radius:0.000}_{seg}", () =>
            {
                // Build a subdivided cube then pull vertices toward an inner box (classic rounded-cube trick).
                int n = seg * 2 + 2; // grid per face
                var verts = new List<Vector3>();
                var norms = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();
                Vector3[] faceN = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                Vector3 inner = Vector3.one * (0.5f - radius);
                foreach (var fn in faceN)
                {
                    var up = Mathf.Abs(fn.y) > 0.5f ? Vector3.forward : Vector3.up;
                    var right = Vector3.Cross(up, fn);
                    int start = verts.Count;
                    for (int y = 0; y <= n; y++)
                        for (int x = 0; x <= n; x++)
                        {
                            float u = x / (float)n - 0.5f, v = y / (float)n - 0.5f;
                            // non-uniform spacing: dense near edges where rounding happens
                            u = Remap(u, radius); v = Remap(v, radius);
                            var p = fn * 0.5f + right * u + up * v;
                            var c = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                            var d = p - c;
                            var nn = d.sqrMagnitude > 1e-8f ? d.normalized : fn;
                            verts.Add(c + nn * radius);
                            norms.Add(nn);
                            uvs.Add(new Vector2(x / (float)n, y / (float)n));
                        }
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                        {
                            int a = start + y * (n + 1) + x, b = a + n + 1;
                            tris.Add(a); tris.Add(a + 1); tris.Add(b);
                            tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
                        }
                }
                var m = new Mesh();
                m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
                m.RecalculateBounds();
                return m;
            });
        }

        static float Remap(float u, float radius)
        {
            // u in [-0.5, 0.5]; push grid lines so that an even share sits inside the rounded band
            float a = Mathf.Abs(u) * 2f; // 0..1
            float inner = 1f - radius * 2f;
            float mapped = a < 0.5f ? (a / 0.5f) * inner : inner + (a - 0.5f) / 0.5f * (1f - inner);
            return Mathf.Sign(u) * mapped * 0.5f;
        }

        /// <summary>Flat disc in XZ with radius 0.5 facing +Y.</summary>
        public static Mesh Disc(int sides = 24)
        {
            return Cached($"disc_{sides}", () => Cylinder(sides, 1f, true));
        }

        /// <summary>Quad in XY facing -Z (like Unity's Quad primitive) with size 1.</summary>
        public static Mesh Quad()
        {
            return Cached("quad", () =>
            {
                var m = new Mesh();
                m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
                m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                m.RecalculateBounds();
                return m;
            });
        }

        /// <summary>Flat quad lying in XZ facing +Y, size 1.</summary>
        public static Mesh FloorQuad()
        {
            return Cached("floorquad", () =>
            {
                var m = new Mesh();
                m.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f) };
                m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                m.RecalculateBounds();
                return m;
            });
        }

        public static Mesh Torus(float major = 0.4f, float minor = 0.1f, int seg = 18, int sides = 8)
        {
            return Cached($"torus_{major:0.000}_{minor:0.000}_{seg}_{sides}", () =>
            {
                var verts = new List<Vector3>();
                var norms = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    var c = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    for (int j = 0; j <= sides; j++)
                    {
                        float b = j / (float)sides * Mathf.PI * 2f;
                        var n = c * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                        verts.Add(c * major + n * minor);
                        norms.Add(n);
                        uvs.Add(new Vector2(i / (float)seg, j / (float)sides));
                    }
                }
                for (int i = 0; i < seg; i++)
                    for (int j = 0; j < sides; j++)
                    {
                        int a = i * (sides + 1) + j, b = a + sides + 1;
                        tris.Add(a); tris.Add(a + 1); tris.Add(b);
                        tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
                    }
                var m = new Mesh();
                m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
                m.RecalculateBounds();
                return m;
            });
        }

        /// <summary>A closed ribbon tracing a 2D polyline on the XZ plane (used for the chalk outline).</summary>
        public static Mesh Ribbon(IList<Vector2> points, float width, bool closed = true)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int count = points.Count;
            float dist = 0f;
            for (int i = 0; i < count; i++)
            {
                var p = points[i];
                var prev = points[closed ? (i - 1 + count) % count : Mathf.Max(0, i - 1)];
                var next = points[closed ? (i + 1) % count : Mathf.Min(count - 1, i + 1)];
                var dir = (next - prev).normalized;
                var nrm = new Vector2(-dir.y, dir.x);
                if (i > 0) dist += Vector2.Distance(points[i - 1], p);
                verts.Add(new Vector3(p.x + nrm.x * width * 0.5f, 0, p.y + nrm.y * width * 0.5f));
                verts.Add(new Vector3(p.x - nrm.x * width * 0.5f, 0, p.y - nrm.y * width * 0.5f));
                uvs.Add(new Vector2(dist, 0)); uvs.Add(new Vector2(dist, 1));
            }
            int segs = closed ? count : count - 1;
            for (int i = 0; i < segs; i++)
            {
                int a = i * 2, b = ((i + 1) % count) * 2;
                tris.Add(a); tris.Add(b); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
            }
            var m = new Mesh { name = "ribbon" };
            m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            var n = new Vector3[verts.Count];
            for (int i = 0; i < n.Length; i++) n[i] = Vector3.up;
            m.normals = n;
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Regular grid in XZ over [0,1]² with (res+1)² vertices.</summary>
        public static Mesh Grid(int resX, int resZ)
        {
            var m = new Mesh { name = $"grid_{resX}x{resZ}" };
            if ((resX + 1) * (resZ + 1) > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            var verts = new Vector3[(resX + 1) * (resZ + 1)];
            var uvs = new Vector2[verts.Length];
            var norms = new Vector3[verts.Length];
            for (int z = 0; z <= resZ; z++)
                for (int x = 0; x <= resX; x++)
                {
                    int i = z * (resX + 1) + x;
                    verts[i] = new Vector3(x / (float)resX, 0, z / (float)resZ);
                    uvs[i] = new Vector2(x / (float)resX, z / (float)resZ);
                    norms[i] = Vector3.up;
                }
            var tris = new int[resX * resZ * 6];
            int t = 0;
            for (int z = 0; z < resZ; z++)
                for (int x = 0; x < resX; x++)
                {
                    int a = z * (resX + 1) + x, b = a + resX + 1;
                    tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
                }
            m.vertices = verts; m.uv = uvs; m.normals = norms; m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        // ---------------------------------------------------------------------------------------------
        // Part helper — the mockup builders compose characters out of these.
        // ---------------------------------------------------------------------------------------------
        public static GameObject Part(string name, Transform parent, Mesh mesh, Material mat,
            Vector3 localPos, Vector3 localScale, Vector3 localEuler = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            if (parent) go.layer = parent.gameObject.layer;
            return go;
        }

        public static Transform Pivot(string name, Transform parent, Vector3 localPos, Vector3 localEuler = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            if (parent) go.layer = parent.gameObject.layer;
            return go.transform;
        }
    }
}
