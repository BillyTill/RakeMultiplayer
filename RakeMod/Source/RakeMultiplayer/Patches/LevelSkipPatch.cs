using HarmonyLib;
using RakeMultiplayer.Networking;
using UnityEngine;

namespace RakeMultiplayer.Patches
{
    /// <summary>
    /// The trailer dialog "skip to the next hunt" (LevelSkip.SkipLevel) used to run the
    /// vanilla flow: fade out and Application.LoadLevel locally.
    ///
    /// In a session that is wrong in both directions:
    /// - on a client the load was refused by the level guard, so the button did nothing
    ///   (or, before the guard, moved that one client into the next level alone);
    /// - on the host the level changed while DayNightManager still believed in the old
    ///   phase, so the phase then had to be "corrected" after the fact.
    ///
    /// In multiplayer the button now means "advance the phase for everybody": the host
    /// queues its own transition, a client asks the host (PhaseSkipRequest). Single player
    /// keeps the vanilla behaviour.
    /// </summary>
    [HarmonyPatch(typeof(LevelSkip))]
    [HarmonyPatch("SkipLevel")]
    public class LevelSkipMultiplayerPatch
    {
        [HarmonyPrefix]
        static bool Prefix(LevelSkip __instance, bool state)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return true;
            if (!RakeMultiplayerPlugin.isConnected) return true; // single player: vanilla
            if (!state) return true;                             // "no" -> vanilla Exit()

            // Close the dialog and hand control back without loading anything locally.
            CloseDialog(__instance);

            if (RakeMultiplayerPlugin.isServer)
            {
                var cycle = DayNightManager.Instance;
                if (cycle == null || !cycle.IsRunning)
                {
                    RakeMultiplayerPlugin.Log?.LogWarning(
                        "[LevelSkip] host skip ignored: the phase cycle is not running");
                    return false;
                }

                RakeMultiplayerPlugin.Log?.LogInfo(
                    "[LevelSkip] host advanced the phase from the trailer dialog");
                cycle.SkipPhase(); // queued: broadcast + one level load for every peer
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogInfo(
                    "[LevelSkip] client asked the host to advance the phase");
                RakeNetworkManager.Instance?.ClientSend(new PhaseSkipRequestPacket().Serialize());
            }

            return false; // never run the vanilla local coroutine / LoadLevel
        }

        /// <summary>Same effect as the vanilla Exit(), without touching the level.</summary>
        private static void CloseDialog(LevelSkip skip)
        {
            try
            {
                if (skip != null && skip.cml != null)
                {
                    skip.cml.bControl = true;
                }

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                if (skip != null && skip.HUD != null)
                {
                    skip.HUD.SetActive(false);
                }
            }
            catch (System.Exception ex)
            {
                RakeMultiplayerPlugin.Log?.LogWarning(
                    "[LevelSkip] could not close the dialog: " + ex.Message);
            }
        }
    }
}
