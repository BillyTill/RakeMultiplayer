using System;
using System.Collections.Generic;
using UnityEngine;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    /// <summary>
    /// Server-authoritative trap zone.
    /// Created by the server when a player deploys a trap; it stuns the Rake for
    /// <see cref="StunDuration"/> seconds and damages animals. Clients keep only a
    /// map marker (the trap prefab itself is client-local).
    /// </summary>
    public class NetworkedTrap : MonoBehaviour
    {
        public const int MaxTraps = 20;
        public const float DefaultTriggerRadius = 2.5f;
        public const float StunDuration = 3f;
        public const float TriggerCooldown = 2f;

        /// <summary>Trigger type value used in QuestTriggerPacket (2 = TrapTriggered).</summary>
        public const byte QuestTrapTriggered = 2;

        public uint TrapId { get; private set; }
        public float TriggerRadius { get; private set; } = DefaultTriggerRadius;
        public bool IsArmed { get; private set; } = true;
        public bool IsTriggered { get; private set; }

        private float _lastTriggerTime = -999f;

        private static readonly Dictionary<uint, NetworkedTrap> _registry = new Dictionary<uint, NetworkedTrap>();
        private static int _nextTrapId = 1;

        public static bool TryGetTrap(uint trapId, out NetworkedTrap trap) => _registry.TryGetValue(trapId, out trap);

        public static int GetTrapCount() => _registry.Count;

        /// <summary>
        /// Server: create a trap zone at the given position.
        /// </summary>
        public static NetworkedTrap SpawnServerTrap(Vector3 position, float radius = DefaultTriggerRadius)
        {
            if (!RakeMultiplayerPlugin.isServer) return null;
            if (_registry.Count >= MaxTraps) return null;
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z))
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[3.11] Cannot spawn trap at invalid position");
                return null;
            }

            var go = new GameObject($"NetworkedTrap_{_nextTrapId}");
            go.transform.position = position;

            var collider = go.AddComponent<SphereCollider>();
            collider.radius = Mathf.Max(0.5f, radius);
            collider.isTrigger = true;

            var trap = go.AddComponent<NetworkedTrap>();
            trap.TrapId = (uint)_nextTrapId++;
            trap.TriggerRadius = Mathf.Max(0.5f, radius);
            _registry[trap.TrapId] = trap;

            trap.BroadcastState();
            trap.BroadcastMapMarker();
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.11] Trap {trap.TrapId} deployed at {position} (radius {trap.TriggerRadius})");
            return trap;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!RakeMultiplayerPlugin.isServer) return;
            if (!IsArmed) return;
            if (Time.time - _lastTriggerTime < TriggerCooldown) return;
            if (other == null) return;

            // Rake: stun for 3 seconds (core trap effect)
            var rake = other.GetComponentInParent<NetworkedRakeAI>();
            if (rake != null)
            {
                rake.Stun(StunDuration);
                Trigger();
                return;
            }

            // Animals: heavy damage
            var animal = other.GetComponentInParent<Animal_AI>();
            if (animal != null)
            {
                animal.TakeDamage(100, "TrapDamage");
                Trigger();
                return;
            }

            // Local (host) player takes damage through the game's own controller
            var fps = other.GetComponentInParent<FPSController>();
            if (fps != null)
            {
                try { fps.TakeDamage(35, "TrapDamage"); }
                catch (Exception ex) { RakeMultiplayerPlugin.Log?.LogWarning($"[3.11] Trap damage failed: {ex.Message}"); }
                Trigger();
            }
        }

        private void Trigger()
        {
            _lastTriggerTime = Time.time;
            IsTriggered = true;
            BroadcastState();

            var quest = new QuestTriggerPacket
            {
                TriggerType = QuestTrapTriggered,
                Position = transform.position,
                CameraId = 0
            };
            RakeNetworkManager.Instance?.ServerBroadcast(quest.Serialize());

            RakeMultiplayerPlugin.Log?.LogInfo($"[3.11] Trap {TrapId} triggered at {transform.position}");
        }

        private void BroadcastState()
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            var packet = new TrapStatePacket
            {
                TrapId = TrapId,
                Position = transform.position,
                IsArmed = IsArmed,
                TriggerRadius = TriggerRadius,
                IsTriggered = IsTriggered
            };
            RakeNetworkManager.Instance?.ServerBroadcast(packet.Serialize());
        }

        /// <summary>Broadcast this trap as a minimap marker (MarkerType 1 = Trap).</summary>
        public void BroadcastMapMarker()
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            var marker = new MapMarkerPacket
            {
                MarkerType = 1, // Trap
                Position = transform.position,
                MarkerId = TrapId,
                IsActive = true
            };

            RakeNetworkManager.Instance?.RegisterLocalMapMarker(marker);
            RakeNetworkManager.Instance?.ServerBroadcast(marker.Serialize());
        }

        /// <summary>Client: apply trap state from the server (used for map/UI).</summary>
        public void ApplyState(TrapStatePacket packet)
        {
            if (RakeMultiplayerPlugin.isServer) return;

            TrapId = packet.TrapId;
            TriggerRadius = packet.TriggerRadius;
            IsArmed = packet.IsArmed;
            IsTriggered = packet.IsTriggered;
            transform.position = packet.Position;
            _registry[packet.TrapId] = this;
        }

        private void OnDestroy()
        {
            if (TrapId != 0 && _registry.TryGetValue(TrapId, out var existing) && existing == this)
            {
                _registry.Remove(TrapId);
            }
        }
    }
}
