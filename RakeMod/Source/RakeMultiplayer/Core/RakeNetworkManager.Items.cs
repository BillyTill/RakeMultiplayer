using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Telepathy;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    public partial class RakeNetworkManager
    {
        // Item pickup, drops and inventory effects. Part of the partial class declared in
        // RakeNetworkManager.cs; state stays there, behaviour lives here.

        /// <summary>Remember the local object for an in-flight pickup request.</summary>
        public void RememberPendingPickup(int itemNetworkId, Item item)
        {
            if (itemNetworkId > 0 && item != null) _pendingPickups[itemNetworkId] = item;
        }

// === Stage 3.5: Items ===
        private void HandleServerItemPickup(int connectionId, ItemPickupPacket packet, byte[] rawData)
        {
            int playerId = connectionId + 1;
            RakeMultiplayerPlugin.Log?.LogInfo(
                $"[3.5] Server received ItemPickup from C{connectionId} -> P{playerId}: itemId={packet.ItemNetworkId}");

            if (!_networkedItems.TryGetValue(packet.ItemNetworkId, out var item) || item == null)
            {
                RakeMultiplayerPlugin.Log?.LogWarning(
                    $"[3.5] Server: item {packet.ItemNetworkId} already picked up or unknown");
                _server.Send(connectionId, new ItemPickupConfirmPacket
                {
                    PlayerId = playerId,
                    ItemNetworkId = packet.ItemNetworkId,
                    Success = false,
                    Reason = "NotFound"
                }.Serialize());
                return;
            }

            // Authoritative proximity check. The old version allowed the pickup when the
            // proxy was missing (the null branch) and used a 100 m radius otherwise, so a
            // client could vacuum the whole map.
            Vector3 pickerPos;
            bool hasPickerPos = false;

            if (playerId == 1)
            {
                var local = PlayerSpawner.Instance != null ? PlayerSpawner.Instance.LocalPlayer : null;
                if (local != null)
                {
                    pickerPos = local.transform.position;
                    hasPickerPos = true;
                }
                else pickerPos = Vector3.zero;
            }
            else if (PlayerSpawner.Instance != null &&
                     PlayerSpawner.Instance.TryGetRemotePlayer(playerId, out var remotePicker) &&
                     remotePicker != null)
            {
                pickerPos = remotePicker.transform.position;
                hasPickerPos = true;
            }
            else if (_playerStates.TryGetValue(connectionId, out var pickerState) && pickerState != null)
            {
                pickerPos = pickerState.Position;
                hasPickerPos = true;
            }
            else pickerPos = Vector3.zero;

            if (!hasPickerPos)
            {
                _server.Send(connectionId, new ItemPickupConfirmPacket
                {
                    PlayerId = playerId,
                    ItemNetworkId = packet.ItemNetworkId,
                    Success = false,
                    Reason = "NoPosition"
                }.Serialize());
                return;
            }

            float pickupDistance = Vector3.Distance(pickerPos, item.transform.position);
            if (pickupDistance > 3.5f)
            {
                RakeMultiplayerPlugin.Log?.LogWarning(
                    $"[3.5] Server rejected pickup: P{playerId} is {pickupDistance:F1}m away from item {packet.ItemNetworkId}");
                _server.Send(connectionId, new ItemPickupConfirmPacket
                {
                    PlayerId = playerId,
                    ItemNetworkId = packet.ItemNetworkId,
                    Success = false,
                    Reason = "TooFar"
                }.Serialize());
                return;
            }

            // Item type comes from the server registry, never from the client packet
            string itemType = item.GetType().Name;

            // 5-camera quota (server-authoritative; broken cameras free a slot)
            if (itemType == "Camera_item" && !NetworkedCamera.CanPlaceCamera())
            {
                RakeMultiplayerPlugin.Log?.LogWarning(
                    $"[3.3] Server rejected camera pickup for P{playerId}: camera limit {NetworkedCamera.MaxCameras} reached " +
                    $"(placed={NetworkedCamera.GetPlacedCameraCount()}, usable={NetworkedCamera.GetUsableCameraCount()})");

                var rejectPacket = new ItemPickupConfirmPacket
                {
                    PlayerId = playerId,
                    ItemNetworkId = packet.ItemNetworkId,
                    Success = false,
                    Reason = "CameraLimit"
                };
                _server.Send(connectionId, rejectPacket.Serialize());
                OnPickupRejected?.Invoke(playerId, "CameraLimit");
                return; // Item stays in the world
            }

            // ---- Box of cameras: stackable, do not delete after the first camera ----
            var box = item as BoxOfCameras_item;
            if (box != null)
            {
                if (box.count <= 0)
                {
                    _server.Send(connectionId, new ItemPickupConfirmPacket
                    {
                        PlayerId = playerId,
                        ItemNetworkId = packet.ItemNetworkId,
                        Success = false,
                        Reason = "Empty"
                    }.Serialize());
                    return;
                }

                box.count--;
                bool active = box.count > 0;
                if (!active && box.obj != null) box.obj.SetActive(false);

                ServerAuthorizeBoxPickup(packet.ItemNetworkId, playerId, box.count, active);
                RakeMultiplayerPlugin.Log?.LogInfo(
                    $"[3.5] Box {packet.ItemNetworkId}: player {playerId} took 1 camera, remaining={box.count}");
                return;
            }

            // Remove the authoritative world object (the host does not receive its own broadcast)
            RakeMultiplayerPlugin.IsApplyingNetworkDestroy = true;
            try { UnityEngine.Object.Destroy(item.gameObject); }
            catch { /* already destroyed */ }
            finally { RakeMultiplayerPlugin.IsApplyingNetworkDestroy = false; }

            ServerAuthorizeItemPickup(packet.ItemNetworkId, playerId, itemType);
            RakeMultiplayerPlugin.Log?.LogInfo(
                $"[3.5] Server authorized pickup of item {packet.ItemNetworkId} ({itemType}) by P{playerId}");
        }

        /// <summary>
        /// Server: authorize an item pickup, remove it from the registry and broadcast it.
        /// </summary>
        public void ServerAuthorizeItemPickup(int itemNetworkId, int pickerId, string itemType = "")
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            _networkedItems.Remove(itemNetworkId);
            if (RakeMultiplayerPlugin.isServer) _consumedItemIds.Add(itemNetworkId);

            Item removed = null;
            foreach (var kvp in _itemToNetworkId)
            {
                if (kvp.Value == itemNetworkId) { removed = kvp.Key; break; }
            }
            if (removed != null) _itemToNetworkId.Remove(removed);

            var packet = new ItemDestroyedPacket
            {
                ItemNetworkId = itemNetworkId,
                PickerId = pickerId,
                ItemType = itemType ?? string.Empty
            };
            ServerBroadcast(packet.Serialize());
        }

        /// <summary>Server: register a dropped item with a shared id.</summary>
        /// <summary>Copy the ids of items that are already gone (for the level snapshot).</summary>
        public void CaptureConsumedItems(List<int> into)
        {
            into.Clear();
            foreach (var id in _consumedItemIds) into.Add(id);
        }

        /// <summary>
        /// The new level re-created every item object. Remove the ones that were picked up
        /// before the level change, so a new day does not hand the loot back.
        /// </summary>
        public void RemoveSceneItems(List<int> ids)
        {
            int removed = 0;

            for (int i = 0; i < ids.Count; i++)
            {
                Item item;
                if (!_networkedItems.TryGetValue(ids[i], out item) || item == null) continue;

                _networkedItems.Remove(ids[i]);
                _itemToNetworkId.Remove(item);
                _consumedItemIds.Add(ids[i]);
                UnityEngine.Object.Destroy(item.gameObject);
                removed++;
            }

            if (removed > 0)
            {
                RakeMultiplayerPlugin.Log?.LogInfo("[Scene] " + removed +
                                                   " picked-up item(s) stay gone after the level change");
            }
        }

        public int RegisterDroppedItem(Item item)
        {
            if (item == null || !RakeMultiplayerPlugin.isServer) return 0;

            int id = _nextItemNetworkId;
            while (_networkedItems.ContainsKey(id)) id++;
            _nextItemNetworkId = id + 1;

            RegisterItem(item, id);
            _droppedItemIds.Add(id);
            return id;
        }

