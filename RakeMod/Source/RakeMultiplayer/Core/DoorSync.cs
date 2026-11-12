using System.Collections.Generic;
using UnityEngine;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    /// <summary>
    /// Syncs Door_obj open/close. Doors are scene objects on every peer, so they only
    /// need a stable id (sorted by position then name, like items) and an action replay.
    /// </summary>
    internal static class DoorSync
    {
        private static readonly Dictionary<int, Door_obj> _byId = new Dictionary<int, Door_obj>();
        private static readonly Dictionary<Door_obj, int> _idByDoor = new Dictionary<Door_obj, int>();

        /// <summary>True while a remote action is being replayed (never re-broadcast).</summary>
        public static bool IsApplying { get; private set; }

        public static void RegisterAll()
        {
            _byId.Clear();
            _idByDoor.Clear();

            var doors = UnityEngine.Object.FindObjectsOfType<Door_obj>();
            System.Array.Sort(doors, (a, b) =>
            {
                if (a == null || b == null) return 0;
                var pa = a.transform.position;
                var pb = b.transform.position;
                int cmp = pa.x.CompareTo(pb.x); if (cmp != 0) return cmp;
                cmp = pa.y.CompareTo(pb.y); if (cmp != 0) return cmp;
                cmp = pa.z.CompareTo(pb.z); if (cmp != 0) return cmp;
                return string.CompareOrdinal(a.gameObject.name, b.gameObject.name);
            });

            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] == null) continue;
                int id = i + 1;
                _byId[id] = doors[i];
                _idByDoor[doors[i]] = id;
            }

            RakeMultiplayerPlugin.Log?.LogInfo("[Door] Registered " + _byId.Count + " doors");
        }

        /// <summary>The local player used a door: tell everyone else.</summary>
        public static void OnLocalAction(Door_obj door)
        {
            if (IsApplying || door == null) return;
            if (!RakeMultiplayerPlugin.isConnected) return;

            int id;
            if (!_idByDoor.TryGetValue(door, out id)) return;

            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;

            var packet = new DoorActionPacket
            {
                DoorId = id,
                SenderId = local != null ? local.PlayerId : (RakeMultiplayerPlugin.isServer ? 1 : 0)
            };

            var manager = RakeNetworkManager.Instance;
            if (manager == null) return;

            if (RakeMultiplayerPlugin.isServer) manager.ServerBroadcast(packet.Serialize());
            else manager.SendToServer(packet.Serialize());
        }

        /// <summary>Server: a client used a door. Apply here and relay to everyone.</summary>
        public static void HandleServerRequest(DoorActionPacket packet)
        {
            ApplyLocal(packet.DoorId);
            RakeNetworkManager.Instance?.ServerBroadcast(packet.Serialize());
        }

        /// <summary>Client: another peer used a door.</summary>
        public static void HandleBroadcast(DoorActionPacket packet)
        {
            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;
            if (local != null && local.PlayerId == packet.SenderId) return; // already applied locally

            ApplyLocal(packet.DoorId);
        }

        private static void ApplyLocal(int doorId)
        {
            Door_obj door;
            if (!_byId.TryGetValue(doorId, out door) || door == null) return;

            IsApplying = true;
            try { door.Action(); }
            finally { IsApplying = false; }
        }
    }
}