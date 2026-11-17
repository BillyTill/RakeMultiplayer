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
        // Cameras, pigs and animals. Part of the partial class declared in
        // RakeNetworkManager.cs; state stays there, behaviour lives here.

// === Stage 3.4: Camera setup/pickup (server-authoritative) ===
        private void HandleServerCameraSetupRequest(int connectionId, CameraSetupRequestPacket packet, byte[] rawData)
        {
            // Validate the claimed player identity (clients cannot act as another player)
            int expectedPlayerId = connectionId + 1;
            if (packet.PlayerId != expectedPlayerId)
            {
                RakeMultiplayerPlugin.Log?.LogWarning(
                    $"[3.4] Server rejected camera request from C{connectionId}: claimed player {packet.PlayerId}, expected {expectedPlayerId}");
                SendCameraSetupResult(connectionId, packet, false, "Unauthorized");
                return;
            }

            // Pig cameras are flagged with the high bit (0x80000000 | pigId)
            if ((packet.CameraId & 0x80000000u) != 0)
            {
                HandleServerPigCameraRequest(connectionId, packet);
                return;
            }

            if (!NetworkedCamera.TryGetCamera(packet.CameraId, out var camera) || camera == null)
            {
                RakeMultiplayerPlugin.Log?.LogWarning($"[3.4] Server: camera {packet.CameraId} not found for setup request");
                SendCameraSetupResult(connectionId, packet, false, "NotFound");
                return;
            }

            bool approved = packet.IsSetup
                ? camera.InitializePlacement(packet.PlayerId)
                : camera.UnplaceCamera();

            string reason = approved ? string.Empty : (packet.IsSetup ? "CameraLimit" : "NotPlaced");
            if (!approved)
            {
                RakeMultiplayerPlugin.Log?.LogWarning(
                    $"[3.4] Server rejected {(packet.IsSetup ? "setup" : "pickup")} of camera {packet.CameraId} by player {packet.PlayerId}: {reason}");
            }

            SendCameraSetupResult(connectionId, packet, approved, reason);
        }

        private void HandleServerPigCameraRequest(int connectionId, CameraSetupRequestPacket packet)
        {
            uint pigId = packet.CameraId & 0x7FFFFFFFu;
            if (!NetworkedPig.TryGetPig(pigId, out var pig) || pig == null)
            {
                RakeMultiplayerPlugin.Log?.LogWarning($"[3.9] Pig {pigId} not found for pig-camera request");
                SendCameraSetupResult(connectionId, packet, false, "NotFound");
                return;
            }

            if (packet.IsSetup)
            {
                bool ok = NetworkedCamera.TryReserveExternalCamera(packet.CameraId);
                if (ok)
                {
                    pig.SetAttachedCamera(packet.CameraId);
                    pig.ForceSendState();
                    RakeMultiplayerPlugin.Log?.LogInfo(
                        $"[3.9] Pig camera {packet.CameraId} attached to pig {pigId} " +
                        $"({NetworkedCamera.GetTotalUsableCameraCount()}/{NetworkedCamera.MaxCameras})");
                }
                else
                {
                    RakeMultiplayerPlugin.Log?.LogWarning(
                        $"[3.9] Pig-camera setup rejected: limit {NetworkedCamera.MaxCameras} reached");
                }
                SendCameraSetupResult(connectionId, packet, ok, ok ? string.Empty : "CameraLimit");
            }
            else
            {
                NetworkedCamera.ReleaseExternalCamera(packet.CameraId);
                pig.SetAttachedCamera(0);
                pig.ForceSendState();
                SendCameraSetupResult(connectionId, packet, true, string.Empty);
            }
        }

        private void SendCameraSetupResult(int connectionId, CameraSetupRequestPacket request, bool approved, string reason)
        {
            var result = new CameraSetupRequestPacket
            {
                CameraId = request.CameraId,
                PlayerId = request.PlayerId,
                IsSetup = request.IsSetup,
                IsResult = true,
                Approved = approved,
                Reason = reason ?? string.Empty
            };
            _server.Send(connectionId, result.Serialize());
        }

        /// <summary>
        /// Assign deterministic CameraIds to every camera object in the scene.
        /// Must be called on BOTH host and client during scene initialization
        /// (same order on every machine: position, then name).
        /// </summary>
        public void RegisterCameras()
        {
            var cameras = UnityEngine.Object.FindObjectsOfType<RecordCamera_obj>();
            if (cameras == null || cameras.Length == 0)
            {
                RakeMultiplayerPlugin.Log?.LogInfo("[3.4] No RecordCamera_obj found in scene");
                return;
            }

            System.Array.Sort(cameras, (a, b) =>
            {
                if (a == null || b == null) return 0;
                var pa = a.transform.position;
                var pb = b.transform.position;
                int cmp = pa.x.CompareTo(pb.x); if (cmp != 0) return cmp;
                cmp = pa.y.CompareTo(pb.y); if (cmp != 0) return cmp;
                cmp = pa.z.CompareTo(pb.z); if (cmp != 0) return cmp;
                return string.CompareOrdinal(a.gameObject.name, b.gameObject.name);
            });

            for (int i = 0; i < cameras.Length; i++)
            {
                var recordCam = cameras[i];
                if (recordCam == null) continue;

                var netCam = recordCam.GetComponent<NetworkedCamera>();
                if (netCam == null)
                {
                    netCam = recordCam.gameObject.AddComponent<NetworkedCamera>();
                }
                netCam.AssignCameraId((uint)(i + 1));
            }

            _nextCameraNetworkId = Math.Max(_nextCameraNetworkId, cameras.Length + 1);
            RakeMultiplayerPlugin.Log?.LogInfo($"[3.4] Registered {cameras.Length} networked cameras with deterministic IDs");
        }

        private void PruneDestroyedBaits()
        {
            for (int i = _activeBaitTransforms.Count - 1; i >= 0; i--)
            {
                if (_activeBaitTransforms[i] == null) _activeBaitTransforms.RemoveAt(i);
            }
        }

        /// <summary>
        /// Attach NetworkedPig to every animal in the scene and assign deterministic IDs.
        /// Must run on BOTH host and client.
        /// </summary>
        public void RegisterAnimals()
        {
            var animals = UnityEngine.Object.FindObjectsOfType<Animal_AI>();
            if (animals == null || animals.Length == 0)
            {
                RakeMultiplayerPlugin.Log?.LogInfo("[3.8] No Animal_AI found in scene");
                return;
            }

            System.Array.Sort(animals, (a, b) =>
            {
                if (a == null || b == null) return 0;
                var pa = a.transform.position;
                var pb = b.transform.position;
                int cmp = pa.x.CompareTo(pb.x); if (cmp != 0) return cmp;
                cmp = pa.y.CompareTo(pb.y); if (cmp != 0) return cmp;
                cmp = pa.z.CompareTo(pb.z); if (cmp != 0) return cmp;
                return string.CompareOrdinal(a.gameObject.name, b.gameObject.name);
            });

            for (int i = 0; i < animals.Length; i++)
            {
                var animal = animals[i];
                if (animal == null) continue;

                var pig = animal.GetComponent<NetworkedPig>();
                if (pig == null) pig = animal.gameObject.AddComponent<NetworkedPig>();
                pig.AssignPigId((uint)(i + 1));
            }

            RakeMultiplayerPlugin.Log?.LogInfo($"[3.8] Registered {animals.Length} networked animals");
        }

        private void HandleClientCameraSetupResult(CameraSetupRequestPacket packet)
        {
            if (!packet.IsResult) return;

            if (packet.Approved)
            {
                RakeMultiplayerPlugin.Log?.LogInfo(
                    $"[3.4] Server approved {(packet.IsSetup ? "setup" : "pickup")} of camera {packet.CameraId}");
                return;
            }

            RakeMultiplayerPlugin.Log?.LogWarning(
                $"[3.4] Server rejected {(packet.IsSetup ? "setup" : "pickup")} of camera {packet.CameraId}: {packet.Reason}");

            if (packet.Reason == "CameraLimit")
            {
                try { InformationUI.instance?.DrawHint($"You cannot setup no more {NetworkedCamera.MaxCameras} cameras"); }
                catch { /* InformationUI may not exist in this scene */ }
            }

            OnCameraSetupRejected?.Invoke(packet.PlayerId, packet.Reason);
        }

        private void HandleClientPigState(PigStatePacket packet)
        {
            if (NetworkedPig.TryGetPig(packet.PigId, out var pig) && pig != null)
            {
                pig.ApplyState(packet);
            }
        }

        private void HandleClientCameraSync(CameraSyncPacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[CameraSync] Client received CameraSync: cameraId={packet.CameraId}, active={packet.IsActive}, broken={packet.IsBroken}, battery={packet.BatteryLevel:F1}%");
            
            // Use static registry for O(1) lookup
            if (NetworkedCamera.TryGetCamera(packet.CameraId, out var cam))
            {
                cam.ApplyCameraSync(packet);
                RakeMultiplayerPlugin.Log?.LogInfo($"[CameraSync] Applied CameraSync to camera {packet.CameraId}");
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogWarning($"[CameraSync] Camera {packet.CameraId} not found in registry");
            }
        }

        private void HandleClientCameraSpawn(CameraSpawnPacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[CameraSync] Client received CameraSpawn: cameraId={packet.CameraId}, position={packet.Position}");
            
            // Use static registry for O(1) lookup
            if (NetworkedCamera.TryGetCamera(packet.CameraId, out var cam))
            {
                cam.HandleCameraSpawn(packet);
                RakeMultiplayerPlugin.Log?.LogInfo($"[CameraSync] Handled CameraSpawn for camera {packet.CameraId}");
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogWarning($"[CameraSync] Camera {packet.CameraId} not found in registry for CameraSpawn");
            }
        }

        private void HandleClientCameraDestroy(CameraDestroyPacket packet)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[CameraSync] Client received CameraDestroy: cameraId={packet.CameraId}");
            
            // Use static registry for O(1) lookup
            if (NetworkedCamera.TryGetCamera(packet.CameraId, out var cam))
            {
                cam.HandleCameraDestroy(packet);
                RakeMultiplayerPlugin.Log?.LogInfo($"[CameraSync] Handled CameraDestroy for camera {packet.CameraId}");
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogWarning($"[CameraSync] Camera {packet.CameraId} not found in registry for CameraDestroy");
            }
        }
    }
}
