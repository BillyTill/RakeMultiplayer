using HarmonyLib;
using UnityEngine;

namespace RakeMultiplayer.Patches
{
    /// <summary>
    /// A fresh install has no PlayerPrefs at all. CameraMouseLook reads "Sens:" with no
    /// default, so sensitivity becomes 0 and the mouse never turns the character. The
    /// debug camera kept working because it uses its own constant (2.5), which is exactly
    /// the symptom reported from a clean Windows install.
    /// </summary>
    [HarmonyPatch(typeof(CameraMouseLook))]
    [HarmonyPatch("Start")]
    internal static class CameraMouseLookDefaultsPatch
    {
        private static void Postfix(CameraMouseLook __instance)
        {
            float sensitivity = PlayerPrefs.GetFloat("Sens:");

            if (sensitivity <= 0.01f)
            {
                // 2 is the value MainMenu uses for a fresh install.
                sensitivity = 2f;
                PlayerPrefs.SetFloat("Sens:", sensitivity);
                PlayerPrefs.Save();
                RakeMultiplayerPlugin.Log?.LogWarning(
                    "[Input] PlayerPrefs 'Sens:' was missing or 0, defaulted to " + sensitivity);
            }

            if (__instance == null) return;
            if (__instance.sensitivityX <= 0.01f) __instance.sensitivityX = sensitivity;
            if (__instance.sensitivityY <= 0.01f) __instance.sensitivityY = sensitivity;
        }
    }
}
