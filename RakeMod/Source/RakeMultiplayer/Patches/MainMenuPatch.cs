using HarmonyLib;
using RakeMultiplayer.UI;

namespace RakeMultiplayer.Patches
{
    /// <summary>
    /// The vanilla main menu has a "Multiplayer" tile that opens the game's own
    /// empty multiplayer panel. Redirect it to our F1 menu instead, so the tile is
    /// the single entry point to the mod's host/join/Steam lobby UI.
    /// </summary>
    [HarmonyPatch(typeof(MainMenu))]
    [HarmonyPatch("OpenMultiplayer")]
    internal static class MainMenuOpenMultiplayerPatch
    {
        static bool Prefix()
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return true;

            var menu = UnityEngine.Object.FindObjectOfType<MultiplayerMenu>();
            if (menu == null) return true;

            menu.OpenMenu();
            return false;
        }
    }
}
