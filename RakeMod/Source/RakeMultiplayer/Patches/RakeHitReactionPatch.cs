using System;
using HarmonyLib;
using UnityEngine;

namespace RakeMultiplayer.Patches
{
    /// <summary>
    /// Rake hit reaction for the local player.
    ///
    /// Prefix: the mod's server-authoritative damage and the vanilla Enemy_AI hunt sequence
    /// can both report the same hit, so duplicates inside a short window are dropped and
    /// only one hit lands.
    ///
    /// Postfix: spawns a physics ragdoll of the body, throws it away from the Rake and keeps
    /// the camera attached to its head for a moment, then restores first person.
    /// </summary>
    [HarmonyPatch(typeof(FPSController), "TakeDamage", new Type[] { typeof(int), typeof(string) })]
    internal static class RakeHitReactionPatch
    {
        private const float DedupWindow = 0.35f;
        private static float _lastRakeHit = -100f;

        private static bool IsRakeDamage(string damageType)
        {
            return damageType == "RakeKick" || damageType == "RakeUppercut" || damageType == "RakeAttack";
        }

        /// <summary>
        /// Remote clones also carry an FPSController (disabled). A hit on a clone must
        /// never ragdoll the local player.
        /// </summary>
        private static bool IsLocalPlayer(FPSController fps)
        {
            if (fps == null) return false;
            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;
            return local != null && local.gameObject == fps.gameObject;
        }

        private static bool Prefix(FPSController __instance, string damageType)
        {
            if (__instance == null || !IsRakeDamage(damageType)) return true;
            if (!IsLocalPlayer(__instance)) return true;

            // Drop a duplicate hit from the other damage source
            if (Time.time - _lastRakeHit < DedupWindow) return false;
            _lastRakeHit = Time.time;
            return true;
        }

        private static void Postfix(FPSController __instance, string damageType)
        {
            try
            {
                if (__instance == null || !IsRakeDamage(damageType)) return;
                if (!IsLocalPlayer(__instance)) return;
                if (RakeMultiplayerPlugin.HitReaction != null && !RakeMultiplayerPlugin.HitReaction.Value) return;
                if (PlayerRagdoll.IsActive) return;

                // Direction only: the body is spawned at the player, this just decides
                // which way the impulse pushes it (away from the attacker).
                Vector3 away = Vector3.zero;
                var rake = RakeHighlight.FindRake();
                if (rake != null)
                {
                    Vector3 delta = __instance.transform.position - rake.position;
                    delta.y = 0f;

                    // If the Rake is almost directly below us the horizontal part is
                    // numerical noise, so fall back to the player's backward direction.
                    if (delta.sqrMagnitude > 0.25f) away = delta;
                }

                if (PlayerRagdoll.Trigger(away))
                {
                    RakeMultiplayerPlugin.Log?.LogInfo("[Ragdoll] Reaction triggered by " + damageType);

                    // Everyone else must see the body fly too, whichever damage path
                    // produced this hit.
                    PlayerRagdoll.ShareLocal(away);
                }
            }
            catch (Exception ex)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Ragdoll] Hit reaction failed: " + ex.Message);
            }
        }
    }
}
