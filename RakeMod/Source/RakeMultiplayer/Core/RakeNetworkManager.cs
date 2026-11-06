using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Telepathy;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    /// <summary>
    /// Custom network manager - simplified for Telepathy-based networking
    /// </summary>
    public class RakeNetworkManager : MonoBehaviour
    {
        public static RakeNetworkManager Instance { get; private set; }

        [Header("Player Prefabs")]
        public GameObject playerPrefab;

        [Header("Scene Names")]
        public string mainMenuScene = "MainMenu";
        public string lobbyScene = "Lobby";
        public string gameScene = "GameScene";

        // Server state
        private Telepathy.Server _server;
        private Dictionary<int, PlayerStatePacket> _playerStates = new Dictionary<int, PlayerStatePacket>();
        private HashSet<int> _connectedClients = new HashSet<int>();
        private Vector3 _hostSpawnPosition = Vector3.zero;
        private bool _hostSpawnPositionSet = false;

        // Client state
        private Telepathy.Client _client;

        // Client-side known players (for UI player count)
        private HashSet<int> _knownPlayerIds = new HashSet<int>();

        // Prevent duplicate scene loading on client
        private bool _clientSceneLoading = false;

        // Pending packets (queued until PlayerSpawner is ready)
        private Queue<PlayerConnectedPacket> _pendingConnects = new Queue<PlayerConnectedPacket>();

        // Cached references for performance (avoid FindObjectOfType in hot paths)
        private NetworkedRakeAI _cachedRake;
        private Enemy_AI _cachedEnemyAI;
        private float _cacheTimer = 0f;

        // Item registry for networked items (key = deterministic network ID)
        private Dictionary<int, Item> _networkedItems = new Dictionary<int, Item>();
        // Reverse lookup: Item -> assigned network ID (for consistent ID retrieval)
        private Dictionary<Item, int> _itemToNetworkId = new Dictionary<Item, int>();
        private int _nextItemNetworkId = 1;

        /// <summary>
        /// Get total known player count (server: connected clients + host; client: known players from packets)
        /// </summary>
        public int GetKnownPlayerCount()
        {
            if (RakeMultiplayerPlugin.isServer)
            {
                return _connectedClients.Count + 1; // +1 for host
            }
            else
            {
                return Math.Max(1, _knownPlayerIds.Count); // at least 1 (self)
            }
        }

        private void Awake()
        {
            Instance = this;
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
        }

        public void StartHost()
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[RakeNetworkManager] Starting host...");
            
            // ОБЯЗАТЕЛЬНО установить оба флага!
            RakeMultiplayerPlugin.IsServer.Value = true;
            RakeMultiplayerPlugin.isServer = true; // <--- КРИТИЧЕСКИЙ ФИКС

            _server = new Telepathy.Server();
            _server.NoDelay = true;

            if (_server.Start(RakeMultiplayerPlugin.NetworkPort.Value))
            {
                RakeMultiplayerPlugin.Log?.LogInfo($"Server started on port {RakeMultiplayerPlugin.NetworkPort.Value}");
                RakeMultiplayerPlugin.isConnected = true;
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogError("Failed to start server");
            }
            
            // Store host spawn position after local player is registered
            if (PlayerSpawner.Instance != null && PlayerSpawner.Instance.LocalPlayer != null)
            {
                _hostSpawnPosition = PlayerSpawner.Instance.LocalPlayer.transform.position;
                _hostSpawnPositionSet = true;
                RakeMultiplayerPlugin.Log?.LogInfo($"[Net] Host spawn position recorded: {_hostSpawnPosition}");
            }
        }

        public void StartClient()
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[RakeNetworkManager] Starting client...");
            
            // ОБЯЗАТЕЛЬНО установить оба флага!
            RakeMultiplayerPlugin.IsServer.Value = false;
            RakeMultiplayerPlugin.isServer = false; // <--- КРИТИЧЕСКИЙ ФИКС

            _client = new Telepathy.Client();
            _client.NoDelay = true;

            try
            {
                _client.Connect(RakeMultiplayerPlugin.NetworkAddress.Value, RakeMultiplayerPlugin.NetworkPort.Value);
            }
            catch (Exception ex)
            {
                RakeMultiplayerPlugin.Log?.LogError($"[Net] Client connect exception: {ex.Message}");
                return;
            }
            RakeMultiplayerPlugin.Log?.LogInfo($"Connecting to {RakeMultiplayerPlugin.NetworkAddress.Value}:{RakeMultiplayerPlugin.NetworkPort.Value}...");
        }

        /// <summary>
        /// Update host spawn position and broadcast to all clients (called when host loads game scene)
        /// </summary>
        public void UpdateHostPosition(Vector3 pos)
        {
            if (!RakeMultiplayerPlugin.isServer) return;
            _hostSpawnPosition = pos;
            _hostSpawnPositionSet = true;
            
            var hostPacket = new PlayerConnectedPacket
            {
                PlayerId = 1,
                SpawnPosition = pos,
                IsYou = false
            };
            var data = hostPacket.Serialize();
            foreach (var connId in _connectedClients)
            {
                _server.Send(connId, data);
            }
            RakeMultiplayerPlugin.Log?.LogInfo($"[Net] Host position updated and broadcasted: {pos}");
        }

        private void Update()
        {
            // ЛОГ ДЛЯ ПРОВЕРКИ, ЧТО UPDATE РАБОТАЕТ (выводить раз в 60 кадров, чтобы не спамить)
            if (Time.frameCount % 60 == 0)
                RakeMultiplayerPlugin.Log?.LogInfo($"[Net] Update running. isServer={RakeMultiplayerPlugin.isServer}, _server={(_server != null)}, _client={(_client != null)}");

            // Cache references once per second (avoid FindObjectOfType in hot paths)
            _cacheTimer += Time.deltaTime;
            if (_cacheTimer >= 1f)
            {
                _cacheTimer = 0f;
                if (_cachedRake == null) _cachedRake = FindObjectOfType<NetworkedRakeAI>();
                if (_cachedEnemyAI == null) _cachedEnemyAI = FindObjectOfType<Enemy_AI>();
            }

            if (RakeMultiplayerPlugin.isServer && _server != null)
            {
                ProcessServerMessages();
            }

            if (!RakeMultiplayerPlugin.isServer && _client != null)
            {
                ProcessClientMessages();
            }
        }

        public void StopHost()
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[RakeNetworkManager] Stopping host...");
            if (_server != null)
            {
                _server.Stop();
                _server = null;
            }
            _playerStates.Clear();
            _connectedClients.Clear();
            _networkedItems.Clear();
            RakeMultiplayerPlugin.isConnected = false;
        }

        public void Disconnect()
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[RakeNetworkManager] Disconnecting...");
            if (_client != null)
            {
                _client.Disconnect();
                _client = null;
            }
            // Clear client-side player tracking
            _knownPlayerIds.Clear();
            _networkedItems.Clear();
            RakeMultiplayerPlugin.isConnected = false;
        }

        private void ProcessServerMessages()
        {
            Telepathy.Message msg;
            while (_server.GetNextMessage(out msg))
            {
                switch (msg.eventType)
                {
                    case Telepathy.EventType.Connected:
                        RakeMultiplayerPlugin.Log?.LogInfo($"[Net] Server: client {msg.connectionId} connected");
                        OnServerClientConnected(msg.connectionId);
                        break;
                    case Telepathy.EventType.Data:
                        OnServerDataReceived(msg.connectionId, msg.data);
                        break;
                    case Telepathy.EventType.Disconnected:
                        RakeMultiplayerPlugin.Log?.LogInfo($"[Net] Server: client {msg.connectionId} disconnected");
                        _connectedClients.Remove(msg.connectionId);
                        _playerStates.Remove(msg.connectionId);
                        OnServerClientDisconnected(msg.connectionId);
                        break;
                }
            }
        }

        private void ProcessClientMessages()
        {
            Telepathy.Message msg;
            while (_client.GetNextMessage(out msg))
            {
                switch (msg.eventType)
                {
                    case Telepathy.EventType.Connected:
                        RakeMultiplayerPlugin.Log?.LogInfo("[Net] Client: connected to server");
                        RakeMultiplayerPlugin.isConnected = true;
                        break;
                    case Telepathy.EventType.Data:
                        OnClientDataReceived(msg.data);
                        break;
                    case Telepathy.EventType.Disconnected:
                        RakeMultiplayerPlugin.Log?.LogWarning("[Net] Client: disconnected from server");
                        RakeMultiplayerPlugin.isConnected = false;
                        break;
                }
            }
        }

        private void OnServerClientConnected(int connectionId)
        {
            try
            {
                RakeMultiplayerPlugin.Log?.LogInfo($"[Net] Server: client {connectionId} connected");

                // Защита от двойного вызова (баг Telepathy)
                if (_connectedClients.Contains(connectionId))
                {
                    RakeMultiplayerPlugin.Log?.LogWarning($"[Net] Duplicate connection event for C{connectionId}, ignoring");
                    return;
                }
                _connectedClients.Add(connectionId);

                // ID scheme: Host = 1, Clients = connectionId + 1
                int assignedPlayerId = connectionId + 1;

                // 0. Send HOST to NEW client (ВСЕГДА, даже если позиция не установлена)
                Vector3 hostPos = _hostSpawnPositionSet ? _hostSpawnPosition : Vector3.zero;
                if (!_hostSpawnPositionSet)
                {
                    RakeMultiplayerPlugin.Log?.LogWarning("[Net] Host spawn pos not set. Sending Vector3.zero.");
                }
                var hostPacket = new PlayerConnectedPacket
                {
                    PlayerId = 1, // Host always has ID 1
                    SpawnPosition = hostPos,
                    IsYou = false
                };
                _server.Send(connectionId, hostPacket.Serialize());
                RakeMultiplayerPlugin.Log?.LogInfo($"[Net] Sent Host info to C{connectionId}: pos={hostPos}");

                // 1. Send existing CLIENTS to the NEW client (use assignedPlayerId = existingId + 1)
                foreach (var existingId in _connectedClients)
                {
                    if (existingId == connectionId) continue;
                    if (_playerStates.ContainsKey(existingId))
                    {
                        int existingPlayerId = existingId + 1;
                        var existingPacket = new PlayerConnectedPacket
                        {
                            PlayerId = existingPlayerId,
                            SpawnPosition = _playerStates[existingId].Position,
                            IsYou = false
                        };
                        _server.Send(connectionId, existingPacket.Serialize());
                    }
                }

                // 2. Broadcast NEW player to ALL
                var newPacketForNew = new PlayerConnectedPacket
                {
                    PlayerId = assignedPlayerId,
                    SpawnPosition = new Vector3(assignedPlayerId * 2f, 0f, 0f),
                    IsYou = true
                };
                _server.Send(connectionId, newPacketForNew.Serialize());

                var newPacketForOthers = new PlayerConnectedPacket
                {
                    PlayerId = assignedPlayerId,
                    SpawnPosition = new Vector3(assignedPlayerId * 2f, 0f, 0f),
                    IsYou = false
                };
                var dataForOthers = newPacketForOthers.Serialize();
                foreach (var connId in _connectedClients)
                {
                    if (connId != connectionId) _server.Send(connId, dataForOthers);
                }

                // 3. HOST spawns remote player for the NEW client directly
                if (PlayerSpawner.Instance != null)
                {
                    var spawnPos = new Vector3(assignedPlayerId * 2f, 0f, 0f);
                    PlayerSpawner.Instance.SpawnRemotePlayer(assignedPlayerId, spawnPos);
                    RakeMultiplayerPlugin.Log?.LogInfo($"[Net] Host spawned remote player for C{connectionId} (PlayerId={assignedPlayerId}) at {spawnPos}");
                }
            }
            catch (Exception ex)
            {
                RakeMultiplayerPlugin.Log?.LogError($"[Net] Error in OnServerClientConnected: {ex.Message}");
            }
        }

        private void OnServerClientDisconnected(int connectionId)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[Net] Server: client {connectionId} disconnected");
            _connectedClients.Remove(connectionId);
            _playerStates.Remove(connectionId);
            _networkedItems.Clear(); // Clear items to avoid ghost items for remaining players

            var packet = new PlayerDisconnectedPacket { PlayerId = connectionId + 1 };
            var data = packet.Serialize();
            foreach (var connId in _connectedClients)
            {
                _server.Send(connId, data);
            }
        }

        private void OnServerDataReceived(int connectionId, byte[] data)
        {
            try
            {
                var packet = PacketSerializer.Deserialize(data);
                RakeMultiplayerPlugin.Log?.LogInfo($"[Net] S<-C{connectionId} {packet.Type} ({data.Length}b)");

                switch (packet.Type)
                {
                    case PacketType.PlayerState:
                        HandleServerPlayerState(connectionId, (PlayerStatePacket)packet, data);
                        break;
                    case PacketType.DamageRequest:
                        HandleServerDamageRequest(connectionId, (DamageRequestPacket)packet, data);
                        break;
                    case PacketType.ItemPickup:
                        HandleServerItemPickup(connectionId, (ItemPickupPacket)packet, data);
                        break;
                    default:
                        RakeMultiplayerPlugin.Log?.LogWarning($"[Net] Server: unexpected {packet.Type} from C{connectionId}");
                        break;
                }
            }
            catch (Exception ex)
            {
                RakeMultiplayerPlugin.Log?.LogError($"[Net] Server deserialize error from C{connectionId}: {ex.Message}");
            }
        }

        private void HandleServerPlayerState(int connectionId, PlayerStatePacket packet, byte[] rawData)
        {
            // Override PlayerId with connectionId + 1 (authoritative, host=1, clients=connectionId+1)
            int assignedPlayerId = connectionId + 1;
            packet.PlayerId = assignedPlayerId;

            // Store state by connectionId
            _playerStates[connectionId] = packet;

            // Re-serialize with corrected PlayerId for broadcast
            var data = packet.Serialize();

            // Broadcast to ALL other clients
            foreach (var connId in _connectedClients)
            {
                if (connId == connectionId) continue;
                _server.Send(connId, data);
            }

            // HOST: apply state to local remote player (if exists)
            if (PlayerSpawner.Instance != null && PlayerSpawner.Instance.TryGetRemotePlayer(assignedPlayerId, out var remote))
            {
                remote.ApplyPosition(packet.Position);
                remote.ApplyRotation(packet.Rotation);
                remote.ApplyAnimation(packet.AnimState);
            }
        }

        public void HandleServerDamageRequest(int connectionId, DamageRequestPacket packet, byte[] rawData)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.4] Server received DamageRequest from C{connectionId}: shooter={packet.ShooterId}, target={packet.TargetId}, amount={packet.Amount}, type={packet.DamageType}, shooterPos={packet.ShooterPosition}");

            // Use cached Enemy_AI (server-authoritative)
            if (_cachedEnemyAI == null)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[3.4] No cached Enemy_AI for DamageRequest");
                return;
            }

            // Apply damage via reflection (TakeDamage method or health field)
            float newHealth = ApplyDamageToEnemy(_cachedEnemyAI, packet.Amount, packet.DamageType);

            // ===== КРИТИЧЕСКИЙ ФИКС: Заставляем Рейка среагировать на выстрел (Danger state) =====
            // Enemy_AI.Start() подписывается на NotificationsManager.Instance.AddListener("Event_PlayerFire")
            // При получении уведомления он вызывает Event_PlayerFire(Component sender)
            // который проверяет дистанцию до sender и вызывает SetDangerState()
            // Мы создаём временный GameObject на позиции стрелка и вызываем Event_PlayerFire через рефлексию
            try
            {
                var fakeShooter = new GameObject("FakeShooter_ForRakeReaction");
                fakeShooter.transform.position = packet.ShooterPosition;
                var fakeComponent = fakeShooter.AddComponent<Component>(); // Component is abstract, use a dummy MonoBehaviour
                
                // Actually we need a Component that has a Transform. Let's use a different approach:
                // Create a MonoBehaviour on the fake object
                var dummyBehaviour = fakeShooter.AddComponent<MonoBehaviour>();
                
                var eventMethod = typeof(Enemy_AI).GetMethod("Event_PlayerFire", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (eventMethod != null)
                {
                    eventMethod.Invoke(_cachedEnemyAI, new object[] { dummyBehaviour });
                    RakeMultiplayerPlugin.Log?.LogInfo($"[3.4] Forced Rake to react to shot from {packet.ShooterPosition} (Danger state)");
                }
                UnityEngine.Object.Destroy(fakeShooter);
            }
            catch (Exception ex)
            {
                RakeMultiplayerPlugin.Log?.LogError($"[3.4] Failed to trigger Rake Event_PlayerFire: {ex.Message}");
            }

            // Broadcast DamageApplied to all clients
            var appliedPacket = new DamageAppliedPacket
            {
                TargetId = packet.TargetId,
                NewHealth = newHealth
            };
            ServerBroadcast(appliedPacket.Serialize());
        }

        private float ApplyDamageToEnemy(Enemy_AI enemyAI, int amount, string damageType)
        {
            try
            {
                // Direct call (Enemy_AI is in Assembly-CSharp.dll which we reference)
                // Signature: public void TakeDamage(int amount, string damageType)
                enemyAI.TakeDamage(amount, damageType ?? "Bullet");
                
                // Get updated health via reflection (field is public float Health)
                var healthField = typeof(Enemy_AI).GetField("Health", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (healthField != null)
                {
                    return Convert.ToSingle(healthField.GetValue(enemyAI));
                }
            }
            catch (Exception ex)
            {
                // Fallback to reflection with correct signature (2 params)
                try
                {
                    var takeDamageMethod = typeof(Enemy_AI).GetMethod("TakeDamage", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (takeDamageMethod != null)
                    {
                        takeDamageMethod.Invoke(enemyAI, new object[] { amount, damageType ?? "Bullet" });
                        
                        var healthField = typeof(Enemy_AI).GetField("Health", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (healthField != null)
                        {
                            return Convert.ToSingle(healthField.GetValue(enemyAI));
                        }
                    }
                }
                catch (Exception ex2)
                {
                    RakeMultiplayerPlugin.Log?.LogError($"[3.4] ApplyDamageToEnemy reflection fallback failed: {ex2.Message}");
                }
                RakeMultiplayerPlugin.Log?.LogError($"[3.4] ApplyDamageToEnemy error: {ex.Message}");
            }
            return 100f; // Fallback
        }

        // === Stage 3.5: Items ===
        private void HandleServerItemPickup(int connectionId, ItemPickupPacket packet, byte[] rawData)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.5] Server received ItemPickup from C{connectionId}: player={packet.PlayerId}, itemId={packet.ItemNetworkId}, type={packet.ItemType}");

            // Race condition protection: check if item still exists in registry
            if (!_networkedItems.ContainsKey(packet.ItemNetworkId))
            {
                RakeMultiplayerPlugin.Log?.LogWarning($"[3.5] Server: item {packet.ItemNetworkId} already picked up by someone else!");
                return;
            }

            // Get the item to clean up reverse lookup
            var item = _networkedItems[packet.ItemNetworkId];
            
            // Remove from server registry immediately to prevent double-pickup
            _networkedItems.Remove(packet.ItemNetworkId);
            if (item != null) _itemToNetworkId.Remove(item);

            // ===== Give item to the player on the server (host) =====
            // Find the WeaponManager for this player and call AddWeapon/AddAmmo
            // PlayerId 1 = Host, PlayerId > 1 = Clients (connectionId = PlayerId - 1)
            try
            {
                WeaponManager weaponManager = null;
                
                if (packet.PlayerId == 1)
                {
                    // Host player - find WeaponManager on the local player
                    if (PlayerSpawner.Instance != null && PlayerSpawner.Instance.LocalPlayer != null)
                    {
                        weaponManager = PlayerSpawner.Instance.LocalPlayer.GetComponent<WeaponManager>();
                    }
                }
                else
                {
                    // Remote player (client) - find their NetworkedPlayer
                    if (PlayerSpawner.Instance != null && PlayerSpawner.Instance.TryGetRemotePlayer(packet.PlayerId, out var remotePlayer))
                    {
                        weaponManager = remotePlayer.GetComponent<WeaponManager>();
                    }
                }

                if (weaponManager != null && item != null)
                {
                    // Determine what to give based on item type
                    // This mimics the logic in Item.PickupItem overrides
                    string itemType = packet.ItemType;
                    int weaponId = 0;
                    int ammoCount = 1;
                    bool isNewWeapon = false;

                    switch (itemType)
                    {
                        case "Medkit_item":
                            weaponId = 8; // Medkit weapon ID
                            isNewWeapon = !weaponManager.CheckWeapon(weaponId);
                            break;
                        case "Rifle_item":
                            weaponId = 1; // Rifle weapon ID
                            ammoCount = 2;
                            isNewWeapon = !weaponManager.CheckWeapon(weaponId);
                            break;
                        case "RifleAmmo_item":
                            weaponId = 1;
                            ammoCount = 2;
                            isNewWeapon = false; // Ammo only
                            break;
                        case "SniperRifle_item":
                            weaponId = 6; // Sniper weapon ID
                            isNewWeapon = !weaponManager.CheckWeapon(weaponId);
                            break;
                        case "SniperAmmo_item":
                            weaponId = 6;
                            ammoCount = 2;
                            isNewWeapon = false;
                            break;
                        case "Trap_item":
                            weaponId = 4;
                            isNewWeapon = !weaponManager.CheckWeapon(weaponId);
                            break;
                        case "Camera_item":
                            weaponId = 2;
                            ammoCount = 1;
                            isNewWeapon = !weaponManager.CheckWeapon(weaponId);
                            break;
                        case "Binocular_weapon":
                        case "Binocular_item":
                            weaponId = 7;
                            isNewWeapon = !weaponManager.CheckWeapon(weaponId);
                            break;
                        case "MapCase_weapon":
                        case "MapCase_item":
                            weaponId = 3;
                            isNewWeapon = !weaponManager.CheckWeapon(weaponId);
                            break;
                        case "Food_item":
                            weaponId = 5;
                            isNewWeapon = !weaponManager.CheckWeapon(weaponId);
                            break;
                        default:
                            RakeMultiplayerPlugin.Log?.LogWarning($"[3.5] Unknown item type for server pickup: {itemType}");
                            break;
                    }

                    if (weaponId > 0)
                    {
                        if (isNewWeapon)
                        {
                            weaponManager.AddWeapon(weaponId, ammoCount, true);
                            RakeMultiplayerPlugin.Log?.LogInfo($"[3.5] Server gave weapon {weaponId} to player {packet.PlayerId}");
                        }
                        else
                        {
                            weaponManager.AddAmmo(weaponId, ammoCount);
                            RakeMultiplayerPlugin.Log?.LogInfo($"[3.5] Server gave ammo {ammoCount} for weapon {weaponId} to player {packet.PlayerId}");
                        }
                    }
                }
                else
                {
                    RakeMultiplayerPlugin.Log?.LogWarning($"[3.5] Could not find WeaponManager for player {packet.PlayerId} or item was null");
                }
            }
            catch (Exception ex)
            {
                RakeMultiplayerPlugin.Log?.LogError($"[3.5] Error giving item to player on server: {ex.Message}");
            }

            var destroyedPacket = new ItemDestroyedPacket
            {
                ItemNetworkId = packet.ItemNetworkId,
                PickerId = packet.PlayerId
            };
            ServerBroadcast(destroyedPacket.Serialize());
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.5] Server authorized pickup of item {packet.ItemNetworkId} by player {packet.PlayerId}");
        }

        private void OnClientDataReceived(byte[] data)
        {
            try
            {
                var packet = PacketSerializer.Deserialize(data);
                RakeMultiplayerPlugin.Log?.LogInfo($"[Net] C<-S {packet.Type} ({data.Length}b)");

                switch (packet.Type)
                {
                    case PacketType.PlayerState:
                        HandleClientPlayerState((PlayerStatePacket)packet);
                        break;
                    case PacketType.PlayerConnected:
                        HandleClientPlayerConnected((PlayerConnectedPacket)packet);
                        break;
                    case PacketType.PlayerDisconnected:
                        HandleClientPlayerDisconnected((PlayerDisconnectedPacket)packet);
                        break;
                    case PacketType.RakeState:
                        HandleClientRakeState((RakeStatePacket)packet);
                        break;
                    case PacketType.DamageApplied:
                        HandleClientDamageApplied((DamageAppliedPacket)packet);
                        break;
                    case PacketType.FireVisuals:
                        HandleClientFireVisuals((FireVisualsPacket)packet);
                        break;
                    case PacketType.StartGame:
                        HandleClientStartGame((StartGamePacket)packet);
                        break;
                    case PacketType.ItemDestroyed:
                        HandleClientItemDestroyed((ItemDestroyedPacket)packet);
                        break;
                    default:
                        RakeMultiplayerPlugin.Log?.LogWarning($"[Net] Client: unexpected {packet.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                RakeMultiplayerPlugin.Log?.LogError($"[Net] Client deserialize error: {ex.Message}");
            }
        }

        private void HandleClientStartGame(StartGamePacket packet)
        {
            if (_clientSceneLoading) return;
            _clientSceneLoading = true;
            
            RakeMultiplayerPlugin.Log?.LogInfo($"[Net] Received StartGame packet! Loading level index: {packet.SceneName}");
            
            // Parse as level index (game uses LoadLevel(int))
            if (int.TryParse(packet.SceneName, out int levelIndex))
            {
                UnityEngine.Application.LoadLevel(levelIndex);
            }
            else
            {
                // Fallback: try as scene name
                UnityEngine.Application.LoadLevel(packet.SceneName);
            }
        }

        private void HandleClientPlayerState(PlayerStatePacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.3] Client received PlayerState: playerId={packet.PlayerId}, pos={packet.Position}");

            // Find remote player and apply state
            if (PlayerSpawner.Instance != null)
            {
                if (PlayerSpawner.Instance.TryGetRemotePlayer(packet.PlayerId, out var remote))
                {
                    remote.ApplyPosition(packet.Position);
                    remote.ApplyRotation(packet.Rotation);
                    remote.ApplyAnimation(packet.AnimState);
                }
            }
        }

        private void HandleClientPlayerConnected(PlayerConnectedPacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.3] Client received PlayerConnected: playerId={packet.PlayerId}, spawnPos={packet.SpawnPosition}, IsYou={packet.IsYou}");

            // Track known player IDs for UI (client-side)
            if (!RakeMultiplayerPlugin.isServer)
            {
                _knownPlayerIds.Add(packet.PlayerId);
            }

            // If PlayerSpawner not ready, queue the packet for later processing
            if (PlayerSpawner.Instance == null)
            {
                RakeMultiplayerPlugin.Log?.LogInfo("[Net] PlayerSpawner not ready, queuing PlayerConnected...");
                _pendingConnects.Enqueue(packet);
                return;
            }

            // If this packet is for us (IsYou = true), set our local player ID
            if (packet.IsYou)
            {
                if (PlayerSpawner.Instance.LocalPlayer != null)
                {
                    PlayerSpawner.Instance.LocalPlayer.SetPlayerId(packet.PlayerId);
                    RakeMultiplayerPlugin.Log?.LogInfo($"[3.3] Local player assigned ID: {packet.PlayerId}");
                }
                return;
            }

            // Otherwise spawn remote player
            PlayerSpawner.Instance.SpawnRemotePlayer(packet.PlayerId, packet.SpawnPosition);
        }

        private void HandleClientPlayerDisconnected(PlayerDisconnectedPacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.3] Client received PlayerDisconnected: playerId={packet.PlayerId}");

            if (PlayerSpawner.Instance != null)
            {
                PlayerSpawner.Instance.RemoveRemotePlayer(packet.PlayerId);
            }
        }

        private void HandleClientRakeState(RakeStatePacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.4] Client received RakeState: pos={packet.Position}, health={packet.Health}, state={packet.State}");
            
            // Use cached NetworkedRakeAI (avoid FindObjectOfType in hot path)
            if (_cachedRake != null)
            {
                _cachedRake.ApplySyncData(packet.Position, packet.Rotation, packet.State, packet.Health);
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[3.4] NetworkedRakeAI not cached on client!");
            }
        }

        private void HandleClientDamageApplied(DamageAppliedPacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.4] Client received DamageApplied: targetId={packet.TargetId}, health={packet.NewHealth}");
            
            // Use cached NetworkedRakeAI
            if (_cachedRake != null)
            {
                // Only update health (position/state handled by RakeStatePacket)
                _cachedRake.ApplyHealth(packet.NewHealth);
            }
        }

        // === Stage 3.5: Client Item Destroyed Handler ===
        private void HandleClientItemDestroyed(ItemDestroyedPacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.5] Client received ItemDestroyed: itemId={packet.ItemNetworkId}, pickerId={packet.PickerId}");

            // Use flag to prevent recursive patch execution
            RakeMultiplayerPlugin.IsApplyingNetworkDestroy = true;
            try
            {
                if (_networkedItems.TryGetValue(packet.ItemNetworkId, out var item))
                {
                    UnityEngine.Object.Destroy(item.gameObject);
                    _networkedItems.Remove(packet.ItemNetworkId);
                    _itemToNetworkId.Remove(item); // Clean up reverse lookup
                    RakeMultiplayerPlugin.Log?.LogInfo($"[3.5] Client destroyed item {packet.ItemNetworkId} ({item.name})");
                }
                else
                {
                    RakeMultiplayerPlugin.Log?.LogError($"[3.5] Client FAILED to find item {packet.ItemNetworkId} in registry! Registered items: {_networkedItems.Count}");
                    foreach (var kvp in _networkedItems)
                    {
                        RakeMultiplayerPlugin.Log?.LogInfo($"  - Registry: {kvp.Key} = {kvp.Value.name} at {kvp.Value.transform.position}");
                    }
                }
            }
            finally
            {
                RakeMultiplayerPlugin.IsApplyingNetworkDestroy = false;
            }
        }

        private void HandleClientFireVisuals(FireVisualsPacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.5] Client received FireVisuals: shooterId={packet.ShooterId}, weaponType={packet.WeaponType}");

            // Find remote player by shooterId
            if (PlayerSpawner.Instance == null) return;
            if (!PlayerSpawner.Instance.TryGetRemotePlayer(packet.ShooterId, out var shooter)) return;

            try
            {
                // WeaponType: 0 = Rifle, 1 = Sniper
                if (packet.WeaponType == 0)
                {
                    var rifle = shooter.GetComponentInChildren<Rifle_weapon>();
                    if (rifle != null)
                    {
                        // Use reflection to access private fields
                        var muzzleFireField = typeof(Rifle_weapon).GetField("muzzleFire", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var fireSoundField = typeof(Rifle_weapon).GetField("fireSound", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var muzzlePosField = typeof(Rifle_weapon).GetField("muzzlePos", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var sfxField = typeof(Rifle_weapon).GetField("SFX", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        // Spawn muzzle flash
                        if (muzzleFireField != null && muzzlePosField != null)
                        {
                            var muzzleFire = muzzleFireField.GetValue(rifle) as GameObject;
                            var muzzlePos = muzzlePosField.GetValue(rifle) as Transform;
                            if (muzzleFire != null && muzzlePos != null)
                            {
                                UnityEngine.Object.Instantiate(muzzleFire, muzzlePos.position, muzzlePos.rotation);
                            }
                        }

                        // Play fire sound
                        if (fireSoundField != null && sfxField != null)
                        {
                            var fireSound = fireSoundField.GetValue(rifle) as AudioClip;
                            var sfx = sfxField.GetValue(rifle) as AudioSource;
                            if (fireSound != null && sfx != null)
                            {
                                sfx.PlayOneShot(fireSound);
                            }
                        }
                    }
                }
                else if (packet.WeaponType == 1)
                {
                    var sniper = shooter.GetComponentInChildren<SniperRifle_weapon>();
                    if (sniper != null)
                    {
                        var muzzleFireField = typeof(SniperRifle_weapon).GetField("muzzleFire", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var fireSoundField = typeof(SniperRifle_weapon).GetField("fireSound", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var muzzlePosField = typeof(SniperRifle_weapon).GetField("muzzlePos", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var sfxField = typeof(SniperRifle_weapon).GetField("SFX", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        if (muzzleFireField != null && muzzlePosField != null)
                        {
                            var muzzleFire = muzzleFireField.GetValue(sniper) as GameObject;
                            var muzzlePos = muzzlePosField.GetValue(sniper) as Transform;
                            if (muzzleFire != null && muzzlePos != null)
                            {
                                UnityEngine.Object.Instantiate(muzzleFire, muzzlePos.position, muzzlePos.rotation);
                            }
                        }

                        if (fireSoundField != null && sfxField != null)
                        {
                            var fireSound = fireSoundField.GetValue(sniper) as AudioClip;
                            var sfx = sfxField.GetValue(sniper) as AudioSource;
                            if (fireSound != null && sfx != null)
                            {
                                sfx.PlayOneShot(fireSound);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RakeMultiplayerPlugin.Log?.LogError($"[3.5] FireVisuals error: {ex.Message}");
            }
        }

        // Public methods for RakeMultiplayerPlugin static helpers
        public void SendToServer(byte[] data)
        {
            if (_client != null && _client.Connected)
            {
                _client.Send(data);
            }
        }

        /// <summary>
        /// Client-side send (alias for SendToServer)
        /// </summary>
        public void ClientSend(byte[] data) => SendToServer(data);

        public void SendToAllClients(byte[] data)
        {
            if (_server != null)
            {
                foreach (var connId in _connectedClients)
                {
                    _server.Send(connId, data);
                }
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
            // Fallback: generate from position (for items not yet registered)
            int x = Mathf.RoundToInt(item.transform.position.x * 100f);
            int y = Mathf.RoundToInt(item.transform.position.y * 100f);
            int z = Mathf.RoundToInt(item.transform.position.z * 100f);
            return (x * 73856093) ^ (y * 19349663) ^ (z * 83492791) ^ item.name.GetHashCode();
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

        /// <summary>
        /// Process queued PlayerConnected packets (called when PlayerSpawner initializes)
        /// </summary>
        public void ProcessPendingConnects()
        {
            while (_pendingConnects.Count > 0)
            {
                var packet = _pendingConnects.Dequeue();
                RakeMultiplayerPlugin.Log?.LogInfo($"[Net] Processing queued PlayerConnected: playerId={packet.PlayerId}, IsYou={packet.IsYou}");
                
                // Track known player IDs for UI (client-side)
                if (!RakeMultiplayerPlugin.isServer)
                {
                    _knownPlayerIds.Add(packet.PlayerId);
                }
                
                // Re-call without the queue check (PlayerSpawner is now ready)
                if (packet.IsYou)
                {
                    if (PlayerSpawner.Instance.LocalPlayer != null)
                    {
                        PlayerSpawner.Instance.LocalPlayer.SetPlayerId(packet.PlayerId);
                        RakeMultiplayerPlugin.Log?.LogInfo($"[3.3] Local player assigned ID: {packet.PlayerId}");
                    }
                }
                else
                {
                    PlayerSpawner.Instance.SpawnRemotePlayer(packet.PlayerId, packet.SpawnPosition);
                }
            }
        }

        // Aliases for NetworkedPlayer
        public void ServerBroadcast(byte[] data) => SendToAllClients(data);

        public int GetConnectedClientCount()
        {
            return _connectedClients.Count;
        }
    }
}