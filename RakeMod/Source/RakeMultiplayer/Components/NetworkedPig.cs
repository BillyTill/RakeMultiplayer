using System;
using System.Collections.Generic;
using UnityEngine;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    /// <summary>
    /// Networked pig/animal synchronization.
    /// The server runs the NavMesh AI and broadcasts position/state at a low rate;
    /// clients apply the authoritative transform and never run the AI themselves.
    /// </summary>
    public class NetworkedPig : MonoBehaviour
    {
        [Header("Sync")]
        [Tooltip("Pig state broadcast rate (Hz)")]
        public float syncRate = 10f;

        /// <summary>Pig states (mirrors PigStatePacket.State)</summary>
        public enum PigNetworkState : byte
        {
            Idle = 0,
            Patrol = 1,
            Escape = 2,
            CameraAttached = 3
        }

        private float _syncTimer;
        private Animal_AI _animal;
        private NavMeshAgent _agent;
        private Animator _animator;

        private Vector3 _lastSentPosition;
        private Quaternion _lastSentRotation;
        private byte _lastSentState = 255;
        private float _lastSentHealth = -1f;
        private bool _localCameraVisible;

        /// <summary>Deterministic ID shared by host and clients.</summary>
        public uint PigId { get; private set; }

        /// <summary>Camera mounted on this pig (Task 3.9), 0 if none.</summary>
        public uint AttachedCameraId { get; private set; }

        private static readonly System.Collections.Generic.Dictionary<uint, NetworkedPig> _registry =
            new System.Collections.Generic.Dictionary<uint, NetworkedPig>();

        public static bool TryGetPig(uint pigId, out NetworkedPig pig) => _registry.TryGetValue(pigId, out pig);

        public void AssignPigId(uint pigId)
        {
            if (pigId == 0) return;
            if (PigId != 0 && _registry.TryGetValue(PigId, out var existing) && existing == this)
            {
                _registry.Remove(PigId);
            }
            PigId = pigId;
            _registry[pigId] = this;
        }

        public void SetAttachedCamera(uint cameraId)
        {
            if (AttachedCameraId != 0 && AttachedCameraId != cameraId)
            {
                NetworkedCamera.ReleaseExternalCamera(AttachedCameraId);
            }
            AttachedCameraId = cameraId;
        }

        /// <summary>Server: send the current state immediately (e.g. after a camera attachment).</summary>
        public void ForceSendState()
        {
            if (!RakeMultiplayerPlugin.isServer) return;
            SendState(force: true);
        }

        public void AssignDeterministicId()
        {
            if (PigId != 0) return;

            var p = transform.position;
            int x = Mathf.RoundToInt(p.x * 100f);
            int y = Mathf.RoundToInt(p.y * 100f);
            int z = Mathf.RoundToInt(p.z * 100f);
            uint hash = (uint)((x * 73856093) ^ (y * 19349663) ^ (z * 83492791)) ^ NetworkedCamera.StableNameHash(gameObject.name);
            AssignPigId(hash == 0 ? 1u : hash);
        }

        private void Awake()
        {
            _animal = GetComponent<Animal_AI>();
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<Animator>();
        }

        private void Start()
        {
            AssignDeterministicId();

            // Clients must never run the animal AI - the server is authoritative
            if (!RakeMultiplayerPlugin.isServer)
            {
                if (_agent != null) _agent.enabled = false;
                if (_animal != null) _animal.enabled = false;
            }
        }

        private void Update()
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (!RakeMultiplayerPlugin.isConnected) return;
            if (!RakeMultiplayerPlugin.isServer) return;

            _syncTimer += Time.deltaTime;
            float interval = 1f / Mathf.Max(1f, syncRate);
            if (_syncTimer < interval) return;
            _syncTimer = 0f;

            SendState();
        }

        private PigNetworkState GetNetworkState()
        {
            if (_animal == null) return PigNetworkState.Idle;
            if (_animal.bTraped) return PigNetworkState.CameraAttached;
            if (_animal.bDanger) return PigNetworkState.Escape;
            if (_animal.bWalk || _animal.bHungry) return PigNetworkState.Patrol;
            return PigNetworkState.Idle;
        }

        private void SendState(bool force = false)
        {
            var state = GetNetworkState();
            float health = _animal != null ? _animal.Health : 0f;

            bool positionChanged = Vector3.Distance(_lastSentPosition, transform.position) > 0.02f;
            bool stateChanged = (byte)state != _lastSentState;
            bool healthChanged = Mathf.Abs(health - _lastSentHealth) > 0.5f;
            if (!force && !positionChanged && !stateChanged && !healthChanged) return;

            var packet = new PigStatePacket
            {
                PigId = PigId,
                Position = transform.position,
                Rotation = transform.rotation,
                State = (byte)state,
                Health = health,
                CameraId = AttachedCameraId
            };

            RakeNetworkManager.Instance?.ServerBroadcast(packet.Serialize());

            _lastSentPosition = transform.position;
            _lastSentRotation = transform.rotation;
            _lastSentState = (byte)state;
            _lastSentHealth = health;
        }

        /// <summary>
        /// Client: apply authoritative state from the server.
        /// </summary>
        public void ApplyState(PigStatePacket packet)
        {
            if (RakeMultiplayerPlugin.isServer) return;

            PigId = packet.PigId;
            AttachedCameraId = packet.CameraId;

            transform.position = packet.Position;
            transform.rotation = packet.Rotation;

            if (_animal != null)
            {
                _animal.Health = Mathf.RoundToInt(packet.Health);

                // Mirror the AI flags so other systems (camera collar, traps) see the right state
                var state = (PigNetworkState)packet.State;
                _animal.bDanger = state == PigNetworkState.Escape;
                _animal.bTraped = state == PigNetworkState.CameraAttached;
            }

            if (_animator != null)
            {
                float speed = packet.State == (byte)PigNetworkState.Escape ? 2f
                            : packet.State == (byte)PigNetworkState.Patrol ? 1f
                            : 0f;
                _animator.SetFloat("Speed", speed);
            }

            // Pig-camera attachment (Task 3.9): enable/disable the animal's camera collar
            if (_animal != null)
            {
                bool hasCamera = packet.CameraId != 0;
                if (hasCamera != _localCameraVisible)
                {
                    _localCameraVisible = hasCamera;

                    // Enable the collar camera transform directly (covers both
                    // RecordCamera_obj and AnimalRecordCamera_obj collars)
                    if (_animal.camera != null)
                    {
                        _animal.camera.gameObject.SetActive(hasCamera);
                    }
                    else
                    {
                        try { _animal.SetCamera(hasCamera); }
                        catch { /* animal may not have a collar camera */ }
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (PigId != 0 && _registry.TryGetValue(PigId, out var existing) && existing == this)
            {
                _registry.Remove(PigId);
            }

            // Free the quota slot held by this pig's camera
            if (RakeMultiplayerPlugin.isServer && AttachedCameraId != 0)
            {
                NetworkedCamera.ReleaseExternalCamera(AttachedCameraId);
            }
        }
    }
}
