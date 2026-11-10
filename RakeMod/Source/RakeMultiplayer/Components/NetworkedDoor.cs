using System;
using UnityEngine;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    /// <summary>
    /// Networked door synchronization - simple Telepathy pattern
    /// </summary>
    public class NetworkedDoor : MonoBehaviour
    {
        [Header("Door Settings")]
        public bool isOpen = false;
        public bool isLocked = false;
        public string requiredKey = "";

        public void Interact(int playerId)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;

            if (isLocked && !string.IsNullOrEmpty(requiredKey))
            {
                // Check if player has key
                // TODO: Implement key check
                return;
            }

            // Toggle door state
            isOpen = !isOpen;
            
            // Apply locally via reflection
            var doorComponent = GetComponent("Door");
            if (doorComponent != null)
            {
                var openField = doorComponent.GetType().GetField("isOpen", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (openField != null)
                {
                    openField.SetValue(doorComponent, isOpen);
                }
            }

            // Send sync - simple packet
            SendDoorStateSync();
        }

        private void SendDoorStateSync()
        {
            // TODO: Implement simple door state packet
            // For now, just log
            RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedDoor] Door state changed: isOpen={isOpen}");
        }

        public void ApplyDoorState(bool open, bool locked)
        {
            isOpen = open;
            isLocked = locked;
            
            var doorComponent = GetComponent("Door");
            if (doorComponent != null)
            {
                var openField = doorComponent.GetType().GetField("isOpen", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (openField != null)
                {
                    openField.SetValue(doorComponent, isOpen);
                }
            }
        }
    }

    /// <summary>
    /// Networked trap synchronization - simple Telepathy pattern
    /// </summary>
    public class NetworkedTrap : MonoBehaviour
    {
        [Header("Trap Settings")]
        public bool isActive = false;
        public bool isTriggered = false;
        public float triggerCooldown = 1f;

        private NetIdentity _netIdentity;
        private float _lastTriggerTime = 0f;

        private void Awake()
        {
            _netIdentity = GetComponent<NetIdentity>();
            if (_netIdentity == null)
            {
                _netIdentity = gameObject.AddComponent<NetIdentity>();
            }
            _netIdentity.PrefabId = 6; // Trap prefab ID
        }

        private void Update()
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (!RakeMultiplayerPlugin.isConnected) return;

            if (isActive && !isTriggered)
            {
                CheckTrigger();
            }
        }

        private void CheckTrigger()
        {
            // Check for nearby players/Rake
            var colliders = Physics.OverlapSphere(transform.position, 2f);
            foreach (var col in colliders)
            {
                var player = col.GetComponentInParent<NetworkedPlayer>();
                var rake = col.GetComponentInParent<NetworkedRakeAI>();
                
                if (player != null || rake != null)
                {
                    Trigger();
                    break;
                }
            }
        }

        public void Trigger()
        {
            if (isTriggered) return;
            if (Time.time - _lastTriggerTime < triggerCooldown) return;

            isTriggered = true;
            _lastTriggerTime = Time.time;

            // Apply locally via reflection
            var trapComponent = GetComponent("Trap");
            if (trapComponent != null)
            {
                var triggerMethod = trapComponent.GetType().GetMethod("Trigger", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (triggerMethod != null)
                {
                    triggerMethod.Invoke(trapComponent, null);
                }
            }

            // Send sync - simple Telepathy packet
            if (RakeMultiplayerPlugin.isServer)
            {
                // TODO: Send trap trigger packet
                RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedTrap] Trap triggered, broadcasting...");
            }
            else
            {
                // TODO: Send trap trigger request to server
                RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedTrap] Requesting trap trigger from server...");
            }
        }

        public void ApplyTrapState(bool active, bool triggered)
        {
            isActive = active;
            isTriggered = triggered;
        }

        public void ResetTrap()
        {
            isTriggered = false;
            isActive = false;
            
            if (RakeMultiplayerPlugin.isServer)
            {
                // TODO: Send trap reset packet
                RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedTrap] Trap reset");
            }
        }
    }
}