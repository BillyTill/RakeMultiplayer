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
        // Player state, damage, death and movement. Part of the partial class declared in
        // RakeNetworkManager.cs; state stays there, behaviour lives here.

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
            if (!PlayerHealthMap.TryGetValue(assignedPlayerId, out knownHealth)) knownHealth = reportedHealth;
            if (reportedHealth < knownHealth) knownHealth = reportedHealth;
            else knownHealth = Mathf.Min(reportedHealth, knownHealth + 10f);
            PlayerHealthMap[assignedPlayerId] = knownHealth;

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
                // A teleport must snap: lerping dragged the body across the whole level.
                remote.ApplyPositionImmediate(packet.Position);
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

                    if (LobbyState.IsRakeOwner(playerId))
                    {
                        // The role may have been assigned after the proxy was spawned.
                        if (spawner.TryGetRemotePlayer(playerId, out _)) spawner.RemoveRemotePlayer(playerId);
                        continue;
                    }

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

                    if (LobbyState.IsRakeOwner(playerId))
                    {
                        if (spawner.TryGetRemotePlayer(playerId, out _)) spawner.RemoveRemotePlayer(playerId);
                        continue;
                    }

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
            // The Rake owner has no hunter: a proxy for them is a second player model
            // standing next to the Rake.
            if (LobbyState.IsRakeOwner(packet.PlayerId)) return;

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

            // Never make a body for ourselves. The host announces the joining player to
            // everybody with IsYou=false, so this packet arrives back at its own client and
            // used to spawn a second copy of the local player.
            int selfId = _pendingLocalPlayerId;
            if (selfId <= 0 && PlayerSpawner.Instance.LocalPlayer != null)
            {
                selfId = PlayerSpawner.Instance.LocalPlayer.PlayerId;
            }
            if (packet.PlayerId == selfId)
            {
                if (PlayerSpawner.Instance.TryGetRemotePlayer(packet.PlayerId, out _))
                {
                    PlayerSpawner.Instance.RemoveRemotePlayer(packet.PlayerId);
                    RakeMultiplayerPlugin.Log?.LogInfo("[Visual] dropped self-proxy P" + packet.PlayerId);
                }
                return;
            }

            // The Rake owner has no hunter body on any peer.
            if (LobbyState.IsRakeOwner(packet.PlayerId))
            {
                if (PlayerSpawner.Instance.TryGetRemotePlayer(packet.PlayerId, out _))
                {
                    PlayerSpawner.Instance.RemoveRemotePlayer(packet.PlayerId);
                    RakeMultiplayerPlugin.Log?.LogInfo("[Visual] dropped hunter proxy P" + packet.PlayerId +
                                                       " (owns the Rake)");
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

        private void HandleClientPlayerRagdoll(PlayerRagdollPacket packet)
        {
            bool shown = PlayerRagdoll.ApplyFor(packet.PlayerId, packet.Direction);
            RakeMultiplayerPlugin.Log?.LogInfo("[Ragdoll] Client received ragdoll for player " +
                                               packet.PlayerId + " (shown=" + shown + ")");
        }

        /// <summary>Display name of a player (host, clients, or a fallback).</summary>
        public string GetPlayerName(int playerId)
        {
            string name;
            if (_playerNames.TryGetValue(playerId, out name) && !string.IsNullOrEmpty(name)) return name;
            return playerId == 1 ? "HOST" : "PLAYER " + playerId;
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
            if (!PlayerHealthMap.TryGetValue(playerId, out knownDeathHealth)) knownDeathHealth = 100f;
            PlayerHealthMap[playerId] = Mathf.Min(knownDeathHealth, Mathf.Max(0f, packet.Health));

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

                    // One weapon of each kind: the server cannot see a client's real
                    // inventory, but it can refuse the all-sniper-rifles loot spam.
                    if (outIds.Contains(id)) continue;

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
            if (!PlayerHealthMap.TryGetValue(playerId, out hp)) hp = 100f;

            hp = Mathf.Max(0f, hp - Mathf.Max(0, amount));
            PlayerHealthMap[playerId] = hp;

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

        /// <summary>Server: put a dead hunter back at the safehouse after a delay.</summary>
        public void SchedulePlayerRespawn(int playerId)
        {
            if (!RakeMultiplayerPlugin.isServer || playerId <= 0) return;
            if (RespawnDueMap.ContainsKey(playerId)) return;

            // Unscaled time: the timer used to be a coroutine on WaitForSeconds, so pausing
            // the game (or a scene change that killed the routine) stranded the player in
            // the ragdoll for good.
            RespawnDueMap[playerId] = Time.unscaledTime + Mathf.Max(1f, PlayerDeathSystem.RespawnDelay);
            RespawnScheduledMap.Add(playerId);
        }

        /// <summary>New scene: drop pending respawn timers from the old level.</summary>
        public void ResetDeathState()
        {
            int health = PlayerHealthMap.Count;
            int pending = RespawnDueMap.Count;

            RespawnScheduledMap.Clear();
            RespawnDueMap.Clear();

            // The level changed: everyone is alive again. Keeping the old zeros made the
            // next trap or bullet on a client force a 9999-damage death.
            PlayerHealthMap.Clear();

            if (health > 0 || pending > 0)
            {
                RakeMultiplayerPlugin.Log?.LogInfo("[Death] level change: cleared " + health +
                                                   " health entries, " + pending + " pending respawns");
            }
        }

        /// <summary>
        /// Dev bridge only: write a health entry through the same accessor the server uses.
        /// It exists so a scripted run can prove that the map really lives in MatchState
        /// (set an entry, change the level, check the entry is gone).
        /// </summary>
        internal void DevSetHealth(int playerId, float health)
        {
            if (playerId <= 0) return;
            PlayerHealthMap[playerId] = health;
        }

        /// <summary>Server: revive dead hunters whose timer is up.</summary>
        private void TickRespawns()
        {
            if (!RakeMultiplayerPlugin.isServer || RespawnDueMap.Count == 0) return;

            float now = Time.unscaledTime;
            List<int> ready = null;

            foreach (var pair in RespawnDueMap)
            {
                if (now < pair.Value) continue;
                if (ready == null) ready = new List<int>();
                ready.Add(pair.Key);
            }

            if (ready == null) return;

            for (int i = 0; i < ready.Count; i++)
            {
                int playerId = ready[i];
                RespawnDueMap.Remove(playerId);
                RespawnScheduledMap.Remove(playerId);

                // A level change already revived everyone: nothing to do.
                if (!PlayerDeathSystem.IsDead(playerId)) continue;

                var packet = new PlayerRespawnPacket
                {
                    PlayerId = playerId,
                    Position = PlayerDeathSystem.GetRespawnPosition(playerId),
                    Rotation = Quaternion.identity
                };

                // Apply on the server view first (host player or the remote clone), then
                // tell every client to restore the same player.
                PlayerHealthMap[playerId] = 100f;
                PlayerDeathSystem.ApplyRespawn(packet);
                ServerBroadcast(packet.Serialize());
                RakeMultiplayerPlugin.Log?.LogInfo("[Death] server respawned player " + playerId +
                                                   " at " + packet.Position.ToString("F1"));
            }
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
    }
}
