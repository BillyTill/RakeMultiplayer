using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RakeMultiplayer
{
    /// <summary>
    /// Persists world state across day/night transitions and scene reloads (Task 4.4)
    /// and per-player inventories (Task 4.3).
    /// Cameras and traps stay in the same scene between nights, but the server can
    /// reload the scene; these snapshots make the transition lossless.
    /// </summary>
    public static class WorldPersistence
    {
        public class CameraState
        {
            public uint CameraId;
            public bool IsPlaced;
            public bool IsActive;
            public bool IsBroken;
            public float BatteryLevel;
            public int PlacedByPlayerId;
        }

        public class TrapState
        {
            public Vector3 Position;
            public float Radius;
        }

        private static readonly Dictionary<uint, CameraState> _cameraStates = new Dictionary<uint, CameraState>();
        private static readonly List<TrapState> _trapStates = new List<TrapState>();
        private static readonly Dictionary<int, List<int>> _inventories = new Dictionary<int, List<int>>();
        private static float _rakeHealth = -1f;

        /// <summary>Capture cameras, traps and Rake health (server only for world state).</summary>
        public static void SaveWorld()
        {
            _cameraStates.Clear();
            _trapStates.Clear();

            var cameras = Object.FindObjectsOfType<NetworkedCamera>();
            foreach (var cam in cameras)
            {
                if (cam == null) continue;
                _cameraStates[cam.CameraId] = new CameraState
                {
                    CameraId = cam.CameraId,
                    IsPlaced = cam.IsPlaced,
                    IsActive = cam.IsActive,
                    IsBroken = cam.IsBroken,
                    BatteryLevel = cam.BatteryLevel,
                    PlacedByPlayerId = cam.PlacedByPlayerId
                };
            }

            foreach (var trap in NetworkedTrap.GetAllTraps())
            {
                if (trap == null) continue;
                _trapStates.Add(new TrapState
                {
                    Position = trap.transform.position,
                    Radius = trap.TriggerRadius
                });
            }

            if (SessionManager.Instance != null)
            {
                _rakeHealth = SessionManager.Instance.RakeHealth;
            }

            RakeMultiplayerPlugin.Log?.LogInfo(
                $"[4.4] World saved: cameras={_cameraStates.Count}, traps={_trapStates.Count}, rakeHP={_rakeHealth:F1}");
        }

        /// <summary>Restore cameras/traps after a scene reload (server reconstructs traps).</summary>
        public static void RestoreWorld()
        {
            foreach (var kvp in _cameraStates)
            {
                var state = kvp.Value;
                if (!NetworkedCamera.TryGetCamera(state.CameraId, out var cam) || cam == null) continue;

                if (state.IsPlaced && !cam.IsPlaced)
                {
                    cam.RestoreState(state.IsActive, state.IsBroken, state.BatteryLevel, state.PlacedByPlayerId);
                }
            }

            if (RakeMultiplayerPlugin.isServer)
            {
                foreach (var trap in _trapStates)
                {
                    NetworkedTrap.SpawnServerTrap(trap.Position, trap.Radius);
                }
            }

            RakeMultiplayerPlugin.Log?.LogInfo(
                $"[4.4] World restored: cameras={_cameraStates.Count}, traps={_trapStates.Count}");
        }

        /// <summary>Snapshot the local player's weapon ammo list before a scene change (Task 4.3).</summary>
        public static void SaveInventory(NetworkedPlayer player)
        {
            if (player == null) return;

            var weaponManager = player.GetComponent<WeaponManager>();
            if (weaponManager == null || weaponManager.weaponsAmmo == null) return;

            _inventories[player.PlayerId] = new List<int>(weaponManager.weaponsAmmo);
            RakeMultiplayerPlugin.Log?.LogInfo($"[4.3] Inventory saved for player {player.PlayerId}");
        }

        /// <summary>Restore the local player's weapon ammo list after a scene change.</summary>
        public static void RestoreInventory(NetworkedPlayer player)
        {
            if (player == null) return;
            if (!_inventories.TryGetValue(player.PlayerId, out var ammo)) return;

            var weaponManager = player.GetComponent<WeaponManager>();
            if (weaponManager == null) return;

            if (weaponManager.weaponsAmmo == null || weaponManager.weaponsAmmo.Count != ammo.Count)
            {
                weaponManager.weaponsAmmo = new List<int>(ammo);
            }
            else
            {
                for (int i = 0; i < ammo.Count; i++)
                {
                    weaponManager.weaponsAmmo[i] = ammo[i];
                }
            }

            RakeMultiplayerPlugin.Log?.LogInfo($"[4.3] Inventory restored for player {player.PlayerId}");
        }

        /// <summary>Rake health captured before a scene reload.</summary>
        public static float SavedRakeHealth => _rakeHealth;

        public static void Clear()
        {
            _cameraStates.Clear();
            _trapStates.Clear();
            _inventories.Clear();
            _rakeHealth = -1f;
        }
    }
}
