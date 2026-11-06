using System;
using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Networked item synchronization
    /// </summary>
    public class NetworkedItem : MonoBehaviour
    {
        [Header("Item Settings")]
        public string itemName;
        public int itemId;
        public bool isPickupable = true;
        public bool isNetworked = true;

        private Inventory inventory;
        private Rigidbody rigidbody;
        private Collider collider;

        private bool isHeld = false;
        private int heldByPlayerId = -1;

        private void Awake()
        {
            inventory = FindObjectOfType<Inventory>();
            rigidbody = GetComponent<Rigidbody>();
            collider = GetComponent<Collider>();
        }

        private void Update()
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (!RakeMultiplayerPlugin.isConnected) return;

            // Sync position for dropped items
            if (!isHeld && rigidbody != null && !rigidbody.isKinematic)
            {
                // TODO: Sync dropped item position
            }
        }

        public void OnPickup(int playerId)
        {
            isHeld = true;
            heldByPlayerId = playerId;
            
            if (rigidbody != null)
            {
                rigidbody.isKinematic = true;
            }
            if (collider != null)
            {
                collider.enabled = false;
            }

            if (RakeMultiplayerPlugin.isServer)
            {
                SendPickupMessage(playerId);
            }
        }

        public void OnDrop(Vector3 position, Vector3 velocity)
        {
            isHeld = false
;
            heldByPlayerId = -1;

            transform.position = position;

            if (rigidbody != null)
            {
                rigidbody.isKinematic = false;
                rigidbody.velocity = velocity;
            }
            if (collider != null)
            {
                collider.enabled = true;
            }

            if (RakeMultiplayerPlugin.isServer)
            {
                SendDropMessage(position, velocity);
            }
        }

        private void SendPickupMessage(int playerId)
        {
            using (var ms = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                writer.Write((byte)MessageType.ItemPickup);
                writer.Write(itemId);
                writer.Write(playerId);
                RakeMultiplayerPlugin.SendToAllClients(ms.ToArray());
            }
        }

        private void SendDropMessage(Vector3 position, Vector3 velocity)
        {
            using (var ms = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                writer.Write((byte)MessageType.ItemDrop);
                writer.Write(itemId);
                writer.Write(position.x);
                writer.Write(position.y);
                writer.Write(position.z);
                writer.Write(velocity.x);
                writer.Write(velocity.y);
                writer.Write(velocity.z);
                RakeMultiplayerPlugin.SendToAllClients(ms.ToArray());
            }
        }

        public void ApplyPickup(int playerId)
        {
            OnPickup(playerId);
        }

        public void ApplyDrop(Vector3 position, Vector3 velocity)
        {
            OnDrop(position, velocity);
        }

        private enum MessageType : byte
        {
            PlayerPosition = 1,
            PlayerRotation = 2,
            PlayerAnimation = 3,
            PlayerHealth = 4,
            PlayerStamina = 5,
            PlayerFlashlight = 6,
            RakePosition = 10,
            RakeState = 11,
            RakeAnimation = 12,
            ItemSpawn = 20,
            ItemPickup = 21,
            ItemDrop = 22,
            CameraSync = 30,
            QuestTrigger = 40
        }
    }
}