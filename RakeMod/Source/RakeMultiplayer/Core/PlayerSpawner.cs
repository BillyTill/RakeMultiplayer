using System.Collections.Generic;
using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Handles spawning of players, Rake, and items
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        public static PlayerSpawner Instance { get; private set; }

        [Header("Prefabs")]
        public GameObject playerPrefab;
        public GameObject rakePrefab;
        public GameObject[] itemPrefabs;

        [Header("Spawn Points")]
        public Transform[] playerSpawnPoints;
        public Transform rakeSpawnPoint;
        public Transform[] itemSpawnPoints;

        // Remote players tracking
        private Dictionary<int, NetworkedPlayer> _remotePlayers = new Dictionary<int, NetworkedPlayer>();
        public NetworkedPlayer LocalPlayer { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Process any queued PlayerConnected packets from network manager
            if (RakeNetworkManager.Instance != null)
            {
                RakeNetworkManager.Instance.ProcessPendingConnects();
            }
        }

        public void RegisterLocalPlayer(NetworkedPlayer player)
        {
            LocalPlayer = player;
            
            if (RakeMultiplayerPlugin.isServer)
            {
                // Host: use existing scene player, assign ID 1
                player.SetAsLocalPlayer();
            }
            else
            {
                // Client: local player ID will be set when PlayerConnected packet arrives
                player.SetAsLocalPlayer();
            }
        }

        public void SpawnRemotePlayer(int playerId, Vector3 spawnPosition)
        {
            if (_remotePlayers.ContainsKey(playerId))
            {
                RakeMultiplayerPlugin.Log?.LogWarning($"[3.3] Remote player {playerId} already spawned");
                return;
            }

            // Use playerPrefab directly (Variant A: no caching to avoid initialization order issues)
            if (playerPrefab == null)
            {
                RakeMultiplayerPlugin.Log?.LogError($"[3.3] playerPrefab is null — cannot spawn remote player {playerId}");
                return;
            }

            var playerObj = (GameObject)Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            playerObj.name = $"RemotePlayer_{playerId}";

            // ===== КРИТИЧЕСКИЙ ФИКС СИНГЛТОНОВ =====
            // Отключаем FPSController и WeaponManager ДО их Start().
            // Awake() уже отработал при Instantiate(), но Start() вызовется только на первом кадре
            // для ВКЛЮЧЁННЫХ компонентов. Отключая их сейчас — предотвращаем перезапись instance.
            var fps = playerObj.GetComponent<FPSController>();
            if (fps != null)
            {
                fps.enabled = false;
                RakeMultiplayerPlugin.Log?.LogInfo($"[3.3] Disabled FPSController on remote player {playerId} BEFORE Start()");
            }

            var wm = playerObj.GetComponent<WeaponManager>();
            if (wm != null)
            {
                wm.enabled = false;
                RakeMultiplayerPlugin.Log?.LogInfo($"[3.3] Disabled WeaponManager on remote player {playerId} BEFORE Start()");
            }

            var pm = playerObj.GetComponent<PlayerMovement>();
            if (pm != null)
            {
                pm.enabled = false;
            }

            // Reset Animator to default state (idle/grounded) to avoid first-frame visual glitch
            var anim = playerObj.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.SetBool("Run", false);
                anim.SetBool("Crouch", false);
                anim.SetBool("Aim", false);
                anim.SetBool("Grounded", true);
            }
            
            // DEBUG: Add visible capsule so we can see remote player position
            var debugCapsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            debugCapsule.name = "DebugCapsule";
            debugCapsule.transform.SetParent(playerObj.transform, false);
            debugCapsule.transform.localPosition = Vector3.up; // At eye level
            UnityEngine.Object.Destroy(debugCapsule.GetComponent<Collider>()); // Disable physics
            RakeMultiplayerPlugin.Log?.LogInfo("[3.5] Spawned DEBUG CAPSULE for remote player!");
            
            var netPlayer = playerObj.GetComponent<NetworkedPlayer>();
            if (netPlayer == null)
            {
                netPlayer = playerObj.AddComponent<NetworkedPlayer>();
            }

            netPlayer.SetPlayerId(playerId);
            netPlayer.SetAsRemotePlayer(); // Отключит рендеры, камеры, руки

            _remotePlayers[playerId] = netPlayer;
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.3] Spawned remote player {playerId} at {spawnPosition}");
        }

        public bool TryGetRemotePlayer(int playerId, out NetworkedPlayer player)
        {
            return _remotePlayers.TryGetValue(playerId, out player);
        }

        public bool RemoveRemotePlayer(int playerId)
        {
            if (_remotePlayers.TryGetValue(playerId, out var player))
            {
                Destroy(player.gameObject);
                _remotePlayers.Remove(playerId);
                RakeMultiplayerPlugin.Log?.LogInfo($"[3.3] Removed remote player {playerId}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Get transforms of all remote players for AI targeting
        /// </summary>
        public IEnumerable<Transform> GetAllRemotePlayerTransforms()
        {
            foreach (var kvp in _remotePlayers)
            {
                if (kvp.Value != null && kvp.Value.transform != null)
                    yield return kvp.Value.transform;
            }
        }

        public void SpawnPlayerForConnection(NetworkedPlayer playerPrefab, int connectionId)
        {
            Vector3 spawnPos = GetPlayerSpawnPoint(connectionId);
            GameObject playerObj = (GameObject)UnityEngine.Object.Instantiate(playerPrefab.gameObject, spawnPos, Quaternion.identity);

            var netPlayer = playerObj.GetComponent<NetworkedPlayer>();
            if (netPlayer != null)
            {
                netPlayer.SetAsLocalPlayer();
            }
        }

        private MapSettings _mapSettings;
        private int _nightIndex;

        /// <summary>
        /// Apply per-night map settings (Task 4.2). Rotates spawn assignment by night
        /// so hunters do not always start on the same spot.
        /// </summary>
        public void ApplyMapSettings(MapSettings settings, int nightIndex)
        {
            _mapSettings = settings;
            _nightIndex = Mathf.Max(0, nightIndex);

            RakeMultiplayerPlugin.Log?.LogInfo(
                $"[4.2] PlayerSpawner using MapSettings for '{settings?.sceneName}' (night index {_nightIndex})");
        }

        private Vector3 GetPlayerSpawnPoint(int connectionId)
        {
            if (_mapSettings != null && _mapSettings.spawnPoints != null && _mapSettings.spawnPoints.Length > 0)
            {
                int index = (connectionId + _nightIndex) % _mapSettings.spawnPoints.Length;
                if (index < 0) index += _mapSettings.spawnPoints.Length;
                return _mapSettings.spawnPoints[index];
            }

            if (playerSpawnPoints != null && playerSpawnPoints.Length > 0)
            {
                int index = connectionId % playerSpawnPoints.Length;
                return playerSpawnPoints[index].position;
            }
            return Vector3.zero;
        }

        public void SpawnRake()
        {
            if (rakePrefab != null && rakeSpawnPoint != null)
            {
                GameObject rakeObj = (GameObject)UnityEngine.Object.Instantiate(rakePrefab, rakeSpawnPoint.position, rakeSpawnPoint.rotation);
                var netRake = rakeObj.GetComponent<NetworkedRakeAI>();
                if (netRake == null)
                {
                    netRake = rakeObj.AddComponent<NetworkedRakeAI>();
                }
            }
        }

        public void SpawnItems()
        {
            if (itemPrefabs != null && itemSpawnPoints != null)
            {
                for (int i = 0; i < Math.Min(itemPrefabs.Length, itemSpawnPoints.Length); i++)
                {
                    if (itemPrefabs[i] != null && itemSpawnPoints[i] != null)
                    {
                        GameObject itemObj = (GameObject)UnityEngine.Object.Instantiate(itemPrefabs[i], itemSpawnPoints[i].position, itemSpawnPoints[i].rotation);
                        var netItem = itemObj.GetComponent<NetworkedItem>();
                        if (netItem == null)
                        {
                            netItem = itemObj.AddComponent<NetworkedItem>();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Refresh player spawner state after scene load
        /// </summary>
        public void Refresh()
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[PlayerSpawner] Refreshed after scene load");
            // Clear remote players dictionary to prevent stale references
            _remotePlayers.Clear();
            LocalPlayer = null;
        }

        /// <summary>
        /// Get all remote players (read-only access for iteration)
        /// </summary>
        public System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int, NetworkedPlayer>> GetAllRemotePlayers()
        {
            return _remotePlayers;
        }
    }
}