using HarmonyLib;

namespace RakeMultiplayer
{
    /// <summary>
    /// While the debug console or the F1 menu is open, the vanilla input readers must not
    /// run. They all read Input directly in Update and do not know about the mod, so typing
    /// "model" used to switch weapons, "e" picked items up, "i" opened the inventory and
    /// Escape opened the pause menu behind the console (Time.timeScale = 0).
    /// </summary>
    internal static class InputGate
    {
        public static bool Blocked
        {
            get { return DebugConsole.ConsoleOpen || UI.MultiplayerMenu.IsOpen; }
        }
    }

    [HarmonyPatch(typeof(FPSController), "Update")]
    internal static class GateFpsControllerUpdate
    {
        private static bool Prefix() { return !InputGate.Blocked; }
    }

    [HarmonyPatch(typeof(WeaponManager), "Update")]
    internal static class GateWeaponManagerUpdate
    {
        private static bool Prefix() { return !InputGate.Blocked; }
    }

    [HarmonyPatch(typeof(FPSCamera), "Update")]
    internal static class GateFpsCameraUpdate
    {
        private static bool Prefix() { return !InputGate.Blocked; }
    }

    [HarmonyPatch(typeof(GamePauseMenu), "Update")]
    internal static class GateGamePauseMenuUpdate
    {
        private static bool Prefix() { return !InputGate.Blocked; }
    }

    [HarmonyPatch(typeof(PauseManager), "Update")]
    internal static class GatePauseManagerUpdate
    {
        private static bool Prefix() { return !InputGate.Blocked; }
    }

    [HarmonyPatch(typeof(Rifle_weapon), "Update")]
    internal static class GateRifleWeaponUpdate
    {
        private static bool Prefix() { return !InputGate.Blocked; }
    }

    [HarmonyPatch(typeof(SniperRifle_weapon), "Update")]
    internal static class GateSniperWeaponUpdate
    {
        private static bool Prefix() { return !InputGate.Blocked; }
    }

    [HarmonyPatch(typeof(Inventory), "Update")]
    internal static class GateInventoryUpdate
    {
        private static bool Prefix() { return !InputGate.Blocked; }
    }

    [HarmonyPatch(typeof(CameraComputer), "Update")]
    internal static class GateCameraComputerUpdate
    {
        private static bool Prefix() { return !InputGate.Blocked; }
    }

    [HarmonyPatch(typeof(MapCase_weapon), "Update")]
    internal static class GateMapCaseUpdate
    {
        private static bool Prefix() { return !InputGate.Blocked; }
    }

    [HarmonyPatch(typeof(Food_weapon), "Update")]
    internal static class GateFoodUpdate
    {
        private static bool Prefix() { return !InputGate.Blocked; }
    }

    [HarmonyPatch(typeof(MedKit_weapon), "Update")]
    internal static class GateMedkitUpdate
    {
        private static bool Prefix() { return !InputGate.Blocked; }
    }

    [HarmonyPatch(typeof(Trap_weapon), "Update")]
    internal static class GateTrapUpdate
    {
        private static bool Prefix() { return !InputGate.Blocked; }
    }

    [HarmonyPatch(typeof(Camera_weapon), "Update")]
    internal static class GateCameraWeaponUpdate
    {
        private static bool Prefix() { return !InputGate.Blocked; }
    }

    [HarmonyPatch(typeof(Binocular_weapon), "Update")]
    internal static class GateBinocularUpdate
    {
        private static bool Prefix() { return !InputGate.Blocked; }
    }
}
