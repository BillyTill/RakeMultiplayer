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
    /// Rifle: detect an actual shot via ammo decrease (the vanilla Update fires a
    /// coroutine that sets bCanFire=false and decrements ammo before any postfix runs).
    /// Clients ask the server for authoritative damage; the host already applied it locally.
    /// </summary>
    [HarmonyPatch(typeof(Rifle_weapon))]
    [HarmonyPatch("Update")]
    public class RifleWeaponFireSyncPatch
    {
        private static readonly System.Reflection.FieldInfo AmmoField =
            typeof(Rifle_weapon).GetField("currentAmmo",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        private static readonly System.Reflection.FieldInfo CasterField =
            typeof(Rifle_weapon).GetField("caster",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        [HarmonyPrefix]
        static void Prefix(Rifle_weapon __instance, out int __state)
        {
            __state = GetAmmo(__instance);
        }

        [HarmonyPostfix]
        static void Postfix(Rifle_weapon __instance, int __state)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (!RakeMultiplayerPlugin.isConnected) return;

            var netPlayer = __instance.GetComponentInParent<NetworkedPlayer>();
            if (netPlayer == null || !netPlayer.isLocalPlayer) return;

            int after = GetAmmo(__instance);
            if (after >= __state) return; // no shot this frame

            var caster = GetCaster(__instance);
            if (caster == null) return;

            var manager = RakeNetworkManager.Instance;
            if (manager == null) return;

            int shooterId = PlayerSpawner.Instance != null && PlayerSpawner.Instance.LocalPlayer != null
                ? PlayerSpawner.Instance.LocalPlayer.PlayerId : 1;

            var visual = new FireVisualsPacket
            {
                ShooterId = shooterId,
                WeaponType = 0,
                Origin = caster.position,
                Direction = caster.forward
            };

            if (RakeMultiplayerPlugin.isServer) manager.ServerBroadcast(visual.Serialize());
            else manager.ClientSend(visual.Serialize());

            if (RakeMultiplayerPlugin.isServer) return; // host damage already applied by vanilla

            var packet = new DamageRequestPacket
            {
                ShooterId = shooterId,
                TargetId = 0,
                Amount = 100,
                DamageType = "Bullet",
                ShooterPosition = caster.position
            };
            manager.ClientSend(packet.Serialize());
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.4] Client sent rifle DamageRequest from {caster.position}");
        }
        private static int GetAmmo(Rifle_weapon w)
        {
            return AmmoField != null ? (int)AmmoField.GetValue(w) : 0;
        }

        private static Transform GetCaster(Rifle_weapon w)
        {
            return CasterField != null ? CasterField.GetValue(w) as Transform : null;
        }
    }
    /// <summary>
    /// Sniper rifle: same ammo-delta shot detection as the rifle.
    /// </summary>
    [HarmonyPatch(typeof(SniperRifle_weapon))]
    [HarmonyPatch("Update")]
    public class SniperRifleWeaponFireSyncPatch
    {
        private static readonly System.Reflection.FieldInfo AmmoField =
            typeof(SniperRifle_weapon).GetField("currentAmmo",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        private static readonly System.Reflection.FieldInfo CasterField =
            typeof(SniperRifle_weapon).GetField("caster",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        [HarmonyPrefix]
        static void Prefix(SniperRifle_weapon __instance, out int __state)
        {
            __state = GetAmmo(__instance);
        }

        [HarmonyPostfix]
        static void Postfix(SniperRifle_weapon __instance, int __state)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (!RakeMultiplayerPlugin.isConnected) return;

            var netPlayer = __instance.GetComponentInParent<NetworkedPlayer>();
            if (netPlayer == null || !netPlayer.isLocalPlayer) return;

            int after = GetAmmo(__instance);
            if (after >= __state) return;

            var caster = GetCaster(__instance);
            if (caster == null) return;

            var manager = RakeNetworkManager.Instance;
            if (manager == null) return;

            int shooterId = PlayerSpawner.Instance != null && PlayerSpawner.Instance.LocalPlayer != null
                ? PlayerSpawner.Instance.LocalPlayer.PlayerId : 1;

            var visual = new FireVisualsPacket
            {
                ShooterId = shooterId,
                WeaponType = 1,
                Origin = caster.position,
                Direction = caster.forward
            };

            if (RakeMultiplayerPlugin.isServer) manager.ServerBroadcast(visual.Serialize());
            else manager.ClientSend(visual.Serialize());

            if (RakeMultiplayerPlugin.isServer) return;

            var packet = new DamageRequestPacket
            {
                ShooterId = shooterId,
                TargetId = 0,
                Amount = 200,
                DamageType = "SniperShot",
                ShooterPosition = caster.position
            };
            manager.ClientSend(packet.Serialize());
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.4] Client sent sniper DamageRequest from {caster.position}");
        }
        private static int GetAmmo(SniperRifle_weapon w)
        {
            return AmmoField != null ? (int)AmmoField.GetValue(w) : 0;
        }

        private static Transform GetCaster(SniperRifle_weapon w)
        {
            return CasterField != null ? CasterField.GetValue(w) as Transform : null;
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

                // Host: execute the original PickupItem. The removal is broadcast by the
                // per-item pickup postfix (ItemPickupSync) only if the item was consumed.
                return true;
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
            if (!RakeMultiplayerPlugin.isConnected) return true; // single-player: vanilla behavior

            if (RakeMultiplayerPlugin.isServer)
            {
                // Remember consumed items so the pickup postfix can broadcast the removal
                var serverItem = obj as Item;
                if (serverItem != null)
                {
                    var mgr = RakeNetworkManager.Instance;
                    if (mgr != null)
                    {
                        ItemPickupSync.MarkConsumed(mgr.GetItemNetworkId(serverItem));
                    }
                }
                return true; // Host can destroy
            }

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
                // If the camera was not tracked as placed (e.g. loaded from a save),
                // let vanilla SetUnsetupState run so it is removed from CameraManager.
                return netCam.UnplaceCamera();
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
    }
    // === Stage 3.11: Trap synchronization ===

    /// <summary>
    /// A trap placed by a client must create an authoritative server zone.
    /// The client keeps its local trap visual but cannot deal damage itself.
    /// </summary>
    [HarmonyPatch(typeof(LevelItemsManager))]
    [HarmonyPatch("AddTrap")]
    public class LevelItemsManagerAddTrapPatch
    {
        [HarmonyPostfix]
        static void Postfix(LevelItemsManager __instance, GameObject obj)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (obj == null) return;
            if (RakeMultiplayerPlugin.isServer) return; // host trap is already server-side

            var marker = new RakeMultiplayer.Networking.MapMarkerPacket
            {
                MarkerType = 1, // Trap
                Position = obj.transform.position,
                MarkerId = 0,
                IsActive = true
            };
            RakeNetworkManager.Instance?.ClientSend(marker.Serialize());
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.11] Client reported trap placement at {obj.transform.position}");
        }
    }

    /// <summary>
    /// Trap triggers are server-authoritative. Clients do not apply damage locally;
    /// the host trap also stuns the Rake for 3 seconds.
    /// </summary>
    [HarmonyPatch(typeof(Trap_proj))]
    [HarmonyPatch("OnTriggerEnter", new Type[] { typeof(Collider) })]
    public class TrapProjTriggerPatch
    {
        [HarmonyPrefix]
        static bool Prefix(Trap_proj __instance, Collider info)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return true;

            // Clients never resolve trap damage/effects
            if (!RakeMultiplayerPlugin.isServer) return false;

            return true; // server runs the original damage logic
        }

        [HarmonyPostfix]
        static void Postfix(Trap_proj __instance, Collider info)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (!RakeMultiplayerPlugin.isServer) return;
            if (info == null) return;

            // Extra effect required by the mechanic spec: trap stuns the Rake for 3s
            var rake = info.GetComponentInParent<NetworkedRakeAI>();
            if (rake != null)
            {
                rake.Stun(NetworkedTrap.StunDuration);

                var quest = new RakeMultiplayer.Networking.QuestTriggerPacket
                {
                    TriggerType = NetworkedTrap.QuestTrapTriggered,
                    Position = __instance.transform.position,
                    CameraId = 0
                };
                RakeNetworkManager.Instance?.ServerBroadcast(quest.Serialize());
            }
        }
    }
    // === Multiplayer damage authority (Rake / animals) ===

    /// <summary>
    /// Only the server applies damage to the Rake. Clients receive health through
    /// RakeState/DamageApplied; local raycast damage would desync or locally kill it.
    /// </summary>
    [HarmonyPatch(typeof(Enemy_AI))]
    [HarmonyPatch("TakeDamage")]
    public class EnemyTakeDamageAuthorityPatch
    {
        [HarmonyPrefix]
        static bool Prefix(Enemy_AI __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return true;
            if (!RakeMultiplayerPlugin.isConnected) return true; // single-player
            return RakeMultiplayerPlugin.isServer;
        }

        [HarmonyPostfix]
        static void Postfix(Enemy_AI __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (__instance == null) return;

            // A player-controlled Rake cannot attack for a moment after being hit.
            if (RakePlayerController.IsActive) RakePlayerController.NotifyDamageTaken(__instance.transform);

            if (!RakeMultiplayerPlugin.isConnected) return;
            if (!RakeMultiplayerPlugin.isServer) return;

            if (__instance.Health <= 0f)
            {
                SessionManager.Instance?.UpdateRakeHealth(0f);
            }
        }
    }

    /// <summary>
    /// Animals are server-authoritative too (health/ragdoll driven by NetworkedPig).
    /// </summary>
    [HarmonyPatch(typeof(Animal_AI))]
    [HarmonyPatch("TakeDamage")]
    public class AnimalTakeDamageAuthorityPatch
    {
        [HarmonyPrefix]
        static bool Prefix(Animal_AI __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return true;
            if (!RakeMultiplayerPlugin.isConnected) return true;
            return RakeMultiplayerPlugin.isServer;
        }
    }

    // === Remote clone singleton guards ===

    /// <summary>
    /// Clones are created from the scene player prefab. Their FPSController.Start must
    /// never run: it would capture the FPSController.instance and initialize the clone
    /// as another local player. (Disabled before Start in SpawnRemotePlayer; this is a
    /// hard safety net for any other instantiation path.)
    /// </summary>
    [HarmonyPatch(typeof(FPSController))]
    [HarmonyPatch("Start")]
    public class FPSControllerCloneGuardPatch
    {
        [HarmonyPrefix]
        static bool Prefix(FPSController __instance)
        {
            if (__instance == null) return true;
            if (__instance.gameObject.name.StartsWith("RemotePlayer_"))
            {
                __instance.enabled = false;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// The same for WeaponManager.instance - a remote clone must not become the weapon
    /// manager used by the local HUD/keyboard.
    /// </summary>
    [HarmonyPatch(typeof(WeaponManager))]
    [HarmonyPatch("Start")]
    public class WeaponManagerCloneGuardPatch
    {
        [HarmonyPrefix]
        static bool Prefix(WeaponManager __instance)
        {
            if (__instance == null) return true;

            var root = __instance.transform.root;
            if (root != null && root.gameObject.name.StartsWith("RemotePlayer_"))
            {
                __instance.enabled = false;
                return false;
            }

            var netPlayer = __instance.GetComponentInParent<NetworkedPlayer>();
            if (netPlayer != null && netPlayer.PlayerId > 0 && !netPlayer.isLocalPlayer)
            {
                __instance.enabled = false;
                return false;
            }

            return true;
        }
    }
    /// <summary>
    /// The vanilla GameTimer counts down independently on every peer and loads levels
    /// on its own. In multiplayer the server's DayNightManager is authoritative, so we
    /// keep the timer text in sync with it and never allow a local timeout load.
    /// </summary>
    [HarmonyPatch(typeof(GameTimer))]
    [HarmonyPatch("Update")]
    public class GameTimerMultiplayerPatch
    {
        [HarmonyPrefix]
        static bool Prefix(GameTimer __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return true;
            if (!RakeMultiplayerPlugin.isConnected) return true; // single-player

            var dayNight = DayNightManager.Instance;
            if (dayNight != null)
            {
                float remaining = dayNight.GetRemainingTime();
                int minutes = Mathf.Max(0, Mathf.FloorToInt(remaining / 60f));

                if (__instance.text != null) __instance.text.text = minutes.ToString();
                __instance.minute = minutes;
                __instance.second = remaining - minutes * 60f;
            }

            return false; // never run the independent countdown / level load
        }
    }}