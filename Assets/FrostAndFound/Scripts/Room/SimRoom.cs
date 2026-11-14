using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// The stand-in for your real room when there is no headset (Editor / desktop testing): a cosy living room
    /// with walls, trim, a window, a sofa, a bookshelf and a lamp — so layout, furniture avoidance, snowball
    /// splats on walls and photo backdrops can all be tested without passthrough.
    /// </summary>
    public class SimRoom : MonoBehaviour
    {
        readonly List<Bounds> m_Obstacles = new List<Bounds>();

        public static RoomInfo Build(Vector2 size, out SimRoom room)
        {
            var go = new GameObject("Simulated Room (desktop only)");
            room = go.AddComponent<SimRoom>();
            room.Create(size);
            var info = RoomInfo.Rectangle(Vector3.zero, size, 0f, 0f);
            info.Source = "simulated room";
            info.CeilingY = 2.5f;
            var obstacles = room.m_Obstacles;
            info.IsBlocked = (p, r) =>
            {
                foreach (var b in obstacles)
                {
                    var e = b; e.Expand(new Vector3(r * 2f, 0f, r * 2f));
                    if (p.x > e.min.x && p.x < e.max.x && p.z > e.min.z && p.z < e.max.z) return true;
                }
                return false;
            };
            return info;
        }

        void Create(Vector2 size)
        {
            float hx = size.x * 0.5f, hz = size.y * 0.5f, h = 2.5f, t = 0.12f;
            var wall = Palette.Toon(Palette.Wall, false, 0.05f);
            var trim = Palette.Toon(Palette.WallTrim, false, 0.1f);
            var floor = Palette.Toon(Palette.Floorboards, false, 0.05f);

            // floor (under the snow) + boards
            Box("Floor", new Vector3(0, -0.05f, 0), new Vector3(size.x + 2 * t, 0.1f, size.y + 2 * t), floor, true);
            for (float x = -hx + 0.3f; x < hx; x += 0.6f)
                Box("Board seam", new Vector3(x, 0.001f, 0), new Vector3(0.01f, 0.002f, size.y), Palette.Toon(Palette.Wood), false);

            // walls with baseboards and crown trim
            Box("Wall N", new Vector3(0, h / 2, hz + t / 2), new Vector3(size.x + 2 * t, h, t), wall, true);
            Box("Wall S", new Vector3(0, h / 2, -hz - t / 2), new Vector3(size.x + 2 * t, h, t), wall, true);
            Box("Wall E", new Vector3(hx + t / 2, h / 2, 0), new Vector3(t, h, size.y), wall, true);
            Box("Wall W", new Vector3(-hx - t / 2, h / 2, 0), new Vector3(t, h, size.y), wall, true);
            Box("Ceiling", new Vector3(0, h + 0.05f, 0), new Vector3(size.x + 2 * t, 0.1f, size.y + 2 * t), Palette.Toon(Palette.SkyHorizon), true);
            foreach (var (pos, scale) in new[]
            {
                (new Vector3(0, 0.07f, hz - 0.015f), new Vector3(size.x, 0.14f, 0.03f)),
                (new Vector3(0, 0.07f, -hz + 0.015f), new Vector3(size.x, 0.14f, 0.03f)),
                (new Vector3(hx - 0.015f, 0.07f, 0), new Vector3(0.03f, 0.14f, size.y)),
                (new Vector3(-hx + 0.015f, 0.07f, 0), new Vector3(0.03f, 0.14f, size.y)),
                (new Vector3(0, h - 0.05f, hz - 0.02f), new Vector3(size.x, 0.1f, 0.04f)),
                (new Vector3(0, h - 0.05f, -hz + 0.02f), new Vector3(size.x, 0.1f, 0.04f)),
            })
                Box("Trim", pos, scale, trim, false);

            // window on the north wall (the light story)
            var frame = Palette.Toon(Palette.Paper, true, 0.1f);
            Box("Window glow", new Vector3(0.6f, 1.45f, hz - 0.005f), new Vector3(1.2f, 0.9f, 0.01f), Palette.Unlit(Palette.SkyTop), false);
            Box("Window frame T", new Vector3(0.6f, 1.92f, hz - 0.03f), new Vector3(1.32f, 0.07f, 0.06f), frame, false);
            Box("Window frame B", new Vector3(0.6f, 0.98f, hz - 0.05f), new Vector3(1.4f, 0.07f, 0.1f), frame, false);
            Box("Window frame L", new Vector3(-0.03f, 1.45f, hz - 0.03f), new Vector3(0.07f, 0.95f, 0.06f), frame, false);
            Box("Window frame R", new Vector3(1.23f, 1.45f, hz - 0.03f), new Vector3(0.07f, 0.95f, 0.06f), frame, false);
            Box("Window mullion", new Vector3(0.6f, 1.45f, hz - 0.03f), new Vector3(0.04f, 0.9f, 0.05f), frame, false);

            // sofa against the west wall
            var sofa = Palette.Toon(Palette.Sofa, true, 0.15f);
            var sofaPos = new Vector3(-hx + 0.45f, 0, -0.4f);
            Obstacle(sofaPos + new Vector3(0, 0.25f, 0), new Vector3(0.85f, 0.5f, 1.7f));
            RoundBox("Sofa base", sofaPos + new Vector3(0.05f, 0.22f, 0), new Vector3(0.8f, 0.36f, 1.7f), sofa);
            RoundBox("Sofa back", sofaPos + new Vector3(-0.3f, 0.55f, 0), new Vector3(0.22f, 0.7f, 1.7f), sofa);
            RoundBox("Sofa arm L", sofaPos + new Vector3(0.05f, 0.45f, 0.78f), new Vector3(0.8f, 0.3f, 0.2f), sofa);
            RoundBox("Sofa arm R", sofaPos + new Vector3(0.05f, 0.45f, -0.78f), new Vector3(0.8f, 0.3f, 0.2f), sofa);
            RoundBox("Cushion", sofaPos + new Vector3(0.08f, 0.47f, 0.35f), new Vector3(0.55f, 0.12f, 0.6f), Palette.Toon(Palette.Manila, true));
            RoundBox("Cushion", sofaPos + new Vector3(0.08f, 0.47f, -0.35f), new Vector3(0.55f, 0.12f, 0.6f), Palette.Toon(Palette.Manila, true));

            // bookshelf in the north-east corner
            var wood = Palette.Toon(Palette.Wood, true, 0.1f);
            var shelfPos = new Vector3(hx - 0.25f, 0, hz - 0.6f);
            Obstacle(shelfPos + new Vector3(0, 0.8f, 0), new Vector3(0.4f, 1.6f, 0.9f));
            RoundBox("Shelf", shelfPos + new Vector3(0, 0.8f, 0), new Vector3(0.38f, 1.6f, 0.9f), wood);
            Color[] spines = { Palette.Books1, Palette.Books2, Palette.Books3, Palette.LucyBlue, Palette.SerenaPurple };
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 6; i++)
                    Box("Book", shelfPos + new Vector3(-0.2f, 0.35f + row * 0.45f, -0.33f + i * 0.13f), new Vector3(0.05f, 0.3f + (i % 3) * 0.03f, 0.1f),
                        Palette.Toon(spines[(i + row) % spines.Length]), false);

            // lamp in the south-east corner (warm accent)
            var lampPos = new Vector3(hx - 0.35f, 0, -hz + 0.35f);
            Obstacle(lampPos + new Vector3(0, 0.6f, 0), new Vector3(0.35f, 1.2f, 0.35f));
            ProcMesh.Part("Lamp base", transform, ProcMesh.Cylinder(16), wood, lampPos + new Vector3(0, 0.02f, 0), new Vector3(0.3f, 0.04f, 0.3f));
            ProcMesh.Part("Lamp pole", transform, ProcMesh.Cylinder(8), wood, lampPos + new Vector3(0, 0.7f, 0), new Vector3(0.04f, 1.4f, 0.04f));
            ProcMesh.Part("Lamp shade", transform, ProcMesh.Cylinder(16, 0.6f), Palette.Toon(Palette.LampGlow, true, 0.2f, 0.6f), lampPos + new Vector3(0, 1.45f, 0), new Vector3(0.42f, 0.3f, 0.42f));

            // rug in front of the sofa
            ProcMesh.Part("Rug", transform, ProcMesh.Disc(28), Palette.Toon(Palette.StampRed.WithAlpha(1f) * 0.8f + Palette.Manila * 0.2f), new Vector3(-0.6f, 0.002f, -0.4f), new Vector3(1.4f, 0.004f, 1.0f));

            // a picture frame on the east wall
            Box("Frame", new Vector3(hx - 0.01f, 1.55f, -0.6f), new Vector3(0.03f, 0.5f, 0.7f), wood, false);
            Box("Picture", new Vector3(hx - 0.025f, 1.55f, -0.6f), new Vector3(0.01f, 0.4f, 0.6f), Palette.Toon(Palette.SkyTop), false);
            MeshMerger.Merge(transform, null, 0.12f);
        }

        void Obstacle(Vector3 center, Vector3 size) => m_Obstacles.Add(new Bounds(center, size));

        GameObject Box(string name, Vector3 pos, Vector3 scale, Material m, bool collider)
        {
            var go = ProcMesh.Part(name, transform, ProcMesh.RoundedBox(0.02f, 1), m, pos, scale);
            go.layer = Layers.Room;
            // The shell (walls, ceiling, floor, trim) stands in for the real room, which never blocks the
            // virtual sun on the headset. Letting it cast would put the whole scene in the toon shadow band.
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (collider) go.AddComponent<BoxCollider>();
            return go;
        }

        GameObject RoundBox(string name, Vector3 pos, Vector3 scale, Material m)
        {
            var go = ProcMesh.Part(name, transform, ProcMesh.RoundedBox(0.18f, 3), m, pos, scale);
            go.layer = Layers.Room;
            go.AddComponent<BoxCollider>();
            return go;
        }
    }
}
