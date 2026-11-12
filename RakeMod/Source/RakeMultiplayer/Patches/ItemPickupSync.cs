using System;
using HarmonyLib;
using UnityEngine;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer.Patches
{
    /// <summary>
    /// Network-aware item pickup for the real item classes.
    ///
    /// <c>Item.PickupItem</c> is virtual and EVERY concrete item overrides it, so a patch
    /// on the base method never intercepts actual pickups. Each override is patched here.
    ///
    /// Flow:
    ///  - Server/host: run vanilla pickup locally, then broadcast ItemDestroyed (postfix).
    ///  - Client: block vanilla pickup and request it from the server.
    ///  - Client receiving an authorized pickup: replay the vanilla pickup locally with
    ///    the network flag set, so the exact original effect (weapon/ammo/hints/sounds) runs.
    /// </summary>
    internal static class ItemPickupSync
    {
        // Unity defers Object.Destroy to the end of the frame, so a just-picked item is
        // NOT null yet. DestroyPatch marks consumed items here and the pickup postfix
        // consumes the mark to decide whether the vanilla pickup actually succeeded.
        private static readonly System.Collections.Generic.HashSet<int> _consumed =
            new System.Collections.Generic.HashSet<int>();

        private static readonly System.Collections.Generic.Dictionary<int, int> _boxCountBefore =
            new System.Collections.Generic.Dictionary<int, int>();

        internal static void MarkConsumed(int itemId)
        {
            if (itemId > 0) _consumed.Add(itemId);
        }

        internal static bool ConsumeMark(int itemId)
        {
            return itemId > 0 && _consumed.Remove(itemId);
        }

        internal static void ClearConsumed()
        {
            _consumed.Clear();
        }

        internal static int GetItemId(Item item)
        {
            if (item == null) return 0;
            var manager = RakeNetworkManager.Instance;
            return manager != null ? manager.GetItemNetworkId(item) : 0;
        }

        internal static bool BeforePickup(Item item)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return true;
            if (item == null) return true;

            // Authorized replay on the picking client - let the vanilla method run
            if (RakeMultiplayerPlugin.IsApplyingNetworkDestroy) return true;

            if (!RakeMultiplayerPlugin.isConnected) return true;

            var manager = RakeNetworkManager.Instance;
            if (manager == null) return true;

            int itemId = GetItemId(item);

            // Remember the count for stackable items (host only) to detect a real pickup
            if (RakeMultiplayerPlugin.isServer)
            {
                var boxBefore = item as BoxOfCameras_item;
                if (boxBefore != null && itemId > 0)
                {
                    _boxCountBefore[itemId] = boxBefore.count;
                }
            }

            if (itemId == 0)
            {
                RakeMultiplayerPlugin.Log?.LogWarning(
                    $"[Pickup] {item.GetType().Name} has no network ID - vanilla only");
                return true;
            }

            // Host: camera quota must stop a 6th camera before vanilla pickup runs
            if (RakeMultiplayerPlugin.isServer)
            {
                if (item.GetType().Name == "Camera_item" && !NetworkedCamera.CanPlaceCamera())
                {
                    try { InformationUI.instance?.DrawHint($"You cannot setup no more {NetworkedCamera.MaxCameras} cameras"); }
                    catch { /* UI may be absent */ }
                    return false;
                }

                // Execute vanilla pickup; the postfix broadcasts the removal
                return true;
            }

            // Client: block local pickup and request authorization
            var packet = new ItemPickupPacket
            {
                PlayerId = PlayerSpawner.Instance?.LocalPlayer?.PlayerId ?? 1,
                ItemNetworkId = itemId,
                ItemType = item.GetType().Name
            };
            manager.RememberPendingPickup(itemId, item);
            manager.ClientSend(packet.Serialize());
            RakeMultiplayerPlugin.Log?.LogInfo($"[Pickup] Client requested {item.GetType().Name} (id={itemId})");
            return false;
        }

        internal static void AfterPickup(Item item, int itemId)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (!RakeMultiplayerPlugin.isServer) return;
            if (!RakeMultiplayerPlugin.isConnected) return;
            if (RakeMultiplayerPlugin.IsApplyingNetworkDestroy) return;
            if (itemId <= 0) return;

            int pickerId = PlayerSpawner.Instance?.LocalPlayer?.PlayerId ?? 1;

            // Box of cameras is stackable: vanilla only decrements and hides its visual
            var box = item as BoxOfCameras_item;
            if (box != null)
            {
                int before;
                bool had = _boxCountBefore.TryGetValue(itemId, out before);
                _boxCountBefore.Remove(itemId);
                if (!had || before == box.count) return; // no real pickup (e.g. full ammo)

                bool active = box.count > 0;
                if (!active && box.obj != null) box.obj.SetActive(false);
                RakeNetworkManager.Instance?.ServerAuthorizeBoxPickup(itemId, pickerId, box.count, active);
                return;
            }

            // DestroyPatch marks the item when the vanilla pickup actually called Destroy
            // (e.g. "Full ammo" returns early and never destroys it).
            if (!ConsumeMark(itemId)) return;

            string itemType = item != null ? item.GetType().Name : string.Empty;
            RakeNetworkManager.Instance?.ServerAuthorizeItemPickup(itemId, pickerId, itemType);
        }

        // Shared prefix/postfix bodies used by the per-type patch classes below.
        internal static bool PrefixImpl(Item item, out int state)
        {
            state = GetItemId(item);
            return BeforePickup(item);
        }
    }

    // ==== Rifle ====
    [HarmonyPatch(typeof(Rifle_item), "PickupItem")]
    internal static class RifleItemPickupPatch
    {
        static bool Prefix(Rifle_item __instance, out int __state)
            => ItemPickupSync.PrefixImpl(__instance, out __state);
        static void Postfix(Rifle_item __instance, int __state)
            => ItemPickupSync.AfterPickup(__instance, __state);
    }

    // ==== Rifle ammo ====
    [HarmonyPatch(typeof(RifleAmmo_item), "PickupItem")]
    internal static class RifleAmmoItemPickupPatch
    {
        static bool Prefix(RifleAmmo_item __instance, out int __state)
            => ItemPickupSync.PrefixImpl(__instance, out __state);
        static void Postfix(RifleAmmo_item __instance, int __state)
            => ItemPickupSync.AfterPickup(__instance, __state);
    }

    // ==== Sniper rifle ====
    [HarmonyPatch(typeof(SniperRifle_item), "PickupItem")]
    internal static class SniperRifleItemPickupPatch
    {
        static bool Prefix(SniperRifle_item __instance, out int __state)
            => ItemPickupSync.PrefixImpl(__instance, out __state);
        static void Postfix(SniperRifle_item __instance, int __state)
            => ItemPickupSync.AfterPickup(__instance, __state);
    }

    // ==== Sniper ammo ====
    [HarmonyPatch(typeof(SniperAmmo_item), "PickupItem")]
    internal static class SniperAmmoItemPickupPatch
    {
        static bool Prefix(SniperAmmo_item __instance, out int __state)
            => ItemPickupSync.PrefixImpl(__instance, out __state);
        static void Postfix(SniperAmmo_item __instance, int __state)
            => ItemPickupSync.AfterPickup(__instance, __state);
    }

    // ==== Medkit ====
    [HarmonyPatch(typeof(Medkit_item), "PickupItem")]
    internal static class MedkitItemPickupPatch
    {
        static bool Prefix(Medkit_item __instance, out int __state)
            => ItemPickupSync.PrefixImpl(__instance, out __state);
        static void Postfix(Medkit_item __instance, int __state)
            => ItemPickupSync.AfterPickup(__instance, __state);
    }

    // ==== Food ====
    [HarmonyPatch(typeof(Food_item), "PickupItem")]
    internal static class FoodItemPickupPatch
    {
        static bool Prefix(Food_item __instance, out int __state)
            => ItemPickupSync.PrefixImpl(__instance, out __state);
        static void Postfix(Food_item __instance, int __state)
            => ItemPickupSync.AfterPickup(__instance, __state);
    }

    // ==== Camera ====
    [HarmonyPatch(typeof(Camera_item), "PickupItem")]
    internal static class CameraItemPickupPatch
    {
        static bool Prefix(Camera_item __instance, out int __state)
            => ItemPickupSync.PrefixImpl(__instance, out __state);
        static void Postfix(Camera_item __instance, int __state)
            => ItemPickupSync.AfterPickup(__instance, __state);
    }

    // ==== Box of cameras ====
    [HarmonyPatch(typeof(BoxOfCameras_item), "PickupItem")]
    internal static class BoxOfCamerasItemPickupPatch
    {
        static bool Prefix(BoxOfCameras_item __instance, out int __state)
            => ItemPickupSync.PrefixImpl(__instance, out __state);
        static void Postfix(BoxOfCameras_item __instance, int __state)
            => ItemPickupSync.AfterPickup(__instance, __state);
    }

    // ==== Trap ====
    [HarmonyPatch(typeof(Trap_item), "PickupItem")]
    internal static class TrapItemPickupPatch
    {
        static bool Prefix(Trap_item __instance, out int __state)
            => ItemPickupSync.PrefixImpl(__instance, out __state);
        static void Postfix(Trap_item __instance, int __state)
            => ItemPickupSync.AfterPickup(__instance, __state);
    }

    // ==== Tablet computer ====
    [HarmonyPatch(typeof(TabletComputer_item), "PickupItem")]
    internal static class TabletComputerItemPickupPatch
    {
        static bool Prefix(TabletComputer_item __instance, out int __state)
            => ItemPickupSync.PrefixImpl(__instance, out __state);
        static void Postfix(TabletComputer_item __instance, int __state)
            => ItemPickupSync.AfterPickup(__instance, __state);
    }
}
