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

        /// <summary>
        /// Fired when the server rejects an item pickup (playerId, reason).
        /// UI/HUD code can subscribe for player-facing feedback.
        /// </summary>
        public static event Action<int, string> OnPickupRejected;

        /// <summary>
        /// Fired when the server rejects a camera setup/pickup request (playerId, reason).
        /// </summary>
        public static event Action<int, string> OnCameraSetupRejected;

        /// <summary>
        /// Fired when a map marker (bait, trap, camera, ...) is received/updated.
        /// </summary>
        public static event Action<MapMarkerPacket> OnMapMarkerReceived;

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

        // Camera registry tracking (deterministic IDs assigned by RegisterCameras)
        private int _nextCameraNetworkId = 1;

        // Map markers (bait/trap/camera...) received from the server, for MapUI
        private readonly Dictionary<uint, MapMarkerPacket> _mapMarkers = new Dictionary<uint, MapMarkerPacket>();
        private readonly Dictionary<uint, TrapStatePacket> _trapStates = new Dictionary<uint, TrapStatePacket>();
        private readonly List<Transform> _activeBaitTransforms = new List<Transform>();
        private int _nextMarkerId = 1;

        /// <summary>Map markers received from the server (MarkerId -> packet).</summary>
        public Dictionary<uint, MapMarkerPacket> GetMapMarkers() => _mapMarkers;
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
                    case PacketType.StartGame:
                        HandleServerStartGame(connectionId, (StartGamePacket)packet, data);
                        break;
                    case PacketType.CameraSync:
                        // CameraSync is SERVER-AUTHORITATIVE only — clients must not send it
                        RakeMultiplayerPlugin.Log?.LogWarning($"[Net] Server: received CameraSync from C{connectionId} (client-spoofed, ignoring)");
                        break;
                    case PacketType.CameraSpawn:
                        // CameraSpawn is SERVER-AUTHORITATIVE only — clients must not send it
                        RakeMultiplayerPlugin.Log?.LogWarning($"[Net] Server: received CameraSpawn from C{connectionId} (client-spoofed, ignoring)");
                        break;
                    case PacketType.CameraDestroy:
                        // CameraDestroy is SERVER-AUTHORITATIVE only — clients must not send it
                        RakeMultiplayerPlugin.Log?.LogWarning($"[Net] Server: received CameraDestroy from C{connectionId} (client-spoofed, ignoring)");
                        break;
                    case PacketType.QuestTrigger:
                        // QuestTrigger is SERVER-AUTHORITATIVE only — clients must not send it
                        RakeMultiplayerPlugin.Log?.LogWarning($"[Net] Server: received QuestTrigger from C{connectionId} (client-spoofed, ignoring)");
                        break;
                    case PacketType.CameraSetupRequest:
                        HandleServerCameraSetupRequest(connectionId, (CameraSetupRequestPacket)packet, data);
                        break;
                    case PacketType.MapMarker:
                        HandleServerMapMarker(connectionId, (MapMarkerPacket)packet, data);
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
            float newHealth = ApplyDamageToEnemy(_cachedEnemyAI, packet.Amount, packet.DamageType, packet.ShooterPosition);

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

        private float ApplyDamageToEnemy(Enemy_AI enemyAI, int amount, string damageType, Vector3 shooterPosition)
        {
            try
            {
                // Calculate damage reduction if NetworkedRakeAI is present
                int finalAmount = amount;
                Vector3 hitDirection = Vector3.zero;
                var rakeAI = enemyAI.GetComponent<NetworkedRakeAI>();
                
                if (rakeAI != null)
                {
                    // hitDirection: direction FROM shooter TO target (enemy)
                    hitDirection = (enemyAI.transform.position - shooterPosition).normalized;
                    float reduction = rakeAI.CalculateDamageReduction(hitDirection, enemyAI.transform.forward);
                    finalAmount = Mathf.RoundToInt(amount * reduction);
                    
                    // Notify AI of damage for state reactions (blocking, stealth break, etc.)
                    rakeAI.OnDamageTaken(hitDirection, amount, damageType ?? "Bullet");
                    
                    RakeMultiplayerPlugin.Log?.LogInfo($"[3.4] Damage reduction applied: {amount} -> {finalAmount} (reduction: {(1f-reduction)*100:F0}%)");
                }

                // Direct call (Enemy_AI is in Assembly-CSharp.dll which we reference)
                // Signature: public void TakeDamage(int amount, string damageType)
                enemyAI.TakeDamage(finalAmount, damageType ?? "Bullet");
                
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
                        // Apply reduction in fallback too
                        int finalAmount = amount;
                        Vector3 hitDirection = Vector3.zero;
                        var rakeAI = enemyAI.GetComponent<NetworkedRakeAI>();
                        if (rakeAI != null)
                        {
                            hitDirection = (enemyAI.transform.position - shooterPosition).normalized;
                            float reduction = rakeAI.CalculateDamageReduction(hitDirection, enemyAI.transform.forward);
                            finalAmount = Mathf.RoundToInt(amount * reduction);
                            rakeAI.OnDamageTaken(hitDirection, amount, damageType ?? "Bullet");
                        }
                        takeDamageMethod.Invoke(enemyAI, new object[] { finalAmount, damageType ?? "Bullet" });
                        
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

            // ===== Task 3.3: 5-camera limit enforcement (server-authoritative) =====
            // The original game allows at most 5 cameras set up at once
            // (MP_RecordCamera_obj.CheckSetup: "You cannot setup no more 5 cameras").
            // Reject acquiring further camera items once the quota is full so a 6th
            // camera can never enter play. Broken cameras free a slot (see NetworkedCamera).
            if (string.Equals(packet.ItemType, "Camera_item", StringComparison.Ordinal) && !NetworkedCamera.CanPlaceCamera())
            {
                RakeMultiplayerPlugin.Log?.LogWarning(
                    $"[3.3] Server rejected camera pickup for player {packet.PlayerId}: " +
                    $"camera limit {NetworkedCamera.MaxCameras} reached " +
                    $"(placed={NetworkedCamera.GetPlacedCameraCount()}, usable={NetworkedCamera.GetUsableCameraCount()})");

                var rejectPacket = new ItemPickupConfirmPacket
                {
                    PlayerId = packet.PlayerId,
                    ItemNetworkId = packet.ItemNetworkId,
                    Success = false,
                    Reason = "CameraLimit"
                };
                _server.Send(connectionId, rejectPacket.Serialize());
                OnPickupRejected?.Invoke(packet.PlayerId, "CameraLimit");
                return; // Item stays in the world
            }
            
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

        // === Stage 3.4: Camera setup/pickup (server-authoritative) ===
        private void HandleServerCameraSetupRequest(int connectionId, CameraSetupRequestPacket packet, byte[] rawData)
        {
            // Validate the claimed player identity (clients cannot act as another player)
            int expectedPlayerId = connectionId + 1;
            if (packet.PlayerId != expectedPlayerId)
            {
                RakeMultiplayerPlugin.Log?.LogWarning(
                    $"[3.4] Server rejected camera request from C{connectionId}: claimed player {packet.PlayerId}, expected {expectedPlayerId}");
                SendCameraSetupResult(connectionId, packet, false, "Unauthorized");
                return;
            }

            // Pig cameras are flagged with the high bit (0x80000000 | pigId)
            if ((packet.CameraId & 0x80000000u) != 0)
            {
                HandleServerPigCameraRequest(connectionId, packet);
                return;
            }

            if (!NetworkedCamera.TryGetCamera(packet.CameraId, out var camera) || camera == null)
            {
                RakeMultiplayerPlugin.Log?.LogWarning($"[3.4] Server: camera {packet.CameraId} not found for setup request");
                SendCameraSetupResult(connectionId, packet, false, "NotFound");
                return;
            }

            bool approved = packet.IsSetup
                ? camera.InitializePlacement(packet.PlayerId)
                : camera.UnplaceCamera();

            string reason = approved ? string.Empty : (packet.IsSetup ? "CameraLimit" : "NotPlaced");
            if (!approved)
            {
                RakeMultiplayerPlugin.Log?.LogWarning(
                    $"[3.4] Server rejected {(packet.IsSetup ? "setup" : "pickup")} of camera {packet.CameraId} by player {packet.PlayerId}: {reason}");
            }

            SendCameraSetupResult(connectionId, packet, approved, reason);
        }

        private void HandleServerPigCameraRequest(int connectionId, CameraSetupRequestPacket packet)
        {
            uint pigId = packet.CameraId & 0x7FFFFFFFu;
            if (!NetworkedPig.TryGetPig(pigId, out var pig) || pig == null)
            {
                RakeMultiplayerPlugin.Log?.LogWarning($"[3.9] Pig {pigId} not found for pig-camera request");
                SendCameraSetupResult(connectionId, packet, false, "NotFound");
                return;
            }

            if (packet.IsSetup)
            {
                bool ok = NetworkedCamera.TryReserveExternalCamera(packet.CameraId);
                if (ok)
                {
                    pig.SetAttachedCamera(packet.CameraId);
                    pig.ForceSendState();
                    RakeMultiplayerPlugin.Log?.LogInfo(
                        $"[3.9] Pig camera {packet.CameraId} attached to pig {pigId} " +
                        $"({NetworkedCamera.GetTotalUsableCameraCount()}/{NetworkedCamera.MaxCameras})");
                }
                else
                {
                    RakeMultiplayerPlugin.Log?.LogWarning(
                        $"[3.9] Pig-camera setup rejected: limit {NetworkedCamera.MaxCameras} reached");
                }
                SendCameraSetupResult(connectionId, packet, ok, ok ? string.Empty : "CameraLimit");
            }
            else
            {
                NetworkedCamera.ReleaseExternalCamera(packet.CameraId);
                pig.SetAttachedCamera(0);
                pig.ForceSendState();
                SendCameraSetupResult(connectionId, packet, true, string.Empty);
            }
        }

        private void SendCameraSetupResult(int connectionId, CameraSetupRequestPacket request, bool approved, string reason)
        {
            var result = new CameraSetupRequestPacket
            {
                CameraId = request.CameraId,
                PlayerId = request.PlayerId,
                IsSetup = request.IsSetup,
                IsResult = true,
                Approved = approved,
                Reason = reason ?? string.Empty
            };
            _server.Send(connectionId, result.Serialize());
        }

        /// <summary>
        /// Assign deterministic CameraIds to every camera object in the scene.
        /// Must be called on BOTH host and client during scene initialization
        /// (same order on every machine: position, then name).
        /// </summary>
        public void RegisterCameras()
        {
            var cameras = UnityEngine.Object.FindObjectsOfType<RecordCamera_obj>();
            if (cameras == null || cameras.Length == 0)
            {
                RakeMultiplayerPlugin.Log?.LogInfo("[3.4] No RecordCamera_obj found in scene");
                return;
            }

            System.Array.Sort(cameras, (a, b) =>
            {
                if (a == null || b == null) return 0;
                var pa = a.transform.position;
                var pb = b.transform.position;
                int cmp = pa.x.CompareTo(pb.x); if (cmp != 0) return cmp;
                cmp = pa.y.CompareTo(pb.y); if (cmp != 0) return cmp;
                cmp = pa.z.CompareTo(pb.z); if (cmp != 0) return cmp;
                return string.CompareOrdinal(a.gameObject.name, b.gameObject.name);
            });

            for (int i = 0; i < cameras.Length; i++)
            {
                var recordCam = cameras[i];
                if (recordCam == null) continue;

                var netCam = recordCam.GetComponent<NetworkedCamera>();
                if (netCam == null)
                {
                    netCam = recordCam.gameObject.AddComponent<NetworkedCamera>();
                }
                netCam.AssignCameraId((uint)(i + 1));
            }

            _nextCameraNetworkId = Math.Max(_nextCameraNetworkId, cameras.Length + 1);
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.4] Registered {cameras.Length} networked cameras with deterministic IDs");
        }

        // === Stage 3.8: Bait / animals ===
        private const int MaxActiveBaits = 20;

        private void HandleServerMapMarker(int connectionId, MapMarkerPacket packet, byte[] rawData)
        {
            // Clients may place bait (2) and trap (1) markers; other marker types are server-owned.
            if (packet.MarkerType == 1) // Trap
            {
                var trap = NetworkedTrap.SpawnServerTrap(packet.Position);
                if (trap == null)
                {
                    RakeMultiplayerPlugin.Log?.LogWarning($"[3.11] Trap placement rejected (limit {NetworkedTrap.MaxTraps} or bad position)");
                    return;
                }
                packet.MarkerId = trap.TrapId;
                ServerBroadcast(packet.Serialize());
                _mapMarkers[packet.MarkerId] = packet;
                OnMapMarkerReceived?.Invoke(packet);
                return;
            }

            if (packet.MarkerType != 2) // 2 = Bait
            {
                RakeMultiplayerPlugin.Log?.LogWarning(
                    $"[3.8] Server: ignoring client MapMarker type {packet.MarkerType} from C{connectionId}");
                return;
            }

            PruneDestroyedBaits();
            if (_activeBaitTransforms.Count >= MaxActiveBaits)
            {
                RakeMultiplayerPlugin.Log?.LogWarning($"[3.8] Bait limit ({MaxActiveBaits}) reached, ignoring placement");
                return;
            }

            // Sanity check position (avoid absurd coordinates from a spoofed client)
            if (float.IsNaN(packet.Position.x) || float.IsNaN(packet.Position.y) || float.IsNaN(packet.Position.z) ||
                packet.Position.sqrMagnitude > 100000000f)
            {
                RakeMultiplayerPlugin.Log?.LogWarning($"[3.8] Rejected bait placement with invalid position {packet.Position}");
                return;
            }

            var baitGo = new GameObject($"NetworkedBait_{_nextMarkerId}");
            baitGo.transform.position = packet.Position;
            var foodPoint = baitGo.AddComponent<FoodPoint>();
            foodPoint.count = 3;

            _activeBaitTransforms.Add(baitGo.transform);

            if (AnimalsManager.instance != null)
            {
                AnimalsManager.instance.AddFoodPoint(baitGo.transform);
                RakeMultiplayerPlugin.Log?.LogInfo($"[3.8] Bait placed at {packet.Position}, attracting nearest animal");
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[3.8] AnimalsManager not found - bait will not attract animals");
            }

            // Broadcast so every client can show the bait on the map
            packet.MarkerId = (uint)_nextMarkerId++;
            ServerBroadcast(packet.Serialize());
            _mapMarkers[packet.MarkerId] = packet;
            OnMapMarkerReceived?.Invoke(packet);
        }

        private void PruneDestroyedBaits()
        {
            for (int i = _activeBaitTransforms.Count - 1; i >= 0; i--)
            {
                if (_activeBaitTransforms[i] == null) _activeBaitTransforms.RemoveAt(i);
            }
        }

        /// <summary>
        /// Attach NetworkedPig to every animal in the scene and assign deterministic IDs.
        /// Must run on BOTH host and client.
        /// </summary>
        public void RegisterAnimals()
        {
            var animals = UnityEngine.Object.FindObjectsOfType<Animal_AI>();
            if (animals == null || animals.Length == 0)
            {
                RakeMultiplayerPlugin.Log?.LogInfo("[3.8] No Animal_AI found in scene");
                return;
            }

            System.Array.Sort(animals, (a, b) =>
            {
                if (a == null || b == null) return 0;
                var pa = a.transform.position;
                var pb = b.transform.position;
                int cmp = pa.x.CompareTo(pb.x); if (cmp != 0) return cmp;
                cmp = pa.y.CompareTo(pb.y); if (cmp != 0) return cmp;
                cmp = pa.z.CompareTo(pb.z); if (cmp != 0) return cmp;
                return string.CompareOrdinal(a.gameObject.name, b.gameObject.name);
            });

            for (int i = 0; i < animals.Length; i++)
            {
                var animal = animals[i];
                if (animal == null) continue;

                var pig = animal.GetComponent<NetworkedPig>();
                if (pig == null) pig = animal.gameObject.AddComponent<NetworkedPig>();
                pig.AssignPigId((uint)(i + 1));
            }

            RakeMultiplayerPlugin.Log?.LogInfo($"[3.8] Registered {animals.Length} networked animals");
        }

        private void HandleServerStartGame(int connectionId, StartGamePacket packet, byte[] rawData)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[Net] Server received StartGame from C{connectionId}: scene={packet.SceneName}");

            // Only host can start the game
            if (connectionId != 0) // 0 would be host, but host doesn't send to self
            {
                // Verify this is the host (playerId == 1)
                int playerId = connectionId + 1;
                if (playerId != 1)
                {
                    RakeMultiplayerPlugin.Log?.LogWarning($"[Net] Non-host player {playerId} attempted to start game");
                    return;
                }
            }

            // Start the session
            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.StartSession();
            }

            // Broadcast StartGame to all clients
            ServerBroadcast(packet.Serialize());
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
                    case PacketType.ItemPickupConfirm:
                        HandleClientItemPickupConfirm((ItemPickupConfirmPacket)packet);
                        break;
                    case PacketType.PhaseChange:
                        HandleClientPhaseChange((PhaseChangePacket)packet);
                        break;
                    case PacketType.SessionState:
                        HandleClientSessionState((SessionStatePacket)packet);
                        break;
                    case PacketType.CameraSync:
                        HandleClientCameraSync((CameraSyncPacket)packet);
                        break;
                    case PacketType.CameraSpawn:
                        HandleClientCameraSpawn((CameraSpawnPacket)packet);
                        break;
                    case PacketType.CameraDestroy:
                        HandleClientCameraDestroy((CameraDestroyPacket)packet);
                        break;
                    case PacketType.CameraSetupRequest:
                        HandleClientCameraSetupResult((CameraSetupRequestPacket)packet);
                        break;
                    case PacketType.PigState:
                        HandleClientPigState((PigStatePacket)packet);
                        break;
                    case PacketType.MapMarker:
                        HandleClientMapMarker((MapMarkerPacket)packet);
                        break;
                    case PacketType.TrapState:
                        HandleClientTrapState((TrapStatePacket)packet);
                        break;
                    case PacketType.QuestTrigger:
                        HandleClientQuestTrigger((QuestTriggerPacket)packet);
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
                    // Grant the pickup effect to the local player when they were the picker
                    int localPlayerId = PlayerSpawner.Instance?.LocalPlayer?.PlayerId ?? 1;
                    if (packet.PickerId == localPlayerId)
                    {
                        GrantItemEffectLocally(item);
                    }

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

        private void HandleClientItemPickupConfirm(ItemPickupConfirmPacket packet)
        {
            if (packet.Success)
            {
                RakeMultiplayerPlugin.Log?.LogInfo($"[3.3] Client: pickup {packet.ItemNetworkId} authorized for player {packet.PlayerId}");
                return;
            }

            RakeMultiplayerPlugin.Log?.LogWarning(
                $"[3.3] Client: pickup {packet.ItemNetworkId} REJECTED for player {packet.PlayerId}: {packet.Reason}");

            switch (packet.Reason)
            {
                case "CameraLimit":
                    RakeMultiplayerPlugin.Log?.LogInfo(
                        $"[3.3] Cannot place more than {NetworkedCamera.MaxCameras} cameras at once");
                    break;
                default:
                    RakeMultiplayerPlugin.Log?.LogInfo($"[3.3] Pickup rejected: {packet.Reason}");
                    break;
            }

            OnPickupRejected?.Invoke(packet.PlayerId, packet.Reason);
        }

        /// <summary>
        /// Mirror the server-side item effect locally for the picking client
        /// (clients block the original PickupItem, so this is the only place the
        /// weapon/ammo actually reaches a remote player).
        /// </summary>
        private void GrantItemEffectLocally(Item item)
        {
            if (item == null) return;

            var weaponManager = PlayerSpawner.Instance?.LocalPlayer?.GetComponent<WeaponManager>();
            if (weaponManager == null) return;

            string itemType = item.GetType().Name;
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

        private void HandleClientCameraSetupResult(CameraSetupRequestPacket packet)
        {
            if (!packet.IsResult) return;

            if (packet.Approved)
            {
                RakeMultiplayerPlugin.Log?.LogInfo(
                    $"[3.4] Server approved {(packet.IsSetup ? "setup" : "pickup")} of camera {packet.CameraId}");
                return;
            }

            RakeMultiplayerPlugin.Log?.LogWarning(
                $"[3.4] Server rejected {(packet.IsSetup ? "setup" : "pickup")} of camera {packet.CameraId}: {packet.Reason}");

            if (packet.Reason == "CameraLimit")
            {
                try { InformationUI.instance?.DrawHint($"You cannot setup no more {NetworkedCamera.MaxCameras} cameras"); }
                catch { /* InformationUI may not exist in this scene */ }
            }

            OnCameraSetupRejected?.Invoke(packet.PlayerId, packet.Reason);
        }

        private void HandleClientPigState(PigStatePacket packet)
        {
            if (NetworkedPig.TryGetPig(packet.PigId, out var pig) && pig != null)
            {
                pig.ApplyState(packet);
            }
        }

        private void HandleClientMapMarker(MapMarkerPacket packet)
        {
            _mapMarkers[packet.MarkerId] = packet;
            OnMapMarkerReceived?.Invoke(packet);
        }

        private void HandleClientTrapState(TrapStatePacket packet)
        {
            _trapStates[packet.TrapId] = packet;

            if (NetworkedTrap.TryGetTrap(packet.TrapId, out var trap) && trap != null)
            {
                trap.ApplyState(packet);
            }
        }

        /// <summary>Trap states received from the server (TrapId -> packet), for MapUI.</summary>
        public Dictionary<uint, TrapStatePacket> GetTrapStates() => _trapStates;

        private void HandleClientPhaseChange(PhaseChangePacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[DayNight] Client received PhaseChange: phase={packet.Phase}, remaining={packet.RemainingTime:F1}s, night={packet.NightNumber + 1}");

            if (DayNightManager.Instance != null)
            {
                DayNightManager.Instance.HandlePhaseChangePacket(packet);
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[DayNight] DayNightManager not initialized yet, phase change will be applied on init");
            }
        }

        private void HandleClientSessionState(SessionStatePacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[Session] Client received SessionState: state={packet.State}, night={packet.CurrentNight}, rakeHP={packet.RakeHealth}, victory={packet.VictoryType}");

            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.HandleSessionStatePacket(packet);
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Session] SessionManager not initialized yet, session state will be applied on init");
            }
        }

        private void HandleClientCameraSync(CameraSyncPacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[CameraSync] Client received CameraSync: cameraId={packet.CameraId}, active={packet.IsActive}, broken={packet.IsBroken}, battery={packet.BatteryLevel:F1}%");
            
            // Use static registry for O(1) lookup
            if (NetworkedCamera.TryGetCamera(packet.CameraId, out var cam))
            {
                cam.ApplyCameraSync(packet);
                RakeMultiplayerPlugin.Log?.LogInfo($"[CameraSync] Applied CameraSync to camera {packet.CameraId}");
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogWarning($"[CameraSync] Camera {packet.CameraId} not found in registry");
            }
        }

        private void HandleClientCameraSpawn(CameraSpawnPacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[CameraSync] Client received CameraSpawn: cameraId={packet.CameraId}, position={packet.Position}");
            
            // Use static registry for O(1) lookup
            if (NetworkedCamera.TryGetCamera(packet.CameraId, out var cam))
            {
                cam.HandleCameraSpawn(packet);
                RakeMultiplayerPlugin.Log?.LogInfo($"[CameraSync] Handled CameraSpawn for camera {packet.CameraId}");
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogWarning($"[CameraSync] Camera {packet.CameraId} not found in registry for CameraSpawn");
            }
        }

        private void HandleClientCameraDestroy(CameraDestroyPacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[CameraSync] Client received CameraDestroy: cameraId={packet.CameraId}");
            
            // Use static registry for O(1) lookup
            if (NetworkedCamera.TryGetCamera(packet.CameraId, out var cam))
            {
                cam.HandleCameraDestroy(packet);
                RakeMultiplayerPlugin.Log?.LogInfo($"[CameraSync] Handled CameraDestroy for camera {packet.CameraId}");
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogWarning($"[CameraSync] Camera {packet.CameraId} not found in registry for CameraDestroy");
            }
        }

        private void HandleClientQuestTrigger(QuestTriggerPacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[QuestTrigger] Client received QuestTrigger: type={packet.TriggerType}, cameraId={packet.CameraId}, position={packet.Position}");
            
            // Use static registry for O(1) lookup
            if (NetworkedCamera.TryGetCamera(packet.CameraId, out var cam))
            {
                cam.HandleQuestTrigger(packet);
                RakeMultiplayerPlugin.Log?.LogInfo($"[QuestTrigger] Handled QuestTrigger for camera {packet.CameraId}");
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogWarning($"[QuestTrigger] Camera {packet.CameraId} not found in registry");
            }
            
            // Handle quest trigger events locally (for non-camera triggers)
            if (packet.TriggerType == 1) // BaitTaken
            {
                RakeMultiplayerPlugin.Log?.LogInfo($"[QuestTrigger] Bait taken at {packet.Position}");
            }
            else if (packet.TriggerType == 2) // TrapTriggered
            {
                RakeMultiplayerPlugin.Log?.LogInfo($"[QuestTrigger] Trap triggered at {packet.Position}");
            }
            // CameraBreak (0) is handled by NetworkedCamera.HandleQuestTrigger
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

        /// <summary>
        /// Re-register packet handlers after scene load
        /// </summary>
        public void ReregisterHandlers()
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[RakeNetworkManager] Re-registering packet handlers");
            // Just need to ensure PlayerSpawner is refreshed
        }

        /// <summary>
        /// Refresh network manager state after scene load
        /// </summary>
        public void Refresh()
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[RakeNetworkManager] Refreshed after scene load");
        }
    }
}