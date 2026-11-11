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
    /// Patch the camera monitor for network synchronization (Task 3.6).
    /// Cameras are scene objects replicated on every client, so view switching is
    /// local; what must be kept in sync is the monitor list itself. When a camera
    /// the player is watching breaks or is picked up, switch to another camera or
    /// leave the monitor instead of rendering a dead feed.
    /// </summary>
    [HarmonyPatch(typeof(CameraComputer))]
    [HarmonyPatch("Update")]
    public class CameraComputerPatch
    {
        static void Postfix(CameraComputer __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (__instance == null || !__instance.bWatching) return;

            var manager = CameraManager.instance;
            if (manager == null) return;

            // Drop references to cameras that were picked up (SetUnsetupState removed them),
            // then verify the currently watched camera is still usable.
            var current = manager.currentCamera;
            if (current == null)
            {
                if (manager.GetCameraCount() > 0) manager.ActiveCamera(0);
                else __instance.Exit();
                return;
            }

            var recordCamera = current.parent != null ? current.parent.GetComponent<RecordCamera_obj>() : null;
            if (recordCamera == null) return;

            var netCam = recordCamera.GetComponent<NetworkedCamera>();
            if (netCam != null && netCam.IsBroken)
            {
                RakeMultiplayerPlugin.Log?.LogInfo($"[3.6] Watched camera {netCam.CameraId} was broken - switching away");

                manager.RemoveCamera(current);
                if (manager.GetCameraCount() > 0) manager.ActiveCamera(0);
                else __instance.Exit();
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
                // Task 3.3: server-authoritative 5-camera limit (host path).
                // The host bypasses HandleServerItemPickup, so enforce the quota here too.
                if (type == "Camera_item" && !NetworkedCamera.CanPlaceCamera())
                {
                    RakeMultiplayerPlugin.Log?.LogWarning(
                        $"[3.3] Host pickup of camera {itemId} blocked: camera limit " +
                        $"{NetworkedCamera.MaxCameras} reached " +
                        $"(placed={NetworkedCamera.GetPlacedCameraCount()}, usable={NetworkedCamera.GetUsableCameraCount()})");
                    return false; // Block local pickup, camera stays in the world
                }

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

    // === Stage 3.4: Camera setup/pickup synchronization ===

    /// <summary>
    /// Route camera setup through the network layer.
    /// Server: enforce the 5-camera quota and broadcast the new state.
    /// Client: send a request; the server remains authoritative.
    /// </summary>
    [HarmonyPatch(typeof(RecordCamera_obj))]
    [HarmonyPatch("SetupCamera")]
    public class RecordCameraSetupPatch
    {
        [HarmonyPrefix]
        static bool Prefix(RecordCamera_obj __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return true;
            if (RakeMultiplayerPlugin.IsApplyingNetworkCameraState) return true; // network-driven visual update
            if (__instance.cameraState != RecordCamera_obj.Camera_state.Unsetup) return true;

            var netCam = __instance.GetComponent<NetworkedCamera>() ?? __instance.GetComponentInParent<NetworkedCamera>();
            if (netCam == null) return true; // not a networked camera - run vanilla logic

            if (RakeMultiplayerPlugin.isServer)
            {
                // Server keeps authority: quota + broadcast (InitializePlacement also applies local visuals)
                if (!netCam.InitializePlacement(1))
                {
                    try { InformationUI.instance?.DrawHint($"You cannot setup no more {NetworkedCamera.MaxCameras} cameras"); }
                    catch { /* InformationUI may be absent */ }
                }
                return false; // handled by the network layer
            }

            // Client: request server authorization
            var request = new RakeMultiplayer.Networking.CameraSetupRequestPacket
            {
                CameraId = netCam.CameraId,
                PlayerId = PlayerSpawner.Instance?.LocalPlayer?.PlayerId ?? 1,
                IsSetup = true,
                IsResult = false,
                Approved = false,
                Reason = string.Empty
            };
            RakeNetworkManager.Instance?.ClientSend(request.Serialize());
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.4] Client requested setup of camera {netCam.CameraId}");
            return false;
        }
    }

    /// <summary>
    /// Route camera pickup (un-setup) through the network layer.
    /// </summary>
    [HarmonyPatch(typeof(RecordCamera_obj))]
    [HarmonyPatch("SetUnsetupState")]
    public class RecordCameraUnsetupPatch
    {
        [HarmonyPrefix]
        static bool Prefix(RecordCamera_obj __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return true;
            if (RakeMultiplayerPlugin.IsApplyingNetworkCameraState) return true; // network-driven visual update
            if (__instance.cameraState != RecordCamera_obj.Camera_state.Setup) return true;

            var netCam = __instance.GetComponent<NetworkedCamera>() ?? __instance.GetComponentInParent<NetworkedCamera>();
            if (netCam == null) return true;

            if (RakeMultiplayerPlugin.isServer)
            {
                netCam.UnplaceCamera(); // applies visuals + broadcasts CameraDestroy
                return false;
            }

            var request = new RakeMultiplayer.Networking.CameraSetupRequestPacket
            {
                CameraId = netCam.CameraId,
                PlayerId = PlayerSpawner.Instance?.LocalPlayer?.PlayerId ?? 1,
                IsSetup = false,
                IsResult = false,
                Approved = false,
                Reason = string.Empty
            };
            RakeNetworkManager.Instance?.ClientSend(request.Serialize());
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.4] Client requested pickup of camera {netCam.CameraId}");
            return false;
        }
    }

    /// <summary>
    /// Keep the game's local CheckSetup gate in sync with the authoritative
    /// 5-camera quota (the vanilla check counts broken cameras, ours frees them).
    /// </summary>
    [HarmonyPatch(typeof(RecordCamera_obj))]
    [HarmonyPatch("CheckSetup")]
    public class RecordCameraCheckSetupPatch
    {
        [HarmonyPostfix]
        static void Postfix(RecordCamera_obj __instance, ref bool __result)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (__instance.cameraState != RecordCamera_obj.Camera_state.Unsetup) return;

            bool canPlace = NetworkedCamera.CanPlaceCamera();
            if (__result != canPlace)
            {
                __result = canPlace;
                if (!canPlace)
                {
                    try { InformationUI.instance?.DrawHint($"You cannot setup no more {NetworkedCamera.MaxCameras} cameras"); }
                    catch { /* InformationUI may be absent */ }
                }
            }
        }
    }
    // === Stage 3.8: Bait and animal trap synchronization ===

    /// <summary>
    /// Bait is an animal attractor, so it must exist on the server.
    /// Clients forward the placement position instead of registering it locally.
    /// </summary>
    [HarmonyPatch(typeof(AnimalsManager))]
    [HarmonyPatch("AddFoodPoint")]
    public class AnimalsManagerAddFoodPointPatch
    {
        [HarmonyPrefix]
        static bool Prefix(AnimalsManager __instance, Transform point)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return true;
            if (point == null) return true;
            if (RakeMultiplayerPlugin.isServer) return true; // server owns the AI

            var marker = new RakeMultiplayer.Networking.MapMarkerPacket
            {
                MarkerType = 2, // Bait
                Position = point.position,
                MarkerId = 0,
                IsActive = true
            };
            RakeNetworkManager.Instance?.ClientSend(marker.Serialize());
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.8] Client sent bait placement at {point.position}");

            // Do not register locally - animal AI is server-authoritative
            return false;
        }
    }

    /// <summary>
    /// Notify clients when the server closes an animal cage so the trap event is visible everywhere.
    /// </summary>
    [HarmonyPatch(typeof(AnimalCage_obj))]
    [HarmonyPatch("CloseCage")]
    public class AnimalCageClosePatch
    {
        [HarmonyPostfix]
        static void Postfix(AnimalCage_obj __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (!RakeMultiplayerPlugin.isServer) return;
            if (__instance == null) return;

            var packet = new RakeMultiplayer.Networking.QuestTriggerPacket
            {
                TriggerType = 2, // TrapTriggered
                Position = __instance.transform.position,
                CameraId = 0
            };
            RakeNetworkManager.Instance?.ServerBroadcast(packet.Serialize());
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.8] Animal cage closed at {__instance.transform.position}");
        }
    }
    // === Stage 3.9: Pig-camera attachment ===

    /// <summary>
    /// Pig cameras count toward the same 5-camera limit and are synced through
    /// NetworkedPig (PigStatePacket.CameraId). The high bit flags a pig camera ID.
    /// </summary>
    [HarmonyPatch(typeof(AnimalRecordCamera_obj))]
    [HarmonyPatch("SetupCamera")]
    public class AnimalRecordCameraSetupPatch
    {
        [HarmonyPrefix]
        static bool Prefix(AnimalRecordCamera_obj __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return true;
            if (RakeMultiplayerPlugin.IsApplyingNetworkCameraState) return true;

            var pig = __instance.GetComponentInParent<NetworkedPig>();
            uint pigId = pig != null ? pig.PigId : (uint)(__instance.GetInstanceID() & 0x7FFFFFFF);
            uint cameraId = 0x80000000u | pigId;

            if (RakeMultiplayerPlugin.isServer)
            {
                if (!NetworkedCamera.TryReserveExternalCamera(cameraId))
                {
                    try { InformationUI.instance?.DrawHint($"You cannot setup no more {NetworkedCamera.MaxCameras} cameras"); }
                    catch { /* InformationUI may be absent */ }
                    return false;
                }

                if (pig != null)
                {
                    pig.SetAttachedCamera(cameraId);
                    pig.ForceSendState();
                }

                // Let the original method enable the collar camera and destroy the pickup object
                return true;
            }

            // Client: request authorization, then run the local visual logic
            var request = new RakeMultiplayer.Networking.CameraSetupRequestPacket
            {
                CameraId = cameraId,
                PlayerId = PlayerSpawner.Instance?.LocalPlayer?.PlayerId ?? 1,
                IsSetup = true,
                IsResult = false,
                Approved = false,
                Reason = string.Empty
            };
            RakeNetworkManager.Instance?.ClientSend(request.Serialize());
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.9] Client requested pig-camera setup (camera {cameraId})");
            return true;
        }
    }}