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
}
