using System;
using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Networked camera system synchronization
    /// </summary>
    public class NetworkedCameraSystem : MonoBehaviour
    {
        [Header("Sync Settings")]
        public float syncRate = 0.1f; // 10Hz

        private CameraComputer cameraComputer;
        private float syncTimer;

        [Serializable]
        public struct CameraData
        {
            public uint netId;
            public Vector3 position;
            public Quaternion rotation;
            public bool isActive;
            public bool isBroken;
            public uint placedByPlayerNetId;
            public float batteryLevel;
        }

        private CameraData[] syncedCameras = new CameraData[0];

        private void Awake()
        {
            cameraComputer = FindObjectOfType<CameraComputer>();
        }

        private void Update()
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (!RakeMultiplayerPlugin.isConnected) return;

            syncTimer += Time.deltaTime;
            if (syncTimer >= syncRate)
            {
                SyncCameras();
                syncTimer = 0f;
            }
        }

        private void SyncCameras()
        {
            if (cameraComputer == null) return;

            // TODO: Get camera data from CameraComputer and sync
            // This depends on the actual CameraComputer implementation
        }

        public void OnCameraPlaced(GameObject cameraObj, int playerId)
        {
            if (RakeMultiplayerPlugin.isServer)
            {
                SendCameraSpawn(cameraObj, playerId);
            }
        }

        public void OnCameraDestroyed(GameObject cameraObj)
        {
            if (RakeMultiplayerPlugin.isServer)
            {
                SendCameraDestroy(cameraObj);
            }
        }

        private void SendCameraSpawn(GameObject cameraObj, int playerId)
        {
            var cam = cameraObj.GetComponent<Camera>();
            if (cam == null) return;

            using (var ms = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                writer.Write((byte)MessageType.CameraSync);
                writer.Write((byte)1); // Spawn
                writer.Write(cameraObj.transform.position.x);
                writer.Write(cameraObj.transform.position.y);
                writer.Write(cameraObj.transform.position.z);
                writer.Write(cameraObj.transform.rotation.x);
                writer.Write(cameraObj.transform.rotation.y);
                writer.Write(cameraObj.transform.rotation.z);
                writer.Write(cameraObj.transform.rotation.w);
                writer.Write(playerId);
                RakeMultiplayerPlugin.SendToAllClients(ms.ToArray());
            }
        }

        private void SendCameraDestroy(GameObject cameraObj)
        {
            using (var ms = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                writer.Write((byte)MessageType.CameraSync);
                writer.Write((byte)2); // Destroy
                writer.Write(cameraObj.GetInstanceID());
                RakeMultiplayerPlugin.SendToAllClients(ms.ToArray());
            }
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