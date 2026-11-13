using HarmonyLib;

namespace RakeMultiplayer.Patches
{
    /// <summary>
    /// The vanilla death screen waits five seconds and then loads the main menu
    /// (Application.LoadLevel(0)), which would kick the whole match out. In multiplayer
    /// the Rake death system takes over instead: permanent ragdoll, corpse, loot, respawn.
    /// </summary>
    [HarmonyPatch(typeof(FPSController), "KillPlayer")]
    internal static class PlayerDeathPatch
    {
        private static bool Prefix(FPSController __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return true;
            if (!RakeMultiplayerPlugin.isConnected) return true; // single-player keeps vanilla

            // In multiplayer the vanilla "Game over -> LoadLevel(0)" must never run:
            // it would kick the whole match to the main menu. BeginLocalDeath is
            // idempotent, so a second lethal pellet cannot restart anything either.
            PlayerDeathSystem.BeginLocalDeath(__instance);
            return false;
        }
    }
}
