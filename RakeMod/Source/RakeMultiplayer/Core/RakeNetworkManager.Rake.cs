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
        // The Rake: owner, state, attacks and sounds. Part of the partial class declared in
        // RakeNetworkManager.cs; state stays there, behaviour lives here.

// ===================== Rake ownership (any player can be the Rake) =====================

        public void SetRakeOwner(int ownerId)
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            int clamped = Mathf.Max(0, ownerId);
            if (LobbyState.RakeOwnerId == clamped) return;

            LobbyState.RakeOwnerId = clamped;
            _lastRakeOwnerTime = -1f;
            RakeMultiplayerPlugin.Log?.LogInfo("[Lobby] Rake owner set to P" + clamped);
            ServerBroadcast(new RakeOwnerPacket { OwnerId = clamped }.Serialize());

            // Whoever just took the Rake stops being a hunter on every peer, starting here.
            if (clamped > 0) PlayerSpawner.Instance?.RemoveRemotePlayer(clamped);

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

            // The new owner is the Rake, not a hunter: drop the proxy we may have made
            // before the ownership arrived.
            if (packet.OwnerId > 0) PlayerSpawner.Instance?.RemoveRemotePlayer(packet.OwnerId);

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

            // Per-kind cooldown: the client shows 20/10/50 s, the server is the authority
            // (I-ROLE-3). A swing consumes its cooldown even if it misses, exactly like the
            // owning client does.
            float cooldown = packet.Kind == 0 ? RakePlayerAttackCooldown
                           : packet.Kind == 1 ? RakeAnimalAttackCooldown
                                              : RakeCameraAttackCooldown;
            float lastSwing = packet.Kind == 0 ? _lastRakePlayerAttackTime
                            : packet.Kind == 1 ? _lastRakeAnimalAttackTime
                                               : _lastRakeCameraAttackTime;
            if (Time.time - lastSwing < cooldown)
            {
                RakeMultiplayerPlugin.Log?.LogInfo("[RakePlay] kind " + packet.Kind +
                                                   " on cooldown (" + cooldown.ToString("F0") +
                                                   " s), request ignored");
                return;
            }

            if (packet.Kind == 0) _lastRakePlayerAttackTime = Time.time;
            else if (packet.Kind == 1) _lastRakeAnimalAttackTime = Time.time;
            else _lastRakeCameraAttackTime = Time.time;

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

                // ServerGrabPlayer freezes the host locally and broadcasts to clients;
                // the plain broadcast skipped the host, so it was never grabbed.
                ServerGrabPlayer(packet.TargetPlayerId, rakePos, focus);

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

        /// <summary>
        /// Server: run the vanilla grab on a hunter - freeze them and turn their camera to
        /// the Rake. The host does it locally, a client gets the packet. Used by both the
        /// AI attack and a player-controlled Rake.
        /// </summary>
        public void ServerGrabPlayer(int playerId, Vector3 rakePosition, Vector3 focusPosition)
        {
            if (!RakeMultiplayerPlugin.isServer || playerId <= 0) return;

            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;

            if (local != null && local.PlayerId == playerId)
            {
                StartCoroutine(RakeGrabRoutine(focusPosition));
                return;
            }

            ServerBroadcast(new RakeGrabPacket
            {
                PlayerId = playerId,
                RakePosition = rakePosition,
                FocusPosition = focusPosition
            }.Serialize());
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
    }
}
