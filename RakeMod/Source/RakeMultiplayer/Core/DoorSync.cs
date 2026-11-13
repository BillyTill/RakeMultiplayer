using System.Collections.Generic;
using UnityEngine;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    /// <summary>
    /// Syncs Door_obj open/close. Doors are scene objects on every peer, so they only
    /// need a stable id (sorted by position then name, like items) and the *target state*.
    ///
    /// The old protocol replayed "Action()" and nothing else, so two fast presses, a press
    /// during the 1 s animation, or a late joiner produced a permanently desynced door.
    /// </summary>
    internal static class DoorSync
    {
        private static readonly Dictionary<int, Door_obj> _byId = new Dictionary<int, Door_obj>();
        private static readonly Dictionary<Door_obj, int> _idByDoor = new Dictionary<Door_obj, int>();

        // doorId -> wanted state, applied as soon as the door finishes its animation
        private static readonly Dictionary<int, KeyValuePair<int, float>> _pending =
            new Dictionary<int, KeyValuePair<int, float>>();

        /// <summary>True while a remote action is being replayed (never re-broadcast).</summary>
        public static bool IsApplying { get; private set; }

        public static void RegisterAll()
        {
            _byId.Clear();
            _idByDoor.Clear();
            _pending.Clear();

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

        /// <summary>The local player used a door: send the resulting state.</summary>
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
                SenderId = local != null ? local.PlayerId : (RakeMultiplayerPlugin.isServer ? 1 : 0),
                // Action() flips doorState inside a 0.5 s coroutine, so at postfix time
                // doorState is still the OLD value. Send the state it is moving to.
                DoorState = (int)(door.doorState == Door_obj.Door_state.Close
                    ? Door_obj.Door_state.Open
                    : Door_obj.Door_state.Close)
            };

            var manager = RakeNetworkManager.Instance;
            if (manager == null) return;

            if (RakeMultiplayerPlugin.isServer) manager.ServerBroadcast(packet.Serialize());
            else manager.SendToServer(packet.Serialize());
        }

        /// <summary>Server: a client used a door. Apply here and relay to everyone.</summary>
        public static void HandleServerRequest(DoorActionPacket packet)
        {
            ApplyState(packet.DoorId, packet.DoorState);
            RakeNetworkManager.Instance?.ServerBroadcast(packet.Serialize());
        }

        /// <summary>Client: another peer used a door.</summary>
        public static void HandleBroadcast(DoorActionPacket packet)
        {
            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;
            if (local != null && local.PlayerId != 0 && local.PlayerId == packet.SenderId) return;

            ApplyState(packet.DoorId, packet.DoorState);
        }

        /// <summary>Late join: the server sends the current state of every door.</summary>
        public static void SendSnapshotToPlayer(int playerId)
        {
            var manager = RakeNetworkManager.Instance;
            if (manager == null) return;

            foreach (var pair in _byId)
            {
                if (pair.Value == null) continue;
                var packet = new DoorActionPacket
                {
                    DoorId = pair.Key,
                    SenderId = 1,
                    DoorState = (int)pair.Value.doorState
                };
                manager.ServerSendToPlayer(playerId, packet.Serialize());
            }
        }

        /// <summary>Retry pending states (Action is ignored while the door animates).</summary>
        public static void Update()
        {
            if (_pending.Count == 0) return;

            var done = new List<int>();
            var keys = new List<int>(_pending.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                int doorId = keys[i];
                Door_obj door;
                if (!_byId.TryGetValue(doorId, out door) || door == null)
                {
                    done.Add(doorId);
                    continue;
                }

                int wanted = _pending[doorId].Key;
                float deadline = _pending[doorId].Value;

                if ((int)door.doorState == wanted)
                {
                    done.Add(doorId);
                    continue;
                }

                if (Time.time > deadline)
                {
                    done.Add(doorId);
                    RakeMultiplayerPlugin.Log?.LogWarning("[Door] state sync timed out for door " + doorId);
                    continue;
                }

                IsApplying = true;
                try { door.Action(); }
                finally { IsApplying = false; }
            }

            for (int i = 0; i < done.Count; i++) _pending.Remove(done[i]);
        }

        private static void ApplyState(int doorId, int state)
        {
            Door_obj door;
            if (!_byId.TryGetValue(doorId, out door) || door == null) return;

            if ((int)door.doorState == state && !_pending.ContainsKey(doorId)) return;

            _pending[doorId] = new KeyValuePair<int, float>(state, Time.time + 3f);
        }
    }
}
