using HarmonyLib;

namespace RakeMultiplayer
{
    /// <summary>
    /// Vanilla save/load is a single-player system: it hides every item/trap it does not
    /// find in the local PlayerPrefs and can spawn traps from an old save. In a multiplayer
    /// session every peer has its own prefs, so loading one produced different worlds
    /// (on any level index other than 1, where the vanilla guard applies).
    /// </summary>
    [HarmonyPatch(typeof(SaveGameSystem), "LoadGame")]
    internal static class SaveGameLoadPatch
    {
        private static bool Prefix()
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return true;
            if (!RakeMultiplayerPlugin.isConnected) return true;

            RakeMultiplayerPlugin.Log?.LogInfo("[Save] LoadGame skipped in multiplayer");
            return false;
        }
    }

    [HarmonyPatch(typeof(SaveGameSystem), "SaveGame")]
    internal static class SaveGameSavePatch
    {
        private static bool Prefix()
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return true;
            if (!RakeMultiplayerPlugin.isConnected) return true;

            // Do not let vanilla persistence overwrite PlayerPrefs with mod state.
            return false;
        }
    }
}
