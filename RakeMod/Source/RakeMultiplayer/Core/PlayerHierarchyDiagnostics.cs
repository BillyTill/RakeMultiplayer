using System;
using System.Text;
using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Dumps the local player prefab hierarchy (renderers, skinned meshes, animators
    /// and their clips) to the BepInEx log. Press F9 in game.
    /// </summary>
    public static class PlayerHierarchyDiagnostics
    {
        public static void Dump(string reason)
        {
            var log = RakeMultiplayerPlugin.Log;
            if (log == null) return;

            log.LogInfo($"[PlayerDiag] ===== HIERARCHY DUMP ({reason}) =====");

            var local = PlayerSpawner.Instance != null ? PlayerSpawner.Instance.LocalPlayer : null;
            if (local == null)
            {
                log.LogWarning("[PlayerDiag] LocalPlayer is null");
                return;
            }

            var root = local.transform;
            log.LogInfo($"[PlayerDiag] Root: {root.name} (children={root.childCount})");

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var sb = new StringBuilder();
                var smr = t.GetComponent<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    sb.Append($" SMR(mesh={(smr.sharedMesh != null ? smr.sharedMesh.name : "null")}, enabled={smr.enabled}, bones={(smr.bones != null ? smr.bones.Length : 0)}, rootBone={(smr.rootBone != null ? smr.rootBone.name : "null")})");
                }

                var mr = t.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    var mf = t.GetComponent<MeshFilter>();
                    sb.Append($" MeshRenderer(mesh={(mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "null")}, enabled={mr.enabled})");
                }

                var animator = t.GetComponent<Animator>();
                if (animator != null)
                {
                    string ctrl = animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "null";
                    string avatar = animator.avatar != null ? animator.avatar.name : "null";
                    sb.Append($" Animator(enabled={animator.enabled}, avatar={avatar}, controller={ctrl})");

                    if (animator.runtimeAnimatorController != null)
                    {
                        try
                        {
                            var clips = animator.runtimeAnimatorController.animationClips;
                            if (clips != null)
                            {
                                var names = new StringBuilder();
                                foreach (var c in clips)
                                {
                                    if (c != null) names.Append(c.name).Append("|");
                                }
                                sb.Append($" clips=[{names}]");
                            }
                        }
                        catch (Exception ex)
                        {
                            sb.Append($" clips ERR:{ex.Message}");
                        }
                    }
                }

                var anim = t.GetComponent<Animation>();
                if (anim != null && anim.GetClipCount() > 0)
                {
                    sb.Append(" LegacyAnimation(clips=");
                    foreach (AnimationState st in anim)
                    {
                        if (st != null && st.clip != null) sb.Append(st.clip.name).Append("|");
                    }
                    sb.Append(")");
                }

                if (sb.Length > 0)
                {
                    string path = GetPath(root, t);
                    log.LogInfo($"[PlayerDiag] {path}{sb}");
                }
            }

            log.LogInfo("[PlayerDiag] ===== END HIERARCHY DUMP =====");
        }

        private static string GetPath(Transform root, Transform t)
        {
            if (t == root) return t.name;
            var sb = new StringBuilder(t.name);
            var cur = t.parent;
            int guard = 0;
            while (cur != null && cur != root && guard++ < 50)
            {
                sb.Insert(0, cur.name + "/");
                cur = cur.parent;
            }
            return sb.ToString();
        }
    }
}
