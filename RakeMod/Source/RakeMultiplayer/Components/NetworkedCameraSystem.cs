using System;
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
        /// <summary>
        /// Maximum number of cameras that may be deployed at the same time
        /// (matches the original game: "You cannot setup no more 5 cameras").
        /// Broken cameras do not count toward this limit, so a replacement can be set up.
        /// </summary>
        public const int MaxCameras = 5;

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
        [SerializeField, ReadOnly] private int _placedByPlayerId = 0; // Packets.cs uses int
        [SerializeField, ReadOnly] private bool _isPlaced = false;   // Counts toward the 5-camera limit

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
                        // NOTE: QuestTrigger for CameraBreak IS sent in BreakCamera() to alert Rake AI.
                        // Client HandleQuestTrigger does NOT fire OnCameraBroken (avoids race with CameraSync).
                        // CameraSync packet is the authoritative source for IsBroken state on clients.
                    }
                }
            }
        }

        /// <summary>
        /// Network ID of the player who placed this camera (int to match Packets.cs)
        /// </summary>
        public int PlacedByPlayerId
        {
            get => _placedByPlayerId;
            internal set => _placedByPlayerId = value;
        }

        /// <summary>
        /// Whether this camera has been deployed in the world.
        /// Only placed cameras count toward <see cref="MaxCameras"/>.
        /// </summary>
        public bool IsPlaced
        {
            get => _isPlaced;
            private set => _isPlaced = value;
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

        /// <summary>
        /// Static registry for O(1) camera lookup by CameraId
        /// </summary>
        private static readonly Dictionary<uint, NetworkedCamera> _cameraRegistry = new Dictionary<uint, NetworkedCamera>();

        /// <summary>
        /// Try to find a NetworkedCamera by CameraId
        /// </summary>
        public static bool TryGetCamera(uint cameraId, out NetworkedCamera camera)
        {
            return _cameraRegistry.TryGetValue(cameraId, out camera);
        }

        /// <summary>
        /// Number of cameras currently deployed in the world (includes broken ones).
        /// </summary>
        public static int GetPlacedCameraCount()
        {
            int count = 0;
            foreach (var cam in _cameraRegistry.Values)
            {
                if (cam != null && cam._isPlaced) count++;
            }
            return count;
        }

        /// <summary>
        /// Number of deployed cameras that still count against the quota
        /// (broken cameras free a slot so a replacement can be set up).
        /// </summary>
        public static int GetUsableCameraCount()
        {
            int count = 0;
            foreach (var cam in _cameraRegistry.Values)
            {
                if (cam != null && cam._isPlaced && !cam._isBroken) count++;
            }
            return count;
        }

        /// <summary>
        /// Cameras that live outside the NetworkedCamera registry (e.g. a camera
        /// mounted on a trapped pig). They still consume a slot in the 5-camera limit.
        /// </summary>
        private static readonly System.Collections.Generic.HashSet<uint> _externalCameras =
            new System.Collections.Generic.HashSet<uint>();

        /// <summary>Number of external (pig) cameras currently deployed.</summary>
        public static int GetExternalCameraCount() => _externalCameras.Count;

        /// <summary>Deployed cameras across both registries.</summary>
        public static int GetTotalUsableCameraCount() => GetUsableCameraCount() + _externalCameras.Count;

        /// <summary>
        /// Reserve a quota slot for a non-registry camera (pig camera).
        /// Idempotent for the same ID.
        /// </summary>
        public static bool TryReserveExternalCamera(uint cameraId)
        {
            if (cameraId == 0) return false;
            if (_externalCameras.Contains(cameraId)) return true;
            if (GetTotalUsableCameraCount() >= MaxCameras) return false;
            _externalCameras.Add(cameraId);
            return true;
        }

        /// <summary>Release an external camera slot (pig died / camera removed).</summary>
        public static void ReleaseExternalCamera(uint cameraId)
        {
            if (cameraId != 0) _externalCameras.Remove(cameraId);
        }

        /// <summary>
        /// Server-authoritative quota check: may another camera be deployed?
        /// Includes pig cameras reserved via <see cref="TryReserveExternalCamera"/>.
        /// </summary>
        public static bool CanPlaceCamera()
        {
            return GetTotalUsableCameraCount() < MaxCameras;
        }

        /// <summary>Cross-process stable string hash (FNV-1a).</summary>
        internal static uint StableNameHash(string value)
        {
            uint hash = 2166136261u;
            if (!string.IsNullOrEmpty(value))
            {
                foreach (char c in value)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }
            }
            return hash;
        }

        /// <summary>
        /// Assign a deterministic CameraId (called by RakeNetworkManager.RegisterCameras
        /// on both host and client so scene cameras share IDs).
        /// </summary>
        public void AssignCameraId(uint cameraId)
        {
            if (cameraId == 0) return;

            if (_cameraId != 0 && _cameraRegistry.TryGetValue(_cameraId, out var existing) && existing == this)
            {
                _cameraRegistry.Remove(_cameraId);
            }

            _cameraId = cameraId;
            _cameraRegistry[cameraId] = this;
        }

        /// <summary>
        /// Deterministic CameraId used when explicit registration did not run.
        /// Mirrors the item-ID fallback: position hash + name hash.
        /// </summary>
        private uint ComputeDeterministicCameraId()
        {
            var p = transform.position;
            int x = Mathf.RoundToInt(p.x * 100f);
            int y = Mathf.RoundToInt(p.y * 100f);
            int z = Mathf.RoundToInt(p.z * 100f);
            uint hash = (uint)((x * 73856093) ^ (y * 19349663) ^ (z * 83492791)) ^ StableNameHash(gameObject.name);
            return hash == 0 ? 1u : hash;
        }

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

            // NOTE: CameraId assignment moved to Start() to avoid race with NetIdentity registration
            // Server's InitializePlacement() is the sole authority for CameraId
        }

        private void Start()
        {
            // Ensure NetIdentity is registered
            if (!NetIdentity.Registry.ContainsKey(_netIdentity.NetId) && _netIdentity.NetId != 0)
            {
                _netIdentity.Register();
            }

            // CameraId is assigned by RakeNetworkManager.RegisterCameras() on BOTH
            // host and client so the same scene camera gets the same ID everywhere.
            // Fallback to a deterministic position hash only if registration was skipped.
            if (_cameraId == 0)
            {
                _cameraId = ComputeDeterministicCameraId();
            }

            // Register in static registry if CameraId is set
            if (_cameraId != 0)
            {
                if (!_cameraRegistry.ContainsKey(_cameraId))
                {
                    _cameraRegistry[_cameraId] = this;
                }
                else
                {
                    RakeMultiplayerPlugin.Log?.LogWarning($"[NetworkedCamera] CameraId {_cameraId} already in registry, replacing");
                    _cameraRegistry[_cameraId] = this;
                }
            }

            // Disable camera visuals initially
            UpdateCameraVisuals();
        }

        private void OnDestroy()
        {
            // Server: send CameraDestroy notification before cleanup
            if (RakeMultiplayerPlugin.isServer && _netIdentity != null && NetIdentity.Registry.ContainsKey(_netIdentity.NetId))
            {
                SendCameraDestroy();
            }

            // Unregister from static registry
            if (_cameraId != 0 && _cameraRegistry.ContainsKey(_cameraId))
            {
                _cameraRegistry.Remove(_cameraId);
            }
        }

        private void Update()
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (!RakeMultiplayerPlugin.isConnected) return;

            // Server-authoritative battery drain and state updates
            if (RakeMultiplayerPlugin.isServer)
            {
                UpdateServer();
            }

            // Periodic sync (both server and clients)
            _syncTimer += Time.deltaTime;
            if (_syncTimer >= SyncInterval)
            {
                if (RakeMultiplayerPlugin.isServer)
                {
                    SendCameraUpdate();
                }
                _syncTimer = 0f;
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
            SendCameraUpdate();
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
            SendCameraUpdate();
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
            SendCameraBreak();
            // Send QuestTriggerPacket to alert Rake AI (CameraBreak = 0)
            SendQuestTrigger(0);
            BroadcastMapMarker(false); // mark the broken camera on the minimap
        }

        /// <summary>
        /// Server: recharge battery (e.g., from battery item)
        /// </summary>
        public void RechargeBattery(float amount)
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            BatteryLevel = Mathf.Min(100f, BatteryLevel + amount);
            RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Camera {CameraId} recharged to {BatteryLevel:F1}%");
            SendCameraUpdate();
        }

        /// <summary>
        /// Server: set initial placement data.
        /// Enforces the 5-camera limit (see <see cref="MaxCameras"/>).
        /// </summary>
        /// <returns>true if the camera was deployed; false if the limit rejected it.</returns>
        public bool InitializePlacement(int placedByPlayerId)
        {
            if (!RakeMultiplayerPlugin.isServer) return false;

            // Ensure CameraId is assigned (sole authority)
            if (_cameraId == 0 && _netIdentity != null && _netIdentity.NetId != 0)
            {
                _cameraId = _netIdentity.NetId;
                // Register in static registry
                if (!_cameraRegistry.ContainsKey(_cameraId))
                {
                    _cameraRegistry[_cameraId] = this;
                }
            }

            // Enforce the 5-camera quota (server-authoritative)
            if (!_isPlaced)
            {
                if (!CanPlaceCamera())
                {
                    RakeMultiplayerPlugin.Log?.LogWarning(
                        $"[NetworkedCamera] Cannot setup camera {CameraId}: limit of {MaxCameras} active cameras reached " +
                        $"(placed={GetPlacedCameraCount()}, usable={GetUsableCameraCount()})");
                    return false;
                }
                _isPlaced = true;
            }

            _placedByPlayerId = placedByPlayerId;
            _batteryLevel = 100f;
            _isActive = false;
            _isBroken = false;

            RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Camera {CameraId} placed by player {placedByPlayerId} ({GetUsableCameraCount()}/{MaxCameras})");

            // Host does not receive its own broadcasts - apply the visual setup locally
            ApplySetupVisuals();

            SendCameraSpawn();
            BroadcastMapMarker(true);
            return true;
        }

        /// <summary>
        /// Restore a persisted camera state after a scene reload (Task 4.4).
        /// </summary>
        public void RestoreState(bool active, bool broken, float battery, int placedByPlayerId)
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            _isPlaced = true;
            _batteryLevel = Mathf.Clamp(battery, 0f, 100f);
            _isBroken = broken;
            _isActive = active && !broken;
            _placedByPlayerId = placedByPlayerId;

            if (!_cameraRegistry.ContainsKey(_cameraId))
            {
                _cameraRegistry[_cameraId] = this;
            }

            ApplySetupVisuals();
            UpdateCameraVisuals();
            SendCameraSpawn();
            SendCameraUpdate();

            RakeMultiplayerPlugin.Log?.LogInfo(
                $"[4.4] Camera {CameraId} restored (active={active}, broken={broken}, battery={_batteryLevel:F1})");
        }

        /// <summary>
        /// Server: un-deploy the camera (player picked it up).
        /// </summary>
        /// <returns>true if the camera was deployed and is now un-deployed.</returns>
        public bool UnplaceCamera()
        {
            if (!RakeMultiplayerPlugin.isServer) return false;
            if (!_isPlaced) return false;

            _isPlaced = false;
            _isActive = false;

            ApplyUnsetupVisuals();
            SendCameraDestroy();
            BroadcastMapMarker(false);

            RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Camera {CameraId} picked up ({GetUsableCameraCount()}/{MaxCameras})");
            return true;
        }

        #endregion

        #region Client Sync Handling

        /// <summary>
        /// Apply camera state from server (client-side) - uses Packets.cs CameraSyncPacket structure
        /// </summary>
        public void ApplyCameraSync(CameraSyncPacket packet)
        {
            if (RakeMultiplayerPlugin.isServer) return; // Don't apply on server

            bool batteryChanged = Mathf.Abs(_batteryLevel - packet.BatteryLevel) > 0.01f;
            bool activeChanged = _isActive != packet.IsActive;
            bool brokenChanged = _isBroken != packet.IsBroken;

            _cameraId = packet.CameraId;
            _batteryLevel = packet.BatteryLevel;
            _isActive = packet.IsActive;
            _isBroken = packet.IsBroken;
            _placedByPlayerId = packet.PlacedByPlayerId;

            UpdateCameraVisuals();

            // Fire events for changes
            if (batteryChanged)
            {
                OnBatteryChanged?.Invoke(_batteryLevel);
            }
            if (brokenChanged && _isBroken)
            {
                OnCameraBroken?.Invoke();
            }

            RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Client received camera sync: {CameraId}, active={_isActive}, broken={_isBroken}, battery={_batteryLevel:F1}");
        }

        /// <summary>
        /// Handle camera spawn notification (client-side)
        /// </summary>
        public void HandleCameraSpawn(CameraSpawnPacket packet)
        {
            if (RakeMultiplayerPlugin.isServer) return;

            _cameraId = packet.CameraId;
            _placedByPlayerId = packet.PlacedByPlayerId;
            _batteryLevel = 100f;
            _isActive = false;
            _isBroken = false;
            _isPlaced = true;

            // Ensure the client registry can resolve this camera by ID
            if (_cameraId != 0 && !_cameraRegistry.ContainsKey(_cameraId))
            {
                _cameraRegistry[_cameraId] = this;
            }

            ApplySetupVisuals();
            UpdateCameraVisuals();
            RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Client received camera spawn: {CameraId} placed by player {packet.PlacedByPlayerId}");
        }

        /// <summary>
        /// Handle camera destroy notification (client-side)
        /// </summary>
        public void HandleCameraDestroy(CameraDestroyPacket packet)
        {
            if (RakeMultiplayerPlugin.isServer) return;

            RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Client received camera destroy: {packet.CameraId}");

            _isPlaced = false;

            ApplyUnsetupVisuals();

            // Remove from client registry so the slot is freed locally
            if (packet.CameraId != 0 && _cameraRegistry.ContainsKey(packet.CameraId))
            {
                _cameraRegistry.Remove(packet.CameraId);
            }
        }

        /// <summary>
        /// Handle quest trigger events (client-side)
        /// </summary>
        public void HandleQuestTrigger(QuestTriggerPacket packet)
        {
            if (RakeMultiplayerPlugin.isServer) return;

            // TriggerType: 0=CameraBreak, 1=BaitTaken, 2=TrapTriggered
            // NOTE: OnCameraBroken is fired ONLY by ApplyCameraSync (CameraSync packet)
            // to avoid race conditions where QuestTrigger arrives before CameraSync
            if (packet.TriggerType == 0 && packet.CameraId == _cameraId)
            {
                RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Client received camera break trigger for camera {_cameraId} (UI/sound only, state from CameraSync)");
                // Could play break sound effect here if needed
            }
            else if (packet.TriggerType == 1) // BaitTaken
            {
                RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Client received bait taken trigger at camera {_cameraId}");
            }
            else if (packet.TriggerType == 2) // TrapTriggered
            {
                RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Client received trap triggered trigger at camera {_cameraId}");
            }
        }

        #endregion

        #region Network Sync

        /// <summary>
        /// Send camera state update (periodic or on state change)
        /// Uses Packets.cs CameraSyncPacket (no Action field)
        /// </summary>
        private void SendCameraUpdate()
        {
            if (!RakeMultiplayerPlugin.isConnected) return;
            if (!RakeMultiplayerPlugin.isServer) return;

            // Delta compression: skip if nothing significant changed
            bool batteryChanged = Mathf.Abs(_batteryLevel - _lastSentBattery) > 0.5f;
            bool activeChanged = _isActive != _lastSentActive;
            bool brokenChanged = _isBroken != _lastSentBroken;

            if (!batteryChanged && !activeChanged && !brokenChanged)
            {
                return; // No significant change
            }

            var packet = new CameraSyncPacket
            {
                CameraId = _cameraId,
                Position = transform.position,
                Rotation = transform.rotation,
                IsActive = _isActive,
                IsBroken = _isBroken,
                BatteryLevel = _batteryLevel,
                PlacedByPlayerId = _placedByPlayerId
            };

            var data = packet.Serialize();
            RakeNetworkManager.Instance?.ServerBroadcast(data);

            // Update last sent values
            _lastSentBattery = _batteryLevel;
            _lastSentActive = _isActive;
            _lastSentBroken = _isBroken;
        }

        /// <summary>
        /// Send camera spawn notification
        /// Uses Packets.cs CameraSpawnPacket
        /// </summary>
        private void SendCameraSpawn()
        {
            if (!RakeMultiplayerPlugin.isConnected) return;
            if (!RakeMultiplayerPlugin.isServer) return;

            var packet = new CameraSpawnPacket
            {
                CameraId = _cameraId,
                Position = transform.position,
                Rotation = transform.rotation,
                PlacedByPlayerId = _placedByPlayerId
            };

            var data = packet.Serialize();
            RakeNetworkManager.Instance?.ServerBroadcast(data);

            RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Sent CameraSpawn: {CameraId}");
        }

        /// <summary>
        /// Send camera break notification (and quest trigger)
        /// Uses Packets.cs CameraSyncPacket with IsBroken=true, and QuestTriggerPacket
        /// </summary>
        private void SendCameraBreak()
        {
            if (!RakeMultiplayerPlugin.isConnected) return;
            if (!RakeMultiplayerPlugin.isServer) return;

            // Send updated camera state with IsBroken=true
            var syncPacket = new CameraSyncPacket
            {
                CameraId = _cameraId,
                Position = transform.position,
                Rotation = transform.rotation,
                IsActive = _isActive,
                IsBroken = _isBroken,
                BatteryLevel = _batteryLevel,
                PlacedByPlayerId = _placedByPlayerId
            };

            var data = syncPacket.Serialize();
            RakeNetworkManager.Instance?.ServerBroadcast(data);

            // Update last sent values
            _lastSentBattery = _batteryLevel;
            _lastSentActive = _isActive;
            _lastSentBroken = _isBroken;

            RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Sent CameraSync (Break): {CameraId}");
        }

        /// <summary>
        /// Broadcast this camera as a minimap marker (MarkerType 0 = Camera).
        /// </summary>
        private void BroadcastMapMarker(bool active)
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            var marker = new MapMarkerPacket
            {
                MarkerType = 0, // Camera
                Position = transform.position,
                MarkerId = CameraId,
                IsActive = active
            };

            RakeNetworkManager.Instance?.RegisterLocalMapMarker(marker);
            RakeNetworkManager.Instance?.ServerBroadcast(marker.Serialize());
        }

        /// <summary>
        /// Send camera destroy notification
        /// Uses Packets.cs CameraDestroyPacket
        /// </summary>
        private void SendCameraDestroy()
        {
            if (!RakeMultiplayerPlugin.isConnected) return;
            if (!RakeMultiplayerPlugin.isServer) return;

            var packet = new CameraDestroyPacket
            {
                CameraId = _cameraId
            };

            var data = packet.Serialize();
            RakeNetworkManager.Instance?.ServerBroadcast(data);

            RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Sent CameraDestroy: {CameraId}");
        }

        /// <summary>
        /// Send quest trigger packet (camera break, bait taken, trap triggered)
        /// Uses Packets.cs QuestTriggerPacket (TriggerType byte, Position, CameraId)
        /// </summary>
        private void SendQuestTrigger(byte triggerType)
        {
            if (!RakeMultiplayerPlugin.isConnected) return;
            if (!RakeMultiplayerPlugin.isServer) return;

            var packet = new QuestTriggerPacket
            {
                TriggerType = triggerType,
                Position = transform.position,
                CameraId = _cameraId
            };

            var data = packet.Serialize();
            RakeNetworkManager.Instance?.ServerBroadcast(data);

            RakeMultiplayerPlugin.Log?.LogInfo($"[NetworkedCamera] Sent QuestTrigger: type={triggerType} for camera {_cameraId}");
        }

        #endregion

        #region Visual Updates

        /// <summary>
        /// Drive the game's RecordCamera_obj into its "setup" visual state
        /// (enables the camera child and registers it with CameraManager for the monitor view).
        /// Safe to call repeatedly; guarded against recursive patch execution.
        /// </summary>
        private void ApplySetupVisuals()
        {
            var recordCamera = GetComponent<RecordCamera_obj>();
            if (recordCamera == null) return;
            if (recordCamera.cameraState == RecordCamera_obj.Camera_state.Setup) return; // already set up

            bool prev = RakeMultiplayerPlugin.IsApplyingNetworkCameraState;
            RakeMultiplayerPlugin.IsApplyingNetworkCameraState = true;
            try
            {
                recordCamera.SetSetupState();
            }
            finally
            {
                RakeMultiplayerPlugin.IsApplyingNetworkCameraState = prev;
            }
        }

        /// <summary>
        /// Drive the game's RecordCamera_obj back into its "unsetup" visual state
        /// (removes it from CameraManager and disables the camera child).
        /// </summary>
        private void ApplyUnsetupVisuals()
        {
            var recordCamera = GetComponent<RecordCamera_obj>();
            if (recordCamera == null) return;
            if (recordCamera.cameraState == RecordCamera_obj.Camera_state.Unsetup) return; // already unsetup

            bool prev = RakeMultiplayerPlugin.IsApplyingNetworkCameraState;
            RakeMultiplayerPlugin.IsApplyingNetworkCameraState = true;
            try
            {
                recordCamera.SetUnsetupState();
            }
            finally
            {
                RakeMultiplayerPlugin.IsApplyingNetworkCameraState = prev;
            }
        }

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
    /// Camera sync action types (local enum for internal logic only, not serialized)
    /// </summary>
    public enum CameraSyncAction : byte
    {
        Spawn = 0,
        Update = 1,
        Destroy = 2,
        Break = 3
    }

    /// <summary>
    /// ReadOnly attribute for inspector (Unity doesn't have built-in ReadOnly)
    /// </summary>
    public class ReadOnlyAttribute : PropertyAttribute { }
}
