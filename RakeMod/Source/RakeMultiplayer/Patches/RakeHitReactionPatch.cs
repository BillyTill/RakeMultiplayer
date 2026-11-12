using System;
using HarmonyLib;
using UnityEngine;

namespace RakeMultiplayer.Patches
{
    /// <summary>
    /// Plays the ragdoll hit reaction when the local player is hit by the Rake.
    ///
    /// FPSController.TakeDamage is only ever called for the local player, and the vanilla
    /// Rake uses the damage types "RakeKick" / "RakeUppercut" (see Enemy_AI.PlayerHount).
    /// The reaction is purely visual: it spawns a physics ragdoll of the body, throws it
    /// away from the Rake and keeps the camera attached to its head for a moment.
    /// </summary>
    [HarmonyPatch(typeof(FPSController), "TakeDamage")]
    internal static class RakeHitReactionPatch
    {
        private static void Postfix(FPSController __instance, string damageType)
        {
            try
            {
                if (__instance == null || damageType == null) return;
                if (damageType != "RakeKick" && damageType != "RakeUppercut" && damageType != "RakeAttack") return;
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
