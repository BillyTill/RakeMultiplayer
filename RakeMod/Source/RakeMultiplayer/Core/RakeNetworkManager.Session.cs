using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Telepathy;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    public partial class RakeNetworkManager
    {
        // Session, level changes, handshake and the match lifecycle. Part of the partial class declared in
        // RakeNetworkManager.cs; state stays there, behaviour lives here.

        /// <summary>
        /// Called per game scene load: scene objects (items, cameras, animals) are recreated,
        /// so the per-scene registries must be rebuilt with the same deterministic IDs.
        /// </summary>
        public void PrepareForSceneLoad()
        {
            _networkedItems.Clear();
            _droppedItemIds.Clear();
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

            // The Rake owner is teleported by the level change; the anti-teleport check
            // compared the new position with the old level and dropped every snapshot
            // (the Rake stood still for everyone else for ~17 s).
            _lastRakeOwnerTime = -1f;
            _lastRakeOwnerPosition = Vector3.zero;

            RakeMultiplayerPlugin.Log?.LogInfo("[Scene] Per-scene network registries reset");
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

            // Asking twice is normal (a player clicks RETURN TO LOBBY again, or a script
            // retries). Loading level 0 while level 0 is already loaded - or while another
            // load is in flight - leaves Unity 5 stuck inside LoadLevel: the frame never
            // finishes and the whole game stops answering.
            if (UnityEngine.Application.loadedLevel == 0) return;
            if (IsApplyingNetworkLevelChange) return;

            // The session was GameOver. Leaving it in that state re-armed the results
            // overlay the instant level 0 loaded, so RETURN TO LOBBY looped forever and a
            // new match could not be started.
            SessionManager.Instance?.ForceState(SessionState.Waiting, 0, 100f, VictoryType.None);

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

        /// <summary>Client: my scene is loaded and registries exist.</summary>
        public void NotifyClientReady()
        {
            if (!RakeMultiplayerPlugin.isConnected || RakeMultiplayerPlugin.isServer) return;
            ClientSend(new ClientReadyPacket { Name = RakeMultiplayerPlugin.PlayerName.Value }.Serialize());
        }

        /// <summary>Server: send the current world state to a client that just loaded.</summary>
        private void HandleServerClientReady(int connectionId, ClientReadyPacket packet)
        {
            int playerId = connectionId + 1;

            // A client with a different packet layout would corrupt the session in
            // unpredictable ways. Refuse it explicitly instead of letting it half-join.
            if (!Protocol.IsCompatible(packet.ProtocolVersion))
            {
                string reason = Protocol.MismatchMessage(packet.ProtocolVersion);
                RakeMultiplayerPlugin.Log?.LogError("[Net] rejected C" + connectionId + ": " + reason);

                try
                {
                    if (_server != null)
                    {
                        _server.Send(connectionId, new ProtocolRejectPacket
                        {
                            HostVersion = Protocol.Version,
                            ClientVersion = packet.ProtocolVersion,
                            Message = reason
                        }.Serialize());
                    }
                }
                catch (Exception ex)
                {
                    RakeMultiplayerPlugin.Log?.LogWarning("[Net] could not send the reject: " + ex.Message);
                }

                if (_server != null) _server.Disconnect(connectionId);
                return;
            }

            RakeMultiplayerPlugin.Log?.LogInfo("[Net] C" + connectionId + " ready (protocol " +
                                               packet.ProtocolVersion + ") - sending world snapshot");

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

            // Loot dropped before this client joined still exists for everyone else. Only
            // dropped items are replayed: scene items are already in the client's own copy
            // of the level, and re-sending them would duplicate them.
            foreach (var droppedId in _droppedItemIds)
            {
                Item dropped;
                if (!_networkedItems.TryGetValue(droppedId, out dropped) || dropped == null) continue;

                _server.Send(connectionId, new ItemSpawnPacket
                {
                    ItemNetworkId = droppedId,
                    ItemTypeName = dropped.GetType().Name,
                    Position = dropped.transform.position,
                    Rotation = dropped.transform.rotation
                }.Serialize());
            }

            // World markers are not per-player: the joiner either has them or does not.
            foreach (var marker in _mapMarkers.Values)
            {
                if (marker != null) _server.Send(connectionId, marker.Serialize());
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

        /// <summary>
        /// The host refused this build. Say so on screen and drop the connection: retrying
        /// with the same binary can only produce the same result.
        /// </summary>
        private void HandleClientProtocolReject(ProtocolRejectPacket packet)
        {
            string message = packet != null && !string.IsNullOrEmpty(packet.Message)
                ? packet.Message
                : Protocol.MismatchMessage(packet != null ? packet.ClientVersion : 0);

            RakeMultiplayerPlugin.Log?.LogError("[Net] host rejected this build: " + message);
            UI.MultiplayerMenu.ShowConnectionError(message);
            Disconnect();
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

        /// <summary>
        /// Re-register packet handlers after scene load
        /// </summary>
        public void ReregisterHandlers()
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[RakeNetworkManager] Re-registering packet handlers");
            // Just need to ensure PlayerSpawner is refreshed
        }
    }
}
