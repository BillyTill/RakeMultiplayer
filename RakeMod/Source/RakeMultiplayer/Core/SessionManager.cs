using System;
using UnityEngine;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    /// <summary>
    /// Manages the 3-night session lifecycle, victory conditions, and game over logic.
    /// Server-authoritative: only the host runs the session state machine.
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }

        [Header("Session Configuration")]
        [Tooltip("Total number of nights to survive (default 3)")]
        public int TotalNights = 3;
        [Tooltip("Maximum session duration in minutes (0 = no limit)")]
        public float MaxSessionMinutes = 0f;

        [Header("Victory Conditions")]
        [Tooltip("Rake health threshold for hunter victory (0 = must kill Rake)")]
        public float RakeDefeatHealthThreshold = 0f;

        // Session state
        private SessionState _currentState = SessionState.Waiting;
        private int _currentNight = 0;
        private float _sessionTimer = 0f;
        private float _rakeHealth = 100f;
        private VictoryType _victoryType = VictoryType.None;
        private float _healthPollTimer;
        private bool _isRunning = false;

        // References
        private DayNightManager _dayNightManager;
        private NetworkedRakeAI _networkedRake;

        // Events
        public event Action<SessionState, int, float, VictoryType> OnSessionStateChanged; // state, night, rakeHealth, victoryType
        public event Action<int> OnNightStarted; // nightNumber (1-based)
        public event Action<int> OnNightCompleted; // nightNumber (1-based)
        public event Action<VictoryType> OnGameOver; // victoryType

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
            _dayNightManager = DayNightManager.Instance;
            if (_dayNightManager != null)
            {
                _dayNightManager.OnPhaseChanged += OnDayNightPhaseChanged;
            }
        }

        /// <summary>
        /// Start a new session (called by host when game begins)
        /// </summary>
        public void SendStateTo(int playerId)
        {
            if (!RakeMultiplayerPlugin.isServer) return;
            var packet = new SessionStatePacket
            {
                CurrentNight = _currentNight,
                State = _currentState,
                RakeHealth = _rakeHealth,
                VictoryType = _victoryType
            };
            RakeNetworkManager.Instance?.ServerSendToPlayer(playerId, packet.Serialize());
        }

        public void StartSession()
        {
            if (_isRunning)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[SessionManager] Session already running");
                return;
            }

            // StartSession may run before SessionManager.Start() (scene load order).
            if (_dayNightManager == null)
            {
                _dayNightManager = DayNightManager.Instance;
                if (_dayNightManager != null)
                {
                    _dayNightManager.OnPhaseChanged -= OnDayNightPhaseChanged;
                    _dayNightManager.OnPhaseChanged += OnDayNightPhaseChanged;
                }
            }

            _currentState = SessionState.Active;
            _currentNight = 0;
            WorldPersistence.Clear();
            _sessionTimer = 0f;
            _rakeHealth = 100f;
            _victoryType = VictoryType.None;
            _isRunning = true;

            // Find NetworkedRakeAI
            _networkedRake = FindObjectOfType<NetworkedRakeAI>();

            // Start first night
            if (_dayNightManager != null)
            {
                _dayNightManager.StartCycle(isNight: true, startingNight: 1);
            }

            BroadcastSessionState();
            RakeMultiplayerPlugin.Log?.LogInfo("[SessionManager] Session started - Night 1 begins");
        }

        /// <summary>
        /// End the current session with a victory type
        /// </summary>
        public void EndSession(VictoryType victoryType)
        {
            if (!_isRunning) return;

            _isRunning = false;
            _currentState = SessionState.GameOver;
            _victoryType = victoryType;

            // Stop day/night cycle
            if (_dayNightManager != null)
            {
                _dayNightManager.StopCycle();
            }

            BroadcastSessionState();
            OnGameOver?.Invoke(victoryType);

            RakeMultiplayerPlugin.Log?.LogInfo($"[SessionManager] Session ended: {victoryType}");

            // TODO: Trigger GameOver UI, show results, return to lobby
        }

        /// <summary>
        /// Update Rake health (called when DamageApplied is received)
        /// </summary>
        public void UpdateRakeHealth(float health)
        {
            float previous = _rakeHealth;
            _rakeHealth = health;

            // Check for Rake defeat
            if (_rakeHealth <= RakeDefeatHealthThreshold && _isRunning)
            {
                EndSession(VictoryType.HuntersWin);
                return;
            }

            // Do not spam a SessionState packet every frame
            if (Mathf.Abs(previous - health) > 0.25f)
            {
                BroadcastSessionState();
            }
        }

        /// <summary>
        /// Handle hunter death - check if all hunters are dead
        /// </summary>
        public void OnHunterDied(int playerId)
        {
            if (!_isRunning) return;

            // Check if all hunters are dead (only host can check this reliably)
            if (RakeMultiplayerPlugin.isServer)
            {
                int aliveHunters = CountAliveHunters();
                if (aliveHunters == 0)
                {
                    EndSession(VictoryType.RakeWin);
                }
            }
        }

        private int CountAliveHunters()
        {
            if (PlayerSpawner.Instance == null) return 0;

            int count = 0;
            // Count local player if exists
            if (PlayerSpawner.Instance.LocalPlayer != null)
            {
                count++;
            }

            // Count remote players
            foreach (var kvp in PlayerSpawner.Instance.GetAllRemotePlayers())
            {
                if (kvp.Value != null) count++;
            }

            return count;
        }

        private void Update()
        {
            if (!_isRunning || !RakeMultiplayerPlugin.isServer) return;

            // Session timer
            if (MaxSessionMinutes > 0)
            {
                _sessionTimer += Time.deltaTime;
                if (_sessionTimer >= MaxSessionMinutes * 60f)
                {
                    EndSession(VictoryType.Timeout);
                }
            }

            // Poll health at 4 Hz (not every frame) and re-acquire after level reloads
            _healthPollTimer += Time.deltaTime;
            if (_healthPollTimer < 0.25f) return;
            _healthPollTimer = 0f;

            if (_networkedRake == null)
            {
                _networkedRake = FindObjectOfType<NetworkedRakeAI>();
            }

            // Update Rake health from NetworkedRakeAI if available
            if (_networkedRake != null)
            {
                var enemyAI = _networkedRake.GetComponent<Enemy_AI>();
                if (enemyAI != null)
                {
                    var healthField = typeof(Enemy_AI).GetField("Health", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (healthField != null)
                    {
                        float health = Convert.ToSingle(healthField.GetValue(enemyAI));
                        UpdateRakeHealth(health);
                    }
                }
            }
        }

        private void OnDayNightPhaseChanged(Phase phase, float remainingTime, int nightNumber)
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            // Only react to night start (Day -> Night transition)
            if (phase == Phase.Night)
            {
                // DayNightManager already counts nights from 1 (it increments before
                // invoking the event), so the old "+1" skipped the first night.
                _currentNight = nightNumber;

                if (_currentNight == 1)
                {
                    // First night just started
                    OnNightStarted?.Invoke(_currentNight);
                    RakeMultiplayerPlugin.Log?.LogInfo($"[SessionManager] Night {_currentNight} started");
                }
                else if (_currentNight <= TotalNights)
                {
                    // Subsequent nights
                    OnNightStarted?.Invoke(_currentNight);
                    RakeMultiplayerPlugin.Log?.LogInfo($"[SessionManager] Night {_currentNight} started");
                }
            }
            // Day phase - check if night completed
            else if (phase == Phase.Day && _currentNight > 0)
            {
                // Night completed
                OnNightCompleted?.Invoke(_currentNight);
                RakeMultiplayerPlugin.Log?.LogInfo($"[SessionManager] Night {_currentNight} completed");

                // Check victory: survived all nights
                if (_currentNight >= TotalNights)
                {
                    EndSession(VictoryType.HuntersWin);
                }
            }

            BroadcastSessionState();
        }

        private void BroadcastSessionState()
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            var packet = new SessionStatePacket
            {
                CurrentNight = _currentNight,
                State = _currentState,
                RakeHealth = _rakeHealth,
                VictoryType = _victoryType
            };

            RakeNetworkManager.Instance?.ServerBroadcast(packet.Serialize());
            OnSessionStateChanged?.Invoke(_currentState, _currentNight, _rakeHealth, _victoryType);
        }

        /// <summary>
        /// Handle incoming SessionStatePacket from server (client-side)
        /// </summary>
        public void HandleSessionStatePacket(SessionStatePacket packet)
        {
            _currentState = packet.State;
            _currentNight = packet.CurrentNight;
            _rakeHealth = packet.RakeHealth;
            _victoryType = packet.VictoryType;
            _isRunning = (packet.State == SessionState.Active);

            OnSessionStateChanged?.Invoke(_currentState, _currentNight, _rakeHealth, _victoryType);

            if (packet.State == SessionState.GameOver)
            {
                OnGameOver?.Invoke(packet.VictoryType);
            }

            RakeMultiplayerPlugin.Log?.LogInfo($"[SessionManager] Client received session state: {packet.State}, Night {packet.CurrentNight}, Rake HP: {packet.RakeHealth}, Victory: {packet.VictoryType}");
        }

        // Public getters
        public SessionState CurrentState => _currentState;
        public int CurrentNight => _currentNight;
        public float SessionTimer => _sessionTimer;
        public float RakeHealth => _rakeHealth;
        public VictoryType VictoryType => _victoryType;
        public bool IsRunning => _isRunning;
        public int TotalNightsConfig => TotalNights;

        /// <summary>
        /// Force set session state (for testing or session restore)
        /// </summary>
        public void ForceState(SessionState state, int night, float rakeHealth, VictoryType victoryType)
        {
            _currentState = state;
            _currentNight = night;
            _rakeHealth = rakeHealth;
            _victoryType = victoryType;
            _isRunning = (state == SessionState.Active);

            if (RakeMultiplayerPlugin.isServer)
            {
                BroadcastSessionState();
            }

            OnSessionStateChanged?.Invoke(_currentState, _currentNight, _rakeHealth, _victoryType);
        }
    }
}