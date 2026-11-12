using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Loads the baked shotgun (Rem870_3rd) and attaches it to the bone that owns it in
    /// the body prefab (the right hand). The mesh has four submeshes with three textures.
    ///
    /// File format (little-endian):
    ///   char[4] "RKRF", u32 version = 1
    ///   i32 nodeIndex
    ///   u32 vertexCount, subMeshCount, totalIndexCount
    ///   submesh[subMeshCount]: u32 indexCount
    ///   f32 vertices[v*3], f32 normals[v*3], f32 uv[v*2]
    ///   u16 indices[totalIndexCount]
    ///   submesh[subMeshCount]: u16 len, utf8 textureFileName
    /// </summary>
    internal static class RemotePlayerWeaponData
    {
        public const string FileName = "RakePlayerRifle.bin";

        private static bool _attempted;
        private static bool _loaded;
        private static string _error;

        private static int _nodeIndex = -1;
        private static Mesh _mesh;
        private static Material[] _materials;

        public static bool Loaded { get { return _loaded; } }
        public static string Error { get { return _error; } }

        public static bool EnsureLoaded()
        {
            if (_attempted) return _loaded;
            _attempted = true;

            try
            {
                string path = RemotePlayerModelData.Locate(FileName);
                if (path == null)
                {
                    _error = FileName + " not found next to the plugin";
                    return false;
                }

                string dir = Path.GetDirectoryName(path);
                var textures = new List<string>();

                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var r = new BinaryReader(stream, Encoding.UTF8))
                {
                    byte[] magic = r.ReadBytes(4);
                    if (magic.Length != 4 || magic[0] != (byte)'R' || magic[1] != (byte)'K' ||
                        magic[2] != (byte)'R' || magic[3] != (byte)'F')
                    {
                        throw new Exception("bad magic header");
                    }

                    uint version = r.ReadUInt32();
                    if (version != 1) throw new Exception("unsupported version " + version);

                    _nodeIndex = r.ReadInt32();
                    int vertexCount = (int)r.ReadUInt32();
                    int subCount = (int)r.ReadUInt32();
                    int totalIndices = (int)r.ReadUInt32();

                    if (vertexCount <= 0 || vertexCount > 200000) throw new Exception("bad vertex count");
                    if (subCount <= 0 || subCount > 64) throw new Exception("bad submesh count");

                    var subCounts = new int[subCount];
                    for (int s = 0; s < subCount; s++) subCounts[s] = (int)r.ReadUInt32();

                    var verts = new Vector3[vertexCount];
                    for (int i = 0; i < vertexCount; i++)
                        verts[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

                    var norms = new Vector3[vertexCount];
                    for (int i = 0; i < vertexCount; i++)
                        norms[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

                    var uvs = new Vector2[vertexCount];
                    for (int i = 0; i < vertexCount; i++)
                        uvs[i] = new Vector2(r.ReadSingle(), r.ReadSingle());

                    var indices = new int[totalIndices];
                    for (int i = 0; i < totalIndices; i++) indices[i] = r.ReadUInt16();

                    for (int s = 0; s < subCount; s++)
                    {
                        int len = r.ReadUInt16();
                        textures.Add(Encoding.UTF8.GetString(r.ReadBytes(len)));
                    }

                    var mesh = new Mesh();
                    mesh.name = "RakeWeaponRifle";
                    mesh.vertices = verts;
                    mesh.normals = norms;
                    mesh.uv = uvs;
                    mesh.subMeshCount = subCount;

                    int offset = 0;
                    for (int s = 0; s < subCount; s++)
                    {
                        int count = subCounts[s];
                        var tri = new int[count];
                        Array.Copy(indices, offset, tri, 0, count);
                        mesh.SetTriangles(tri, s);
                        offset += count;
                    }
                    mesh.RecalculateBounds();
                    _mesh = mesh;
                }

                Shader shader = RemotePlayerModelData.FindSkinShader();
                if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");

                _materials = new Material[textures.Count];
                for (int i = 0; i < textures.Count; i++)
                {
                    _materials[i] = MakeMaterial(shader, dir, textures[i]);
                }

                _loaded = true;
                RakeMultiplayerPlugin.Log?.LogInfo(
                    "[Weapon] Loaded " + FileName + ": verts=" + _mesh.vertexCount +
                    " subMeshes=" + _mesh.subMeshCount + " node=" + _nodeIndex +
                    " textures=" + string.Join(", ", textures.ToArray()));
                return true;
            }
            catch (Exception ex)
            {
                _error = ex.Message;
                RakeMultiplayerPlugin.Log?.LogError("[Weapon] Failed to load " + FileName + ": " + ex);
                return false;
            }
        }

        private static Material MakeMaterial(Shader shader, string dir, string textureName)
        {
            var material = new Material(shader);
            material.name = "RakeWeapon_" + textureName;
            material.color = Color.white;

            try
            {
                string texPath = string.IsNullOrEmpty(dir) ? textureName : Path.Combine(dir, textureName);
                if (File.Exists(texPath))
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.LoadImage(File.ReadAllBytes(texPath));
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.filterMode = FilterMode.Bilinear;
                    tex.anisoLevel = 2;
                    tex.name = textureName;
                    material.mainTexture = tex;
                }
                else
                {
                    RakeMultiplayerPlugin.Log?.LogWarning("[Weapon] texture missing: " + textureName);
                }
            }
            catch (Exception ex)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Weapon] texture load failed for " + textureName + ": " + ex.Message);
            }

            return material;
        }

        /// <summary>Add a static mesh renderer for the weapon to its bone node.</summary>
        public static MeshRenderer Attach(Transform[] nodes)
        {
            if (nodes == null) return null;
            if (!EnsureLoaded()) return null;
            if (_nodeIndex < 0 || _nodeIndex >= nodes.Length)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Weapon] node index out of range: " + _nodeIndex);
                return null;
            }

            var go = nodes[_nodeIndex].gameObject;
            if (go.GetComponent<MeshFilter>() != null) return go.GetComponent<MeshRenderer>(); // already attached

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = _mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = _materials;
            renderer.receiveShadows = true;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return renderer;
        }
    }
}
