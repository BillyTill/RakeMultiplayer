using HarmonyLib;
using UnityEngine;

namespace RakeMultiplayer.Patches
{
    /// <summary>
    /// The vanilla Rake treats every gunshot as danger and runs to a random escape
    /// point up to 300 m away, which fights the mod's stalk state machine. While the
    /// multiplayer AI owns the Rake, route the notification into the mod instead:
    /// it can start a lurk cycle or turn towards the noise.
    /// </summary>
    [HarmonyPatch(typeof(Enemy_AI), "Event_PlayerFire")]
    internal static class RakeNoisePatch
    {
        private static bool Prefix(Enemy_AI __instance, Component sender)
        {
            if (__instance == null || sender == null) return true;

            var ai = __instance.GetComponent<NetworkedRakeAI>();
            if (ai == null || !ai.IsAiActive) return true;

            ai.NotifyNoise(sender.transform.position);
            return false; // do not run the vanilla SetDangerState/Escape
        }
    }
}