// === Stage 3.5: Client Item Destroyed Handler ===
        private void HandleClientItemDestroyed(ItemDestroyedPacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo(
                $"[3.5] Client received ItemDestroyed: itemId={packet.ItemNetworkId}, pickerId={packet.PickerId}, type={packet.ItemType}");

            Item item = null;
            if (!_networkedItems.TryGetValue(packet.ItemNetworkId, out item) || item == null)
            {
                // Fall back to the object remembered when this client sent the request
                _pendingPickups.TryGetValue(packet.ItemNetworkId, out item);
                if (item == null)
                {
                    RakeMultiplayerPlugin.Log?.LogWarning(
                        $"[3.5] Client has no local object for item {packet.ItemNetworkId}; applying '{packet.ItemType}' by type");
                }
            }
            _pendingPickups.Remove(packet.ItemNetworkId);

            // Remember it locally too: the new level rebuilds its items, and a client has no
            // other way to know what it already took (the host only broadcasts on pickup).
            if (packet.ItemNetworkId > 0) _consumedItemIds.Add(packet.ItemNetworkId);

            string itemType = !string.IsNullOrEmpty(packet.ItemType)
                ? packet.ItemType
                : (item != null ? item.GetType().Name : string.Empty);

            // Remove registries first (the vanilla pickup may destroy the object)
            if (item != null)
            {
                _networkedItems.Remove(packet.ItemNetworkId);
                _itemToNetworkId.Remove(item);
            }

            int localPlayerId = PlayerSpawner.Instance?.LocalPlayer?.PlayerId ?? 1;
            bool isPicker = packet.PickerId == localPlayerId;

            if (isPicker)
            {
                // Force the local player's WeaponManager: a remote clone must never
                // become WeaponManager.instance (otherwise the weapon lands on the clone
                // and the local HUD says "You do not have ...").
                var localPlayer = PlayerSpawner.Instance != null ? PlayerSpawner.Instance.LocalPlayer : null;
                var localWm = localPlayer != null ? localPlayer.GetComponentInChildren<WeaponManager>() : null;
                if (localWm != null)
                {
                    WeaponManager.instance = localWm;
                }
                if (localPlayer != null)
                {
                    FPSController.instance = localPlayer.GetComponent<FPSController>();
                }

                // Replay the exact vanilla pickup (weapon/ammo/hints/sounds). The network
                // flag lets our per-item prefix pass through the original override.
                RakeMultiplayerPlugin.IsApplyingNetworkDestroy = true;
                try
                {
                    if (item != null)
                    {
                        item.PickupItem();
                    }
                    else
                    {
                        // No world object available (id/scene desync) - grant by type
                        GrantItemEffectByType(itemType);
                    }

                    bool hasCamera = false;
                    try { hasCamera = localWm != null && localWm.CheckWeapon(2); } catch { }

                    // Safety net: a camera pickup must leave the local player with the
                    // camera weapon, otherwise the HUD shows ammo but pressing C says
                    // "You do not have camera".
                    if (itemType == "Camera_item" && localWm != null && !hasCamera)
                    {
                        try
                        {
                            if (localWm.allWeapons != null && localWm.allWeapons.Count > 2 &&
                                localWm.allWeapons[2] != null)
                            {
                                localWm.AddWeapon(2, 1, true);
                                hasCamera = localWm.CheckWeapon(2);
                                RakeMultiplayerPlugin.Log?.LogWarning(
                                    "[3.5] Camera weapon was missing after replay - re-added to local player");
                            }
                        }
                        catch (Exception weaponEx)
                        {
                            RakeMultiplayerPlugin.Log?.LogError($"[3.5] Camera weapon repair failed: {weaponEx.Message}");
                        }
                    }

                    RakeMultiplayerPlugin.Log?.LogInfo(
                        $"[3.5] Pickup replay {itemType} -> local WM (camera weapon={hasCamera})");
                }
                catch (Exception ex)
                {
                    RakeMultiplayerPlugin.Log?.LogError($"[3.5] Client pickup replay failed ({itemType}): {ex.Message}");
                    GrantItemEffectByType(itemType);
                }
                finally
                {
                    RakeMultiplayerPlugin.IsApplyingNetworkDestroy = false;
                }
            }

            // Make sure the world object disappears even if the replay did not destroy it
            if (item != null)
            {
                RakeMultiplayerPlugin.IsApplyingNetworkDestroy = true;
                try { UnityEngine.Object.Destroy(item.gameObject); }
                finally { RakeMultiplayerPlugin.IsApplyingNetworkDestroy = false; }
            }

            RakeMultiplayerPlugin.Log?.LogInfo($"[3.5] Client applied pickup {packet.ItemNetworkId} (picker={packet.PickerId}, local={localPlayerId})");
        }

        /// <summary>
        /// Server: synchronize the remaining camera count of a box (or remove it when empty).
        /// </summary>
        public void ServerAuthorizeBoxPickup(int itemNetworkId, int pickerId, int remaining, bool active)
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            if (!active)
            {
                // Remember that this box is gone for good: without the consumed flag the
                // next level recreated it full of cameras (N10, I-ITEM-3).
                if (!_consumedItemIds.Contains(itemNetworkId)) _consumedItemIds.Add(itemNetworkId);

                _networkedItems.Remove(itemNetworkId);
                foreach (var kvp in _itemToNetworkId)
                {
                    if (kvp.Value == itemNetworkId)
                    {
                        _itemToNetworkId.Remove(kvp.Key);
                        break;
                    }
                }
            }

            var packet = new ItemStatePacket
            {
                ItemNetworkId = itemNetworkId,
                RemainingCount = remaining,
                IsActive = active,
                PickerId = pickerId,
                ItemType = "BoxOfCameras_item"
            };
            ServerBroadcast(packet.Serialize());
        }

        private void HandleClientItemState(ItemStatePacket packet)
        {
            if (RakeMultiplayerPlugin.isServer) return;

            Item item = null;
            if (!_networkedItems.TryGetValue(packet.ItemNetworkId, out item) || item == null)
            {
                _pendingPickups.TryGetValue(packet.ItemNetworkId, out item);
            }
            _pendingPickups.Remove(packet.ItemNetworkId);

            int localPlayerId = PlayerSpawner.Instance?.LocalPlayer?.PlayerId ?? 1;
            bool isPicker = packet.PickerId != 0 && packet.PickerId == localPlayerId;

            if (item != null)
            {
                item.count = packet.RemainingCount;
                var box = item as BoxOfCameras_item;
                if (box != null && box.obj != null) box.obj.SetActive(packet.IsActive);

                if (isPicker)
                {
                    var localPlayer = PlayerSpawner.Instance != null ? PlayerSpawner.Instance.LocalPlayer : null;
                    var localWm = localPlayer != null ? localPlayer.GetComponentInChildren<WeaponManager>() : null;
                    if (localWm != null) WeaponManager.instance = localWm;
                    if (localPlayer != null) FPSController.instance = localPlayer.GetComponent<FPSController>();

                    RakeMultiplayerPlugin.IsApplyingNetworkDestroy = true;
                    try { item.PickupItem(); }
                    catch (Exception ex)
                    {
                        RakeMultiplayerPlugin.Log?.LogError($"[3.5] Box pickup replay failed: {ex.Message}");
                        GrantItemEffectByType(packet.ItemType);
                    }
                    finally { RakeMultiplayerPlugin.IsApplyingNetworkDestroy = false; }

                    // Reapply the authoritative count (the vanilla replay decremented it again)
                    item.count = packet.RemainingCount;
                    if (box != null && box.obj != null) box.obj.SetActive(packet.IsActive);
                }

                if (!packet.IsActive)
                {
                    // Keep the client's own view consistent with the host (N10).
                    if (!_consumedItemIds.Contains(packet.ItemNetworkId))
                    {
                        _consumedItemIds.Add(packet.ItemNetworkId);
                    }
                    _networkedItems.Remove(packet.ItemNetworkId);
                    _itemToNetworkId.Remove(item);
                }
            }
            else if (isPicker)
            {
                // Local object is gone/desynced - still grant the camera
                GrantItemEffectByType(string.IsNullOrEmpty(packet.ItemType) ? "BoxOfCameras_item" : packet.ItemType);
            }

            RakeMultiplayerPlugin.Log?.LogInfo(
                $"[3.5] BoxState {packet.ItemNetworkId}: remaining={packet.RemainingCount}, active={packet.IsActive}, picker={packet.PickerId}, local={localPlayerId}");
        }

        private void HandleClientItemPickupConfirm(ItemPickupConfirmPacket packet)
        {
            if (packet.Success)
            {
                RakeMultiplayerPlugin.Log?.LogInfo($"[3.3] Client: pickup {packet.ItemNetworkId} authorized for player {packet.PlayerId}");
                return;
            }

            _pendingPickups.Remove(packet.ItemNetworkId);

            RakeMultiplayerPlugin.Log?.LogWarning(
                $"[3.3] Client: pickup {packet.ItemNetworkId} REJECTED for player {packet.PlayerId}: {packet.Reason}");

            switch (packet.Reason)
            {
                case "CameraLimit":
                    RakeMultiplayerPlugin.Log?.LogInfo(
                        $"[3.3] Cannot place more than {NetworkedCamera.MaxCameras} cameras at once");
                    break;

                case "NotFound":
                case "NoPosition":
                    // The server has no such item (already taken). Remove the local ghost,
                    // otherwise it stays on the ground forever as an unpickable prop.
                    RemoveLocalGhostItem(packet.ItemNetworkId);
                    break;

                default:
                    RakeMultiplayerPlugin.Log?.LogInfo($"[3.3] Pickup rejected: {packet.Reason}");
                    break;
            }

            OnPickupRejected?.Invoke(packet.PlayerId, packet.Reason);
        }

        private void RemoveLocalGhostItem(int itemNetworkId)
        {
            Item ghost;
            if (!_networkedItems.TryGetValue(itemNetworkId, out ghost) || ghost == null) return;

            _networkedItems.Remove(itemNetworkId);
            _itemToNetworkId.Remove(ghost);

            RakeMultiplayerPlugin.IsApplyingNetworkDestroy = true;
            try
            {
                Destroy(ghost.gameObject);
            }
            finally
            {
                RakeMultiplayerPlugin.IsApplyingNetworkDestroy = false;
            }

            RakeMultiplayerPlugin.Log?.LogInfo("[3.5] Removed ghost item " + itemNetworkId);
        }

        /// <summary>
        /// Mirror the server-side item effect locally for the picking client
        /// (clients block the original PickupItem, so this is the only place the
        /// weapon/ammo actually reaches a remote player).
        /// </summary>
        private void GrantItemEffectLocally(Item item)
        {
            if (item != null) GrantItemEffectByType(item.GetType().Name);
        }

        private void GrantItemEffectByType(string itemType)
        {
            if (string.IsNullOrEmpty(itemType)) return;

            var weaponManager = PlayerSpawner.Instance?.LocalPlayer?.GetComponentInChildren<WeaponManager>();
            if (weaponManager == null) return;

            int weaponId = 0;
            int ammoCount = 1;
            bool isNewWeapon = false;

            switch (itemType)
            {
                case "Medkit_item": weaponId = 8; isNewWeapon = !weaponManager.CheckWeapon(weaponId); break;
                case "Rifle_item": weaponId = 1; ammoCount = 2; isNewWeapon = !weaponManager.CheckWeapon(weaponId); break;
                case "RifleAmmo_item": weaponId = 1; ammoCount = 2; isNewWeapon = false; break;
                case "SniperRifle_item": weaponId = 6; isNewWeapon = !weaponManager.CheckWeapon(weaponId); break;
                case "SniperAmmo_item": weaponId = 6; ammoCount = 2; isNewWeapon = false; break;
                case "Trap_item": weaponId = 4; isNewWeapon = !weaponManager.CheckWeapon(weaponId); break;
                case "Camera_item": weaponId = 2; ammoCount = 1; isNewWeapon = !weaponManager.CheckWeapon(weaponId); break;
                case "BoxOfCameras_item": weaponId = 2; ammoCount = 1; isNewWeapon = !weaponManager.CheckWeapon(weaponId); break;
                case "Binocular_weapon":
                case "Binocular_item": weaponId = 7; isNewWeapon = !weaponManager.CheckWeapon(weaponId); break;
                case "MapCase_weapon":
                case "MapCase_item": weaponId = 3; isNewWeapon = !weaponManager.CheckWeapon(weaponId); break;
                case "Food_item": weaponId = 5; isNewWeapon = !weaponManager.CheckWeapon(weaponId); break;
                default:
                    RakeMultiplayerPlugin.Log?.LogWarning($"[3.5] Client: unknown item type for local grant: {itemType}");
                    break;
            }

            if (weaponId <= 0) return;

            if (isNewWeapon)
            {
                weaponManager.AddWeapon(weaponId, ammoCount, true);
                RakeMultiplayerPlugin.Log?.LogInfo($"[3.5] Client granted weapon {weaponId} (ammo {ammoCount})");
            }
            else
            {
                weaponManager.AddAmmo(weaponId, ammoCount);
                RakeMultiplayerPlugin.Log?.LogInfo($"[3.5] Client granted ammo {ammoCount} for weapon {weaponId}");
            }
        }

