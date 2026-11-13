using System;
using System.Collections.Generic;
using UnityEngine;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    /// <summary>
    /// Networked pig/animal synchronization.
    /// The server runs the NavMesh AI and broadcasts position/state at a low rate; clients
    /// apply the authoritative transform and never run the AI themselves.
    ///
    /// Clients interpolate towards the last received snapshot instead of snapping, otherwise
    /// a 10 Hz broadcast makes the animal visibly twitch.
    /// </summary>
    public class NetworkedPig : MonoBehaviour
    {
        [Header("Sync")]
        [Tooltip("Pig state broadcast rate (Hz)")]
        public float syncRate = 10f;

        [Tooltip("How quickly a client catches up to the last snapshot (higher = snappier)")]
        public float interpolationSpeed = 12f;

        [Tooltip("Distance beyond which the client teleports instead of interpolating")]
        public float snapDistance = 8f;

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

        // Client-side interpolation targets
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private bool _hasTarget;

        // Smoothed animator speed so a flickering Idle/Patrol state cannot make the
        // animation blend back and forth on clients.
        private float _animSpeedTarget;

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

            _targetPosition = transform.position;
            _targetRotation = transform.rotation;
        }

        private void Start()
        {
            AssignDeterministicId();

            // Clients must never run the animal AI - the server is authoritative.
            // In single-player (not connected) the vanilla AI must keep running.
            if (RakeMultiplayerPlugin.isConnected && !RakeMultiplayerPlugin.isServer)
            {
                if (_agent != null) _agent.enabled = false;
                if (_animal != null) _animal.enabled = false;

                // Root motion would move the transform locally and then the next snapshot
                // would drag it back, which reads as jitter.
                if (_animator != null) _animator.applyRootMotion = false;
            }
        }

        private void Update()
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (!RakeMultiplayerPlugin.isConnected) return;

            if (RakeMultiplayerPlugin.isServer)
            {
                _syncTimer += Time.deltaTime;
                float interval = 1f / Mathf.Max(1f, syncRate);
                if (_syncTimer < interval) return;
                _syncTimer = 0f;

                SendState();
                return;
            }

            // Client: follow the last snapshot smoothly instead of hard snapping
            if (!_hasTarget) return;

            float t = 1f - Mathf.Exp(-Mathf.Max(0.01f, interpolationSpeed) * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, _targetPosition, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, t);

            if (_animator != null)
            {
                float current = _animator.GetFloat("Speed");
                _animator.SetFloat("Speed", Mathf.MoveTowards(current, _animSpeedTarget, Time.deltaTime * 2.5f));
            }
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

            // Teleport on the first snapshot and after a big jump, interpolate otherwise
            if (!_hasTarget || Vector3.Distance(transform.position, packet.Position) > snapDistance)
            {
                transform.position = packet.Position;
                transform.rotation = packet.Rotation;
            }

            _targetPosition = packet.Position;
            _targetRotation = packet.Rotation;
            _hasTarget = true;

            if (_animal != null)
            {
                _animal.Health = Mathf.RoundToInt(packet.Health);

                // Mirror the AI flags so other systems (camera collar, traps) see the right state
                var state = (PigNetworkState)packet.State;
                _animal.bDanger = state == PigNetworkState.Escape;
                _animal.bTraped = state == PigNetworkState.CameraAttached;
            }

            // Smooth the animator speed so a flickering Idle/Patrol state on the server
            // does not make the animation blend back and forth.
            _animSpeedTarget = packet.State == (byte)PigNetworkState.Escape ? 2f
                             : packet.State == (byte)PigNetworkState.Patrol ? 1f
                             : 0f;

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
