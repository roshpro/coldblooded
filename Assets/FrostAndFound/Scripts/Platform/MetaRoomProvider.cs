#if FROST_META
using System.Collections;
using UnityEngine;
#if FROST_MRUK
using Meta.XR.MRUtilityKit;
#endif

namespace FrostAndFound
{
    /// <summary>
    /// Builds the game's RoomInfo from the headset's Space Setup via Meta MR Utility Kit:
    /// floor outline (snow goes here), ceiling (snowfall starts here), furniture volumes (kids avoid them)
    /// and invisible colliders on walls and furniture so snowballs splat on your real room.
    /// Falls back to a rectangle around the player if no scene data arrives.
    /// </summary>
    public class MetaRoomProvider : MonoBehaviour
    {
        public float timeoutSeconds = 45f;
        bool m_SkipRequested;
        public void SkipRoomScan() => m_SkipRequested = true;
        bool m_Done;

        public void Begin(PlayerRig rig) => StartCoroutine(Run(rig));

        IEnumerator Run(PlayerRig rig)
        {
#if FROST_MRUK
            var mruk = MRUK.Instance;
            if (mruk)
            {
                bool loaded = false;
                mruk.RegisterSceneLoadedCallback(() => loaded = true);
                float t = 0f;
                while (!loaded && t < timeoutSeconds && !m_SkipRequested) { t += Time.unscaledDeltaTime; yield return null; }
                var room = loaded ? mruk.GetCurrentRoom() : null;
                if (room != null && TryBuild(room, out var info))
                {
                    AddColliders(room);
                    if (FrostTuning.I.furnitureOccludes) AddOccluders(room);
                    m_Done = true;
                    RoomService.Set(info);
                    yield break;
                }
                Debug.LogWarning("[Frost] No Space Setup room found — using a rectangle around you. Run Space Setup on the headset for the full MR layout.");
            }
#endif
            yield return null;
            if (m_Done) yield break;
            var head = rig.Head;
            var size = FrostTuning.I.fallbackRoomSize;
            var center = head.position + rig.FlatForward * (size.y * 0.5f - 0.6f);
            var fallback = RoomInfo.Rectangle(new Vector3(center.x, rig.FloorY, center.z), size, head.eulerAngles.y, rig.FloorY);
            fallback.Source = "fallback rectangle";
            RoomService.Set(fallback);
        }

#if FROST_MRUK
        static bool TryBuild(MRUKRoom room, out RoomInfo info)
        {
            info = new RoomInfo { Source = "Space Setup (MRUK)" };
            var outline = room.GetRoomOutline();
            if (outline == null || outline.Count < 3) return false;
            // High-fidelity scenes can have several floors/ceilings: use the lowest floor and the highest ceiling
            float floorY = outline[0].y;
            if (room.FloorAnchors != null && room.FloorAnchors.Count > 0)
            {
                floorY = float.MaxValue;
                foreach (var f in room.FloorAnchors) if (f) floorY = Mathf.Min(floorY, f.transform.position.y);
            }
            foreach (var p in outline) info.Polygon.Add(p.XZ());
            info.FloorY = floorY;
            info.CeilingY = floorY + 2.4f;
            if (room.CeilingAnchors != null && room.CeilingAnchors.Count > 0)
            {
                float c = float.MinValue;
                foreach (var a in room.CeilingAnchors) if (a) c = Mathf.Max(c, a.transform.position.y);
                if (c > floorY + 1.5f) info.CeilingY = c;
            }
            info.IsBlocked = (p, r) =>
            {
                // test a little above the floor so rugs/floor planes don't count as furniture
                var probe = new Vector3(p.x, floorY + 0.25f, p.z);
                return room.IsPositionInSceneVolume(probe, r);
            };
            info.Recalculate();
            return true;
        }

        static void AddColliders(MRUKRoom room)
        {
            var go = new GameObject("Room Colliders (MRUK)");
            var fx = go.AddComponent<EffectMesh>();
            fx.Colliders = true;
            fx.HideMesh = true;
            fx.CastShadow = false;
            fx.Layer = Layers.Room;
            fx.Labels = MRUKAnchor.SceneLabels.WALL_FACE | MRUKAnchor.SceneLabels.TABLE | MRUKAnchor.SceneLabels.COUCH |
                        MRUKAnchor.SceneLabels.BED | MRUKAnchor.SceneLabels.STORAGE | MRUKAnchor.SceneLabels.SCREEN |
                        MRUKAnchor.SceneLabels.LAMP | MRUKAnchor.SceneLabels.PLANT | MRUKAnchor.SceneLabels.OTHER |
                        MRUKAnchor.SceneLabels.DOOR_FRAME | MRUKAnchor.SceneLabels.WINDOW_FRAME | MRUKAnchor.SceneLabels.CEILING;
            fx.MeshMaterial = Palette.Unlit(Palette.Ink.WithAlpha(0f), true);
            fx.SpawnOnStart = MRUK.RoomFilter.None;   // we build it now, once
            fx.CreateMesh();
        }

        /// <summary>
        /// Furniture from Space Setup drawn as invisible depth-only shapes: your real table/sofa/chair still shows through
        /// passthrough, but virtual snow, drifts, kids and snowballs behind it are hidden instead of being drawn on top.
        /// (The Depth API on the snow shader can't do this reliably: its depth map is coarse at chair legs and table edges.)
        /// </summary>
        static void AddOccluders(MRUKRoom room)
        {
            var go = new GameObject("Furniture Occluders (MRUK)");
            var fx = go.AddComponent<EffectMesh>();
            fx.Colliders = false;
            fx.HideMesh = false;
            fx.CastShadow = false;
            fx.Layer = Layers.Room;
            fx.Labels = MRUKAnchor.SceneLabels.TABLE | MRUKAnchor.SceneLabels.COUCH | MRUKAnchor.SceneLabels.BED |
                        MRUKAnchor.SceneLabels.STORAGE | MRUKAnchor.SceneLabels.SCREEN | MRUKAnchor.SceneLabels.LAMP |
                        MRUKAnchor.SceneLabels.PLANT | MRUKAnchor.SceneLabels.OTHER;
            fx.MeshMaterial = Palette.DepthOccluder();
            fx.SpawnOnStart = MRUK.RoomFilter.None;
            fx.CreateMesh();
            int n = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                n++;
            }
            Debug.Log($"[Frost] Furniture occluders: {n} pieces of furniture hide the snow behind them.");
        }
#endif
    }
}
#endif
