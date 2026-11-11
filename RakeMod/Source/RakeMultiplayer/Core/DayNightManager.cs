using System;
using UnityEngine;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    /// <summary>
    /// Manages the Day/Night cycle for the multiplayer session.
    /// Server-authoritative: only the host runs the timer and broadcasts PhaseChangePacket.
    /// Clients receive PhaseChangePacket and apply visual changes locally.
    /// </summary>
    public class DayNightManager : MonoBehaviour
    {
        public static DayNightManager Instance { get; private set; }

        [Header("Phase Configuration")]
        [Tooltip("Duration of day phase in seconds")]
        public float DayDuration = 300f; // 5 minutes default
        [Tooltip("Duration of night phase in seconds")]
        public float NightDuration = 300f; // 5 minutes default

        [Header("Lighting")]
        [Tooltip("Directional light to animate (sun/moon)")]
        public Light DirectionalLight;
        [Tooltip("Day light intensity")]
        public float DayIntensity = 1.0f;
        [Tooltip("Night light intensity")]
        public float NightIntensity = 0.15f;
        [Tooltip("Day light color")]
        public Color DayColor = Color.white;
        [Tooltip("Night light color")]
        public Color NightColor = new Color(0.3f, 0.3f, 0.5f);

        [Header("Fog")]
        [Tooltip("Enable fog animation")]
        public bool AnimateFog = true;
        [Tooltip("Day fog density")]
        public float DayFogDensity = 0.005f;
        [Tooltip("Night fog density")]
        public float NightFogDensity = 0.02f;

        // Current state
        private Phase _currentPhase = Phase.Day;
        private float _phaseTimer = 0f;
        private int _nightNumber = 0;
        private bool _isRunning = false;
        private bool _initialized = false;

        // Lighting interpolation
        private float _lightTransitionTimer = 0f;
        private const float LightTransitionDuration = 10f; // seconds

        // Events
        public event Action<Phase, float, int> OnPhaseChanged; // phase, remainingTime, nightNumber

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Initialize();
        }

        /// <summary>
        /// Initialize the DayNightManager - find directional light, register with network manager
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;

            // Find directional light if not assigned
            if (DirectionalLight == null)
            {
                DirectionalLight = FindObjectOfType<Light>();
                if (DirectionalLight != null && DirectionalLight.type != LightType.Directional)
                {
                    // Try to find specifically directional
                    var lights = FindObjectsOfType<Light>();
                    foreach (var l in lights)
                    {
                        if (l.type == LightType.Directional)
                        {
                            DirectionalLight = l;
                            break;
                        }
                    }
                }
            }

            // Subscribe to network packets
            if (RakeNetworkManager.Instance != null)
            {
                // Network manager will call HandlePhaseChangePacket when received
            }

            _initialized = true;
            RakeMultiplayerPlugin.Log?.LogInfo("[DayNightManager] Initialized");
        }

        /// <summary>
        /// Start the day/night cycle (called by SessionManager when session begins)
        /// </summary>
        public void StartCycle(bool isNight = false, int startingNight = 0)
        {
            _currentPhase = isNight ? Phase.Night : Phase.Day;
            _nightNumber = startingNight;
            _phaseTimer = 0f;
            _isRunning = true;

            // Set initial lighting
            ApplyLighting(_currentPhase, 1f);

            // Broadcast initial phase to all clients
            if (RakeMultiplayerPlugin.isServer)
            {
                BroadcastPhaseChange();
            }

            RakeMultiplayerPlugin.Log?.LogInfo($"[DayNightManager] Cycle started: {_currentPhase}, Night #{_nightNumber + 1}");
        }

        /// <summary>
        /// Stop the cycle (called when session ends)
        /// </summary>
        public void StopCycle()
        {
            _isRunning = false;
            RakeMultiplayerPlugin.Log?.LogInfo("[DayNightManager] Cycle stopped");
        }

        private void Update()
        {
            if (!_isRunning || !_initialized) return;

            // Only server runs the timer
            if (!RakeMultiplayerPlugin.isServer) return;

            float phaseDuration = (_currentPhase == Phase.Day) ? DayDuration : NightDuration;
            _phaseTimer += Time.deltaTime;

            // Broadcast timer updates periodically (every second)
            if (Mathf.FloorToInt(_phaseTimer) != Mathf.FloorToInt(_phaseTimer - Time.deltaTime))
            {
                BroadcastPhaseChange();
            }

            // Check for phase transition
            if (_phaseTimer >= phaseDuration)
            {
                TransitionPhase();
            }
        }

        /// <summary>
        /// Transition to the next phase (Day -> Night or Night -> Day)
        /// </summary>
        private void TransitionPhase()
        {
            _currentPhase = (_currentPhase == Phase.Day) ? Phase.Night : Phase.Day;

            if (_currentPhase == Phase.Night)
            {
                _nightNumber++;
            }

            _phaseTimer = 0f;

            RakeMultiplayerPlugin.Log?.LogInfo($"[DayNightManager] Phase transition: {_currentPhase}, Night #{_nightNumber + 1}");

            // Start lighting transition
            _lightTransitionTimer = 0f;

            // Broadcast to all clients
            BroadcastPhaseChange();

            // Server-side phase hooks (Tasks 4.5 / 4.6)
            if (RakeMultiplayerPlugin.isServer)
            {
                if (_currentPhase == Phase.Night)
                {
                    ApplyPersistentRakeHealth();
                }
                else if (_currentPhase == Phase.Day)
                {
                    RespawnAtSafehouse();
                }
            }

            // Fire event for local listeners (SessionManager, etc.)
            OnPhaseChanged?.Invoke(_currentPhase, GetRemainingTime(), _nightNumber);
        }

        /// <summary>
        /// Night start: keep the Rake's health across nights (Task 4.5).
        /// Health is only reset when a brand-new session starts.
        /// </summary>
        private void ApplyPersistentRakeHealth()
        {
            if (SessionManager.Instance == null) return;

            float health = SessionManager.Instance.RakeHealth;
            if (health <= 0f) return;

            var rake = UnityEngine.Object.FindObjectOfType<NetworkedRakeAI>();
            if (rake != null)
            {
                rake.ApplyHealth(health);
                RakeMultiplayerPlugin.Log?.LogInfo($"[4.5] Restored Rake health for night {_nightNumber + 1}: {health:F1}");
            }
        }

        /// <summary>
        /// Day start: teleport all hunters back to the safehouse (Task 4.6).
        /// </summary>
        private void RespawnAtSafehouse()
        {
            var settings = MapSettings.Current;
            Vector3 target;
            Quaternion rotation = Quaternion.identity;

            if (settings != null && settings.hasSafehouse)
            {
                target = settings.safehousePosition;
            }
            else if (settings != null && settings.spawnPoints != null && settings.spawnPoints.Length > 0)
            {
                target = settings.spawnPoints[0];
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[4.6] No safehouse or spawn point configured - skipping respawn");
                return;
            }

            RakeNetworkManager.Instance?.ServerTeleportAllPlayers(target, rotation);
            RakeMultiplayerPlugin.Log?.LogInfo($"[4.6] Day phase: hunters respawned at {target}");
        }

        /// <summary>
        /// Broadcast current phase state to all clients via PhaseChangePacket
        /// </summary>
        private void BroadcastPhaseChange()
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            var packet = new PhaseChangePacket
            {
                Phase = _currentPhase,
                RemainingTime = GetRemainingTime(),
                NightNumber = _nightNumber
            };

            RakeNetworkManager.Instance?.ServerBroadcast(packet.Serialize());
        }

        /// <summary>
        /// Get remaining time in current phase
        /// </summary>
        public float GetRemainingTime()
        {
            float phaseDuration = (_currentPhase == Phase.Day) ? DayDuration : NightDuration;
            return Mathf.Max(0f, phaseDuration - _phaseTimer);
        }

        /// <summary>
        /// Handle incoming PhaseChangePacket from server (client-side)
        /// </summary>
        public void HandlePhaseChangePacket(PhaseChangePacket packet)
        {
            bool wasNight = _currentPhase == Phase.Night;
            bool isNight = packet.Phase == Phase.Night;

            _currentPhase = packet.Phase;
            _phaseTimer = (_currentPhase == Phase.Day ? DayDuration : NightDuration) - packet.RemainingTime;
            _nightNumber = packet.NightNumber;

            // Start lighting transition if phase actually changed
            if (wasNight != isNight)
            {
                _lightTransitionTimer = 0f;
            }

            // Apply lighting immediately (will interpolate in LateUpdate)
            ApplyLighting(_currentPhase, _lightTransitionTimer / LightTransitionDuration);

            // Fire event for local listeners
            OnPhaseChanged?.Invoke(_currentPhase, packet.RemainingTime, _nightNumber);

            RakeMultiplayerPlugin.Log?.LogInfo($"[DayNightManager] Client received phase: {_currentPhase}, Remaining: {packet.RemainingTime:F1}s, Night: {_nightNumber + 1}");
        }

        private void LateUpdate()
        {
            // Smooth lighting transition
            if (_lightTransitionTimer < LightTransitionDuration)
            {
                _lightTransitionTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_lightTransitionTimer / LightTransitionDuration);
                // Smooth step for more natural transition
                t = t * t * (3f - 2f * t);
                ApplyLighting(_currentPhase, t);
            }
        }

        /// <summary>
        /// Apply lighting based on current phase and transition progress
        /// </summary>
        private void ApplyLighting(Phase targetPhase, float transitionProgress)
        {
            // Level reloads destroy the old sun; re-acquire it
            if (DirectionalLight == null)
            {
                var lights = FindObjectsOfType<Light>();
                foreach (var l in lights)
                {
                    if (l != null && l.type == LightType.Directional) { DirectionalLight = l; break; }
                }
            }

            if (DirectionalLight == null) return;

            // Interpolate intensity
            float targetIntensity = (targetPhase == Phase.Day) ? DayIntensity : NightIntensity;
            float startIntensity = (targetPhase == Phase.Day) ? NightIntensity : DayIntensity;
            DirectionalLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, transitionProgress);

            // Interpolate color
            Color targetColor = (targetPhase == Phase.Day) ? DayColor : NightColor;
            Color startColor = (targetPhase == Phase.Day) ? NightColor : DayColor;
            DirectionalLight.color = Color.Lerp(startColor, targetColor, transitionProgress);

            // Interpolate fog
            if (AnimateFog && RenderSettings.fog)
            {
                float targetFog = (targetPhase == Phase.Day) ? DayFogDensity : NightFogDensity;
                float startFog = (targetPhase == Phase.Day) ? NightFogDensity : DayFogDensity;
                RenderSettings.fogDensity = Mathf.Lerp(startFog, targetFog, transitionProgress);
            }
        }

        // Public getters
        public Phase CurrentPhase => _currentPhase;
        public float PhaseTimer => _phaseTimer;
        public int NightNumber => _nightNumber;
        public bool IsRunning => _isRunning;
        public float DayDurationConfig => DayDuration;
        public float NightDurationConfig => NightDuration;

        /// <summary>
        /// Force set phase (for testing or session restore)
        /// </summary>
        public void ForcePhase(Phase phase, int nightNumber, float remainingTime)
        {
            _currentPhase = phase;
            _nightNumber = nightNumber;
            float phaseDuration = (phase == Phase.Day) ? DayDuration : NightDuration;
            _phaseTimer = phaseDuration - remainingTime;

            if (RakeMultiplayerPlugin.isServer)
            {
                BroadcastPhaseChange();
            }
            else
            {
                ApplyLighting(phase, 1f);
            }

            OnPhaseChanged?.Invoke(_currentPhase, remainingTime, _nightNumber);
        }
    }
}