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
            // Never let the vanilla Game over run in a connected session: it loads the
            // main menu after 5 s and kicks the client out of the match. BeginLocalDeath
            // is idempotent; if it cannot handle this hit, simply keep the body as is.
            bool handled = PlayerDeathSystem.BeginLocalDeath(__instance);

            if (!handled && __instance != null && __instance.Health < 0)
            {
                // The death flow needs PlayerSpawner.LocalPlayer, which does not exist yet
                // during a level load. Do not leave a negative-health body with the vanilla
                // "Game over" suppressed: clamp it so the next hit can kill it properly.
                __instance.Health = 1;
            }

            return false;
        }
    }
}
