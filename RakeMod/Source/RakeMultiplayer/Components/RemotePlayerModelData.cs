using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>One baked animation clip (bone local poses sampled at a fixed frame rate).</summary>
    internal sealed class RemotePlayerModelClip
    {
        public string Name;
        public float Fps;
        public int FrameCount;
        public Vector3[] Positions;   // [frame * animCount + k]
        public Quaternion[] Rotations;

        public float Length
        {
            get { return FrameCount > 0 && Fps > 0f ? FrameCount / Fps : 0f; }
        }
    }

    /// <summary>One transform of the baked skeleton hierarchy.</summary>
    internal sealed class RemotePlayerModelNode
    {
        public string Name;
        public int Parent;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public bool IsBone;
    }

    /// <summary>
    /// Loads the player body model (mesh + skeleton + sampled animations) that was baked
    /// offline with Unity from the retail game assets.
    ///
    /// The file format is little-endian and self describing, see <see cref="Parse"/>.
    /// </summary>
    internal static class RemotePlayerModelData
    {
        public const string FileName = "RakePlayerModel.bin";
        public const string TextureName = "RakePlayerSkin.png";

        /// <summary>
        /// Canonical placement of the model root: feet on y = 0, centred on x/z, facing +Z,
        /// 1.76 m tall. The baked root transform contains scene leftovers, so the loader
        /// always overwrites it with these values.
        /// </summary>
        public static readonly Vector3 RootPosition = new Vector3(0.0313f, 0.8673f, -0.02741f);
        public static readonly Quaternion RootRotation = new Quaternion(-0.709104f, 0.003993f, 0.004015f, 0.705081f);
        public static readonly Vector3 RootScale = new Vector3(0.822672f, 0.822672f, 0.822672f);

        private static bool _attempted;
        private static bool _loaded;
        private static string _error;

        private static RemotePlayerModelNode[] _nodes;
        private static int[] _boneNodes;
        private static int[] _animNodes;
        private static int _smrNode = -1;
        private static int _smrRootBone = -1;
        private static Mesh _mesh;
        private static Material _material;
        private static readonly Dictionary<string, RemotePlayerModelClip> Clips =
            new Dictionary<string, RemotePlayerModelClip>(StringComparer.OrdinalIgnoreCase);

        public static bool Loaded { get { return _loaded; } }
        public static string Error { get { return _error; } }
        public static RemotePlayerModelNode[] Nodes { get { return _nodes; } }
        public static int[] BoneNodes { get { return _boneNodes; } }
        public static int[] AnimNodes { get { return _animNodes; } }
        public static int SmrNode { get { return _smrNode; } }
        public static int SmrRootBone { get { return _smrRootBone; } }
        public static Mesh Mesh { get { return _mesh; } }
        public static Material Material { get { return _material; } }

        public static RemotePlayerModelClip GetClip(string name)
        {
            if (name == null) return null;
            RemotePlayerModelClip clip;
            return Clips.TryGetValue(name, out clip) ? clip : null;
        }

        public static string ClipNames()
        {
            var sb = new StringBuilder();
            foreach (var kv in Clips)
            {
                if (sb.Length > 0) sb.Append('|');
                sb.Append(kv.Key);
            }
            return sb.ToString();
        }

        public static bool EnsureLoaded()
        {
            if (_attempted) return _loaded;
            _attempted = true;

            try
            {
                string path = Locate(FileName);
                if (path == null)
                {
                    _error = FileName + " not found next to the plugin";
                    RakeMultiplayerPlugin.Log?.LogWarning("[Model] " + _error + " - falling back to the primitive proxy");
                    return false;
                }

                byte[] bytes = File.ReadAllBytes(path);
                Parse(bytes);
                BuildMesh();
                BuildMaterial(path);

                _loaded = true;
                RakeMultiplayerPlugin.Log?.LogInfo(
                    "[Model] Loaded " + FileName + " (" + (bytes.Length / 1024) + " KB): nodes=" + _nodes.Length +
                    " bones=" + _boneNodes.Length + " animNodes=" + _animNodes.Length +
                    " verts=" + _mesh.vertexCount + " clips=" + ClipNames());
                return true;
            }
            catch (Exception ex)
            {
                _error = ex.Message;
                RakeMultiplayerPlugin.Log?.LogError("[Model] Failed to load " + FileName + ": " + ex);
                return false;
            }
        }

        private static string Locate(string fileName)
        {
            var candidates = new List<string>();

            try
            {
                string asm = typeof(RemotePlayerModelData).Assembly.Location;
                if (!string.IsNullOrEmpty(asm))
                {
                    string dir = Path.GetDirectoryName(asm);
                    if (!string.IsNullOrEmpty(dir)) candidates.Add(Path.Combine(dir, fileName));
                }
            }
            catch
            {
                // Assembly.Location can throw on some Mono runtimes
            }

            try
            {
                string pluginPath = BepInEx.Paths.PluginPath;
                if (!string.IsNullOrEmpty(pluginPath))
                {
                    candidates.Add(Path.Combine(pluginPath, fileName));
                    candidates.Add(Path.Combine(Path.Combine(pluginPath, "RakeMultiplayer"), fileName));
                }
            }
            catch
            {
                // ignore
            }

            foreach (var c in candidates)
            {
                if (File.Exists(c)) return c;
            }
            return null;
        }

        private static void Parse(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes, false))
            using (var r = new BinaryReader(stream, Encoding.UTF8))
            {
                byte[] magic = r.ReadBytes(4);
                if (magic.Length != 4 || magic[0] != (byte)'R' || magic[1] != (byte)'K' ||
                    magic[2] != (byte)'P' || magic[3] != (byte)'L')
                {
                    throw new Exception("bad magic header");
                }

                uint version = r.ReadUInt32();
                if (version != 1) throw new Exception("unsupported version " + version);

                int nodeCount = (int)r.ReadUInt32();
                int boneCount = (int)r.ReadUInt32();
                int animCount = (int)r.ReadUInt32();
                int clipCount = (int)r.ReadUInt32();
                int vertexCount = (int)r.ReadUInt32();
                int indexCount = (int)r.ReadUInt32();
                _smrNode = r.ReadInt32();
                _smrRootBone = r.ReadInt32();

                if (nodeCount <= 0 || nodeCount > 4096) throw new Exception("bad node count " + nodeCount);
                if (boneCount <= 0 || boneCount > 512) throw new Exception("bad bone count " + boneCount);
                if (vertexCount <= 0 || vertexCount > 200000) throw new Exception("bad vertex count " + vertexCount);

                // ---- skeleton ----
                _nodes = new RemotePlayerModelNode[nodeCount];
                for (int i = 0; i < nodeCount; i++)
                {
                    int nameLen = r.ReadUInt16();
                    string name = Encoding.UTF8.GetString(r.ReadBytes(nameLen));
                    _nodes[i] = new RemotePlayerModelNode
                    {
                        Name = name,
                        Parent = r.ReadInt32(),
                        Position = ReadVector3(r),
                        Rotation = ReadQuaternion(r),
                        Scale = ReadVector3(r),
                        IsBone = r.ReadByte() != 0,
                    };
                }

                _boneNodes = new int[boneCount];
                for (int i = 0; i < boneCount; i++) _boneNodes[i] = r.ReadInt32();

                _animNodes = new int[animCount];
                for (int i = 0; i < animCount; i++) _animNodes[i] = r.ReadInt32();

                // ---- mesh ----
                var vertices = new Vector3[vertexCount];
                for (int i = 0; i < vertexCount; i++) vertices[i] = ReadVector3(r);

                var normals = new Vector3[vertexCount];
                for (int i = 0; i < vertexCount; i++) normals[i] = ReadVector3(r);

                var uvs = new Vector2[vertexCount];
                for (int i = 0; i < vertexCount; i++) uvs[i] = new Vector2(r.ReadSingle(), r.ReadSingle());

                var weights = new BoneWeight[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                {
                    weights[i].weight0 = r.ReadSingle();
                    weights[i].weight1 = r.ReadSingle();
                    weights[i].weight2 = r.ReadSingle();
                    weights[i].weight3 = r.ReadSingle();
                }
                for (int i = 0; i < vertexCount; i++)
                {
                    weights[i].boneIndex0 = r.ReadByte();
                    weights[i].boneIndex1 = r.ReadByte();
                    weights[i].boneIndex2 = r.ReadByte();
                    weights[i].boneIndex3 = r.ReadByte();
                }

                var bindposes = new Matrix4x4[boneCount];
                for (int b = 0; b < boneCount; b++)
                {
                    var m = new Matrix4x4();
                    for (int row = 0; row < 4; row++)
                        for (int col = 0; col < 4; col++)
                            m[row, col] = r.ReadSingle();
                    bindposes[b] = m;
                }

                var triangles = new int[indexCount];
                for (int i = 0; i < indexCount; i++) triangles[i] = r.ReadUInt16();

                // ---- clips ----
                Clips.Clear();
                for (int c = 0; c < clipCount; c++)
                {
                    int nameLen = r.ReadUInt16();
                    string name = Encoding.UTF8.GetString(r.ReadBytes(nameLen));
                    var clip = new RemotePlayerModelClip
                    {
                        Name = name,
                        Fps = r.ReadSingle(),
                        FrameCount = (int)r.ReadUInt32(),
                    };

                    int total = clip.FrameCount * animCount;
                    clip.Positions = new Vector3[total];
                    clip.Rotations = new Quaternion[total];
                    for (int f = 0; f < clip.FrameCount; f++)
                    {
                        for (int k = 0; k < animCount; k++)
                        {
                            int idx = f * animCount + k;
                            clip.Positions[idx] = ReadVector3(r);
                            clip.Rotations[idx] = ReadQuaternion(r);
                        }
                    }

                    Clips[name] = clip;
                }

                // Keep the parsed arrays for the mesh builder
                _pendingVertices = vertices;
                _pendingNormals = normals;
                _pendingUvs = uvs;
                _pendingWeights = weights;
                _pendingBindposes = bindposes;
                _pendingTriangles = triangles;
            }
        }

        private static Vector3[] _pendingVertices;
        private static Vector3[] _pendingNormals;
        private static Vector2[] _pendingUvs;
        private static BoneWeight[] _pendingWeights;
        private static Matrix4x4[] _pendingBindposes;
        private static int[] _pendingTriangles;

        private static void BuildMesh()
        {
            var mesh = new Mesh();
            mesh.name = "RakeRemotePlayerBody";
            mesh.vertices = _pendingVertices;
            mesh.normals = _pendingNormals;
            mesh.uv = _pendingUvs;
            mesh.boneWeights = _pendingWeights;
            mesh.bindposes = _pendingBindposes;
            mesh.triangles = _pendingTriangles;
            mesh.RecalculateBounds();
            _mesh = mesh;

            _pendingVertices = null;
            _pendingNormals = null;
            _pendingUvs = null;
            _pendingWeights = null;
            _pendingBindposes = null;
            _pendingTriangles = null;
        }

        /// <summary>
        /// Pick a shader that is guaranteed to be present in the shipped build and that
        /// supports skinning. Falls back to the shader used by an existing skinned
        /// renderer (the local player hands), which is always available.
        /// </summary>
        private static Shader FindSkinShader()
        {
            string[] candidates =
            {
                "Standard",
                "Legacy Shaders/Diffuse",
                "Diffuse",
                "Mobile/Diffuse",
                "Legacy Shaders/VertexLit",
            };

            foreach (var name in candidates)
            {
                var found = Shader.Find(name);
                if (found != null) return found;
            }

            // Borrow from a skinned renderer that already exists in the scene.
            var skinned = Resources.FindObjectsOfTypeAll<SkinnedMeshRenderer>();
            for (int i = 0; i < skinned.Length; i++)
            {
                var smr = skinned[i];
                if (smr == null || smr.sharedMaterial == null) continue;
                if (smr.sharedMaterial.shader != null) return smr.sharedMaterial.shader;
            }

            var renderers = Resources.FindObjectsOfTypeAll<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r.sharedMaterial == null) continue;
                if (r.sharedMaterial.shader != null) return r.sharedMaterial.shader;
            }

            return null;
        }

        private static void BuildMaterial(string binPath)
        {
            Shader shader = FindSkinShader();
            if (shader == null)
            {
                RakeMultiplayerPlugin.Log?.LogError("[Model] No usable shader found");
                shader = Shader.Find("Hidden/InternalErrorShader");
            }

            var material = new Material(shader);
            material.name = "RakeRemotePlayerSkin";
            material.color = new Color(0.78f, 0.63f, 0.52f);

            try
            {
                string dir = Path.GetDirectoryName(binPath);
                string texPath = dir != null ? Path.Combine(dir, TextureName) : null;
                if (texPath != null && File.Exists(texPath))
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.LoadImage(File.ReadAllBytes(texPath));
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.filterMode = FilterMode.Bilinear;
                    tex.anisoLevel = 2;
                    tex.name = "RakeRemotePlayerSkin";
                    material.mainTexture = tex;
                    RakeMultiplayerPlugin.Log?.LogInfo("[Model] Skin texture " + tex.width + "x" + tex.height);
                }
                else
                {
                    RakeMultiplayerPlugin.Log?.LogWarning("[Model] " + TextureName + " not found, using flat colour");
                }
            }
            catch (Exception ex)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Model] Texture load failed: " + ex.Message);
            }

            _material = material;
        }

        private static Vector3 ReadVector3(BinaryReader r)
        {
            return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }

        private static Quaternion ReadQuaternion(BinaryReader r)
        {
            return new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }
    }
}
