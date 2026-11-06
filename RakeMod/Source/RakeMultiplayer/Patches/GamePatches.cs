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

    /// <summary>
    /// Patch Rifle weapon to send DamageRequest to server when local player fires
    /// </summary>
    [HarmonyPatch(typeof(Rifle_weapon))]
    [HarmonyPatch("Update")]
    public class RifleWeaponFireSyncPatch
    {
        [HarmonyPostfix]
        static void Postfix(Rifle_weapon __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (!RakeMultiplayerPlugin.isConnected) return;

            var netPlayer = __instance.GetComponentInParent<NetworkedPlayer>();
            if (netPlayer == null || !netPlayer.isLocalPlayer) return;

            // Check if shot was fired this frame (replicate the fire condition)
            // Note: We can't easily detect "this frame only" without state tracking,
            // so we'll use a simple approach: check if fire conditions are met
            // The original code does the raycast and damage in Update, so we replicate that logic here
            // for the network packet only (server will do authoritative raycast)

            if (Input.GetButtonDown("Fire1"))
            {
                // Get the caster position and direction for the shot
                // We need to access private fields via reflection
                var casterField = typeof(Rifle_weapon).GetField("caster", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var bSightField = typeof(Rifle_weapon).GetField("bSight", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var bCanFireField = typeof(Rifle_weapon).GetField("bCanFire", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var currentAmmoField = typeof(Rifle_weapon).GetField("currentAmmo", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                bool bSight = bSightField != null && (bool)bSightField.GetValue(__instance);
                bool bCanFire = bCanFireField != null && (bool)bCanFireField.GetValue(__instance);
                int currentAmmo = currentAmmoField != null ? (int)currentAmmoField.GetValue(__instance) : 0;

                if (bSight && bCanFire && currentAmmo > 0 && casterField != null)
                {
                    var caster = casterField.GetValue(__instance) as Transform;
                    if (caster != null)
                    {
                        // Send DamageRequest to server with shooter position
                        var packet = new DamageRequestPacket
                        {
                            ShooterId = PlayerSpawner.Instance?.LocalPlayer?.PlayerId ?? 1,
                            TargetId = 0, // 0 means "Rake" (server will find the Rake)
                            Amount = 100, // Rifle damage
                            DamageType = "Bullet",
                            ShooterPosition = caster.position
                        };

                        if (RakeMultiplayerPlugin.isServer)
                        {
                            // Host: process locally via HandleServerDamageRequest
                            RakeNetworkManager.Instance?.HandleServerDamageRequest(0, packet, packet.Serialize());
                        }
                        else
                        {
                            // Client: send to server
                            RakeNetworkManager.Instance?.ClientSend(packet.Serialize());
                        }

                        RakeMultiplayerPlugin.Log?.LogInfo($"[3.4] Sent DamageRequest for Rifle shot from {caster.position}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Patch Sniper Rifle weapon to send DamageRequest to server when local player fires
    /// </summary>
    [HarmonyPatch(typeof(SniperRifle_weapon))]
    [HarmonyPatch("Update")]
    public class SniperRifleWeaponFireSyncPatch
    {
        [HarmonyPostfix]
        static void Postfix(SniperRifle_weapon __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (!RakeMultiplayerPlugin.isConnected) return;

            var netPlayer = __instance.GetComponentInParent<NetworkedPlayer>();
            if (netPlayer == null || !netPlayer.isLocalPlayer) return;

            if (Input.GetButtonDown("Fire1"))
            {
                var casterField = typeof(SniperRifle_weapon).GetField("caster", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var bSightField = typeof(SniperRifle_weapon).GetField("bSight", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var bCanFireField = typeof(SniperRifle_weapon).GetField("bCanFire", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var currentAmmoField = typeof(SniperRifle_weapon).GetField("currentAmmo", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                bool bSight = bSightField != null && (bool)bSightField.GetValue(__instance);
                bool bCanFire = bCanFireField != null && (bool)bCanFireField.GetValue(__instance);
                int currentAmmo = currentAmmoField != null ? (int)currentAmmoField.GetValue(__instance) : 0;

                if (bSight && bCanFire && currentAmmo > 0 && casterField != null)
                {
                    var caster = casterField.GetValue(__instance) as Transform;
                    if (caster != null)
                    {
                        var packet = new DamageRequestPacket
                        {
                            ShooterId = PlayerSpawner.Instance?.LocalPlayer?.PlayerId ?? 1,
                            TargetId = 0,
                            Amount = 200, // Sniper damage
                            DamageType = "SniperShot",
                            ShooterPosition = caster.position
                        };

                        if (RakeMultiplayerPlugin.isServer)
                        {
                            RakeNetworkManager.Instance?.HandleServerDamageRequest(0, packet, packet.Serialize());
                        }
                        else
                        {
                            RakeNetworkManager.Instance?.ClientSend(packet.Serialize());
                        }

                        RakeMultiplayerPlugin.Log?.LogInfo($"[3.4] Sent DamageRequest for Sniper shot from {caster.position}");
                    }
                }
            }
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

            // Get deterministic network ID from RakeNetworkManager instance (uses assigned sequential ID)
            var netManager = RakeNetworkManager.Instance;
            int itemId = netManager != null ? netManager.GetItemNetworkId(__instance) : 0;
            string type = __instance.GetType().Name;

            if (itemId == 0)
            {
                RakeMultiplayerPlugin.Log?.LogWarning($"[3.5] ItemPickupPatch: Item {__instance.name} has no network ID! Skipping sync.");
                return true; // Allow original but no sync
            }

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
                // Client: BLOCK local pickup execution. Only send request to server.
                // Server will authorize and broadcast ItemDestroyed.
                RakeNetworkManager.Instance?.ClientSend(packet.Serialize());
                RakeMultiplayerPlugin.Log?.LogInfo($"[3.5] Client requested pickup of item {itemId} ({type})");
                return false; // BLOCK local PickupItem - wait for server authorization
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