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
        // Map markers, traps and fire visuals. Part of the partial class declared in
        // RakeNetworkManager.cs; state stays there, behaviour lives here.

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

        private void HandleServerMapMarker(int connectionId, MapMarkerPacket packet, byte[] rawData)
        {
            // Clients may place bait (2) and trap (1) markers; other marker types are server-owned.
            if (packet.MarkerType == 1) // Trap
            {
                // Same sanity checks the bait path already had: a spoofed client could
                // otherwise plant armed traps anywhere, including under other players.
                if (float.IsNaN(packet.Position.x) || float.IsNaN(packet.Position.y) ||
                    float.IsNaN(packet.Position.z) || packet.Position.sqrMagnitude > 100000000f)
                {
                    RakeMultiplayerPlugin.Log?.LogWarning("[3.11] Rejected trap with an invalid position");
                    return;
                }

                Vector3 senderPosition = Vector3.zero;
                bool senderKnown = false;

                PlayerStatePacket senderState;
                if (_playerStates.TryGetValue(connectionId, out senderState))
                {
                    senderPosition = senderState.Position;
                    senderKnown = true;
                }
                else
                {
                    NetworkedPlayer senderRemote;
                    if (PlayerSpawner.Instance != null &&
                        PlayerSpawner.Instance.TryGetRemotePlayer(connectionId + 1, out senderRemote) &&
                        senderRemote != null)
                    {
                        senderPosition = senderRemote.transform.position;
                        senderKnown = true;
                    }
                }

                if (senderKnown && Vector3.Distance(senderPosition, packet.Position) > 12f)
                {
                    RakeMultiplayerPlugin.Log?.LogWarning("[3.11] Rejected trap placed " +
                        Vector3.Distance(senderPosition, packet.Position).ToString("F0") +
                        " m away from C" + connectionId);
                    return;
                }

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
    }
}
