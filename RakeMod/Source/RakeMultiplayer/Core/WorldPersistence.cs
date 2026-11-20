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
            /// <summary>A sprung trap stays spent across the phase change (I-PHASE-1).</summary>
            public bool Triggered;
        }

        private static readonly Dictionary<uint, CameraState> _cameraStates = new Dictionary<uint, CameraState>();
        private static readonly List<TrapState> _trapStates = new List<TrapState>();
        private static readonly Dictionary<int, List<int>> _inventories = new Dictionary<int, List<int>>();
        private static float _rakeHealth = -1f;

        /// <summary>Item ids that were already picked up. The level rebuilds its items on every load,
        /// so without this list everything you picked up came back at dawn.</summary>
        private static readonly List<int> _consumedItems = new List<int>();

        // One restore per loaded level, no matter which entry point gets there first
        // (I-PHASE-3: GameEntryPatch and MultiplayerSceneManager used to race, and the
        // snapshot is one-shot, so the loser silently restored nothing).
        private static int _restoredLevel = -1;
        private static bool _restoreDone;

        // Same idea for the Rake's carried-over health: it can only be applied once a body
        // exists in the new scene, which is not true at the first entry point.
        private static int _rakeHealthAppliedLevel = -1;

        /// <summary>
        /// Apply the world snapshot for the level that is loaded now, exactly once.
        /// Safe to call from every scene-entry hook: the second call is a no-op.
        /// </summary>
        public static void EnsureWorldRestored(string where)
        {
            int level = Application.loadedLevel;
            if (level < 1) return;
            if (_restoreDone && _restoredLevel == level) return;

            _restoredLevel = level;
            _restoreDone = true;

            ApplyConsumedItems();
            if (RakeMultiplayerPlugin.isServer) RestoreWorld();

            RakeMultiplayerPlugin.Log?.LogInfo("[4.4] World snapshot applied for level " + level +
                                               " (" + where + ")");
        }

        /// <summary>
        /// Put the freshly built Rake back on the health it had before the phase change.
        /// Called from both scene-entry hooks: whichever runs after the body exists applies
        /// it, the other one is a no-op.
        /// </summary>
        public static void ApplySavedRakeHealth(string where)
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            int level = Application.loadedLevel;
            if (level < 1 || _rakeHealthAppliedLevel == level) return;
            if (!HasSavedRakeHealth) return;

            var rake = Object.FindObjectOfType<NetworkedRakeAI>();
            if (rake == null) return; // a later call in this scene will find it

            float health = SavedRakeHealth;
            rake.ApplyHealth(health);
            _rakeHealthAppliedLevel = level;

            RakeMultiplayerPlugin.Log?.LogInfo("[4.5] Rake health carried over: " +
                                               health.ToString("F1") + " (" + where + ")");
        }

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
                    Radius = trap.TriggerRadius,
                    Triggered = trap.IsTriggered
                });
            }

            // Also persist host-placed vanilla traps (they have no NetworkedTrap zone).
            // Skip the ones a restored zone already covers: re-adding them on every save
            // doubled the trap count on each phase change (I-PHASE-1 / I-PHASE-4).
            var levelItems = LevelItemsManager.instance;
            if (levelItems != null && levelItems.trapObjects != null)
            {
                foreach (var trapObj in levelItems.trapObjects)
                {
                    if (trapObj == null) continue;

                    Vector3 position = trapObj.transform.position;
                    if (HasTrapAt(position)) continue;

                    // Read the real state: the vanilla trap is spent when bSetup is false
                    // (GamePatches clears it on trigger). Hardcoding false brought a sprung
                    // trap back armed after dawn (N9/N15, I-PHASE-1).
                    var trapProj = trapObj.GetComponent<Trap_proj>() ??
                                   trapObj.GetComponentInChildren<Trap_proj>();
                    bool triggered = trapProj != null && !trapProj.bSetup;

                    _trapStates.Add(new TrapState
                    {
                        Position = position,
                        Radius = NetworkedTrap.DefaultTriggerRadius,
                        Triggered = triggered
                    });
                }
            }

            // The live health of the Rake, not the session bookkeeping value: on the host
            // SessionManager.RakeHealth only changes when the Rake dies, so persisting it
            // healed (or nearly killed) the Rake on every nightfall.
            _rakeHealth = ReadLiveRakeHealth();

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
                    var zone = NetworkedTrap.SpawnServerTrap(trap.Position, trap.Radius);
                    // A trap that was already sprung must come back spent, not armed.
                    if (zone != null && trap.Triggered) zone.RestoreTriggeredState(true);
                }
                _trapStates.Clear(); // avoid duplicating traps on repeated restores
            }

            // Snapshot is one-shot: do not re-apply a stale state later
            _cameraStates.Clear();

            RakeMultiplayerPlugin.Log?.LogInfo($"[4.4] World restored and snapshot cleared");
        }

        /// <summary>Minimum distance that separates two different trap positions.</summary>
        private const float TrapDedupeDistance = 0.5f;

        /// <summary>True when the snapshot already holds a trap at this position.</summary>
        private static bool HasTrapAt(Vector3 position)
        {
            for (int i = 0; i < _trapStates.Count; i++)
            {
                if (Vector3.Distance(_trapStates[i].Position, position) <= TrapDedupeDistance) return true;
            }
            return false;
        }

        /// <summary>Health of the Rake that is actually standing in the level right now.</summary>
        private static float ReadLiveRakeHealth()
        {
            var rake = Object.FindObjectOfType<NetworkedRakeAI>();
            // A body that exists is the truth - including 0 (the Rake was killed).
            if (rake != null) return Mathf.Max(0f, rake.CurrentHealth);

            // No Rake in this scene (menu): keep the session value if any.
            return SessionManager.Instance != null ? SessionManager.Instance.RakeHealth : -1f;
        }

        /// <summary>Snapshot the local player's weapon ammo list before a scene change (Task 4.3).</summary>
        public static void SaveInventory(NetworkedPlayer player)
        {
            if (player == null) return;
            // PlayerId 0 means "not registered yet": the snapshot could never be restored.
            if (player.PlayerId <= 0) return;

            var weaponManager = player.GetComponentInChildren<WeaponManager>();
            if (weaponManager == null || weaponManager.weaponsAmmo == null) return;

            _inventories[player.PlayerId] = new List<int>(weaponManager.weaponsAmmo);
            RakeMultiplayerPlugin.Log?.LogInfo($"[4.3] Inventory saved for player {player.PlayerId}");
        }

        /// <summary>Restore the local player's weapon ammo list after a scene change.</summary>
        /// <summary>Remember which items are gone. Runs on every peer, before the level unloads.</summary>
        public static void SaveConsumedItems()
        {
            _consumedItems.Clear();

            var manager = RakeNetworkManager.Instance;
            if (manager != null) manager.CaptureConsumedItems(_consumedItems);
        }

        /// <summary>After the new level rebuilt its items, remove the ones already taken.</summary>
        public static void ApplyConsumedItems()
        {
            if (_consumedItems.Count == 0) return;

            var manager = RakeNetworkManager.Instance;
            if (manager != null) manager.RemoveSceneItems(_consumedItems);
        }

        public static void RestoreInventory(NetworkedPlayer player)
        {
            if (player == null) return;
            if (!_inventories.TryGetValue(player.PlayerId, out var ammo)) return;

            var weaponManager = player.GetComponentInChildren<WeaponManager>();
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

        /// <summary>True when a real Rake health value was captured for this transition.</summary>
        public static bool HasSavedRakeHealth => _rakeHealth > 0f;

        /// <summary>Remember the current Rake health without a full world snapshot.</summary>
        public static void SaveRakeHealth(float health)
        {
            if (health > 0f) _rakeHealth = health;
        }

        public static void Clear()
        {
            _cameraStates.Clear();
            _trapStates.Clear();
            _inventories.Clear();
            _consumedItems.Clear();
            _rakeHealth = -1f;
            _restoredLevel = -1;
            _restoreDone = false;
            _rakeHealthAppliedLevel = -1;
        }
    }
}
