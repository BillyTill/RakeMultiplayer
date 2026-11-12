using HarmonyLib;

namespace RakeMultiplayer.Patches
{
    /// <summary>
    /// Every door action the local player performs is relayed to the other peers.
    /// Replays coming from the network are marked so they are not sent back out.
    /// </summary>
    [HarmonyPatch(typeof(Door_obj))]
    [HarmonyPatch("Action")]
    internal static class DoorActionPatch
    {
        private static void Postfix(Door_obj __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            DoorSync.OnLocalAction(__instance);
        }
    }
}