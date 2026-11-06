using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer.Patches
{
    /// <summary>
    /// Core game patches for multiplayer synchronization
    /// </summary>
    [HarmonyPatch(typeof(FPSController))]
    [HarmonyPatch("Update")]
    public class FPSControllerUpdatePatch
    {
        static void Postfix(FPSController __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            
            // F1 handler removed - handled by MultiplayerMenu.Update
            // Keep this patch for future multiplayer-specific Update logic if needed
        }
    }

    /// <summary>
    /// Patch player movement for network synchronization
    /// </summary>
    [HarmonyPatch(typeof(PlayerMovement))]
    [HarmonyPatch("Update")]
    public class PlayerMovementPatch
    {
        static void Postfix(PlayerMovement __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;

            var netPlayer = __instance.GetComponent<NetworkedPlayer>();
            if (netPlayer != null && netPlayer.isLocalPlayer)
            {
                // Movement will be synced via NetworkedPlayer.Update
            }
        }
    }

    /// <summary>
    /// Patch camera look for network synchronization
    /// </summary>
    [HarmonyPatch(typeof(CameraMouseLook))]
    [HarmonyPatch("Update")]
    public class CameraMouseLookPatch
    {
        static void Postfix(CameraMouseLook __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            // Rotation sync handled by NetworkedPlayer
        }
    }

    /// <summary>
    /// Patch camera system for network synchronization
    /// </summary>
    [HarmonyPatch(typeof(CameraComputer))]
    [HarmonyPatch("Update")]
    public class CameraComputerPatch
    {
        static void Postfix(CameraComputer __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            
            if (RakeMultiplayerPlugin.isServer)
            {
                // Camera sync handled by NetworkedCameraSystem
            }
        }
    }

    /// <summary>
    /// Patch detection trigger for multiplayer awareness
    /// </summary>
    [HarmonyPatch(typeof(DetectionTriggerRake))]
    [HarmonyPatch("OnTriggerEnter")]
    public class DetectionTriggerPatch
    {
        static void Postfix(DetectionTriggerRake __instance, Collider info)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            
            // Check if player entered detection range
            var player = info.GetComponent<FPSController>();
            if (player != null)
            {
                var netPlayer = player.GetComponent<NetworkedPlayer>();
                if (netPlayer != null)
                {
                    // TODO: Notify Rake AI of player detection
                }
            }
        }
    }

    /// <summary>
    /// Patch Rifle weapon to block firing on remote players
    /// </summary>
    [HarmonyPatch(typeof(Rifle_weapon))]
    [HarmonyPatch("Update")]
    public class RifleWeaponUpdatePatch
    {
        [HarmonyPrefix]
        static bool Prefix(Rifle_weapon __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return true;
            
            var netPlayer = __instance.GetComponentInParent<NetworkedPlayer>();
            if (netPlayer != null && !netPlayer.isLocalPlayer)
            {
                return false; // Block Update on remote players
            }
            return true;
        }
    }

    /// <summary>
    /// Patch Sniper Rifle weapon to block firing on remote players
    /// </summary>
    [HarmonyPatch(typeof(SniperRifle_weapon))]
    [HarmonyPatch("Update")]
    public class SniperRifleWeaponUpdatePatch
    {
        [HarmonyPrefix]
        static bool Prefix(SniperRifle_weapon __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return true;
            
            var netPlayer = __instance.GetComponentInParent<NetworkedPlayer>();
            if (netPlayer != null && !netPlayer.isLocalPlayer)
            {
                return false; // Block Update on remote players
            }
            return true;
        }
    }

    // === Stage 3.5: Item Pickup Synchronization ===
    
    /// <summary>
    /// Patch Item.PickupItem to send pickup request to server
    /// </summary>
    [HarmonyPatch(typeof(Item))]
    [HarmonyPatch("PickupItem")]
    public class ItemPickupPatch
    {
        [HarmonyPrefix]
        static bool Prefix(Item __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return true;
            
            // If we're already destroying item from network, allow local execution
            if (RakeMultiplayerPlugin.IsApplyingNetworkDestroy) return true;

            // Generate DETERMINISTIC network ID using shared method (rounded ints for consistency)
            int itemId = RakeNetworkManager.GetItemNetworkId(__instance);
            string type = __instance.GetType().Name;

            var packet = new ItemPickupPacket 
            { 
                PlayerId = PlayerSpawner.Instance?.LocalPlayer?.PlayerId ?? 1, 
                ItemNetworkId = itemId, 
                ItemType = type 
            };
            
            if (RakeMultiplayerPlugin.isServer)
            {
                // Host: execute original PickupItem (gives effect) + broadcast destruction
                // Original PickupItem will handle effect and call Destroy
                RakeNetworkManager.Instance?.ServerBroadcast(
                    new ItemDestroyedPacket { ItemNetworkId = itemId, PickerId = 1 }.Serialize()
                );
                return true; // Allow original PickupItem to execute (gives effect, destroys object)
            }
            else
            {
                // Client: send request to server + allow original for LOCAL effect (sound, UI)
                // BUT prevent local Destroy via DestroyPatch - server will confirm with ItemDestroyed
                RakeNetworkManager.Instance?.ClientSend(packet.Serialize());
                return true; // Allow original PickupItem for local effect
            }
        }
    }

    // === Prevent clients from locally destroying networked items ===
    // The server is authoritative - clients wait for ItemDestroyed packet
    [HarmonyPatch(typeof(UnityEngine.Object), "Destroy", new Type[] { typeof(UnityEngine.Object) })]
    public static class DestroyPatch
    {
        [HarmonyPrefix]
        static bool Prefix(UnityEngine.Object obj)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return true;
            if (RakeMultiplayerPlugin.isServer) return true; // Host can destroy
            if (RakeMultiplayerPlugin.IsApplyingNetworkDestroy) return true; // Network-driven destroy

            // Block client-side destruction of networked Items
            var item = obj as Item;
            if (item != null)
            {
                RakeMultiplayerPlugin.Log?.LogInfo($"[3.5] Blocked client Destroy on Item: {item.name}");
                return false; // Prevent local destroy - wait for server ItemDestroyed
            }
            return true;
        }
    }
}