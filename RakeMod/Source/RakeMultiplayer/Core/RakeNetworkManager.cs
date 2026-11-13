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

        /// <summary>Set while a network-driven Application.LoadLevel runs (prevents request loops).</summary>
        internal static bool IsApplyingNetworkLevelChange = false;

        [Header("Player Prefabs")]
        public GameObject playerPrefab;

        [Header("Scene Names")]
        public string mainMenuScene = "MainMenu";
        public string lobbyScene = "Lobby";
        public string gameScene = "GameScene";

        // Server state
        private INetServer _server;
        private Dictionary<int, PlayerStatePacket> _playerStates = new Dictionary<int, PlayerStatePacket>();
        private HashSet<int> _connectedClients = new HashSet<int>();
        private Vector3 _hostSpawnPosition = Vector3.zero;
        private bool _hostSpawnPositionSet = false;

        // Client state
        private INetClient _client;

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

        // Local player id decoded from the IsYou packet (may arrive before PlayerSpawner)
        private int _pendingLocalPlayerId = 0;

        // Item objects this client asked to pick up (used if the registry id desyncs)
        private readonly Dictionary<int, Item> _pendingPickups = new Dictionary<int, Item>();

        /// <summary>Remember the local object for an in-flight pickup request.</summary>
        public void RememberPendingPickup(int itemNetworkId, Item item)
        {
            if (itemNetworkId > 0 && item != null) _pendingPickups[itemNetworkId] = item;
        }

        // Map markers (bait/trap/camera...) received from the server, for MapUI
        private readonly Dictionary<uint, MapMarkerPacket> _mapMarkers = new Dictionary<uint, MapMarkerPacket>();
        private readonly Dictionary<uint, TrapStatePacket> _trapStates = new Dictionary<uint, TrapStatePacket>();
        private readonly List<Transform> _activeBaitTransforms = new List<Transform>();
        private int _nextMarkerId = 1;

        private static uint MarkerKey(byte markerType, uint markerId)
        {
            return ((uint)markerType << 28) | (markerId & 0x0FFFFFFFu);
        }

        /// <summary>Map markers received from the server (composite type+id key -> packet).</summary>
        public Dictionary<uint, MapMarkerPacket> GetMapMarkers() => _mapMarkers;

        /// <summary>
        /// Register a server-originated marker locally (the host does not receive its own broadcasts).
        /// </summary>
        public void RegisterLocalMapMarker(MapMarkerPacket marker)
        {
            if (marker == null) return;
            _mapMarkers[MarkerKey(marker.MarkerType, marker.MarkerId)] = marker;
            OnMapMarkerReceived?.Invoke(marker);
        }
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
                // _knownPlayerIds stores the OTHER players only (self is excluded to
                // avoid a self-proxy); add the local player back for the count.
                return Math.Max(1, _knownPlayerIds.Count + 1);
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

            // Never leave a previous listener bound
            if (_server != null) StopHost();
            
            // ОБЯЗАТЕЛЬНО установить оба флага!
            RakeMultiplayerPlugin.IsServer.Value = true;
            RakeMultiplayerPlugin.isServer = true; // <--- КРИТИЧЕСКИЙ ФИКС

            _server = TransportFactory.CreateServer();

            if (_server.Start(RakeMultiplayerPlugin.NetworkPort.Value))
            {
                RakeMultiplayerPlugin.Log?.LogInfo($"Server started on port {RakeMultiplayerPlugin.NetworkPort.Value}");
                RakeMultiplayerPlugin.isConnected = true;
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogError("Failed to start server");
                _server = null;
                RakeMultiplayerPlugin.isServer = false;
                RakeMultiplayerPlugin.isConnected = false;
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

            if (_client != null) Disconnect();

            _client = TransportFactory.CreateClient();

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

            // Cache references once per second (avoid FindObjectOfType in hot paths)
            _cacheTimer += Time.deltaTime;
            if (_cacheTimer >= 1f)
            {
                _cacheTimer = 0f;
                if (_cachedRake == null) _cachedRake = FindObjectOfType<NetworkedRakeAI>();
                if (_cachedEnemyAI == null) _cachedEnemyAI = FindObjectOfType<Enemy_AI>();
            }

            // Retry door states that arrived while a door was still animating.
            DoorSync.Update();

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
            _knownPlayerIds.Clear();
            _pendingConnects.Clear();
            _networkedItems.Clear();
            _consumedItemIds.Clear();
            _itemToNetworkId.Clear();
            _mapMarkers.Clear();
            _trapStates.Clear();
            _activeBaitTransforms.Clear();
            _pendingPickups.Clear();
            _hostSpawnPositionSet = false;

            RakeMultiplayerPlugin.isConnected = false;
            RakeMultiplayerPlugin.isServer = false;
        }

        public void Disconnect()
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[RakeNetworkManager] Disconnecting...");

            if (RakeMultiplayerPlugin.isServer || _server != null)
            {
                StopHost();
                return;
            }

            if (_client != null)
            {
                _client.Disconnect();
                _client = null;
            }

            // Clear all client-side session state
            _knownPlayerIds.Clear();
            _pendingConnects.Clear();
            _networkedItems.Clear();
            _consumedItemIds.Clear();
            _itemToNetworkId.Clear();
            _mapMarkers.Clear();
            _trapStates.Clear();
            _activeBaitTransforms.Clear();
            _pendingPickups.Clear();
            _hostSpawnPositionSet = false;
            _clientSceneLoading = false;

            RakeMultiplayerPlugin.isConnected = false;
        }

        private void ProcessServerMessages()
        {
            NetMessage msg;
            while (_server.GetNextMessage(out msg))
            {
                switch (msg.EventType)
                {
                    case NetEventType.Connected:
                        RakeMultiplayerPlugin.Log?.LogInfo($"[Net] Server: client {msg.ConnectionId} connected");
                        OnServerClientConnected(msg.ConnectionId);
                        break;
                    case NetEventType.Data:
                        OnServerDataReceived(msg.ConnectionId, msg.Data);
                        break;
                    case NetEventType.Disconnected:
                        RakeMultiplayerPlugin.Log?.LogInfo($"[Net] Server: client {msg.ConnectionId} disconnected");
                        _connectedClients.Remove(msg.ConnectionId);
                        _playerStates.Remove(msg.ConnectionId);
                        OnServerClientDisconnected(msg.ConnectionId);
                        break;
                }
            }
        }

        private void ProcessClientMessages()
        {
            NetMessage msg;
            while (_client.GetNextMessage(out msg))
            {
                switch (msg.EventType)
                {
                    case NetEventType.Connected:
                        RakeMultiplayerPlugin.Log?.LogInfo("[Net] Client: connected to server");
                        RakeMultiplayerPlugin.isConnected = true;
                        break;
                    case NetEventType.Data:
                        OnClientDataReceived(msg.Data);
                        break;
                    case NetEventType.Disconnected:
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
                int maxPlayers = Mathf.Clamp(RakeMultiplayerPlugin.MaxPlayers.Value, 2, 4);
                if (_connectedClients.Count + 1 >= maxPlayers)
                {
                    RakeMultiplayerPlugin.Log?.LogWarning("[Net] server full (" + maxPlayers + " players), rejected C" + connectionId);
                    _server.Disconnect(connectionId);
                    return;
                }

                _connectedClients.Add(connectionId);

                // ID scheme: Host = 1, Clients = connectionId + 1
                int assignedPlayerId = connectionId + 1;
                _playerHealth[assignedPlayerId] = 100f;
                _playerNames[assignedPlayerId] = "PLAYER " + assignedPlayerId;

                // 0. Send HOST to NEW client (ВСЕГДА, даже если позиция не установлена)
                Vector3 hostPos = _hostSpawnPositionSet ? _hostSpawnPosition : Vector3.zero;
                if (!_hostSpawnPositionSet)
                {
                    RakeMultiplayerPlugin.Log?.LogWarning("[Net] Host spawn pos not set. Sending Vector3.zero.");
                }
                _playerNames[1] = RakeMultiplayerPlugin.PlayerName.Value;
                var hostPacket = new PlayerConnectedPacket
                {
                    PlayerId = 1, // Host always has ID 1
                    SpawnPosition = hostPos,
                    IsYou = false,
                    Name = _playerNames[1]
                };
                _server.Send(connectionId, hostPacket.Serialize());
            _server.Send(connectionId, new RakeOwnerPacket { OwnerId = LobbyState.RakeOwnerId }.Serialize());

                // Late join: send the current door states, otherwise every door is closed
                // for the newcomer until somebody touches it again.
                DoorSync.SendSnapshotToPlayer(connectionId + 1);

                // Late join: tell the newcomer about items already taken, otherwise they
                // see ghost items that cannot be picked up.
                foreach (var consumedId in _consumedItemIds)
                {
                    _server.Send(connectionId, new ItemDestroyedPacket
                    {
                        ItemNetworkId = consumedId,
                        PickerId = 0,
                        ItemType = string.Empty
                    }.Serialize());
                }

                // Joining a match that is already running: load the level now. The client
                // sends ClientReady once its scene/registries exist.
                if (SessionManager.Instance != null && SessionManager.Instance.IsRunning &&
                    UnityEngine.Application.loadedLevel > 0)
                {
                    _server.Send(connectionId,
                        new StartGamePacket(UnityEngine.Application.loadedLevel.ToString()).Serialize());
                }
                RakeMultiplayerPlugin.Log?.LogInfo($"[Net] Sent Host info to C{connectionId}: pos={hostPos}");

                // 1. Send existing CLIENTS to the NEW client (use assignedPlayerId = existingId + 1)
                foreach (var existingId in _connectedClients)
                {
                    if (existingId == connectionId) continue;

                    // Always announce existing players. Requiring a PlayerState entry meant
                    // a hunter who had not moved yet was unknown to the new client.
                    Vector3 existingPos = hostPos;
                    if (_playerStates.TryGetValue(existingId, out var existingState))
                    {
                        existingPos = existingState.Position;
                    }
                    else if (PlayerSpawner.Instance != null &&
                             PlayerSpawner.Instance.TryGetRemotePlayer(existingId + 1, out var existingProxy) &&
                             existingProxy != null)
                    {
                        existingPos = existingProxy.transform.position;
                    }

                    int existingPlayerId = existingId + 1;
                    var existingPacket = new PlayerConnectedPacket
                    {
                        PlayerId = existingPlayerId,
                        SpawnPosition = existingPos,
                        IsYou = false
                    };
                    _server.Send(connectionId, existingPacket.Serialize());
                }

                // 2. Broadcast NEW player to ALL
                var newPacketForNew = new PlayerConnectedPacket
                {
                    PlayerId = assignedPlayerId,
                    SpawnPosition = hostPos,
                    IsYou = true,
                    Name = _playerNames[assignedPlayerId]
                };
                _server.Send(connectionId, newPacketForNew.Serialize());

                var newPacketForOthers = new PlayerConnectedPacket
                {
                    PlayerId = assignedPlayerId,
                    SpawnPosition = hostPos,
                    IsYou = false,
                    Name = _playerNames[assignedPlayerId]
                };
                var dataForOthers = newPacketForOthers.Serialize();
                foreach (var connId in _connectedClients)
                {
                    if (connId != connectionId) _server.Send(connId, dataForOthers);
                }

                // 3. HOST spawns remote player for the NEW client directly
                if (PlayerSpawner.Instance != null)
                {
                    // Spawn next to the host instead of the old (2*id, 0, 0) point outside
                    // the level; the first PlayerState snaps it to the real position.
                    Vector3 spawnPos = _hostSpawnPositionSet
                        ? _hostSpawnPosition + new Vector3(assignedPlayerId * 0.8f, 0f, 0f)
                        : new Vector3(assignedPlayerId * 2f, 0f, 0f);
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
            _playerHealth.Remove(connectionId + 1);
            _playerNames.Remove(connectionId + 1);

            // If the departed client owned the Rake, hand it back to the AI. The owner id
            // used to stay stale, so nobody could send RakeState and the Rake froze forever.
            if (LobbyState.RakeOwnerId == connectionId + 1)
            {
                RakeMultiplayerPlugin.Log?.LogInfo("[RakePlay] owner disconnected, returning the Rake to the AI");
                SetRakeOwner(0);
            }

            // Remove the host-side proxy of the departed player
            PlayerSpawner.Instance?.RemoveRemotePlayer(connectionId + 1);

            var packet = new PlayerDisconnectedPacket { PlayerId = connectionId + 1 };
            var data = packet.Serialize();
            foreach (var connId in _connectedClients)
            {
                _server.Send(connId, data);
            }
        }

        /// <summary>
        /// Server: send raw packet data to a single player.
        /// Id scheme: host = 1, clients = connectionId + 1.
        /// </summary>
        public bool SendToPlayer(int playerId, byte[] data)
        {
            if (!RakeMultiplayerPlugin.isServer || _server == null) return false;
            if (playerId <= 1) return false; // host is local, nothing to send

            int connectionId = playerId - 1;
            if (!_connectedClients.Contains(connectionId)) return false;

            _server.Send(connectionId, data);
            return true;
        }

        private void OnServerDataReceived(int connectionId, byte[] data)
        {
            try
            {
                var packet = PacketSerializer.Deserialize(data);
                // PlayerState/RakeState arrive ~20 Hz per player: logging them flooded the
                // log and caused disk stalls during combat.
                if (packet.Type != PacketType.PlayerState && packet.Type != PacketType.RakeState)
                {
                    RakeMultiplayerPlugin.Log?.LogInfo($"[Net] S<-C{connectionId} {packet.Type} ({data.Length}b)");
                }

                switch (packet.Type)
                {
                    case PacketType.PlayerState:
                        HandleServerPlayerState(connectionId, (PlayerStatePacket)packet, data);
                        break;
                    case PacketType.DamageRequest:
                        HandleServerDamageRequest(connectionId, (DamageRequestPacket)packet, data);
                        break;
                    case PacketType.PlayerRagdollRequest:
                        HandleServerPlayerRagdollRequest(connectionId, (PlayerRagdollRequestPacket)packet);
                        break;
                    case PacketType.PlayerDied:
                        HandleServerPlayerDied(connectionId, (PlayerDiedPacket)packet);
                        break;
                    case PacketType.ClientReady:
                        HandleServerClientReady(connectionId, (ClientReadyPacket)packet);
                        break;
                    case PacketType.DoorAction:
                        DoorSync.HandleServerRequest((DoorActionPacket)packet);
                        break;
                    case PacketType.FireVisuals:
                        HandleServerFireVisuals(connectionId, (FireVisualsPacket)packet);
                        break;
                    case PacketType.RakeState:
                        HandleServerRakeState(connectionId, (RakeStatePacket)packet);
                        break;
                    case PacketType.RakeRoleRequest:
                        HandleServerRakeRoleRequest(connectionId, (RakeRoleRequestPacket)packet);
                        break;
                    case PacketType.RakeAttackRequest:
                        HandleServerRakeAttackRequest(connectionId, (RakeAttackRequestPacket)packet);
                        break;
                    case PacketType.RakeSound:
                        HandleServerRakeSound(connectionId, (RakeSoundPacket)packet);
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
                    case PacketType.LevelChange:
                        HandleServerLevelChange(connectionId, (LevelChangePacket)packet, data);
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

            // A NaN/huge position would poison the transforms of every peer (and the item
            // proximity checks below).
            Vector3 pos = packet.Position;
            if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z) ||
                Mathf.Abs(pos.x) > 5000f || Mathf.Abs(pos.y) > 5000f || Mathf.Abs(pos.z) > 5000f)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Net] rejected invalid PlayerState from C" + connectionId);
                return;
            }

            Quaternion rot = packet.Rotation;
            if (float.IsNaN(rot.x) || float.IsNaN(rot.y) || float.IsNaN(rot.z) || float.IsNaN(rot.w) ||
                Mathf.Abs(rot.x * rot.x + rot.y * rot.y + rot.z * rot.z + rot.w * rot.w - 1f) > 0.2f)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Net] rejected invalid PlayerState rotation from C" + connectionId);
                return;
            }

            // Client-reported health: decreases are accepted immediately, healing is
            // rate-limited, so environment damage reaches the server but a client cannot
            // simply declare itself at full HP after taking hits.
            float reportedHealth = Mathf.Clamp(packet.Health, 0f, 100f);
            float knownHealth;
            if (!_playerHealth.TryGetValue(assignedPlayerId, out knownHealth)) knownHealth = reportedHealth;
            if (reportedHealth < knownHealth) knownHealth = reportedHealth;
            else knownHealth = Mathf.Min(reportedHealth, knownHealth + 10f);
            _playerHealth[assignedPlayerId] = knownHealth;

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

        /// <summary>
        /// Server: a client says its player just got hit. Validate the sender and broadcast
        /// the ragdoll so every peer sees the victim body fly.
        /// </summary>
        private void HandleServerPlayerRagdollRequest(int connectionId, PlayerRagdollRequestPacket packet)
        {
            // Never trust packet.PlayerId: a client could ragdoll-lock any other player.
            // The request is always about the sender.
            int playerId = connectionId + 1;

            RakeMultiplayerPlugin.Log?.LogInfo("[Ragdoll] Server got request from C" + connectionId +
                                               " for player " + playerId);

            PlayerRagdoll.TriggerNetworked(playerId, packet.Direction);
        }

        public void HandleServerDamageRequest(int connectionId, DamageRequestPacket packet, byte[] rawData)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.4] Server received DamageRequest from C{connectionId}: shooter={packet.ShooterId}, target={packet.TargetId}, amount={packet.Amount}, type={packet.DamageType}, shooterPos={packet.ShooterPosition}");

            // Do NOT gate the whole shot on a live Enemy_AI: after the Rake dies the cached
            // reference is null, and every client shot (including PvP) was silently dropped.

            int shooterId = connectionId + 1;
            if (packet.ShooterId <= 0) packet.ShooterId = shooterId;
            if (packet.ShooterId != shooterId)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Ballistics] spoofed shooter id from C" + connectionId);
                return;
            }

            Vector3 origin = packet.ShooterPosition;
            Vector3 direction = packet.Direction;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = _cachedEnemyAI != null
                    ? _cachedEnemyAI.transform.position + Vector3.up * 1.2f - origin
                    : Vector3.forward;
            }
            direction.Normalize();

            Transform shooter = FindPlayerTransform(shooterId);
            if (shooter != null && Vector3.Distance(origin, shooter.position) > 8f)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Ballistics] rejected shot: origin too far from the shooter");
                return;
            }

            // The server decides the weapon profile. A modified client used to be able to
            // send Amount = 300, Pellets = 12 (3600 damage) with any weapon in hand.
            string damageType = string.IsNullOrEmpty(packet.DamageType) ? "SniperShot" : packet.DamageType;
            if (!string.Equals(damageType, "SniperShot", System.StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(damageType, "Bullet", System.StringComparison.OrdinalIgnoreCase))
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Ballistics] rejected unknown damage type: " + damageType);
                return;
            }
            int amount;
            int pellets;
            float spread;
            if (string.Equals(damageType, "SniperShot", System.StringComparison.OrdinalIgnoreCase))
            {
                amount = 200; pellets = 1; spread = 0f;
            }
            else if (string.Equals(damageType, "Bullet", System.StringComparison.OrdinalIgnoreCase))
            {
                amount = 17; pellets = 6; spread = 4.5f;
            }
            else
            {
                amount = Mathf.Clamp(packet.Amount, 1, 100);
                pellets = Mathf.Clamp(packet.Pellets, 1, 6);
                spread = Mathf.Clamp(packet.Spread, 0f, 10f);
            }

            // Real ballistics: the server simulates the bullet and applies damage on impact.
            ProjectileSystem.SpawnShot(origin, direction, pellets, spread, amount, damageType, shooterId);

            // The shot is heard: let the mod AI decide (start a lurk cycle, turn
            // towards the noise). The vanilla "danger" escape is suppressed by
            // RakeNoisePatch so it cannot fight the stalk state machine.
            var rakeAI = _cachedEnemyAI != null ? _cachedEnemyAI.GetComponent<NetworkedRakeAI>() : null;
            if (rakeAI != null) rakeAI.NotifyNoise(packet.ShooterPosition);


        }

        /// <summary>
        /// Server-side hit validation for a client's shot. The client sends its aim
        /// direction; the hit is accepted only when the Rake is in front of the muzzle,
        /// within weapon range, and not behind a wall.
        /// </summary>
        private static bool ValidateClientShot(DamageRequestPacket packet, Enemy_AI enemy)
        {
            if (enemy == null) return false;

            Vector3 origin = packet.ShooterPosition;
            Vector3 target = enemy.transform.position + Vector3.up * 1.2f;
            Vector3 toTarget = target - origin;
            float distance = toTarget.magnitude;

            bool sniper = string.Equals(packet.DamageType, "SniperShot",
                                        System.StringComparison.OrdinalIgnoreCase);

            float maxRange = sniper ? 600f : 300f;
            float maxAngle = sniper ? 3.5f : 8f;

            if (distance < 0.5f || distance > maxRange) return false;

            Vector3 aim = packet.Direction;
            if (aim.sqrMagnitude < 0.001f) aim = toTarget;
            aim.Normalize();

            // Sanity angle, then a real capsule test against the body: aiming at the
            // head or legs of a distant Rake must still count, while a clear miss must not.
            if (Vector3.Angle(aim, toTarget) > 25f) return false;
            if (!RayHitsRake(origin, aim, enemy.transform.position)) return false;

            // A solid object between the muzzle and the Rake blocks the shot.
            RaycastHit hit;
            if (Physics.Linecast(origin, target, out hit))
            {
                if (hit.collider == null) return false;
                if (!hit.collider.transform.IsChildOf(enemy.transform)) return false;
            }

            return true;
        }
        /// <summary>Does the shot ray pass through the Rake's body capsule?</summary>
        private static bool RayHitsRake(Vector3 origin, Vector3 direction, Vector3 basePosition)
        {
            Vector3 a = basePosition + Vector3.up * 0.25f;
            Vector3 b = basePosition + Vector3.up * 2.2f;
            Vector3 ab = b - a;
            Vector3 w0 = origin - a;

            float aa = Vector3.Dot(direction, direction);
            float bb = Vector3.Dot(direction, ab);
            float cc = Vector3.Dot(ab, ab);
            float dd = Vector3.Dot(direction, w0);
            float ee = Vector3.Dot(ab, w0);

            float denom = aa * cc - bb * bb;
            float s, t;

            if (Mathf.Abs(denom) < 0.0001f)
            {
                s = 0f;
                t = cc > 0.0001f ? Mathf.Clamp01(ee / cc) : 0f;
            }
            else
            {
                s = (bb * ee - cc * dd) / denom;
                t = (aa * ee - bb * dd) / denom;
            }

            if (s < 0f)
            {
                s = 0f;
                t = cc > 0.0001f ? Mathf.Clamp01(ee / cc) : 0f;
            }
            if (t < 0f)
            {
                t = 0f;
                s = aa > 0.0001f ? Mathf.Max(0f, -dd / aa) : 0f;
            }
            else if (t > 1f)
            {
                t = 1f;
                s = aa > 0.0001f ? Mathf.Max(0f, (bb - dd) / aa) : 0f;
            }

            Vector3 pointRay = origin + direction * s;
            Vector3 pointBody = a + ab * Mathf.Clamp01(t);

            return Vector3.Distance(pointRay, pointBody) <= 0.7f;
        }
        private float ApplyDamageToEnemy(Enemy_AI enemyAI, int amount, string damageType, Vector3 shooterPosition)
        {
            if (enemyAI == null) return 0f;

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
                _mapMarkers[MarkerKey(packet.MarkerType, packet.MarkerId)] = packet;
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
            _mapMarkers[MarkerKey(packet.MarkerType, packet.MarkerId)] = packet;
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

        /// <summary>
        /// Called per game scene load: scene objects (items, cameras, animals) are recreated,
        /// so the per-scene registries must be rebuilt with the same deterministic IDs.
        /// </summary>
        public void PrepareForSceneLoad()
        {
            _networkedItems.Clear();
            _consumedItemIds.Clear();
            _itemToNetworkId.Clear();
            _mapMarkers.Clear();
            _trapStates.Clear();
            _activeBaitTransforms.Clear();
            _nextItemNetworkId = 1;
            _nextMarkerId = 1;
            _pendingPickups.Clear();

            RakeMultiplayer.Patches.ItemPickupSync.ClearConsumed();
            NetworkedCamera.ClearRegistry();
            NetworkedTrap.ResetSceneState();

            RakeMultiplayerPlugin.Log?.LogInfo("[Scene] Per-scene network registries reset");
        }

        /// <summary>
        /// Spawn remote player objects for everyone we know about.
        /// Must be called after the game scene loads (the player prefab is only assigned
        /// then), otherwise clients that connected in the lobby are never visible to the host.
        /// Idempotent - already-spawned players are skipped.
        /// </summary>
        public void EnsureRemotePlayersSpawned()
        {
            var spawner = PlayerSpawner.Instance;
            if (spawner == null)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Visual] EnsureRemotePlayersSpawned: PlayerSpawner missing");
                return;
            }

            // Objects from the previous scene are destroyed but may still be in the dictionary
            spawner.PruneDestroyedRemotePlayers();

            // The prefab may be a destroyed reference after a level change - re-acquire it
            if (spawner.playerPrefab == null)
            {
                if (spawner.LocalPlayer != null)
                {
                    spawner.playerPrefab = spawner.LocalPlayer.gameObject;
                }
                else if (FPSController.instance != null)
                {
                    spawner.playerPrefab = FPSController.instance.gameObject;
                }
            }

            if (spawner.playerPrefab == null)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Visual] EnsureRemotePlayersSpawned: playerPrefab not assigned yet - will retry on next scene tick");
                return;
            }

            if (RakeMultiplayerPlugin.isServer)
            {
                // Host: one remote object per connected client
                foreach (var connId in _connectedClients)
                {
                    int playerId = connId + 1;
                    if (spawner.TryGetRemotePlayer(playerId, out _)) continue;

                    Vector3 pos = _playerStates.TryGetValue(connId, out var state)
                        ? state.Position
                        : new Vector3(playerId * 2f, 0f, 0f);

                    spawner.SpawnRemotePlayer(playerId, pos);
                    RakeMultiplayerPlugin.Log?.LogInfo($"[Visual] Host spawned missing remote player P{playerId}");
                }

                // Push authoritative positions so clients can place the host/other players
                var host = spawner.LocalPlayer;
                if (host != null)
                {
                    var hostState = new PlayerStatePacket
                    {
                        PlayerId = host.PlayerId,
                        Position = host.transform.position,
                        Rotation = host.transform.rotation,
                        AnimState = 0
                    };
                    ServerBroadcast(hostState.Serialize());
                }

                foreach (var kvp in spawner.GetAllRemotePlayers())
                {
                    if (kvp.Value == null) continue;
                    var packet = new PlayerStatePacket
                    {
                        PlayerId = kvp.Key,
                        Position = kvp.Value.transform.position,
                        Rotation = kvp.Value.transform.rotation,
                        AnimState = 0
                    };
                    ServerBroadcast(packet.Serialize());
                }
            }
            else
            {
                // Client: one remote object per known other player
                int localId = spawner.LocalPlayer != null && spawner.LocalPlayer.PlayerId > 0
                    ? spawner.LocalPlayer.PlayerId
                    : (spawner.LastLocalPlayerId > 0 ? spawner.LastLocalPlayerId : _pendingLocalPlayerId);

                if (localId > 0 && spawner.TryGetRemotePlayer(localId, out _))
                {
                    spawner.RemoveRemotePlayer(localId); // remove accidental self-proxy
                }

                foreach (var playerId in _knownPlayerIds)
                {
                    if (playerId == localId) continue;
                    if (spawner.TryGetRemotePlayer(playerId, out _)) continue;

                    Vector3 missingPos = spawner.LocalPlayer != null
                        ? spawner.LocalPlayer.transform.position + new Vector3(playerId * 0.8f, 0f, 0f)
                        : new Vector3(playerId * 2f, 0f, 0f);
                    spawner.SpawnRemotePlayer(playerId, missingPos);
                    RakeMultiplayerPlugin.Log?.LogInfo($"[Visual] Client spawned missing remote player P{playerId}");
                }

                // Tell the server/host where we actually are
                spawner.LocalPlayer?.ForceSendState();
            }
        }

        /// <summary>
        /// Called before any local Application.LoadLevel. In a connected session the
        /// level change must be applied on every peer, otherwise players end up in
        /// different levels (different timers/no proxies).
        /// </summary>
        public void NotifyLocalLevelLoad(int levelIndex)
        {
            if (!RakeMultiplayerPlugin.isConnected) return;
            if (IsApplyingNetworkLevelChange) return;
            if (levelIndex <= 0) return; // main menu

            var packet = new LevelChangePacket { LevelIndex = levelIndex };
            if (RakeMultiplayerPlugin.isServer)
            {
                ServerBroadcast(packet.Serialize());
            }
            else
            {
                ClientSend(packet.Serialize());
            }
        }

        private void HandleServerLevelChange(int connectionId, LevelChangePacket packet, byte[] rawData)
        {
            // Level changes are host-authoritative. A client used to be able to reload an
            // arbitrary scene for everyone (or spam reloads and kill the session).
            RakeMultiplayerPlugin.Log?.LogWarning("[Scene] ignored LevelChange from client C" + connectionId);
        }

        private void HandleClientLevelChange(LevelChangePacket packet)
        {
            // Level 0 = back to the lobby/menu after the match.
            if (packet.LevelIndex < 0) return;

            // Dedupe: the host's StartGame already loads the same level.
            if (packet.LevelIndex == UnityEngine.Application.loadedLevel) return;

            ApplyNetworkLevelChange(packet.LevelIndex);
        }

        /// <summary>Server: end-of-match return to the lobby scene, keeping the session.</summary>
        public void ServerReturnToLobby()
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            var packet = new LevelChangePacket { LevelIndex = 0 };
            ServerBroadcast(packet.Serialize());
            ApplyNetworkLevelChange(0);
        }

        private void ApplyNetworkLevelChange(int levelIndex)
        {
            IsApplyingNetworkLevelChange = true;
            try
            {
                UnityEngine.Application.LoadLevel(levelIndex);
            }
            finally
            {
                IsApplyingNetworkLevelChange = false;
            }
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
                    case PacketType.PlayerDamaged:
                        HandleClientPlayerDamaged((PlayerDamagedPacket)packet);
                        break;
                    case PacketType.PlayerRagdoll:
                        HandleClientPlayerRagdoll((PlayerRagdollPacket)packet);
                        break;
                    case PacketType.PlayerDied:
                        PlayerDeathSystem.ApplyDeath((PlayerDiedPacket)packet);
                        break;
                    case PacketType.PlayerRespawn:
                        PlayerDeathSystem.ApplyRespawn((PlayerRespawnPacket)packet);
                        break;
                    case PacketType.ItemSpawn:
                        PlayerDeathSystem.SpawnNetworkedDrop((ItemSpawnPacket)packet);
                        break;
                    case PacketType.DoorAction:
                        DoorSync.HandleBroadcast((DoorActionPacket)packet);
                        break;
                    case PacketType.LobbyRole:
                        LobbyState.HostIsRake = ((LobbyRolePacket)packet).HostIsRake;
                        break;
                    case PacketType.RakeGrab:
                        HandleClientRakeGrab((RakeGrabPacket)packet);
                        break;
                    case PacketType.RakeOwner:
                        HandleClientRakeOwner((RakeOwnerPacket)packet);
                        break;
                    case PacketType.RakeSound:
                        HandleClientRakeSound((RakeSoundPacket)packet);
                        break;
                    case PacketType.RakeDied:
                        HandleClientRakeDied((RakeDiedPacket)packet);
                        break;
                    case PacketType.TestArena:
                        HandleClientTestArena((TestArenaPacket)packet);
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
                    case PacketType.ItemState:
                        HandleClientItemState((ItemStatePacket)packet);
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
                    case PacketType.PlayerTeleport:
                        HandleClientPlayerTeleport((PlayerTeleportPacket)packet);
                        break;
                    case PacketType.LevelChange:
                        HandleClientLevelChange((LevelChangePacket)packet);
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

        // ===================== Rake ownership (any player can be the Rake) =====================

        public void SetRakeOwner(int ownerId)
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            int clamped = Mathf.Max(0, ownerId);
            if (LobbyState.RakeOwnerId == clamped) return;

            LobbyState.RakeOwnerId = clamped;
            RakeMultiplayerPlugin.Log?.LogInfo("[Lobby] Rake owner set to P" + clamped);
            ServerBroadcast(new RakeOwnerPacket { OwnerId = clamped }.Serialize());
            RakeControlSync.Apply("host changed the Rake owner");
        }

        private void HandleServerRakeRoleRequest(int connectionId, RakeRoleRequestPacket packet)
        {
            int playerId = connectionId + 1;
            if (packet.TakeRole) SetRakeOwner(playerId);
            else if (LobbyState.RakeOwnerId == playerId) SetRakeOwner(0);
        }

        private void HandleClientRakeOwner(RakeOwnerPacket packet)
        {
            int previous = LobbyState.RakeOwnerId;
            LobbyState.RakeOwnerId = packet.OwnerId;

            if (previous != packet.OwnerId)
            {
                RakeMultiplayerPlugin.Log?.LogInfo("[Lobby] Rake owner is now P" + packet.OwnerId +
                                                   " (local P" + LobbyState.LocalPlayerId + ")");
            }

            RakeControlSync.Apply("owner packet");
        }

        /// <summary>Server: the owning client published its Rake body. Mirror it and relay.</summary>
        private void HandleServerRakeState(int connectionId, RakeStatePacket packet)
        {
            if (connectionId + 1 != LobbyState.RakeOwnerId) return;

            var rake = _cachedRake != null ? _cachedRake : UnityEngine.Object.FindObjectOfType<NetworkedRakeAI>();
            if (rake == null) return;

            // Never trust the owner's position blindly: reject NaN and speed-hacked jumps.
            if (float.IsNaN(packet.Position.x) || float.IsNaN(packet.Position.y) ||
                float.IsNaN(packet.Position.z))
            {
                return;
            }

            float now = Time.time;
            float dt = Mathf.Max(0.02f, now - _lastRakeOwnerTime);
            if (_lastRakeOwnerTime > 0f &&
                Vector3.Distance(packet.Position, _lastRakeOwnerPosition) > 30f * dt)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[RakePlay] rejected Rake teleport from C" + connectionId);
                return;
            }
            _lastRakeOwnerPosition = packet.Position;
            _lastRakeOwnerTime = now;

            // Health is server-authoritative; the owner only publishes movement/animation.
            packet.Health = rake.CurrentHealth;

            rake.ApplyOwnerSnapshot(packet.Position, packet.Rotation,
                                    packet.AnimFlags, packet.WalkType, packet.AnimSpeed);

            byte[] data = packet.Serialize();
            foreach (var connId in _connectedClients)
            {
                if (connId == connectionId) continue;
                _server.Send(connId, data);
            }
        }

        /// <summary>Server: validate and land an attack from the owning client.</summary>
        private void HandleServerRakeAttackRequest(int connectionId, RakeAttackRequestPacket packet)
        {
            if (connectionId + 1 != LobbyState.RakeOwnerId)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[RakePlay] attack request from a non-owner ignored (C" +
                                                      connectionId + ")");
                return;
            }

            // Rate limit: every accepted request starts a coroutine and broadcasts.
            if (Time.time - _lastRakeAttackTime < 1f) return;
            _lastRakeAttackTime = Time.time;

            var rake = RakeHighlight.FindRake();
            if (rake == null) return;
            Vector3 rakePos = rake.position;

            if (packet.Kind == 0)
            {
                Transform target = FindPlayerTransform(packet.TargetPlayerId);
                if (target == null) return;
                if (Vector3.Distance(target.position, rakePos) > 3.5f)
                {
                    RakeMultiplayerPlugin.Log?.LogWarning("[RakePlay] rejected grab: target out of reach");
                    return;
                }

                Vector3 from = rakePos + Vector3.up * 1.2f;
                Vector3 to = target.position + Vector3.up * 1.2f;
                Vector3 delta = to - from;
                float grabDistance = delta.magnitude;
                if (grabDistance > 0.5f)
                {
                    var blockers = Physics.RaycastAll(from, delta / grabDistance, grabDistance - 0.5f, ~0);
                    for (int i = 0; i < blockers.Length; i++)
                    {
                        var blocker = blockers[i].collider;
                        if (blocker == null || blocker.isTrigger) continue;
                        if (blocker.transform == rake || blocker.transform.IsChildOf(rake)) continue;
                        RakeMultiplayerPlugin.Log?.LogWarning("[RakePlay] rejected grab: wall in the way");
                        return;
                    }
                }

                Vector3 focus = packet.FocusPosition != Vector3.zero
                    ? packet.FocusPosition
                    : rakePos + Vector3.up * 1.6f;

                ServerBroadcast(new RakeGrabPacket
                {
                    PlayerId = packet.TargetPlayerId,
                    RakePosition = rakePos,
                    FocusPosition = focus
                }.Serialize());

                BroadcastRakeSound(0, rakePos);
                // The Rake melee is always 10 damage; a client could otherwise send 99999.
                StartCoroutine(ServerRakePlayerHit(packet.TargetPlayerId,
                                                  Mathf.Clamp(packet.Amount > 0 ? packet.Amount : 10, 1, 10),
                                                  string.IsNullOrEmpty(packet.DamageType) ? "RakeKick" : packet.DamageType,
                                                  rakePos));
            }
            else if (packet.Kind == 1)
            {
                Animal_AI animal = FindAnimalNear(packet.TargetPosition, 3.5f);
                if (animal == null) return;
                StartCoroutine(ServerRakeAnimalKill(animal, rakePos));
            }
            else
            {
                RecordCamera_obj cam = FindCameraNear(packet.TargetPosition, 3.5f);
                if (cam == null) return;
                StartCoroutine(ServerRakeCameraBreak(cam, rakePos));
            }
        }

        private IEnumerator ServerRakePlayerHit(int targetId, int amount, string damageType, Vector3 rakePos)
        {
            yield return new WaitForSeconds(1.2f);

            Transform target = FindPlayerTransform(targetId);
            if (target == null) yield break;
            if (Vector3.Distance(target.position, rakePos) > 8f) yield break;

            var rakeAI = _cachedRake != null ? _cachedRake : UnityEngine.Object.FindObjectOfType<NetworkedRakeAI>();
            if (rakeAI == null) yield break;

            rakeAI.AttackPlayer(target, amount, damageType);
            BroadcastRakeSound(1, rakeAI.transform.position);
        }

        private IEnumerator ServerRakeAnimalKill(Animal_AI animal, Vector3 rakePos)
        {
            yield return new WaitForSeconds(1.8f);

            if (animal == null || !animal.isAlive) yield break;
            if (Vector3.Distance(animal.transform.position, rakePos) > 8f) yield break;

            animal.TakeDamage(250, "RakeAnimalKick");
        }

        private IEnumerator ServerRakeCameraBreak(RecordCamera_obj cam, Vector3 rakePos)
        {
            yield return new WaitForSeconds(1.6f);

            if (cam == null) yield break;
            if (Vector3.Distance(cam.transform.position, rakePos) > 8f) yield break;

            cam.SetUnsetupState();
        }

        private static Transform FindPlayerTransform(int playerId)
        {
            var spawner = PlayerSpawner.Instance;
            if (spawner == null) return null;

            if (spawner.LocalPlayer != null && spawner.LocalPlayer.PlayerId == playerId)
            {
                return spawner.LocalPlayer.transform;
            }

            NetworkedPlayer remote;
            if (spawner.TryGetRemotePlayer(playerId, out remote) && remote != null)
            {
                return remote.transform;
            }

            return null;
        }

        private static Animal_AI FindAnimalNear(Vector3 position, float radius)
        {
            Animal_AI best = null;
            float bestDistance = radius;

            var animals = UnityEngine.Object.FindObjectsOfType<Animal_AI>();
            for (int i = 0; i < animals.Length; i++)
            {
                var animal = animals[i];
                if (animal == null || !animal.isAlive) continue;
                float d = Vector3.Distance(animal.transform.position, position);
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = animal;
            }

            return best;
        }

        private static RecordCamera_obj FindCameraNear(Vector3 position, float radius)
        {
            RecordCamera_obj best = null;
            float bestDistance = radius;

            var cameras = UnityEngine.Object.FindObjectsOfType<RecordCamera_obj>();
            for (int i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (cam == null || cam.cameraState != RecordCamera_obj.Camera_state.Setup) continue;
                float d = Vector3.Distance(cam.transform.position, position);
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = cam;
            }

            return best;
        }

        // ============================ Rake sounds / death ============================

        /// <summary>Server: play locally and relay. Client: ask the server to relay.</summary>
        public void BroadcastRakeSound(byte sound, Vector3 position)
        {
            var packet = new RakeSoundPacket { Sound = sound, Position = position };
            byte[] data = packet.Serialize();

            if (RakeMultiplayerPlugin.isServer)
            {
                PlayRakeSoundLocal(sound, position);
                ServerBroadcast(data);
            }
            else if (RakeMultiplayerPlugin.isConnected)
            {
                ClientSend(data);
            }
            else
            {
                PlayRakeSoundLocal(sound, position);
            }
        }

        private void HandleServerRakeSound(int connectionId, RakeSoundPacket packet)
        {
            if (connectionId + 1 != LobbyState.RakeOwnerId) return;
            PlayRakeSoundLocal(packet.Sound, packet.Position);
            ServerBroadcast(packet.Serialize());
        }

        private void HandleClientRakeSound(RakeSoundPacket packet)
        {
            PlayRakeSoundLocal(packet.Sound, packet.Position);
        }

        /// <summary>Play a Rake attack clip in 3D at the given position on this peer.</summary>
        public static void PlayRakeSoundLocal(byte sound, Vector3 position)
        {
            AudioClip clip = null;

            var rake = RakeHighlight.FindRake();
            var enemy = rake != null ? rake.GetComponent<Enemy_AI>() : null;
            if (enemy != null)
            {
                AudioClip[] clips = sound == 1 ? enemy.playerAttackKicks : enemy.playerAttackRoars;
                if (clips != null && clips.Length > 0)
                {
                    clip = clips[UnityEngine.Random.Range(0, clips.Length)];
                }
            }

            if (clip == null) return;
            PlayerAudio.PlayAt(clip, position, 1f);
        }

        public void BroadcastRakeDied(Vector3 position, Quaternion rotation)
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            RakeMultiplayerPlugin.Log?.LogInfo("[Rake] The Rake was killed at " + position.ToString("F1"));
            ServerBroadcast(new RakeDiedPacket { Position = position, Rotation = rotation }.Serialize());
            LobbyState.RakeOwnerId = 0;
        }

        private void HandleClientRakeDied(RakeDiedPacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[Rake] The Rake is dead (corpse at " +
                                               packet.Position.ToString("F1") + ")");

            var rake = RakeHighlight.FindRake();
            if (rake != null)
            {
                var enemy = rake.GetComponent<Enemy_AI>();
                RakeCorpse.Spawn(rake.gameObject,
                                 enemy != null ? enemy.deadPrefab : null,
                                 packet.Position, packet.Rotation);

                if (RakePlayerController.IsActive) RakePlayerController.SetEnabled(false);
                UnityEngine.Object.Destroy(rake.gameObject);
            }

            LobbyState.RakeOwnerId = 0;
            UI.RuntimeUIBuilder.ShowRakeDeath();
        }

        /// <summary>Server: push the Rake health to every peer (bar sync + scene reloads).</summary>
        public void BroadcastRakeHealth()
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            float health = 0f;
            var rake = RakeHighlight.FindRake();
            if (rake != null)
            {
                var ai = rake.GetComponent<NetworkedRakeAI>();
                if (ai != null) health = ai.CurrentHealth;
            }

            ServerBroadcast(new DamageAppliedPacket { TargetId = 0, NewHealth = health }.Serialize());
        }
        private void HandleClientStartGame(StartGamePacket packet)
        {
            if (_clientSceneLoading) return;
            _clientSceneLoading = true;

            RakeMultiplayerPlugin.Log?.LogInfo($"[Net] Received StartGame packet! Loading level index: {packet.SceneName}");

            // The level arrives in a moment; show the styled loader instead of
            // freezing on the lobby for the whole synchronous LoadLevel.
            UI.LoadingScreen.Show("ENTERING THE FOREST", "CLIENT", "The host has started the hunt", false);
            StartCoroutine(ClientStartGameRoutine(packet));
        }

        private IEnumerator ClientStartGameRoutine(StartGamePacket packet)
        {
            UI.LoadingScreen.SetProgress(0.2f);
            yield return null;
            yield return new WaitForSeconds(1.05f);

            UI.LoadingScreen.SetStatus("Loading the level");
            UI.LoadingScreen.SetProgress(1f);
            yield return new WaitForSeconds(0.35f);

            IsApplyingNetworkLevelChange = true;
            try
            {
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
            finally
            {
                IsApplyingNetworkLevelChange = false;
                // Allow the next match to be started (and do not latch on load failure)
                _clientSceneLoading = false;
            }

            UI.LoadingScreen.SetProgress(1f);
            UI.LoadingScreen.Hide();
        }

        /// <summary>
        /// Server: teleport every player to a position (safehouse respawn, Task 4.6).
        /// The host moves locally; each client receives a targeted teleport packet.
        /// </summary>
        public void BroadcastTestArena(Vector3 center)
        {
            if (!RakeMultiplayerPlugin.isServer) return;
            ServerBroadcast(new TestArenaPacket { Center = center }.Serialize());
        }

        private void HandleClientTestArena(TestArenaPacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[Arena] building test arena at " + packet.Center.ToString("F0"));
            LobbyState.RakeOwnerId = 0;
            RakeControlSync.Apply("test arena");
            TestArena.Build(packet.Center);
            TestArena.SpawnItems(packet.Center);
        }

        /// <summary>Server: teleport one player (host or a specific client) and its proxies.</summary>
        public void ServerTeleportPlayer(int playerId, Vector3 position, Quaternion rotation)
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            var spawner = PlayerSpawner.Instance;
            int hostId = spawner != null && spawner.LocalPlayer != null ? spawner.LocalPlayer.PlayerId : 1;

            if (playerId == hostId)
            {
                if (spawner != null && spawner.LocalPlayer != null)
                {
                    spawner.LocalPlayer.transform.position = position;
                    spawner.LocalPlayer.transform.rotation = rotation;
                }
                return;
            }

            if (spawner != null)
            {
                NetworkedPlayer remote;
                if (spawner.TryGetRemotePlayer(playerId, out remote) && remote != null)
                {
                    remote.transform.position = position;
                    remote.transform.rotation = rotation;
                }
            }

            int connectionId = playerId - 1;
            if (_server != null && _connectedClients.Contains(connectionId))
            {
                var packet = new PlayerTeleportPacket
                {
                    PlayerId = playerId,
                    Position = position,
                    Rotation = rotation
                };
                _server.Send(connectionId, packet.Serialize());
            }
        }
        public void ServerTeleportAllPlayers(Vector3 position, Quaternion rotation)
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            int hostId = PlayerSpawner.Instance?.LocalPlayer?.PlayerId ?? 1;
            if (PlayerSpawner.Instance?.LocalPlayer != null)
            {
                PlayerSpawner.Instance.LocalPlayer.transform.position = position;
                PlayerSpawner.Instance.LocalPlayer.transform.rotation = rotation;
            }

            // Teleport remote player objects on the server
            if (PlayerSpawner.Instance != null)
            {
                foreach (var kvp in PlayerSpawner.Instance.GetAllRemotePlayers())
                {
                    if (kvp.Value != null)
                    {
                        kvp.Value.transform.position = position;
                        kvp.Value.transform.rotation = rotation;
                    }
                }
            }

            // Notify each client for its own local player
            if (_server != null)
            {
                foreach (var connectionId in _connectedClients)
                {
                    int playerId = connectionId + 1;
                    if (playerId == hostId) continue;

                    var packet = new PlayerTeleportPacket
                    {
                        PlayerId = playerId,
                        Position = position,
                        Rotation = rotation
                    };
                    _server.Send(connectionId, packet.Serialize());
                }
            }

            RakeMultiplayerPlugin.Log?.LogInfo($"[4.6] Teleported all players to {position}");
        }

        private void HandleClientPlayerTeleport(PlayerTeleportPacket packet)
        {
            int localId = PlayerSpawner.Instance?.LocalPlayer?.PlayerId ?? 1;

            if (packet.PlayerId == localId)
            {
                if (PlayerSpawner.Instance?.LocalPlayer != null)
                {
                    PlayerSpawner.Instance.LocalPlayer.transform.position = packet.Position;
                    PlayerSpawner.Instance.LocalPlayer.transform.rotation = packet.Rotation;
                    RakeMultiplayerPlugin.Log?.LogInfo($"[4.6] Local player teleported to {packet.Position}");
                }
            }
            else if (PlayerSpawner.Instance != null &&
                     PlayerSpawner.Instance.TryGetRemotePlayer(packet.PlayerId, out var remote) && remote != null)
            {
                remote.ApplyPosition(packet.Position);
                remote.ApplyRotation(packet.Rotation);
            }
        }

        private void HandleClientPlayerState(PlayerStatePacket packet)
        {
            // Find remote player and apply state
            if (PlayerSpawner.Instance != null)
            {
                if (PlayerSpawner.Instance.TryGetRemotePlayer(packet.PlayerId, out var remote))
                {
                    remote.ApplyPosition(packet.Position);
                    remote.ApplyRotation(packet.Rotation);
                    remote.ApplyAnimation(packet.AnimState);
                }
                else if (packet.PlayerId > 0 &&
                         PlayerSpawner.Instance.LocalPlayer != null &&
                         PlayerSpawner.Instance.LocalPlayer.PlayerId != packet.PlayerId)
                {
                    // Late joiner or a player we never heard about (the old code dropped
                    // these packets forever, so that hunter stayed invisible).
                    PlayerSpawner.Instance.SpawnRemotePlayer(packet.PlayerId, packet.Position);
                    _knownPlayerIds.Add(packet.PlayerId);
                }
            }
        }

        private void HandleClientPlayerConnected(PlayerConnectedPacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.3] Client received PlayerConnected: playerId={packet.PlayerId}, spawnPos={packet.SpawnPosition}, IsYou={packet.IsYou}");
            // Track other players' IDs (client-side). Never track our own id.
            if (!RakeMultiplayerPlugin.isServer && !packet.IsYou)
            {
                _knownPlayerIds.Add(packet.PlayerId);
                if (!string.IsNullOrEmpty(packet.Name)) _playerNames[packet.PlayerId] = packet.Name;
            }

            if (!RakeMultiplayerPlugin.isServer && packet.IsYou)
            {
                _pendingLocalPlayerId = packet.PlayerId;
                _knownPlayerIds.Remove(packet.PlayerId);

                // If a self-proxy was spawned before the id was known, remove it
                if (PlayerSpawner.Instance != null && PlayerSpawner.Instance.TryGetRemotePlayer(packet.PlayerId, out _))
                {
                    PlayerSpawner.Instance.RemoveRemotePlayer(packet.PlayerId);
                    RakeMultiplayerPlugin.Log?.LogWarning($"[Visual] Removed accidental self-proxy P{packet.PlayerId}");
                }
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

            // Forget the id, otherwise EnsureRemotePlayersSpawned respawns it as a ghost
            _knownPlayerIds.Remove(packet.PlayerId);

            if (PlayerSpawner.Instance != null)
            {
                PlayerSpawner.Instance.RemoveRemotePlayer(packet.PlayerId);
            }
        }

        private void HandleClientRakeState(RakeStatePacket packet)
        {
            // No per-packet logging: this runs at 15 Hz and would flood the log.
            
            // Use cached NetworkedRakeAI (avoid FindObjectOfType in hot path)
            if (_cachedRake != null)
            {
                _cachedRake.ApplySyncData(packet.Position, packet.Rotation, packet.State, packet.Health,
                                         packet.AnimFlags, packet.WalkType, packet.AnimSpeed);
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[3.4] NetworkedRakeAI not cached on client!");
            }
        }

        /// <summary>
        /// Client: the server says the Rake hit us. Apply it to the local player so the
        /// vanilla damage effects (pain sound, blood, death) and the ragdoll hit reaction
        /// run exactly as they do on the host.
        /// </summary>
        /// <summary>
        /// A Rake hit on a player. The victim applies the damage locally (vanilla effects +
        /// ragdoll hit reaction); everyone else just sees the victim's body fly.
        /// </summary>
        /// <summary>Server told us to show the hit-reaction ragdoll for a player.</summary>
        private void HandleClientRakeGrab(RakeGrabPacket packet)
        {
            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;
            if (local == null || local.PlayerId != packet.PlayerId) return;

            Vector3 focus = packet.FocusPosition != Vector3.zero
                ? packet.FocusPosition
                : packet.RakePosition + Vector3.up * 1.6f;

            StartCoroutine(RakeGrabRoutine(focus));
        }

        private System.Collections.IEnumerator RakeGrabRoutine(Vector3 focusPosition)
        {
            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;
            if (local == null) yield break;

            var movement = local.GetComponent<PlayerMovement>();
            var look = local.GetComponentInChildren<CameraMouseLook>();
            var fps = local.GetComponent<FPSController>();

            if (movement != null) movement.FreezePlayer();
            if (look != null) look.bControl = false;
            if (fps != null) fps.HidePlayerWeapon();

            float end = Time.time + 1.3f;
            while (Time.time < end)
            {
                if (look != null && look.camera != null)
                {
                    look.camera.LookAt(focusPosition);
                }
                yield return null;
            }

            // The damage packet normally starts the ragdoll; if it did not, release input.
            yield return new WaitForSeconds(0.5f);
            if (!PlayerRagdoll.IsActive)
            {
                if (movement != null) movement.UnFreezePlayer();
                if (look != null) look.bControl = true;
            }
        }

        private void HandleClientPlayerRagdoll(PlayerRagdollPacket packet)
        {
            bool shown = PlayerRagdoll.ApplyFor(packet.PlayerId, packet.Direction);
            RakeMultiplayerPlugin.Log?.LogInfo("[Ragdoll] Client received ragdoll for player " +
                                               packet.PlayerId + " (shown=" + shown + ")");
        }

        /// <summary>Client: my scene is loaded and registries exist.</summary>
        public void NotifyClientReady()
        {
            if (!RakeMultiplayerPlugin.isConnected || RakeMultiplayerPlugin.isServer) return;
            ClientSend(new ClientReadyPacket { Name = RakeMultiplayerPlugin.PlayerName.Value }.Serialize());
        }

        /// <summary>Display name of a player (host, clients, or a fallback).</summary>
        public string GetPlayerName(int playerId)
        {
            string name;
            if (_playerNames.TryGetValue(playerId, out name) && !string.IsNullOrEmpty(name)) return name;
            return playerId == 1 ? "HOST" : "PLAYER " + playerId;
        }

        /// <summary>Server: send the current world state to a client that just loaded.</summary>
        private void HandleServerClientReady(int connectionId, ClientReadyPacket packet)
        {
            int playerId = connectionId + 1;
            RakeMultiplayerPlugin.Log?.LogInfo("[Net] C" + connectionId + " ready - sending world snapshot");

            string displayName = string.IsNullOrEmpty(packet.Name) ? "PLAYER " + playerId : packet.Name.Trim();
            if (displayName.Length > 24) displayName = displayName.Substring(0, 24);
            _playerNames[playerId] = displayName;

            // Tell everyone the real name now that the client has introduced itself.
            Vector3 readyPos = _hostSpawnPositionSet ? _hostSpawnPosition : Vector3.zero;
            if (_playerStates.TryGetValue(connectionId, out var readyState)) readyPos = readyState.Position;
            ServerBroadcast(new PlayerConnectedPacket
            {
                PlayerId = playerId,
                SpawnPosition = readyPos,
                IsYou = false,
                Name = displayName
            }.Serialize());

            // Cameras, traps and animals: rebroadcast current state (idempotent).
            var cameras = UnityEngine.Object.FindObjectsOfType<NetworkedCamera>();
            for (int i = 0; i < cameras.Length; i++) cameras[i].BroadcastSpawn();
            var pigs = UnityEngine.Object.FindObjectsOfType<NetworkedPig>();
            for (int i = 0; i < pigs.Length; i++) pigs[i].ForceSendState();
            foreach (var trap in NetworkedTrap.GetAllTraps()) trap.SendStateNow();

            DoorSync.SendSnapshotToPlayer(playerId);

            foreach (var consumedId in _consumedItemIds)
            {
                _server.Send(connectionId, new ItemDestroyedPacket
                {
                    ItemNetworkId = consumedId,
                    PickerId = 0,
                    ItemType = string.Empty
                }.Serialize());
            }

            SessionManager.Instance?.SendStateTo(playerId);
            DayNightManager.Instance?.SendPhaseTo(playerId);

            Vector3 hostPos = _hostSpawnPositionSet ? _hostSpawnPosition : Vector3.zero;
            foreach (var existingId in _connectedClients)
            {
                if (existingId == connectionId) continue;

                Vector3 pos = hostPos;
                if (_playerStates.TryGetValue(existingId, out var st)) pos = st.Position;
                else if (PlayerSpawner.Instance != null &&
                         PlayerSpawner.Instance.TryGetRemotePlayer(existingId + 1, out var proxy) &&
                         proxy != null)
                {
                    pos = proxy.transform.position;
                }

                _server.Send(connectionId, new PlayerConnectedPacket
                {
                    PlayerId = existingId + 1,
                    SpawnPosition = pos,
                    IsYou = false
                }.Serialize());
            }
        }

        /// <summary>Server: register a dropped item with a shared id.</summary>
        public int RegisterDroppedItem(Item item)
        {
            if (item == null || !RakeMultiplayerPlugin.isServer) return 0;

            int id = _nextItemNetworkId;
            while (_networkedItems.ContainsKey(id)) id++;
            _nextItemNetworkId = id + 1;

            RegisterItem(item, id);
            return id;
        }

        /// <summary>Server: a client reports its own death (it knows its inventory).</summary>
        private void HandleServerPlayerDied(int connectionId, PlayerDiedPacket packet)
        {
            if (packet == null) return;

            // The death is always about the sender: trusting packet.PlayerId let any client
            // kill the host (PlayerId = 1) or spawn loot for a non-existent player anywhere.
            int playerId = connectionId + 1;
            if (playerId != 1 && !_connectedClients.Contains(playerId - 1))
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Death] rejected PlayerDied from unknown C" + connectionId);
                return;
            }
            if (PlayerDeathSystem.IsDead(playerId)) return; // host already handled it

            packet.PlayerId = playerId;

            // Never trust the loot list from a client: whitelist item ids, clamp ammo.
            packet.WeaponIds = SanitizeDroppedWeapons(packet.WeaponIds, packet.WeaponAmmo,
                                                      out var cleanAmmo);
            packet.WeaponAmmo = cleanAmmo;

            float knownDeathHealth;
            if (!_playerHealth.TryGetValue(playerId, out knownDeathHealth)) knownDeathHealth = 100f;
            _playerHealth[playerId] = Mathf.Min(knownDeathHealth, Mathf.Max(0f, packet.Health));

            // Use the server's own view of the body instead of the client-supplied position.
            if (playerId == 1)
            {
                var host = PlayerSpawner.Instance != null ? PlayerSpawner.Instance.LocalPlayer : null;
                if (host != null)
                {
                    packet.Position = host.transform.position;
                    packet.Rotation = host.transform.rotation;
                }
            }
            else if (_playerStates.TryGetValue(connectionId, out var state))
            {
                packet.Position = state.Position;
                packet.Rotation = state.Rotation;
            }
            else if (PlayerSpawner.Instance != null &&
                     PlayerSpawner.Instance.TryGetRemotePlayer(playerId, out var remote))
            {
                packet.Position = remote.transform.position;
                packet.Rotation = remote.transform.rotation;
            }

            PlayerDeathSystem.ApplyDeath(packet);
            ServerBroadcast(packet.Serialize());
            SchedulePlayerRespawn(packet.PlayerId);
        }

        private static int[] SanitizeDroppedWeapons(int[] ids, int[] ammo, out int[] cleanAmmo)
        {
            var outIds = new List<int>();
            var outAmmo = new List<int>();

            if (ids != null)
            {
                for (int i = 0; i < ids.Length && outIds.Count < 8; i++)
                {
                    int id = ids[i];
                    if (id != 1 && id != 2 && id != 4 && id != 5 && id != 6 && id != 8) continue;

                    outIds.Add(id);
                    int a = ammo != null && i < ammo.Length ? Mathf.Clamp(ammo[i], 0, 999) : 0;
                    outAmmo.Add(a);
                }
            }

            cleanAmmo = outAmmo.ToArray();
            return outIds.ToArray();
        }

        /// <summary>Server: track a player's health and force death when it reaches zero.</summary>
        public float ServerRegisterDamage(int playerId, int amount)
        {
            if (!RakeMultiplayerPlugin.isServer || playerId <= 0) return 100f;

            float hp;
            if (!_playerHealth.TryGetValue(playerId, out hp)) hp = 100f;

            hp = Mathf.Max(0f, hp - Mathf.Max(0, amount));
            _playerHealth[playerId] = hp;

            if (hp <= 0f && playerId != 1 && !PlayerDeathSystem.IsDead(playerId))
            {
                ForcePlayerDeath(playerId);
            }
            return hp;
        }

        private void ForcePlayerDeath(int playerId)
        {
            if (!RakeMultiplayerPlugin.isServer || playerId <= 1) return;

            int connectionId = playerId - 1;
            if (!_connectedClients.Contains(connectionId)) return;

            RakeMultiplayerPlugin.Log?.LogInfo("[Death] server forced death for player " + playerId);
            _server.Send(connectionId, new PlayerDamagedPacket
            {
                PlayerId = playerId,
                Amount = 9999,
                DamageType = "ServerKill",
                SourcePosition = Vector3.zero
            }.Serialize());
        }

        /// <summary>Server -> all: a player died (used for a host death too).</summary>
        public void BroadcastPlayerDied(PlayerDiedPacket packet)
        {
            if (!RakeMultiplayerPlugin.isServer || packet == null) return;
            ServerBroadcast(packet.Serialize());
        }

        private readonly HashSet<int> _respawnScheduled = new HashSet<int>();
        private readonly HashSet<int> _consumedItemIds = new HashSet<int>();
        private readonly Dictionary<int, float> _playerHealth = new Dictionary<int, float>();
        private readonly Dictionary<int, string> _playerNames = new Dictionary<int, string>();
        private Vector3 _lastRakeOwnerPosition;
        private float _lastRakeOwnerTime = -1f;
        private float _lastRakeAttackTime = -999f;

        /// <summary>Server: put a dead hunter back at the safehouse after a delay.</summary>
        public void SchedulePlayerRespawn(int playerId)
        {
            if (!RakeMultiplayerPlugin.isServer || playerId <= 0) return;
            if (!_respawnScheduled.Add(playerId)) return;
            StartCoroutine(RespawnPlayerRoutine(playerId));
        }

        /// <summary>New scene: drop pending respawn timers from the old level.</summary>
        public void ResetDeathState()
        {
            _respawnScheduled.Clear();
        }

        private IEnumerator RespawnPlayerRoutine(int playerId)
        {
            yield return new WaitForSeconds(PlayerDeathSystem.RespawnDelay);
            if (!RakeMultiplayerPlugin.isServer) yield break;

            _respawnScheduled.Remove(playerId);

            // The level may have changed while the timer was running: do not revive
            // a player who was already respawned by the new scene.
            if (!PlayerDeathSystem.IsDead(playerId)) yield break;

            var packet = new PlayerRespawnPacket
            {
                PlayerId = playerId,
                Position = PlayerDeathSystem.GetRespawnPosition(playerId),
                Rotation = Quaternion.identity
            };

            // Apply on the server view first (host player or the remote clone), then
            // tell every client to restore the same player.
            _playerHealth[playerId] = 100f;
            PlayerDeathSystem.ApplyRespawn(packet);
            ServerBroadcast(packet.Serialize());
            RakeMultiplayerPlugin.Log?.LogInfo("[Death] server respawned player " + playerId +
                                               " at " + packet.Position.ToString("F1"));
        }

        /// <summary>Server: send one packet to one player (the host is local, not a connection).</summary>
        public void ServerSendToPlayer(int playerId, byte[] data)
        {
            if (!RakeMultiplayerPlugin.isServer || data == null) return;
            if (playerId <= 1) return;

            int connectionId = playerId - 1;
            if (_server != null && _connectedClients.Contains(connectionId))
            {
                _server.Send(connectionId, data);
            }
        }

        /// <summary>
        /// Server: one player's bullet hit another player. Damage is applied on the
        /// victim (authoritative hit), and every peer gets the ragdoll impulse.
        /// </summary>
        public void ServerPlayerHit(int playerId, int amount, string damageType,
                                    Vector3 sourcePosition, Vector3 away)
        {
            if (!RakeMultiplayerPlugin.isServer || playerId <= 0) return;
            if (PlayerDeathSystem.IsDead(playerId)) return;

            var spawner = PlayerSpawner.Instance;
            if (spawner == null) return;

            var local = spawner.LocalPlayer;
            if (local != null && local.PlayerId == playerId)
            {
                PlayerDeathSystem.SetLastHitDirection(away);
                var fps = local.GetComponent<FPSController>();
                if (fps != null) fps.TakeDamage(amount, damageType);
                ServerRegisterDamage(playerId, amount);
                PlayerRagdoll.TriggerNetworked(playerId, away);
                return;
            }

            var packet = new PlayerDamagedPacket
            {
                PlayerId = playerId,
                Amount = amount,
                DamageType = damageType,
                SourcePosition = sourcePosition
            };
            ServerSendToPlayer(playerId, packet.Serialize());
            ServerRegisterDamage(playerId, amount);
            PlayerRagdoll.TriggerNetworked(playerId, away);
        }

        private void HandleClientPlayerDamaged(PlayerDamagedPacket packet)
        {
            var spawner = PlayerSpawner.Instance;
            if (spawner == null) return;

            var local = spawner.LocalPlayer;

            if (local != null && local.PlayerId == packet.PlayerId)
            {
                var fps = local.GetComponent<FPSController>();
                if (fps == null)
                {
                    RakeMultiplayerPlugin.Log?.LogWarning("[AI] PlayerDamaged: no FPSController on the local player");
                    return;
                }

                RakeMultiplayerPlugin.Log?.LogInfo($"[AI] Rake hit us for {packet.Amount} ({packet.DamageType})");
                PlayerDeathSystem.SetLastHitDirection(local.transform.position - packet.SourcePosition);
                fps.TakeDamage(packet.Amount, packet.DamageType);
                return;
            }

            // Someone else got hit - show their body flying
            NetworkedPlayer victim;
            if (!spawner.TryGetRemotePlayer(packet.PlayerId, out victim) || victim == null) return;

            Vector3 away = victim.transform.position - packet.SourcePosition;
            away.y = 0f;

            PlayerAudio.PlayPain(victim.transform);

            if (PlayerRagdoll.TriggerRemote(packet.PlayerId, victim.transform, away))
            {
                RakeMultiplayerPlugin.Log?.LogInfo("[Ragdoll] Showing remote hit for player " + packet.PlayerId);
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

                // A client that owns the Rake must also feel the post-damage attack lockout.
                if (_cachedRake.IsLocalOwner)
                {
                    RakePlayerController.NotifyDamageTaken(_cachedRake.transform);
                }
            }
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
            _mapMarkers[MarkerKey(packet.MarkerType, packet.MarkerId)] = packet;
            OnMapMarkerReceived?.Invoke(packet);
        }

        private void HandleClientTrapState(TrapStatePacket packet)
        {
            _trapStates[packet.TrapId] = packet;

            if (NetworkedTrap.TryGetTrap(packet.TrapId, out var trap) && trap != null)
            {
                trap.ApplyState(packet);
            }
            else
            {
                // Clients have no trap objects (the server creates the zones); build a
                // state-only copy so the trap exists in the registry and on the map.
                NetworkedTrap.SpawnClientTrap(packet);
            }
        }

        /// <summary>Trap states received from the server (TrapId -> packet), for MapUI.</summary>
        public Dictionary<uint, TrapStatePacket> GetTrapStates() => _trapStates;

        private void HandleClientPhaseChange(PhaseChangePacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[DayNight] Client received PhaseChange: phase={packet.Phase}, remaining={packet.RemainingTime:F1}s, night={packet.NightNumber}");

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

        private void HandleServerFireVisuals(int connectionId, FireVisualsPacket packet)
        {
            // The shooter is always the sender: a client could otherwise spawn tracers,
            // flashes and gunshots in another player's name anywhere on the map.
            packet.ShooterId = connectionId + 1;
            packet.Pellets = (byte)Mathf.Clamp(packet.Pellets, 1, 12);
            packet.Spread = Mathf.Clamp(packet.Spread, 0f, 10f);

            // Play it for the host, then relay to the other clients. The sender
            // ignores its own packet in HandleClientFireVisuals.
            HandleClientFireVisuals(packet);
            ServerBroadcast(packet.Serialize());
        }

        private void HandleClientFireVisuals(FireVisualsPacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.5] Client received FireVisuals: shooterId={packet.ShooterId}, weaponType={packet.WeaponType}");

            // Remote shot: visible tracer from the shooter to wherever it lands.
            if (PlayerSpawner.Instance == null) return;
            if (PlayerSpawner.Instance.LocalPlayer != null &&
                PlayerSpawner.Instance.LocalPlayer.PlayerId == packet.ShooterId) return;

            // Find remote player by shooterId
            if (!PlayerSpawner.Instance.TryGetRemotePlayer(packet.ShooterId, out var shooter)) return;

            if (packet.Origin != Vector3.zero)
            {
                BulletVisuals.SpawnSpread(packet.Origin, packet.Direction, packet.Pellets, packet.Spread);
            }

            try
            {
                // WeaponType: 0 = Rifle, 1 = Sniper
                if (packet.WeaponType == 0)
                {
                    var rifles = shooter.GetComponentsInChildren<Rifle_weapon>(true);
                    var rifle = rifles.Length > 0 ? rifles[0] : null;
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
                            // The clone AudioSources are disabled, so play a temporary 3D
                            // source at the shooter instead of on its weapon.
                            PlayerAudio.PlayAt(fireSound, packet.Origin != Vector3.zero ? packet.Origin : shooter.transform.position + Vector3.up * 1.4f, 1f);
                        }
                    }
                }
                else if (packet.WeaponType == 1)
                {
                    var snipers = shooter.GetComponentsInChildren<SniperRifle_weapon>(true);
                    var sniper = snipers.Length > 0 ? snipers[0] : null;
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
                            PlayerAudio.PlayAt(fireSound, packet.Origin != Vector3.zero ? packet.Origin : shooter.transform.position + Vector3.up * 1.4f, 1f);
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

        /// <summary>
        /// Process queued PlayerConnected packets (called when PlayerSpawner initializes)
        /// </summary>
        public void ProcessPendingConnects()
        {
            if (PlayerSpawner.Instance == null)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Net] ProcessPendingConnects: PlayerSpawner not ready, keeping queue");
                return;
            }

            while (_pendingConnects.Count > 0)
            {
                var packet = _pendingConnects.Dequeue();
                RakeMultiplayerPlugin.Log?.LogInfo($"[Net] Processing queued PlayerConnected: playerId={packet.PlayerId}, IsYou={packet.IsYou}");
                
                // Track known player IDs for UI (client-side), but never our own: that
                // created a phantom copy of the local body.
                if (!RakeMultiplayerPlugin.isServer && !packet.IsYou)
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
                    else
                    {
                        // Keep the packet: dropping it left PlayerId = 0 forever.
                        _pendingConnects.Enqueue(packet);
                        break;
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