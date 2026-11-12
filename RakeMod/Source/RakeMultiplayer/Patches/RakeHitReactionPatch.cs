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

        private static bool Prefix(FPSController __instance, string damageType)
        {
            if (__instance == null || !IsRakeDamage(damageType)) return true;

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
                if (RakeMultiplayerPlugin.HitReaction != null && !RakeMultiplayerPlugin.HitReaction.Value) return;
                if (PlayerRagdoll.IsActive) return;

                Vector3 away = Vector3.zero;
                var rake = RakeHighlight.FindRake();
                if (rake != null)
                {
                    away = __instance.transform.position - rake.position;
                    away.y = 0f;
                }

                if (PlayerRagdoll.Trigger(away))
                {
                    RakeMultiplayerPlugin.Log?.LogInfo("[Ragdoll] Reaction triggered by " + damageType);
                }
            }
            catch (Exception ex)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Ragdoll] Hit reaction failed: " + ex.Message);
            }
        }
    }
}
