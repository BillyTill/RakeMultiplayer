using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Draw-call analysis and runtime optimization of level meshes.
    ///
    /// The level's heavy meshes split one object into dozens of submeshes that repeat the
    /// same few materials (a cliff with 72 submeshes and 5 materials costs 72 draw calls for
    /// no reason). Merging the submeshes that share a material removes those extra draws
    /// without touching vertices, transforms, LODs or lighting.
    /// </summary>
    public static class BatchDiagnostics
    {
        private sealed class Original
        {
            public MeshFilter Filter;
            public Mesh Mesh;
            public Material[] Materials;
        }

        private static readonly List<Original> _originals = new List<Original>();
        private static readonly Dictionary<string, Mesh> _meshCache = new Dictionary<string, Mesh>();

        // ---------------------------------------------------------------- report

        public static string Report()
        {
            MeshRenderer[] renderers = Object.FindObjectsOfType<MeshRenderer>();

            int total = 0;
            int subMeshes = 0;
            int readable = 0;
            int batched = 0;
            int lodGroups = 0;
            var byMaterial = new Dictionary<Material, int>();

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null) continue;

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                total++;
                if (renderer.isPartOfStaticBatch) batched++;
                if (filter.sharedMesh.isReadable) readable++;
                if (renderer.GetComponentInParent<LODGroup>() != null) lodGroups++;
                subMeshes += Mathf.Max(1, filter.sharedMesh.subMeshCount);

                Material[] materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    if (materials[m] == null) continue;
                    int count;
                    byMaterial.TryGetValue(materials[m], out count);
                    byMaterial[materials[m]] = count + 1;
                }
            }

            var sb = new StringBuilder("{\"ok\":true");
            sb.Append(",\"meshRenderers\":").Append(total);
            sb.Append(",\"subMeshes\":").Append(subMeshes);
            sb.Append(",\"readable\":").Append(readable);
            sb.Append(",\"alreadyBatched\":").Append(batched);
            sb.Append(",\"lodGroupObjects\":").Append(lodGroups);
            sb.Append(",\"uniqueMaterials\":").Append(byMaterial.Count);

            // Current frame only: what is drawn now, and what squashing would leave.
            int visible = 0;
            int visibleSubMeshes = 0;
            int afterSquash = 0;
            int squashable = 0;
            var histogram = new Dictionary<int, int>();
            var seenMaterials = new HashSet<Material>();
            var shrink = new Dictionary<string, int>();

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.isVisible) continue;

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                Mesh mesh = filter.sharedMesh;
                int subs = Mathf.Max(1, mesh.subMeshCount);
                visible++;
                visibleSubMeshes += subs;

                int count;
                histogram.TryGetValue(subs, out count);
                histogram[subs] = count + 1;

                int after = subs;
                List<Material> order = GroupMaterials(mesh, renderer.sharedMaterials);
                if (order != null) after = order.Count;

                afterSquash += after;
                if (after < subs)
                {
                    squashable++;
                    string key = subs + "->" + after;
                    int n;
                    shrink.TryGetValue(key, out n);
                    shrink[key] = n + 1;
                }

                Material[] materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    if (materials[m] != null) seenMaterials.Add(materials[m]);
                }
            }

            sb.Append(",\"visible\":{\"renderers\":").Append(visible);
            sb.Append(",\"subMeshes\":").Append(visibleSubMeshes);
            sb.Append(",\"uniqueMaterials\":").Append(seenMaterials.Count);
            sb.Append(",\"squashable\":").Append(squashable);
            sb.Append(",\"afterSquash\":").Append(afterSquash);

            var hist = new List<string>();
            foreach (var pair in histogram) hist.Add(pair.Key + ":" + pair.Value);
            hist.Sort();
            sb.Append(",\"subMeshHistogram\":\"").Append(string.Join(" ", hist.ToArray())).Append('"');

            var shrinks = new List<string>();
            foreach (var pair in shrink) shrinks.Add(pair.Key + " x" + pair.Value);
            sb.Append(",\"shrink\":\"").Append(string.Join(", ", shrinks.ToArray())).Append('"');
            sb.Append(",\"heavy\":\"").Append(HeavyVisible()).Append('"');
            sb.Append("}}");

            return sb.ToString();
        }

        /// <summary>The visible renderers that own the most submeshes, with the reason.</summary>
        private static string HeavyVisible()
        {
            MeshRenderer[] renderers = Object.FindObjectsOfType<MeshRenderer>();
            var rows = new List<KeyValuePair<int, string>>();

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.isVisible) continue;

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                int subs = Mathf.Max(1, filter.sharedMesh.subMeshCount);
                if (subs < 6) continue;

                string flags = "";
                if (renderer.GetComponentInParent<LODGroup>() != null) flags += "LOD ";
                if (renderer.GetComponent<Animator>() != null) flags += "anim ";
                if (renderer.GetComponent<Rigidbody>() != null) flags += "rb ";
                if (renderer.isPartOfStaticBatch) flags += "batched ";
                if (!filter.sharedMesh.isReadable) flags += "unreadable ";
                if (renderer.lightmapIndex >= 0) flags += "lightmap ";

                rows.Add(new KeyValuePair<int, string>(subs,
                    renderer.name + "[" + subs + "]" + (flags.Length > 0 ? "{" + flags.Trim() + "}" : "")));
            }

            rows.Sort(delegate(KeyValuePair<int, string> a, KeyValuePair<int, string> b)
            {
                return b.Key.CompareTo(a.Key);
            });

            var names = new List<string>();
            for (int i = 0; i < rows.Count && i < 8; i++) names.Add(rows[i].Value);
            return string.Join(", ", names.ToArray());
        }
        // ---------------------------------------------------------------- grouping

        /// <summary>
        /// Distinct materials in the order of their first submesh, or null when the mesh
        /// cannot be safely regrouped (unreadable, or a submesh without a material).
        /// </summary>
        private static List<Material> GroupMaterials(Mesh mesh, Material[] materials)
        {
            if (mesh == null || !mesh.isReadable) return null;

            int subMeshes = Mathf.Max(1, mesh.subMeshCount);
            if (materials == null || materials.Length < subMeshes) return null;

            var order = new List<Material>();
            for (int s = 0; s < subMeshes; s++)
            {
                Material material = materials[s];
                if (material == null) return null;
                if (!order.Contains(material)) order.Add(material);
            }

            return order;
        }

        // ---------------------------------------------------------------- squash

        /// <summary>
        /// Rebuilds meshes so every submesh uses its own material. Vertices are copied
        /// as-is, so geometry, skinning, LODs and lightmaps are unaffected.
        /// </summary>
        public static string Squash()
        {
            MeshRenderer[] renderers = Object.FindObjectsOfType<MeshRenderer>();

            int examined = 0;
            int changed = 0;
            int before = 0;
            int after = 0;
            int reused = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled) continue;

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;

                Mesh mesh = filter.sharedMesh;
                int subMeshes = Mathf.Max(1, mesh.subMeshCount);
                if (subMeshes < 2) continue;

                examined++;
                before += subMeshes;

                List<Material> order = GroupMaterials(mesh, renderer.sharedMaterials);
                if (order == null || order.Count >= subMeshes)
                {
                    after += subMeshes;
                    continue;
                }

                try
                {
                    int wasCached;
                    Mesh squashed = SquashedMesh(mesh, renderer.sharedMaterials, order, out wasCached);
                    reused += wasCached;

                    _originals.Add(new Original
                    {
                        Filter = filter,
                        Mesh = mesh,
                        Materials = renderer.sharedMaterials
                    });

                    filter.sharedMesh = squashed;
                    renderer.sharedMaterials = order.ToArray();

                    changed++;
                    after += order.Count;
                }
                catch (System.Exception ex)
                {
                    after += subMeshes;
                    RakeMultiplayerPlugin.Log?.LogWarning("[Batch] squash failed on " +
                                                          filter.name + ": " + ex.Message);
                }
            }

            RakeMultiplayerPlugin.Log?.LogInfo("[Batch] squashed " + changed + " of " + examined +
                                               " meshes: " + before + " submeshes -> " + after);

            return "{\"ok\":true,\"examined\":" + examined + ",\"changed\":" + changed +
                   ",\"before\":" + before + ",\"after\":" + after + ",\"reused\":" + reused + "}";
        }

        private static Mesh SquashedMesh(Mesh mesh, Material[] sourceMaterials, List<Material> order, out int wasCached)
        {
            wasCached = 0;

            var key = new StringBuilder();
            key.Append(mesh.GetInstanceID());
            for (int i = 0; i < order.Count; i++)
            {
                key.Append('|').Append(order[i].GetInstanceID());
            }

            string id = key.ToString();
            Mesh existing;
            if (_meshCache.TryGetValue(id, out existing) && existing != null)
            {
                wasCached = 1;
                return existing;
            }

            var squashed = new Mesh();
            squashed.name = mesh.name + "_squashed";
            squashed.vertices = mesh.vertices;

            Vector3[] normals = mesh.normals;
            if (normals != null && normals.Length > 0) squashed.normals = normals;

            Vector4[] tangents = mesh.tangents;
            if (tangents != null && tangents.Length > 0) squashed.tangents = tangents;

            Vector2[] uv = mesh.uv;
            if (uv != null && uv.Length > 0) squashed.uv = uv;

            Vector2[] uv2 = mesh.uv2;
            if (uv2 != null && uv2.Length > 0) squashed.uv2 = uv2;

            Color[] colors = mesh.colors;
            if (colors != null && colors.Length > 0) squashed.colors = colors;

            squashed.subMeshCount = order.Count;

            for (int g = 0; g < order.Count; g++)
            {
                var indices = new List<int>();
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    if (sourceMaterials[s] != order[g]) continue;
                    indices.AddRange(mesh.GetTriangles(s));
                }
                squashed.SetTriangles(indices.ToArray(), g);
            }

            squashed.RecalculateBounds();
            _meshCache[id] = squashed;
            return squashed;
        }

        // ---------------------------------------------------------------- merge

        private sealed class BatchGroup
        {
            public Material Material;
            public List<CombineInstance> Instances = new List<CombineInstance>();
            public List<MeshRenderer> Renderers = new List<MeshRenderer>();
            public int Vertices;
        }

        private static readonly List<GameObject> _batches = new List<GameObject>();
        private static readonly List<MeshRenderer> _swallowed = new List<MeshRenderer>();
        private static readonly List<LODGroup> _disabledLods = new List<LODGroup>();

        /// <summary>
        /// Combines static meshes that share a material into one mesh per material and
        /// spatial cell. Cells keep frustum culling working, so distant forest chunks are
        /// still skipped. Geometry is baked into world space.
        /// </summary>
        public static string Merge(float cellSize, bool includeLod)
        {
            if (cellSize < 0f) cellSize = 80f; // 0 = one batch for the whole level

            MeshRenderer[] renderers = Object.FindObjectsOfType<MeshRenderer>();
            var groups = new Dictionary<string, BatchGroup>();

            int skippedDynamic = 0;
            int skippedUnreadable = 0;
            int skippedLod = 0;
            int skippedLightmap = 0;
            int skippedBatched = 0;
            int skippedNull = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled) continue;
                if (renderer.isPartOfStaticBatch) { skippedBatched++; continue; }
                LODGroup lod = renderer.GetComponentInParent<LODGroup>();
                if (lod != null)
                {
                    if (!includeLod) { skippedLod++; continue; }
                    if (!_disabledLods.Contains(lod)) _disabledLods.Add(lod);
                }
                if (renderer.GetComponent<Animator>() != null || renderer.GetComponent<Rigidbody>() != null)
                {
                    skippedDynamic++;
                    continue;
                }
                if (renderer.lightmapIndex >= 0) { skippedLightmap++; continue; }

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                if (!filter.sharedMesh.isReadable) { skippedUnreadable++; continue; }

                Mesh mesh = filter.sharedMesh;
                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length < mesh.subMeshCount) { skippedNull++; continue; }

                bool broken = false;
                for (int m = 0; m < mesh.subMeshCount; m++)
                {
                    if (materials[m] == null) { broken = true; break; }
                }
                if (broken) { skippedNull++; continue; }

                Vector3 position = filter.transform.position;
                int cx = 0;
                int cz = 0;
                if (cellSize > 1f)
                {
                    cx = Mathf.FloorToInt(position.x / cellSize);
                    cz = Mathf.FloorToInt(position.z / cellSize);
                }

                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    string key = cx + ":" + cz + ":" + materials[s].GetInstanceID();
                    BatchGroup group;
                    if (!groups.TryGetValue(key, out group))
                    {
                        group = new BatchGroup { Material = materials[s] };
                        groups[key] = group;
                    }

                    var instance = new CombineInstance();
                    instance.mesh = mesh;
                    instance.subMeshIndex = s;
                    instance.transform = filter.transform.localToWorldMatrix;

                    group.Instances.Add(instance);
                    group.Vertices += mesh.vertexCount;
                    if (!group.Renderers.Contains(renderer)) group.Renderers.Add(renderer);
                }
            }

            var root = new GameObject("RakeBatches");
            int created = 0;
            int mergedMeshes = 0;
            int failed = 0;

            foreach (var pair in groups)
            {
                BatchGroup group = pair.Value;
                if (group.Instances.Count < 2) continue;

                int start = 0;
                while (start < group.Instances.Count)
                {
                    int end = start;
                    int vertices = 0;
                    while (end < group.Instances.Count)
                    {
                        int next = group.Instances[end].mesh.vertexCount;
                        if (end > start && vertices + next > 60000) break;
                        vertices += next;
                        end++;
                    }

                    var chunk = new CombineInstance[end - start];
                    for (int i = start; i < end; i++) chunk[i - start] = group.Instances[i];

                    var combined = new Mesh();
                    combined.name = "RakeBatch_" + group.Material.name;
                    try
                    {
                        combined.CombineMeshes(chunk, true, true);
                    }
                    catch (System.Exception ex)
                    {
                        failed += end - start;
                        Object.Destroy(combined);
                        RakeMultiplayerPlugin.Log?.LogWarning("[Batch] combine failed: " + ex.Message);
                        start = end;
                        continue;
                    }

                    combined.RecalculateBounds();

                    var go = new GameObject("Batch_" + group.Material.name);
                    go.transform.SetParent(root.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = combined;

                    MeshRenderer first = group.Renderers[0];
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = group.Material;
                    mr.shadowCastingMode = first.shadowCastingMode;
                    mr.receiveShadows = first.receiveShadows;

                    _batches.Add(go);
                    mergedMeshes += end - start;
                    created++;
                    start = end;
                }

                for (int i = 0; i < group.Renderers.Count; i++)
                {
                    MeshRenderer renderer = group.Renderers[i];
                    if (!_swallowed.Contains(renderer)) _swallowed.Add(renderer);
                }
                group.Renderers.Clear();
                group.Instances.Clear();
            }

            for (int i = 0; i < _swallowed.Count; i++)
            {
                if (_swallowed[i] != null) _swallowed[i].enabled = false;
            }

            for (int i = 0; i < _disabledLods.Count; i++)
            {
                if (_disabledLods[i] != null) _disabledLods[i].enabled = false;
            }

            RakeMultiplayerPlugin.Log?.LogInfo("[Batch] " + created + " batches, " + mergedMeshes +
                                               " instances, hid " + _swallowed.Count + " renderers" +
                                               " (skipped: lod " + skippedLod + ", dynamic " + skippedDynamic +
                                               ", unreadable " + skippedUnreadable +
                                               ", lightmap " + skippedLightmap +
                                               ", null " + skippedNull + ", batched " + skippedBatched + ")");

            return "{\"ok\":true,\"batches\":" + created + ",\"instances\":" + mergedMeshes +
                   ",\"hidden\":" + _swallowed.Count + ",\"failed\":" + failed +
                   ",\"skippedLod\":" + skippedLod + ",\"skippedDynamic\":" + skippedDynamic +
                   ",\"skippedUnreadable\":" + skippedUnreadable +
                   ",\"skippedLightmap\":" + skippedLightmap +
                   ",\"skippedNull\":" + skippedNull + ",\"skippedBatched\":" + skippedBatched + "}";
        }

        /// <summary>Removes the merged batches and shows the original renderers again.</summary>
        public static string Unmerge()
        {
            for (int i = 0; i < _swallowed.Count; i++)
            {
                if (_swallowed[i] != null) _swallowed[i].enabled = true;
            }
            int hidden = _swallowed.Count;
            _swallowed.Clear();

            for (int i = 0; i < _disabledLods.Count; i++)
            {
                if (_disabledLods[i] != null) _disabledLods[i].enabled = true;
            }
            _disabledLods.Clear();

            for (int i = 0; i < _batches.Count; i++)
            {
                if (_batches[i] == null) continue;
                MeshFilter filter = _batches[i].GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) Object.Destroy(filter.sharedMesh);
                Object.Destroy(_batches[i]);
            }
            int batches = _batches.Count;
            _batches.Clear();

            return "{\"ok\":true,\"batches\":" + batches + ",\"unhidden\":" + hidden + "}";
        }

        /// <summary>Puts the original meshes and material arrays back.</summary>
        public static string Restore()
        {
            for (int i = 0; i < _originals.Count; i++)
            {
                Original original = _originals[i];
                if (original.Filter == null) continue;

                original.Filter.sharedMesh = original.Mesh;
                Renderer renderer = original.Filter.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterials = original.Materials;
            }

            int count = _originals.Count;
            _originals.Clear();

            foreach (var pair in _meshCache)
            {
                if (pair.Value != null) Object.Destroy(pair.Value);
            }
            _meshCache.Clear();

            return "{\"ok\":true,\"restored\":" + count + "}";
        }
    }
}
