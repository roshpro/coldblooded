using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FrostAndFound
{
    /// <summary>
    /// Quest draw-call diet for the primitive mockups: under every joint, the static leaf parts that share a
    /// material are combined into one mesh (the joint still animates them together). Parts that animate on their
    /// own (eyes, mouth, Serena's hat, Sherman's books…) are kept as they are. Tiny details stop casting shadows.
    /// </summary>
    public static class MeshMerger
    {
        static readonly HashSet<string> s_KeepDefault = new HashSet<string>
        {
            "Eye_L", "Eye_R", "Mouth", "Shine", "Lash",           // blink / talk
            "Beanie", "Cuff", "Pom",                              // Serena's hat hop
            "Book", "Pages",                                      // Sherman's dropped books
            "Flash", "Viewfinder",                                // Polaroid live parts
            "Face Snow", "Face Snow Bit",
            "Stain",                                              // Sherman's stains glint with the carrot clue
        };

        public static int Merge(Transform root, ICollection<string> keep = null, float shadowMinSize = 0.07f)
        {
            int saved = 0;
            var pivots = new List<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) pivots.Add(t);
            foreach (var pivot in pivots)
            {
                if (!pivot) continue;
                var groups = new Dictionary<Material, List<MeshFilter>>();
                foreach (Transform child in pivot)
                {
                    if (child.childCount > 0) continue;
                    if (s_KeepDefault.Contains(child.name) || (keep != null && keep.Contains(child.name))) continue;
                    var mf = child.GetComponent<MeshFilter>();
                    var mr = child.GetComponent<MeshRenderer>();
                    if (!mf || !mr || !mf.sharedMesh || mr.sharedMaterials.Length != 1) continue;
                    if (child.GetComponents<Component>().Length > 3) continue; // has colliders / logic: leave it alone
                    var mat = mr.sharedMaterial;
                    if (!mat) continue;
                    if (!groups.TryGetValue(mat, out var list)) groups[mat] = list = new List<MeshFilter>();
                    list.Add(mf);
                }
                foreach (var kv in groups)
                {
                    if (kv.Value.Count < 2) continue;
                    var combine = new CombineInstance[kv.Value.Count];
                    int verts = 0;
                    bool anyShadow = false;
                    for (int i = 0; i < kv.Value.Count; i++)
                    {
                        var mf = kv.Value[i];
                        combine[i] = new CombineInstance { mesh = mf.sharedMesh, transform = pivot.worldToLocalMatrix * mf.transform.localToWorldMatrix };
                        verts += mf.sharedMesh.vertexCount;
                        var r = mf.GetComponent<MeshRenderer>();
                        if (r.shadowCastingMode != ShadowCastingMode.Off && r.bounds.size.magnitude >= shadowMinSize) anyShadow = true;
                    }
                    var mesh = new Mesh { name = pivot.name + " merged (" + kv.Key.name + ")" };
                    if (verts > 65000) mesh.indexFormat = IndexFormat.UInt32;
                    mesh.CombineMeshes(combine, true, true);
                    mesh.RecalculateBounds();
                    var go = new GameObject("Merged · " + kv.Key.name);
                    go.layer = pivot.gameObject.layer;
                    go.transform.SetParent(pivot, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mrn = go.AddComponent<MeshRenderer>();
                    mrn.sharedMaterial = kv.Key;
                    mrn.shadowCastingMode = anyShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    mrn.receiveShadows = true;
                    foreach (var mf in kv.Value) Object.Destroy(mf.gameObject);
                    saved += kv.Value.Count - 1;
                }
            }
            // small details don't need to cast shadows
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                if (r.shadowCastingMode != ShadowCastingMode.Off && r.bounds.size.magnitude < shadowMinSize) r.shadowCastingMode = ShadowCastingMode.Off;
            return saved;
        }
    }
}