// === Item Registry (Stage 3.5) ===
        /// <summary>
        /// Get deterministic network ID for an item.
        /// Uses assigned sequential index (set during RegisterItem) to guarantee
        /// identical IDs on host and client without float precision issues.
        /// </summary>
        public int GetItemNetworkId(Item item)
        {
            if (item == null) return 0;
            if (_itemToNetworkId.TryGetValue(item, out int id))
                return id;

            // Not registered (e.g. spawned by a script after scene init): let vanilla
            // handle it instead of inventing a non-deterministic hash id.
            return 0;
        }

        /// <summary>
        /// Register an item with the next available deterministic network ID.
        /// Call this on BOTH host and client during scene initialization (GameEntryPatch).
        /// Items are assigned sequential IDs based on FindObjectsOfType order.
        /// </summary>
        public void RegisterItem(Item item, int preferredId = -1)
        {
            if (item == null) return;

            // Если у предмета уже есть назначенный ID (например, при регистрации с preferredId), используем его
            if (_itemToNetworkId.TryGetValue(item, out int existingId))
            {
                if (!_networkedItems.ContainsKey(existingId))
                    _networkedItems[existingId] = item;
                RakeMultiplayerPlugin.Log?.LogInfo($"[3.5] Item already registered: {item.GetType().Name} (ID={existingId})");
                return;
            }

            int id;
            if (preferredId > 0)
            {
                id = preferredId;
                // Убеждаемся, что следующий ID не конфликтует
                if (_nextItemNetworkId <= id) _nextItemNetworkId = id + 1;
            }
            else
            {
                id = _nextItemNetworkId++;
            }

            if (!_networkedItems.ContainsKey(id))
            {
                _networkedItems[id] = item;
                _itemToNetworkId[item] = id;
                RakeMultiplayerPlugin.Log?.LogInfo($"[3.5] Registered networked item: {item.GetType().Name} (ID={id})");
            }
            else
            {
                // Редкий случай коллизии — ищем свободный
                while (_networkedItems.ContainsKey(_nextItemNetworkId))
                    _nextItemNetworkId++;
                id = _nextItemNetworkId++;
                _networkedItems[id] = item;
                _itemToNetworkId[item] = id;
                RakeMultiplayerPlugin.Log?.LogWarning($"[3.5] ID collision resolved for {item.GetType().Name}, new ID={id}");
            }
        }

        public void UnregisterItem(Item item)
        {
            if (item == null) return;
            if (_itemToNetworkId.TryGetValue(item, out int id))
            {
                _networkedItems.Remove(id);
                _itemToNetworkId.Remove(item);
                RakeMultiplayerPlugin.Log?.LogInfo($"[3.5] Unregistered networked item: {item.GetType().Name} (ID={id})");
            }
        }
    }
}
