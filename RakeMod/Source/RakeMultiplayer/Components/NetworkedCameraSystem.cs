using System;
using System.IO;
using UnityEngine;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    /// <summary>
    /// Networked camera component - handles individual camera synchronization
    /// Attaches to each camera GameObject for server-authoritative state management
    /// </summary>
    [RequireComponent(typeof(NetIdentity))]
    public class NetworkedCamera : MonoBehaviour
    {
        [Header("Camera Configuration")]
        [Tooltip("Battery drain rate per minute when camera is active (default 5%/min)")]
        public float batteryDrainPerMinute = 5f;

        [Tooltip("Minimum battery level before camera auto-deactivates (0-100)")]
        public float minBatteryLevel = 0f;

        [Header("Runtime State (Server-Authoritative)")]
        [SerializeField, ReadOnly] private uint _cameraId = 0;
        [SerializeField, ReadOnly] private float _batteryLevel = 100f;
        [SerializeField, ReadOnly] private bool _isActive = false;
        [SerializeField, ReadOnly] private bool _isBroken = false;
        [SerializeField, ReadOnly] private uint _placedByPlayerId = 0;

        // Network identity reference
        private NetIdentity _netIdentity;

        // Sync timer for periodic updates
        private float _syncTimer = 0f;
        private const float SyncInterval = 1f; // 1Hz for camera state (low frequency, state changes rarely)

        // Last sent values for delta compression
        private float _lastSentBattery = -1f;
        private bool _lastSentActive = false;
        private bool _lastSentBroken = false;

        // Camera reference (the actual Camera component being controlled)
        private Camera _cameraComponent;
        private GameObject _cameraVisuals; // The visual representation (child with Camera component)

        // Events
        public event Action OnCameraActivated;
        public event Action OnCameraDeactivated;
        public event Action OnCameraBroken;
        public event Action<float> OnBatteryChanged; // new battery level

        #region Properties

        /// <summary>
        /// Unique network identifier for this camera (assigned on spawn)
        /// </summary>
        public uint CameraId
        {
            get => _cameraId;
            internal set => _cameraId = value;
        }

        /// <summary>
        /// Current battery level (0-100%)
        /// </summary>
        public float BatteryLevel
        {
            get => _batteryLevel;
            private set
            {
                float clamped = Mathf.Clamp(value, 0f, 100f);
                if (Mathf.Abs(_batteryLevel - clamped) > 0.01f)
                {
                    _batteryLevel = clamped;
                    OnBatteryChanged?.Invoke(_batteryLevel);
                }
            }
        }

        /// <summary>
        /// Whether the camera is currently active (rendering/transmitting)
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            private set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    UpdateCameraVisuals();
                    if (_isActive) OnCameraActivated?.Invoke();
                    else OnCameraDeactivated?.Invoke();
                }
            }
        }

        /// <summary>
        /// Whether the camera is broken/destroyed (triggers Rake alert)
        /// </summary>
        public bool IsBroken
        {
            get => _isBroken;
            private set
            {
                if (_isBroken != value)
                {
                    _isBroken = value;
                    if (_isBroken)
                    {
                        IsActive = false; // Broken cameras cannot be active
                        OnCameraBroken?.Invoke();
                        // Trigger QuestTriggerPacket on server
                        if (RakeMultiplayerPlugin.isServer)
                        {
                            SendQuestTrigger(QuestTriggerType.CameraBroken);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Network ID of the player who placed this camera
        /// </summary>
        public uint PlacedByPlayerId
        {
            get => _placedByPlayerId;
            internal set => _placedByPlayerId = value;
        }

        /// <summary>
        /// NetIdentity component for network synchronization
        /// </summary>
        public NetIdentity NetIdentity => _netIdentity;

        /// <summary>
        /// Whether this instance is the server-authoritative owner
        /// </summary>
        public bool IsServerOwner => RakeMultiplayerPlugin.isServer && _netIdentity != null && _netIdentity.IsOwner;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _netIdentity = GetComponent<NetIdentity>();
            if (_netIdentity == null)
            {
                _netIdentity = gameObject.AddComponent<NetIdentity>();
            }

            // Find camera component (usually on child object)
            _cameraComponent = GetComponentInChildren<Camera>();
            if (_cameraComponent != null)
            {
                _cameraVisuals = _cameraComponent.gameObject;
            }

            // Assign CameraId from NetIdentity if not set
            if (_cameraId == 0 && _netIdentity.NetId != 0)
            {
                _cameraId = _netIdentity.NetId;
            }
        }

        private void Start()
        {
            // Ensure NetIdentity is registered
            if (!NetIdentity.Registry.ContainsKey(_netIdentity.NetId) && _netIdentity.NetId != 0)
            {
                _netIdentity.Register();
            }

            // Server assigns CameraId if not set
            if (RakeMultiplayerPlugin.isServer && _cameraId == 0)
            {
                _cameraId = _netIdentity.NetId;
            }

            // Disable camera visuals initially
            UpdateCameraVisuals();
        }

        private void Update()
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (!RakeMultiplayerPlugin.isConnected) return;

            // Only server updates battery and state
            if (RakeMultiplayerPlugin.isServer)
            {
                UpdateServer();
            }

            // Both server and clients send/receive sync
            _syncTimer += Time.deltaTime;
            if (_syncTimer >= SyncInterval)
            {
                if (RakeMultiplayerPlugin.isServer)
                {
                    SendCameraSync(CameraSyncAction.Update);
                }
                _syncTimer = 0f;
            }
        }

        private void OnDestroy()
        {
            if (RakeMultiplayerPlugin.isServer && _netIdentity != null && NetIdentity.Registry.ContainsKey(_netIdentity.NetId))
            {
                SendCameraSync(CameraSyncAction.Destroy);
            }
        }

        #endregion

        #region Server Logic

        /// <summary>
        /// Server-authoritative update: battery drain, auto-deactivate on empty
        /// </summary>
        private void UpdateServer()
        {
            if (_isBroken) return; // Broken cameras don't drain battery

            if (_isActive)
            {
                // Drain battery: batteryDrainPerMinute per minute = batteryDrainPerMinute/60 per second
                float drainPerSecond = batteryDrainPerMinute / 60f;
                BatteryLevel -= drainPerSecond * Time.deltaTime;

                // Auto-deactivate when battery depleted
                if (BatteryLevel <= minBatteryLevel)
                {
                    BatteryLevel = 0f;
                    IsActive = false;
                    RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Camera {CameraId} battery depleted, deactivated");
                }
            }
        }

        /// <summary>
        /// Server: activate the camera (called when player views it or manually activates)
        /// </summary>
        public void ActivateCamera()
        {
            if (!RakeMultiplayerPlugin.isServer)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[NetworkedCamera] ActivateCamera called on client - ignored");
                return;
            }

            if (_isBroken)
            {
                RakeMultiplayerPlugin.Log?.LogWarning($"[NetworkedCamera] Cannot activate broken camera {CameraId}");
                return;
            }

            if (BatteryLevel <= minBatteryLevel)
            {
                RakeMultiplayerPlugin.Log?.LogWarning($"[NetworkedCamera] Camera {CameraId} has no battery");
                return;
            }

            IsActive = true;
            RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Camera {CameraId} activated by server");
            SendCameraSync(CameraSyncAction.Update);
        }

        /// <summary>
        /// Server: deactivate the camera
        /// </summary>
        public void DeactivateCamera()
        {
            if (!RakeMultiplayerPlugin.isServer)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[NetworkedCamera] DeactivateCamera called on client - ignored");
                return;
            }

            IsActive = false;
            RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Camera {CameraId} deactivated by server");
            SendCameraSync(CameraSyncAction.Update);
        }

        /// <summary>
        /// Server: break the camera (destroyed by Rake or player)
        /// </summary>
        public void BreakCamera(uint brokenByPlayerId = 0)
        {
            if (!RakeMultiplayerPlugin.isServer)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[NetworkedCamera] BreakCamera called on client - ignored");
                return;
            }

            if (_isBroken) return; // Already broken

            IsBroken = true;
            RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Camera {CameraId} broken by player {brokenByPlayerId}");
            SendCameraSync(CameraSyncAction.Break);
        }

        /// <summary>
        /// Server: recharge battery (e.g., from battery item)
        /// </summary>
        public void RechargeBattery(float amount)
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            BatteryLevel = Mathf.Min(100f, BatteryLevel + amount);
            RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Camera {CameraId} recharged to {BatteryLevel:F1}%");
            SendCameraSync(CameraSyncAction.Update);
        }

        /// <summary>
        /// Server: set initial placement data
        /// </summary>
        public void InitializePlacement(uint placedByPlayerId)
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            _placedByPlayerId = placedByPlayerId;
            _batteryLevel = 100f;
            _isActive = false;
            _isBroken = false;

            RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Camera {CameraId} placed by player {placedByPlayerId}");
            SendCameraSync(CameraSyncAction.Spawn);
        }

        #endregion

        #region Client Sync Handling

        /// <summary>
        /// Apply camera state from server (client-side)
        /// </summary>
        public void ApplyCameraSync(CameraSyncPacket packet)
        {
            if (RakeMultiplayerPlugin.isServer) return; // Don't apply on server

            _cameraId = packet.CameraId;
            _batteryLevel = packet.BatteryLevel;
            _isActive = packet.IsActive;
            _isBroken = packet.IsBroken;
            _placedByPlayerId = packet.PlacedByPlayerId;

            UpdateCameraVisuals();

            // Handle action-specific logic
            switch ((CameraSyncAction)packet.Action)
            {
                case CameraSyncAction.Spawn:
                    RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Client received camera spawn: {CameraId}");
                    break;
                case CameraSyncAction.Break:
                    RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Client received camera break: {CameraId}");
                    OnCameraBroken?.Invoke();
                    break;
                case CameraSyncAction.Destroy:
                    RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Client received camera destroy: {CameraId}");
                    // Object will be destroyed by NetIdentity cleanup
                    break;
            }
        }

        #endregion

        #region Network Sync

        private void SendCameraSync(CameraSyncAction action)
        {
            if (!RakeMultiplayerPlugin.isConnected) return;
            if (!RakeMultiplayerPlugin.isServer) return; // Only server sends sync

            // Delta compression: skip if nothing significant changed (except for Spawn/Break/Destroy)
            if (action == CameraSyncAction.Update)
            {
                bool batteryChanged = Mathf.Abs(_batteryLevel - _lastSentBattery) > 0.5f;
                bool activeChanged = _isActive != _lastSentActive;
                bool brokenChanged = _isBroken != _lastSentBroken;

                if (!batteryChanged && !activeChanged && !brokenChanged)
                {
                    return; // No significant change
                }
            }

            var packet = new CameraSyncPacket
            {
                CameraId = _cameraId,
                Position = transform.position,
                Rotation = transform.rotation,
                BatteryLevel = _batteryLevel,
                IsActive = _isActive,
                IsBroken = _isBroken,
                PlacedByPlayerId = _placedByPlayerId,
                Action = (byte)action
            };

            var data = packet.Serialize();
            RakeNetworkManager.Instance?.ServerBroadcast(data);

            // Update last sent values
            _lastSentBattery = _batteryLevel;
            _lastSentActive = _isActive;
            _lastSentBroken = _isBroken;
        }

        private void SendQuestTrigger(QuestTriggerType triggerType)
        {
            if (!RakeMultiplayerPlugin.isConnected) return;
            if (!RakeMultiplayerPlugin.isServer) return;

            var packet = new QuestTriggerPacket
            {
                TriggerType = triggerType,
                RelatedNetId = _cameraId,
                TriggeredByPlayerId = _placedByPlayerId,
                Position = transform.position,
                ExtraData = $"{{\"cameraId\":{_cameraId}}}"
            };

            var data = packet.Serialize();
            RakeNetworkManager.Instance?.ServerBroadcast(data);

            RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Sent QuestTrigger: {triggerType} for camera {_cameraId}");
        }

        #endregion

        #region Visual Updates

        private void UpdateCameraVisuals()
        {
            if (_cameraVisuals != null)
            {
                bool shouldBeActive = _isActive && !_isBroken;
                if (_cameraVisuals.activeSelf != shouldBeActive)
                {
                    _cameraVisuals.SetActive(shouldBeActive);
                }
            }

            if (_cameraComponent != null)
            {
                _cameraComponent.enabled = _isActive && !_isBroken;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get battery percentage as normalized value (0-1)
        /// </summary>
        public float GetBatteryNormalized() => BatteryLevel / 100f;

        /// <summary>
        /// Check if camera can be activated (has battery, not broken)
        /// </summary>
        public bool CanActivate() => !_isBroken && BatteryLevel > minBatteryLevel;

        /// <summary>
        /// Get remaining active time in seconds at current drain rate
        /// </summary>
        public float GetRemainingActiveTime()
        {
            if (batteryDrainPerMinute <= 0) return float.MaxValue;
            return (BatteryLevel / batteryDrainPerMinute) * 60f;
        }

        #endregion
    }

    /// <summary>
    /// Camera sync action types
    /// </summary>
    public enum CameraSyncAction : byte
    {
        Spawn = 0,
        Update = 1,
        Destroy = 2,
        Break = 3
    }

    /// <summary>
    /// Camera synchronization packet (inline - to be moved to Packets.cs)
    /// Server -> All (server-authoritative camera state sync)
    /// </summary>
    public class CameraSyncPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.CameraSync;

        public uint CameraId;
        public Vector3 Position;
        public Quaternion Rotation;
        public float BatteryLevel; // 0-100%
        public bool IsActive;
        public bool IsBroken;
        public uint PlacedByPlayerId;
        public byte Action; // CameraSyncAction enum

        public override void Write(BinaryWriter writer)
        {
            writer.Write(CameraId);
            writer.Write(Position.x);
            writer.Write(Position.y);
            writer.Write(Position.z);
            writer.Write(Rotation.x);
            writer.Write(Rotation.y);
            writer.Write(Rotation.z);
            writer.Write(Rotation.w);
            writer.Write(BatteryLevel);
            writer.Write(IsActive);
            writer.Write(IsBroken);
            writer.Write(PlacedByPlayerId);
            writer.Write(Action);
        }

        public override void Read(BinaryReader reader)
        {
            CameraId = reader.ReadUInt32();
            Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            BatteryLevel = reader.ReadSingle();
            IsActive = reader.ReadBoolean();
            IsBroken = reader.ReadBoolean();
            PlacedByPlayerId = reader.ReadUInt32();
            Action = reader.ReadByte();
        }
    }

    /// <summary>
    /// Quest trigger types (inline - to be moved to Packets.cs)
    /// </summary>
    public enum QuestTriggerType : byte
    {
        CameraBroken = 0,
        CameraPlaced = 1,
        TrapTriggered = 2,
        BaitPlaced = 3,
        ObjectiveComplete = 4
    }

    /// <summary>
    /// Quest trigger packet for game events (inline - to be moved to Packets.cs)
    /// Client -> Server (trigger event), Server -> All (broadcast event)
    /// </summary>
    public class QuestTriggerPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.QuestTrigger;

        public QuestTriggerType TriggerType;
        public uint RelatedNetId; // CameraId, TrapId, etc.
        public uint TriggeredByPlayerId;
        public Vector3 Position;
        public string ExtraData; // JSON or custom data

        public override void Write(BinaryWriter writer)
        {
            writer.Write((byte)TriggerType);
            writer.Write(RelatedNetId);
            writer.Write(TriggeredByPlayerId);
            writer.Write(Position.x);
            writer.Write(Position.y);
            writer.Write(Position.z);
            writer.Write(ExtraData ?? string.Empty);
        }

        public override void Read(BinaryReader reader)
        {
            TriggerType = (QuestTriggerType)reader.ReadByte();
            RelatedNetId = reader.ReadUInt32();
            TriggeredByPlayerId = reader.ReadUInt32();
            Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            ExtraData = reader.ReadString();
        }
    }

    /// <summary>
    /// ReadOnly attribute for inspector (Unity doesn't have built-in ReadOnly)
    /// </summary>
    public class ReadOnlyAttribute : PropertyAttribute { }
}