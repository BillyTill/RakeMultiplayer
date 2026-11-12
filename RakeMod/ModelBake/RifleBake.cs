using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RakeBake
{
    /// <summary>
    /// The player body prefab carries a shotgun (Rem870_3rd) as a plain MeshRenderer
    /// parented to the right hand bone. Bake its mesh plus the texture name of every
    /// submesh into a standalone file so the mod can attach it to the same bone.
    /// </summary>
    public static class RifleBake
    {
        const string AssetDir = "Assets/RakeBake/";
        public const string OutBin = @"C:\Users\KOMPUHTER\AppData\Local\Temp\opencode\RakePlayerRifle.bin";

        // Submesh order of Rem870_3rd in the asset: body, scope, scope cap, lens.
        static readonly string[] Textures = { "rifle.png", "optika.png", "optika.png", "linza_0.png" };

        public static void Run(SkeletonData sk)
        {
            var mesh = AssetDatabase.LoadMainAssetAtPath(AssetDir + "Rem870_3rd_0.asset") as Mesh;
            if (mesh == null)
            {
                Debug.Log("[Bake] SKIP rifle: mesh not imported");
                return;
            }

            int nodeIndex = -1;
            for (int i = 0; i < sk.nodes.Length; i++)
            {
                if (sk.nodes[i].name == "Rem870_3rd") nodeIndex = i;
            }
            if (nodeIndex < 0)
            {
                Debug.Log("[Bake] SKIP rifle: node not found in skeleton");
                return;
            }

            var verts = mesh.vertices;
            var norms = mesh.normals;
            var uvs = mesh.uv;
            if (norms == null || norms.Length != verts.Length) norms = new Vector3[verts.Length];
            if (uvs == null || uvs.Length != verts.Length) uvs = new Vector2[verts.Length];

            int subCount = mesh.subMeshCount;
            var tris = new int[subCount][];
            int totalIdx = 0;
            for (int s = 0; s < subCount; s++)
            {
                tris[s] = mesh.GetTriangles(s);
                totalIdx += tris[s].Length;
            }

            Debug.Log("[Bake] rifle: verts=" + verts.Length + " subMeshes=" + subCount +
                      " indices=" + totalIdx + " node=" + nodeIndex);

            using (var fs = new FileStream(OutBin, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(fs, Encoding.UTF8))
            {
                w.Write(new byte[] { 82, 75, 82, 70 }); // RKRF
                w.Write(1u);
                w.Write(nodeIndex);
                w.Write((uint)verts.Length);
                w.Write((uint)subCount);
                w.Write((uint)totalIdx);
                for (int s = 0; s < subCount; s++) w.Write((uint)tris[s].Length);

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
                for (int s = 0; s < subCount; s++)
                {
                    for (int i = 0; i < tris[s].Length; i++) w.Write((ushort)tris[s][i]);
                }

                for (int s = 0; s < subCount; s++)
                {
                    string tex = s < Textures.Length ? Textures[s] : Textures[0];
                    var nb = Encoding.UTF8.GetBytes(tex);
                    w.Write((ushort)nb.Length);
                    w.Write(nb);
                }
            }

            Debug.Log("[Bake] WROTE " + OutBin + " size=" + (new FileInfo(OutBin).Length / 1024) + " KB");
        }
    }
}
