using System;
using UnityEngine;
using HarmonyLib;

namespace RakeMultiplayer.Patches
{
    /// <summary>
    /// Additional patches for multiplayer functionality
    /// </summary>
    [HarmonyPatch(typeof(FPSController))]
    [HarmonyPatch("Awake")]
    public class FPSControllerAwakePatch
    {
        static void Postfix(FPSController __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            
            // Ensure NetworkedPlayer is added
            if (__instance.GetComponent<NetworkedPlayer>() == null)
            {
                __instance.gameObject.AddComponent<NetworkedPlayer>();
            }
        }
    }
}