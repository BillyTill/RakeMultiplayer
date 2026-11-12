using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RakeBake
{
    [Serializable]
    public class NodeData
    {
        public string name;
        public int parent;
        public float[] pos;
        public float[] rot;
        public float[] scale;
        public bool bone;
    }

    [Serializable]
    public class SkeletonData
    {
        public int root_index;
        public NodeData[] nodes;
        public int[] boneNodes;
        public int smrRootBone;
    }

    public static class BakePlayer
    {
        const string AssetDir = "Assets/RakeBake/";
        const float Fps = 30f;
        const string SkeletonJson = @"C:\Users\KOMPUHTER\AppData\Local\Temp\opencode\skeleton.json";
        const string OutBin = @"C:\Users\KOMPUHTER\AppData\Local\Temp\opencode\RakePlayerModel.bin";
        const string ReportPath = @"C:\Users\KOMPUHTER\AppData\Local\Temp\opencode\bake_report.txt";

        // asset file name -> logical name written into the bin
        static readonly string[,] Clips =
        {
            { "Idle_10",        "Idle" },
            { "Walk_2",         "Walk" },
            { "Run_0",          "Run" },
            { "RakeAttack_TPS", "Attack" },
            { "Walk Left",      "WalkLeft" },
            { "Walk Right",     "WalkRight" },
            { "Take 001_15",    "RifleHold" },
        };

        static StringBuilder _log;

        static void L(string s)
        {
            _log.AppendLine(s);
            Debug.Log("[Bake] " + s);
        }

        public static void Run()
        {
            _log = new StringBuilder();
            try
            {
                Bake();
            }
            catch (Exception ex)
            {
                L("FATAL " + ex);
            }
            File.WriteAllText(ReportPath, _log.ToString());
            Debug.Log(_log.ToString());
        }

        static void Bake()
        {
            L("unity " + Application.unityVersion);

            // ---------- load assets ----------
            var mesh = AssetDatabase.LoadMainAssetAtPath(AssetDir + "casual12_m_highpoly.asset") as Mesh;
            var avatar = AssetDatabase.LoadMainAssetAtPath(AssetDir + "Main_charAvatar.asset") as Avatar;
            if (mesh == null) { L("FAIL mesh not imported"); return; }
            if (avatar == null) { L("FAIL avatar not imported"); return; }

            L("mesh: verts=" + mesh.vertexCount + " bindposes=" + mesh.bindposes.Length +
              " submeshes=" + mesh.subMeshCount +
              " weights=" + (mesh.boneWeights != null ? mesh.boneWeights.Length : 0) +
              " uv=" + (mesh.uv != null ? mesh.uv.Length : 0) +
              " normals=" + (mesh.normals != null ? mesh.normals.Length : 0));
            L("avatar: valid=" + avatar.isValid + " human=" + avatar.isHuman);

            // ---------- skeleton ----------
            if (!File.Exists(SkeletonJson)) { L("FAIL skeleton json missing"); return; }
            var sk = JsonUtility.FromJson<SkeletonData>(File.ReadAllText(SkeletonJson));
            L("skeleton nodes=" + sk.nodes.Length + " bones=" + sk.boneNodes.Length +
              " smrRootBone=" + sk.smrRootBone);

            // ---------- build hierarchy ----------
            var trs = new Transform[sk.nodes.Length];
            GameObject root = null;
            for (int i = 0; i < sk.nodes.Length; i++)
            {
                var n = sk.nodes[i];
                GameObject go;
                if (i == 0 || n.parent < 0)
                {
                    go = i == 0 ? new GameObject(n.name) : new GameObject(n.name);
                    if (root == null) root = go;
                }
                else
                {
                    go = new GameObject(n.name);
                    go.transform.SetParent(trs[n.parent], false);
                }
                trs[i] = go.transform;
                go.transform.localPosition = V3(n.pos);
                go.transform.localRotation = Q(n.rot);
                go.transform.localScale = V3(n.scale);
            }

            int smrNode = -1;
            for (int i = 0; i < sk.nodes.Length; i++)
                if (sk.nodes[i].name != null && sk.nodes[i].name.StartsWith("casual12"))
                    smrNode = i;
            if (smrNode < 0) { L("FAIL smr node not found"); return; }
            L("smr node = " + smrNode + " (" + sk.nodes[smrNode].name + ")");

            var boneTrs = new Transform[sk.boneNodes.Length];
            for (int i = 0; i < sk.boneNodes.Length; i++) boneTrs[i] = trs[sk.boneNodes[i]];

            var smr = trs[smrNode].gameObject.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = boneTrs;
            smr.rootBone = trs[sk.smrRootBone];
            smr.updateWhenOffscreen = true;
            smr.localBounds = new Bounds(Vector3.zero, Vector3.one * 4f);

            var animator = root.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // animated nodes: root + bones
            var animNodes = new List<int>();
            animNodes.Add(0);
            foreach (var b in sk.boneNodes) if (b != 0 && !animNodes.Contains(b)) animNodes.Add(b);
            L("animNodes=" + animNodes.Count);

            // ---------- bake clips ----------
            var clipData = new List<BakedClip>();
            AnimationMode.StartAnimationMode();
            try
            {
                for (int c = 0; c < Clips.GetLength(0); c++)
                {
                    string assetName = Clips[c, 0];
                    string logical = Clips[c, 1];
                    string path = AssetDir + assetName + ".anim";
                    var clip = AssetDatabase.LoadMainAssetAtPath(path) as AnimationClip;
                    if (clip == null) { L("SKIP clip missing: " + path); continue; }

                    int frames = Mathf.Max(2, Mathf.RoundToInt(clip.length * Fps));
                    var bc = new BakedClip { name = logical, frameCount = frames, fps = Fps };
                    bc.pos = new Vector3[frames * animNodes.Count];
                    bc.rot = new Quaternion[frames * animNodes.Count];

                    bool sampled = false;
                    try
                    {
                        for (int f = 0; f < frames; f++)
                        {
                            float t = f / Fps;
                            if (t > clip.length) t = clip.length;
                            AnimationMode.BeginSampling();
                            AnimationMode.SampleAnimationClip(root, clip, t);
                            AnimationMode.EndSampling();

                            for (int k = 0; k < animNodes.Count; k++)
                            {
                                var tr = trs[animNodes[k]];
                                bc.pos[f * animNodes.Count + k] = tr.localPosition;
                                bc.rot[f * animNodes.Count + k] = tr.localRotation;
                            }
                        }
                        sampled = true;
                    }
                    catch (Exception ex)
                    {
                        L("SAMPLE ERROR " + logical + ": " + ex.Message);
                    }

                    if (!sampled) continue;

                    // ---- strip root motion ----
                    // The humanoid clips carry baked-in forward translation on the pelvis
                    // (Walk 1.56 m, Run 3.34 m, WalkLeft/Right ~3 m per cycle). Played back
                    // as a pure pose that makes the body slide away from its owner, so remove
                    // the linear drift and keep only the natural bob/sway.
                    for (int k = 0; k < animNodes.Count; k++)
                    {
                        int firstIdx = k;
                        int lastIdx = (frames - 1) * animNodes.Count + k;
                        Vector3 drift = bc.pos[lastIdx] - bc.pos[firstIdx];
                        if (drift.sqrMagnitude < 1e-10f) continue;

                        for (int f = 0; f < frames; f++)
                        {
                            float u = frames > 1 ? (float)f / (frames - 1) : 0f;
                            int idx = f * animNodes.Count + k;
                            bc.pos[idx] = bc.pos[idx] - drift * u;
                        }

                        if (drift.magnitude > 0.05f)
                        {
                            L("  root motion removed: node " + animNodes[k] + " (" +
                              trs[animNodes[k]].name + ") drift=" + drift.ToString("F3"));
                        }
                    }

                    // Re-base every animated node so its mean position equals the rest
                    // position. Detrending removes the drift but leaves the offset that
                    // the root motion accumulated at frame 0, which pushed Walk ~1.3 m
                    // away from the owner. A no-op for nodes whose position is constant.
                    for (int k = 0; k < animNodes.Count; k++)
                    {
                        int nodeIndex = animNodes[k];
                        var restNode = sk.nodes[nodeIndex];
                        Vector3 restPos = new Vector3(restNode.pos[0], restNode.pos[1], restNode.pos[2]);

                        Vector3 sum = Vector3.zero;
                        for (int f = 0; f < frames; f++) sum += bc.pos[f * animNodes.Count + k];
                        Vector3 delta = sum / frames - restPos;
                        if (delta.sqrMagnitude < 1e-8f) continue;

                        for (int f = 0; f < frames; f++)
                        {
                            int idx = f * animNodes.Count + k;
                            bc.pos[idx] = bc.pos[idx] - delta;
                        }

                        if (delta.magnitude > 0.02f)
                        {
                            L("  re-based: node " + nodeIndex + " (" + trs[nodeIndex].name +
                              ") by " + delta.ToString("F3"));
                        }
                    }


                    // sanity: how much do bones move compared to rest pose
                    float maxDelta = 0f;
                    for (int k = 0; k < animNodes.Count; k++)
                    {
                        var rest = trs[animNodes[k]].localRotation;
                        for (int f = 0; f < frames; f += Mathf.Max(1, frames / 10))
                        {
                            float d = Quaternion.Angle(rest, bc.rot[f * animNodes.Count + k]);
                            if (d > maxDelta) maxDelta = d;
                        }
                    }
                    L("clip " + logical + " (" + assetName + ") len=" + clip.length.ToString("F2") +
                      "s humanMotion=" + clip.humanMotion + " frames=" + frames +
                      " maxBoneDelta=" + maxDelta.ToString("F1") + "deg");

                    clipData.Add(bc);
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }

            // ---------- write ----------
            WriteBin(mesh, sk, smrNode, animNodes, clipData);

            RifleBake.Run(sk);
        }

        class BakedClip
        {
            public string name;
            public int frameCount;
            public float fps;
            public Vector3[] pos;
            public Quaternion[] rot;
        }

        static void WriteBin(Mesh mesh, SkeletonData sk, int smrNode, List<int> animNodes, List<BakedClip> clips)
        {
            var verts = mesh.vertices;
            var norms = mesh.normals;
            var uvs = mesh.uv;
            var weights = mesh.boneWeights;
            var bindposes = mesh.bindposes;
            var tris = mesh.triangles;

            if (norms == null || norms.Length != verts.Length) norms = new Vector3[verts.Length];
            if (uvs == null || uvs.Length != verts.Length) uvs = new Vector2[verts.Length];
            if (weights == null || weights.Length != verts.Length)
            {
                L("FAIL mesh has no bone weights");
                return;
            }
            if (tris.Length > 65535 * 3)
            {
                L("FAIL too many indices for uint16: " + tris.Length);
                return;
            }

            using (var fs = new FileStream(OutBin, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(fs, Encoding.UTF8))
            {
                w.Write(new[] { (byte)'R', (byte)'K', (byte)'P', (byte)'L' });
                w.Write(1u); // version
                w.Write((uint)sk.nodes.Length);
                w.Write((uint)sk.boneNodes.Length);
                w.Write((uint)animNodes.Count);
                w.Write((uint)clips.Count);
                w.Write((uint)verts.Length);
                w.Write((uint)tris.Length);
                w.Write(smrNode);
                w.Write(sk.smrRootBone);

                for (int i = 0; i < sk.nodes.Length; i++)
                {
                    var n = sk.nodes[i];
                    var bytes = Encoding.UTF8.GetBytes(n.name ?? "");
                    w.Write((ushort)bytes.Length);
                    w.Write(bytes);
                    w.Write(n.parent);
                    w.Write(n.pos[0]); w.Write(n.pos[1]); w.Write(n.pos[2]);
                    w.Write(n.rot[0]); w.Write(n.rot[1]); w.Write(n.rot[2]); w.Write(n.rot[3]);
                    w.Write(n.scale[0]); w.Write(n.scale[1]); w.Write(n.scale[2]);
                    w.Write((byte)(n.bone ? 1 : 0));
                }

                foreach (var b in sk.boneNodes) w.Write(b);
                foreach (var a in animNodes) w.Write(a);

                for (int i = 0; i < verts.Length; i++)
                {
                    w.Write(verts[i].x); w.Write(verts[i].y); w.Write(verts[i].z);
                }
                for (int i = 0; i < verts.Length; i++)
                {
                    w.Write(norms[i].x); w.Write(norms[i].y); w.Write(norms[i].z);
                }
                for (int i = 0; i < verts.Length; i++)
                {
                    w.Write(uvs[i].x); w.Write(uvs[i].y);
                }
                for (int i = 0; i < verts.Length; i++)
                {
                    var bw = weights[i];
                    w.Write(bw.weight0); w.Write(bw.weight1); w.Write(bw.weight2); w.Write(bw.weight3);
                }
                for (int i = 0; i < verts.Length; i++)
                {
                    var bw = weights[i];
                    w.Write((byte)bw.boneIndex0); w.Write((byte)bw.boneIndex1);
                    w.Write((byte)bw.boneIndex2); w.Write((byte)bw.boneIndex3);
                }
                foreach (var m in bindposes)
                {
                    for (int r = 0; r < 4; r++)
                        for (int col = 0; col < 4; col++)
                            w.Write(m[r, col]);
                }
                foreach (var t in tris) w.Write((ushort)t);

                foreach (var c in clips)
                {
                    var nb = Encoding.UTF8.GetBytes(c.name);
                    w.Write((ushort)nb.Length);
                    w.Write(nb);
                    w.Write(c.fps);
                    w.Write((uint)c.frameCount);
                    for (int f = 0; f < c.frameCount; f++)
                    {
                        for (int k = 0; k < animNodes.Count; k++)
                        {
                            int idx = f * animNodes.Count + k;
                            var p = c.pos[idx];
                            var q = c.rot[idx];
                            w.Write(p.x); w.Write(p.y); w.Write(p.z);
                            w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w);
                        }
                    }
                }
            }

            long size = new FileInfo(OutBin).Length;
            L("WROTE " + OutBin + " size=" + (size / 1024) + " KB verts=" + verts.Length +
              " tris=" + tris.Length + " clips=" + clips.Count);
        }

        static Vector3 V3(float[] a)
        {
            if (a == null || a.Length < 3) return Vector3.zero;
            return new Vector3(a[0], a[1], a[2]);
        }

        static Quaternion Q(float[] a)
        {
            if (a == null || a.Length < 4) return Quaternion.identity;
            return new Quaternion(a[0], a[1], a[2], a[3]);
        }
    }
}
