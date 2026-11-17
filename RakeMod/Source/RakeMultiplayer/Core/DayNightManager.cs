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

        [Tooltip("Let the mod drive ambient light and sun intensity. Disabled by default: the " +
                 "game levels ship their own lighting (night levels use a very dark flat " +
                 "ambient), and overriding it makes night look like day under a black sky.")]
        public bool ControlLighting = false;

        [Tooltip("Ambient multiplier during the day, relative to the scene ambient")]
        public float DayAmbientScale = 1f;
        [Tooltip("Ambient multiplier during the night, relative to the scene ambient")]
        public float NightAmbientScale = 0.25f;

        [Tooltip("Sun intensity multiplier during the day")]
        public float DaySunScale = 1f;
        [Tooltip("Sun intensity multiplier during the night")]
        public float NightSunScale = 0.2f;

        [Tooltip("Tint blended into the sun colour at night")]
        public Color NightColor = new Color(0.3f, 0.3f, 0.5f);

        // Baseline captured from the loaded scene; phase changes are relative to it.
        private bool _hasBaseline;
        private Color _baseAmbient = Color.white;
        private float _baseSunIntensity = 1f;
        private Color _baseSunColor = Color.white;
        private bool _baseSunEnabled = true;

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
            if (RakeMultiplayerPlugin.ControlLighting != null)
            {
                ControlLighting = RakeMultiplayerPlugin.ControlLighting.Value;
            }

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

            // Announce the initial phase: StartCycle used to skip this event, so
            // SessionManager stayed at night 0 until the first transition.
            OnPhaseChanged?.Invoke(_currentPhase, GetRemainingTime(), _nightNumber);

            // Set initial lighting
            ApplyLighting(_currentPhase, 1f);

            // Broadcast initial phase to all clients
            if (RakeMultiplayerPlugin.isServer)
            {
                BroadcastPhaseChange();
            }

            RakeMultiplayerPlugin.Log?.LogInfo($"[DayNightManager] Cycle started: {_currentPhase}, Night #{_nightNumber}");
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

            // Real time on purpose: one peer opening the pause menu used to freeze the
            // 300 s night for everybody, with no phase packet ever sent again.
            float dt = Time.unscaledDeltaTime;
            float before = _phaseTimer;
            _phaseTimer += dt;

            // Broadcast timer updates periodically (every second)
            if (Mathf.FloorToInt(_phaseTimer) != Mathf.FloorToInt(before))
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

            RakeMultiplayerPlugin.Log?.LogInfo($"[DayNightManager] Phase transition: {_currentPhase}, Night #{_nightNumber}");

            // Start lighting transition
            _lightTransitionTimer = 0f;

            // Broadcast to all clients
            BroadcastPhaseChange();

            // Server-side phase hooks (Tasks 4.5 / 4.6). They must never be able to cancel
            // the level change below: when the host drives the Rake there is no hunter body,
            // and a hook that expected one used to throw here, leaving the host in the old
            // level while every client had already been told the new phase (the client's
            // day "ended" and nothing happened on the host).
            if (RakeMultiplayerPlugin.isServer)
            {
                try
                {
                    if (_currentPhase == Phase.Night) ApplyPersistentRakeHealth();
                    else if (_currentPhase == Phase.Day) RespawnAtSafehouse();
                }
                catch (Exception ex)
                {
                    RakeMultiplayerPlugin.Log?.LogError("[DayNight] phase hook failed: " + ex);
                }
            }

            // Fire event for local listeners (SessionManager, etc.). Same rule: a listener
            // that throws must not leave the host and the clients in different phases.
            try
            {
                OnPhaseChanged?.Invoke(_currentPhase, GetRemainingTime(), _nightNumber);
            }
            catch (Exception ex)
            {
                RakeMultiplayerPlugin.Log?.LogError("[DayNight] phase listener failed: " + ex);
            }

            // Day and night are separate levels in this game, so the phase has to load
            // its scene. Without this the timer expired, the sky stayed as it was and the
            // HUD claimed a phase the level did not show.
            LoadLevelForPhase(_currentPhase);
        }

        /// <summary>Dev bridge: force the next phase without waiting for the timer.</summary>
        public void SkipPhase()
        {
            if (!RakeMultiplayerPlugin.isServer) return;
            TransitionPhase();
        }

        /// <summary>Server: loads the level that matches the new phase.</summary>
        private void LoadLevelForPhase(Phase phase)
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            string current = Application.loadedLevelName;
            string target = PhaseSceneFor(current, phase);
            if (string.IsNullOrEmpty(target) || target == current) return;

            RakeMultiplayerPlugin.Log?.LogInfo("[DayNight] phase " + phase + " -> loading '" +
                                               target + "' (was '" + current + "')");

            UI.LoadingScreen.Show("THE HUNT CONTINUES",
                                  phase == Phase.Night ? "NIGHT FALLS" : "DAWN BREAKS",
                                  "The forest is changing", false);

            Application.LoadLevel(target);

            // The load is synchronous, so nothing else closes the overlay: without this
            // the screen stayed at 90% over the new level and swallowed the input.
            UI.LoadingScreen.Hide();
        }

        /// <summary>
        /// True when a level belongs to the given phase. Levels without a "(Day)"/"(Night)"
        /// suffix return true, so a mode that ships one level keeps the old behaviour.
        /// </summary>
        public static bool LevelMatchesPhase(Phase phase, string scene)
        {
            if (string.IsNullOrEmpty(scene)) return true;
            if (Contains(scene, "(Day)")) return phase == Phase.Day;
            if (Contains(scene, "(Night)")) return phase == Phase.Night;
            return true;
        }

        /// <summary>
        /// Level_1(Day) &lt;-&gt; Level_1(Night). Returns null for scenes without a phase
        /// suffix, so modes that do not ship a night variant stay untouched.
        /// </summary>
        public static string PhaseSceneFor(string scene, Phase phase)
        {
            if (string.IsNullOrEmpty(scene)) return null;

            if (Contains(scene, "(Day)"))
            {
                return phase == Phase.Night ? ReplaceNoCase(scene, "(Day)", "(Night)") : scene;
            }

            if (Contains(scene, "(Night)"))
            {
                return phase == Phase.Day ? ReplaceNoCase(scene, "(Night)", "(Day)") : scene;
            }

            return null;
        }

        private static bool Contains(string text, string token)
        {
            return text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ReplaceNoCase(string text, string token, string replacement)
        {
            int index = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return text;
            return text.Substring(0, index) + replacement + text.Substring(index + token.Length);
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

            // The server owns the timer, but the client still has to treat the cycle as
            // running: the HUD reads IsRunning for the countdown and the phase label.
            _initialized = true;
            _isRunning = true;

            // Start lighting transition if phase actually changed
            if (wasNight != isNight)
            {
                _lightTransitionTimer = 0f;
            }

            // Apply lighting immediately (will interpolate in LateUpdate) - but only when the
            // level on this machine belongs to that phase. A client whose level is still the
            // day level used to go dark the moment the "night" packet arrived, which reads as
            // "the client skipped the day" while the host is still standing in daylight.
            if (LevelMatchesPhase(_currentPhase, Application.loadedLevelName))
            {
                ApplyLighting(_currentPhase, _lightTransitionTimer / LightTransitionDuration);
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogWarning(
                    "[DayNight] client: phase " + _currentPhase + " announced while level is '" +
                    Application.loadedLevelName + "' - waiting for the level change");
            }

            // Fire event for local listeners
            OnPhaseChanged?.Invoke(_currentPhase, packet.RemainingTime, _nightNumber);

            RakeMultiplayerPlugin.Log?.LogInfo($"[DayNightManager] Client received phase: {_currentPhase}, Remaining: {packet.RemainingTime:F1}s, Night: {_nightNumber + 1}");
        }

        private void LateUpdate()
        {
            if (!ControlLighting) return;

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
        /// Apply phase lighting relative to the scene baseline. Does nothing unless
        /// <see cref="ControlLighting"/> is enabled, so the level keeps its authored look.
        /// </summary>
        private void ApplyLighting(Phase targetPhase, float transitionProgress)
        {
            if (!ControlLighting) return;
            if (!_hasBaseline) CaptureBaseline();
            if (DirectionalLight == null) return;

            // 0 = fully night, 1 = fully day
            float dayFactor = (targetPhase == Phase.Day) ? transitionProgress : 1f - transitionProgress;

            RenderSettings.ambientLight = _baseAmbient * Mathf.Lerp(NightAmbientScale, DayAmbientScale, dayFactor);

            DirectionalLight.enabled = _baseSunEnabled;
            DirectionalLight.intensity = _baseSunIntensity * Mathf.Lerp(NightSunScale, DaySunScale, dayFactor);
            DirectionalLight.color = Color.Lerp(Color.Lerp(_baseSunColor, NightColor, 0.5f), _baseSunColor, dayFactor);

            if (AnimateFog && RenderSettings.fog)
            {
                float targetFog = (targetPhase == Phase.Day) ? DayFogDensity : NightFogDensity;
                float startFog = (targetPhase == Phase.Day) ? NightFogDensity : DayFogDensity;
                RenderSettings.fogDensity = Mathf.Lerp(startFog, targetFog, transitionProgress);
            }
        }

        /// <summary>Remember the lighting the loaded scene was authored with.</summary>
        private void CaptureBaseline()
        {
            _baseAmbient = RenderSettings.ambientLight;

            if (DirectionalLight != null)
            {
                _baseSunIntensity = DirectionalLight.intensity;
                _baseSunColor = DirectionalLight.color;
                _baseSunEnabled = DirectionalLight.enabled;
            }

            _hasBaseline = true;
        }

        /// <summary>Put the scene lighting back exactly as it was authored.</summary>
        private void RestoreBaseline()
        {
            if (!_hasBaseline) return;

            RenderSettings.ambientLight = _baseAmbient;

            if (DirectionalLight != null)
            {
                DirectionalLight.intensity = _baseSunIntensity;
                DirectionalLight.color = _baseSunColor;
                DirectionalLight.enabled = _baseSunEnabled;
            }
        }

        /// <summary>
        /// Re-acquire the scene light, remember the authored lighting and (only when
        /// <see cref="ControlLighting"/> is on) force the current phase lighting.
        /// Called on every gameplay scene load.
        /// </summary>
        public void RefreshSceneLighting()
        {
            DirectionalLight = null;
            var lights = FindObjectsOfType<Light>();
            foreach (var l in lights)
            {
                if (l != null && l.type == LightType.Directional)
                {
                    DirectionalLight = l;
                    break;
                }
            }

            CaptureBaseline();

            if (ControlLighting)
            {
                ApplyLighting(_currentPhase, 1f);
            }
            else
            {
                RestoreBaseline();
            }

            _lightTransitionTimer = LightTransitionDuration; // no interpolation on load

            RakeMultiplayerPlugin.Log?.LogInfo(
                $"[DayNight] Scene lighting refreshed: phase={_currentPhase}, control={ControlLighting}, " +
                $"light={(DirectionalLight != null ? DirectionalLight.name : "null")}, ambient={RenderSettings.ambientLight}");
        }

        // Public getters
        public Phase CurrentPhase => _currentPhase;
        public float PhaseTimer => _phaseTimer;
        public int NightNumber => _nightNumber;
        public bool IsRunning => _isRunning;

        /// <summary>Server: send the current phase to one client (late join).</summary>
        public void SendPhaseTo(int playerId)
        {
            if (!RakeMultiplayerPlugin.isServer || !_isRunning) return;
            var packet = new PhaseChangePacket
            {
                Phase = _currentPhase,
                RemainingTime = GetRemainingTime(),
                NightNumber = _nightNumber
            };
            RakeNetworkManager.Instance?.ServerSendToPlayer(playerId, packet.Serialize());
        }
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