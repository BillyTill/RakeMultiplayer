using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Telepathy;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    /// <summary>
    /// Host/client orchestration: connection lifecycle, message dispatch and the shared
    /// state of a session. Behaviour is split by domain into partial files (Players,
    /// Items, Cameras, Rake, Session, World) so a change stays local.
    /// </summary>
    public partial class RakeNetworkManager : MonoBehaviour
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

        /// <summary>Items dropped as loot (not scene items). Only these are replayed to a joiner.</summary>
        private readonly HashSet<int> _droppedItemIds = new HashSet<int>();

// Camera registry tracking (deterministic IDs assigned by RegisterCameras)
        private int _nextCameraNetworkId = 1;

// Local player id decoded from the IsYou packet (may arrive before PlayerSpawner)
        private int _pendingLocalPlayerId = 0;

// Item objects this client asked to pick up (used if the registry id desyncs)
        private readonly Dictionary<int, Item> _pendingPickups = new Dictionary<int, Item>();

// Map markers (bait/trap/camera...) received from the server, for MapUI
        private readonly Dictionary<uint, MapMarkerPacket> _mapMarkers = new Dictionary<uint, MapMarkerPacket>();

        private readonly Dictionary<uint, TrapStatePacket> _trapStates = new Dictionary<uint, TrapStatePacket>();

        private readonly List<Transform> _activeBaitTransforms = new List<Transform>();

        private int _nextMarkerId = 1;

// Reverse lookup: Item -> assigned network ID (for consistent ID retrieval)
        private Dictionary<Item, int> _itemToNetworkId = new Dictionary<Item, int>();

        private int _nextItemNetworkId = 1;

        private void Awake()
        {
            Instance = this;
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
        }

        public void StartHost()
        {
            if (IntegrityCheck.BlocksMultiplayer)
            {
                RakeMultiplayerPlugin.Log?.LogError("[Net] refusing to host: " + IntegrityCheck.Problem);
                UI.MultiplayerMenu.ShowConnectionError("Mod files changed since install: " + IntegrityCheck.Problem);
                return;
            }

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
            if (IntegrityCheck.BlocksMultiplayer)
            {
                RakeMultiplayerPlugin.Log?.LogError("[Net] refusing to connect: " + IntegrityCheck.Problem);
                UI.MultiplayerMenu.ShowConnectionError("Mod files changed since install: " + IntegrityCheck.Problem);
                return;
            }

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
            // The match level follows the engine, not the request that started the load: a
            // request can name a scene, be dropped while another load finishes, or land on a
            // different index. A stale value here is what made level-dependent logic use the
            // old level, and it makes /state describe a level the player is not standing in.
            if (MatchState.IsActive)
            {
                int loaded = UnityEngine.Application.loadedLevel;
                if (loaded >= 1) MatchState.Current.SetLevel(loaded);
            }

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

            TickRespawns();

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
            _droppedItemIds.Clear();
            _consumedItemIds.Clear();
            _itemToNetworkId.Clear();
            _mapMarkers.Clear();
            _trapStates.Clear();
            _activeBaitTransforms.Clear();
            _pendingPickups.Clear();

            // Positions belong to the level that is being unloaded.
            _playerStates.Clear();
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
            _droppedItemIds.Clear();
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

                        // The host left mid-match: without this the client keeps a frozen
                        // Rake, a countdown that never moves and no way out but the pause
                        // menu. AbortSession runs first on a voluntary exit, so the state
                        // is already Waiting and we do not fight the menu path.
                        if (SessionManager.Instance != null &&
                            SessionManager.Instance.CurrentState == SessionState.Active)
                        {
                            UI.MultiplayerMenu.ExitToMainMenu("host disconnected");
                        }
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
                PlayerHealthMap[assignedPlayerId] = 100f;
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
            PlayerHealthMap.Remove(connectionId + 1);
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

// === Stage 3.8: Bait / animals ===
        private const int MaxActiveBaits = 20;

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
                    case PacketType.ProtocolReject:
                        HandleClientProtocolReject((ProtocolRejectPacket)packet);
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

// Match-scoped combat state lives in MatchState, so a new match cannot inherit it.
        // The local copies only serve the window before a match starts (lobby), where there
        // is no match to own them.
        private readonly HashSet<int> _looseRespawnScheduled = new HashSet<int>();

        private readonly Dictionary<int, float> _looseRespawnDue = new Dictionary<int, float>();

        private readonly HashSet<int> _consumedItemIds = new HashSet<int>();

        private readonly Dictionary<int, float> _loosePlayerHealth = new Dictionary<int, float>();

        private HashSet<int> RespawnScheduledMap
        {
            get { return MatchState.IsActive ? MatchState.Current.RespawnScheduled : _looseRespawnScheduled; }
        }

        private Dictionary<int, float> RespawnDueMap
        {
            get { return MatchState.IsActive ? MatchState.Current.RespawnDue : _looseRespawnDue; }
        }

        private Dictionary<int, float> PlayerHealthMap
        {
            get { return MatchState.IsActive ? MatchState.Current.PlayerHealth : _loosePlayerHealth; }
        }

        private readonly Dictionary<int, string> _playerNames = new Dictionary<int, string>();

        private Vector3 _lastRakeOwnerPosition;

        private float _lastRakeOwnerTime = -1f;

        private float _lastRakeAttackTime = -999f;

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

// Aliases for NetworkedPlayer
        public void ServerBroadcast(byte[] data) => SendToAllClients(data);

        public int GetConnectedClientCount()
        {
            return _connectedClients.Count;
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
