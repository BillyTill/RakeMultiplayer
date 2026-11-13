using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    /// <summary>
    /// Networked Rake AI - server-authoritative Rake synchronization
    /// Implements 5-phase AI behavior: Follow, Stealth, Chase, Blocking, FeignedRetreat
    /// Uses RakeStatePacket protocol for 15Hz state sync
    /// </summary>
    public class NetworkedRakeAI : MonoBehaviour
    {
        [Header("Sync Settings")]
        public float syncRate = 0.066f; // 15Hz unified rate

        [Header("Client Interpolation")]
        [Tooltip("How quickly a client catches up to the last snapshot (higher = snappier)")]
        public float interpolationSpeed = 12f;

        [Tooltip("Distance beyond which the client teleports instead of interpolating")]
        public float snapDistance = 10f;

        [Header("AI Configuration")]
        public float followMinDistance = 15f;
        public float followMaxDistance = 25f;
        public float stealthTransitionDistance = 20f;
        public float stealthLookConeAngle = 30f;
        public float stealthLookRange = 20f;
        public float blockingDurationMin = 2f;
        public float blockingDurationMax = 3f;
        public float blockingFrontalAngle = 45f;
        public float blockingDamageReduction = 0.8f; // 80% reduction
        public float feignedRetreatHealthThreshold = 0.3f; // 30%
        public float feignedRetreatNoTargetTime = 3f;
        public float feignedRetreatWaitTime = 5f;
        public float feignedRetreatTeleportOffsetMin = 5f;
        public float feignedRetreatTeleportOffsetMax = 10f;
        public float feignedRetreatCooldown = 60f;
        public float attackRange = 3f;
        public float chaseVocalizationInterval = 3f;

        [Header("Stalk Cycle (pack 2)")]
        [Tooltip("The Rake walks to this distance from the hunter and watches.")]
        public float lurkStandoffDistance = 60f;

        [Tooltip("How long it stands still and watches before repositioning.")]
        public float lurkWatchDuration = 20f;

        [Tooltip("Pause between two lurk cycles.")]
        public float lurkIntervalMin = 10f;
        public float lurkIntervalMax = 22f;

        [Tooltip("Below this distance the Rake starts the crouched approach.")]
        public float sneakStartDistance = 30f;

        [Tooltip("Below this distance the crouched approach becomes a charge.")]
        public float chargeStartDistance = 20f;

        [Tooltip("Fleeing ends when the hunter is farther than this.")]
        public float fleeStopDistance = 100f;

        [Tooltip("How far from the hunter the flee destination is placed.")]
        public float fleeDestinationDistance = 130f;

        [Tooltip("The hunter must aim this close to the Rake's centre to count as seeing it.")]
        public float seenConeAngle = 10f;

        [Tooltip("Maximum distance at which a hunter can notice the Rake.")]
        public float seenRange = 120f;

        [Tooltip("A gunshot within this range can start a lurk cycle.")]
        public float noiseHearRange = 300f;

        [Range(0f, 1f)]
        [Tooltip("Chance the Rake uses the crouched (frozen) pose while watching.")]
        public float lurkCrouchChance = 0.7f;

        private Enemy_AI enemyAI;
        private Animator animator;
        private AudioSource audioSource;

        // Sync timer
        private float sendTimer;

        // Last sent values
        private Vector3 lastSentPosition;
        private byte lastSentState;
        private float stunDuration = 3f;
        private float lastSentHealth;

        // Interpolation targets for clients
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private byte targetState;
        private float targetHealth;
        private bool hasTargetPosition = false;
        private bool hasTargetRotation = false;

        // Client-side state tracking for animator triggers
        private byte lastAppliedState = 255;

        // Smoothed animator speed (clients only) so state flapping does not jerk the walk blend
        private float targetSpeed;

        // Animator snapshot received from the host (clients)
        private byte targetAnimFlags;
        private byte targetWalkType = 1;
        private float targetAnimSpeed;
        private bool hasAnimState;

        // Last animator snapshot sent (server)
        private byte lastSentAnimFlags;
        private byte lastSentWalkType = 1;
        private float lastSentAnimSpeed;

        // Client-side audio mirror (the host plays the vanilla sounds)
        private float _clientStepTimer;
        private float _clientVocalTimer;
        private Vector3 _clientAudioPosition;
        private bool _clientAudioValid;
        private RakeState _clientAudioState = RakeState.Idle;

        // Server-authoritative Rake hit
        private const float AttackInterval = 1.4f;
        private const int AttackDamage = 10;
        private const string AttackDamageType = "RakeAttack";
        private float _attackCooldown;

        // AI State Machine
        public enum RakeState : byte
        {
            Idle = 0,
            Patrol = 1,
            Chase = 2,
            Attack = 3,
            Search = 4,
            Stunned = 5,
            Dead = 6,
            Follow = 7,
            Stealth = 8,
            Blocking = 9,
            FeignedRetreat = 10,
            Lurk = 11,
            Flee = 12
        }

        // Current state tracking
        private RakeState currentState = RakeState.Idle;
        private RakeState previousState = RakeState.Idle;

        // State-specific data
        private Transform currentTarget;
        private float stateTimer = 0f;
        private float chaseVocalizationTimer = 0f;
        private float noTargetTimer = 0f;
        private float feignedRetreatCooldownTimer = 0f;
        private Transform caveTransform;
        private bool isBlocking = false;
        private float blockingEndTime = 0f;
        private Vector3 lastDamageDirection;
        private float lastDamageTime = 0f;

        // Pack 2: lurk / sneak / charge / flee cycle
        private Vector3 _lurkPoint;
        private bool _lurkPointValid;
        private bool _lurkArrived;
        private bool _lurkCrouch;
        private float _lurkWatchTimer;
        private float _lurkCooldownTimer;
        private float _lurkFreezeTimer;
        private float _repathTimer = -999f;
        private Vector3 _lastDestination;
        private float _seenTimer;
        private bool _seenCached;
        private float _noiseCooldown;
        private float _fleeRepathTimer;

        private float _clientLurkFreezeTimer;


        // Hunter tracking
        private List<Transform> hunters = new List<Transform>();
        private Transform nearestHunter;
        private Transform isolatedHunter;

        // Cave transform refresh
        private float caveRefreshTimer = 0f;
        private const float CaveRefreshInterval = 30f; // Refresh cave transform every 30 seconds

        // Constants for magic numbers
        private const float PositionDeltaThreshold = 0.05f;
        private const float DamageTimeWindow = 0.5f;
        private const float NavMeshSampleRadius = 5f;
        private const float HealthDeltaThreshold = 1f;

        // Reflection cache for Enemy_AI
        private System.Reflection.FieldInfo enemyHealthField;
        private System.Reflection.FieldInfo enemyMaxHealthField;
        private System.Reflection.FieldInfo enemyAgentField;
        private System.Reflection.FieldInfo enemyChaseVocalsField;
        private System.Reflection.MethodInfo enemyAttackMethod;
        private System.Reflection.MethodInfo enemyTakeDamageMethod;

        // Cached original agent speed for restoration
        private float originalAgentSpeed = 5f;

        private bool _initialized = false;
        private bool _playerControlled;
        private bool _localOwner;
        private bool _remoteOwner;
        private bool _daySpawnChecked;
        private float _lastDayWarp;
        private bool _loggedHealthWarning = false;
        private bool _loggedMaxHealthWarning = false;

        private void Awake()
        {
            enemyAI = GetComponent<Enemy_AI>();
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            audioSource = GetComponent<AudioSource>();

            lastSentPosition = transform.position;
            targetPosition = transform.position;
            targetRotation = transform.rotation;

            // Cache reflection for Enemy_AI
            CacheEnemyAIReflection();

            // Find cave transform
            FindCaveTransform();
        }

        private void CacheEnemyAIReflection()
        {
            if (enemyAI == null) return;
            var type = typeof(Enemy_AI);
            var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            enemyHealthField = type.GetField("Health", flags);
            enemyMaxHealthField = type.GetField("MaxHealth", flags);
            // Enemy_AI exposes the NavMeshAgent as "nav"; keep the old name as a fallback.
            enemyAgentField = type.GetField("nav", flags) ?? type.GetField("agent", flags);
            enemyChaseVocalsField = type.GetField("chaseVocals", flags);
            enemyAttackMethod = type.GetMethod("Attack", flags);
            enemyTakeDamageMethod = type.GetMethod("TakeDamage", flags);

            // Cache original agent speed for restoration after silent movement
            if (enemyAgentField != null)
            {
                var agent = enemyAgentField.GetValue(enemyAI) as NavMeshAgent;
                if (agent != null && agent.speed > 0.1f)
                {
                    originalAgentSpeed = agent.speed;
                }
                else if (enemyAI != null && enemyAI.runSpeed > 0.1f)
                {
                    originalAgentSpeed = enemyAI.runSpeed;
                }
            }
        }

        private IEnumerator Start()
        {
            // Wait one frame for all Awake/Start to complete (race condition fix)
            yield return null;

            // Disable local AI on clients (server-authoritative)
            if (!RakeMultiplayerPlugin.isServer && RakeMultiplayerPlugin.isConnected && enemyAI != null)
            {
                enemyAI.enabled = false;
            }

            if (!RakeMultiplayerPlugin.isServer && animator != null)
            {
                // Root motion would move the transform locally and then the next
                // snapshot would drag it back, which reads as jitter.
                animator.applyRootMotion = false;
            }

            // Initial state: the stalk cycle starts from Idle after a short pause.
            if (RakeMultiplayerPlugin.isServer)
            {
                // First cycle starts soon so the opening read is "the Rake is watching
                // from the tree line", not "it stands in the spawn forever".
                _lurkCooldownTimer = UnityEngine.Random.Range(3f, 8f);
                TransitionToState(RakeState.Idle);
            }

            _initialized = true;
        }

        private void Update()
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (!_initialized) return;

            // Only server runs AI logic
            if (RakeMultiplayerPlugin.isServer)
            {
                if (_clientFrozeSimulation) RestoreServerSimulation();

                if (_remoteOwner)
                {
                    // A client drives the Rake: mirror its snapshots, never run the AI.
                    float t = 1f - Mathf.Exp(-interpolationSpeed * Time.deltaTime);
                    if (hasTargetPosition) transform.position = Vector3.Lerp(transform.position, targetPosition, t);
                    if (hasTargetRotation) transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
                    UpdateClientAnimation();
                    UpdateClientAudio();
                    return;
                }

                // Player-controlled Rakes (and the AFK test-arena Rake) must not be teleported
                // to the forest by the day spawn logic.
                if (!_playerControlled) UpdateDaySpawn();

                if (!RakeMultiplayerPlugin.isConnected)
                {
                    // Single-player: the vanilla Enemy_AI is the brain, leave it alone.
                    RestoreVanillaBrain();
                    return;
                }

                if (!_playerControlled)
                {
                    // The stalking cycle is the only brain while the AI owns the Rake.
                    EnforceAiAuthority();

                    // Update hunter list
                    UpdateHunterList();

                    // Periodically refresh cave transform (in case it was added/loaded after Awake)
                    caveRefreshTimer += Time.deltaTime;
                    if (caveRefreshTimer >= CaveRefreshInterval)
                    {
                        caveRefreshTimer = 0f;
                        if (caveTransform == null)
                        {
                            FindCaveTransform();
                        }
                    }

                    // Run state machine
                    UpdateStateMachine();
                    UpdateAiAnimation();
                    UpdateClientAudio();
                }

                // Sync at 15Hz
                sendTimer += Time.deltaTime;
                if (sendTimer >= syncRate)
                {
                    SendRakeState();
                    sendTimer = 0f;
                }
            }
            else
            {
                // Clients must never simulate the Rake themselves, otherwise every
                // player sees it in a different place. Enforced every frame because the
                // role may only become known after Start().
                if (RakeMultiplayerPlugin.isConnected)
                {
                    EnforceClientAuthority();
                }

                if (!RakeMultiplayerPlugin.isConnected) return;

                if (_localOwner)
                {
                    // This client owns the Rake: publish the local body instead of
                    // interpolating towards the server's copy.
                    sendTimer += Time.deltaTime;
                    if (sendTimer >= syncRate)
                    {
                        SendRakeState();
                        sendTimer = 0f;
                    }
                    return;
                }

                // Client: interpolate to target values

                float t = 1f - Mathf.Exp(-interpolationSpeed * Time.deltaTime);
                if (hasTargetPosition)
                {
                    transform.position = Vector3.Lerp(transform.position, targetPosition, t);
                }
                if (hasTargetRotation)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
                }

                UpdateClientAnimation();
                UpdateClientAudio();
            }
        }

        /// <summary>True while a human is driving the Rake instead of the AI.</summary>
        public bool IsPlayerControlled { get { return _playerControlled; } }

        /// <summary>True while this server runs the stalk AI (mod brain owns the Rake).</summary>
        public bool IsAiActive
        {
            get
            {
                return RakeMultiplayerPlugin.isServer && RakeMultiplayerPlugin.isConnected &&
                       !_playerControlled && !_remoteOwner;
            }
        }

        /// <summary>This peer drives the Rake and publishes its state to the server.</summary>
        public bool IsLocalOwner { get { return _localOwner; } }

        public float CurrentHealth { get { return GetCurrentHealth(); } }

        public float MaxHealth
        {
            get
            {
                float max = GetMaxHealth();
                float hp = GetCurrentHealth();
                if (max <= 0f || max < hp) max = Mathf.Max(hp, 2500f);
                return max;
            }
        }

        public void SetLocalOwner(bool value)
        {
            if (_localOwner == value) return;
            _localOwner = value;
            if (value)
            {
                hasTargetPosition = false;
                hasTargetRotation = false;
                hasAnimState = false;
            }
            RakeMultiplayerPlugin.Log?.LogInfo("[RakeControl] local owner=" + value);
        }

        /// <summary>Server: the Rake is driven by a client, so mirror its snapshots.</summary>
        public void SetRemoteOwner(bool value)
        {
            if (_remoteOwner == value) return;
            _remoteOwner = value;
            if (value)
            {
                sendTimer = 0f;
                hasTargetPosition = false;
                hasTargetRotation = false;
            }
            RakeMultiplayerPlugin.Log?.LogInfo("[RakeControl] remote owner=" + value);
        }

        /// <summary>Server: apply a snapshot from the owning client.</summary>
        public void ApplyOwnerSnapshot(Vector3 position, Quaternion rotation, byte animFlags,
                                       byte walkType, float animSpeed)
        {
            if (!hasTargetPosition || Vector3.Distance(transform.position, position) > snapDistance)
            {
                transform.position = position;
                transform.rotation = rotation;
            }

            targetPosition = position;
            targetRotation = rotation;
            hasTargetPosition = true;
            hasTargetRotation = true;
            targetAnimFlags = animFlags;
            targetWalkType = walkType;
            targetAnimSpeed = animSpeed;
            hasAnimState = true;
        }

        public void SetPlayerControlled(bool value)
        {
            if (_playerControlled == value) return;
            _playerControlled = value;

            if (value)
            {
                if (enemyAI != null) enemyAI.enabled = false;

                // A player-driven Rake is moved by its own controller (or by the test
                // arena); the NavMeshAgent must not snap it back onto the NavMesh.
                var agent = GetEnemyAgent();
                if (agent != null)
                {
                    agent.Stop();
                    agent.ResetPath();
                    agent.enabled = false;
                }

                currentState = RakeState.Chase;
                RakeMultiplayerPlugin.Log?.LogInfo("[RakePlay] Rake switched to player control");
            }
            else
            {
                if (RakeMultiplayerPlugin.isServer && enemyAI != null) enemyAI.enabled = true;

                var agent = GetEnemyAgent();
                if (agent != null && RakeMultiplayerPlugin.isServer) agent.enabled = true;

                RakeMultiplayerPlugin.Log?.LogInfo("[RakePlay] Rake returned to AI");
            }
        }

        /// <summary>Player-controlled Rake hit a player (same damage path as the AI).</summary>
        public bool AttackPlayer(Transform victim)
        {
            return AttackPlayer(victim, AttackDamage, AttackDamageType);
        }

        public bool AttackPlayer(Transform victim, int damage, string damageType)
        {
            if (victim == null) return false;
            DealRakeDamage(victim, damage, damageType);
            return true;
        }

        /// <summary>
        /// During the day the level keeps the Rake on a staging platform under the map.
        /// Move it to the forest instead, once per day phase.
        /// </summary>
        private void UpdateDaySpawn()
        {
            DayNightManager dayNight = DayNightManager.Instance;
            if (dayNight == null) return;

            if (dayNight.CurrentPhase == Phase.Night)
            {
                _daySpawnChecked = false;
                return;
            }

            if (!_daySpawnChecked)
            {
                _daySpawnChecked = true;
                _lastDayWarp = Time.time;
                MoveToForestSpawn();
                return;
            }

            // Safety net: the level (or the vanilla AI) can put the Rake back on the
            // staging platform far outside the map. Bring it back to the forest.
            if (Time.time - _lastDayWarp < 10f) return;
            _lastDayWarp = Time.time;

            if (NearestPlayerDistance(transform.position) > 1200f) MoveToForestSpawn();
        }

        private float NearestPlayerDistance(Vector3 position)
        {
            float best = float.MaxValue;
            bool found = false;

            var spawner = PlayerSpawner.Instance;
            if (spawner != null)
            {
                var local = spawner.LocalPlayer;
                if (local != null)
                {
                    best = Vector3.Distance(position, local.transform.position);
                    found = true;
                }

                foreach (var remote in spawner.GetAllRemotePlayerTransforms())
                {
                    if (remote == null) continue;
                    float d = Vector3.Distance(position, remote.position);
                    if (d < best) best = d;
                    found = true;
                }
            }

            return found ? best : -1f;
        }

        private void MoveToForestSpawn()
        {
            Vector3 target;
            string source;
            if (!TryFindForestSpawn(out target, out source))
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[AI] Day spawn: no forest point found");
                return;
            }

            NavMeshHit hit;
            if (NavMesh.SamplePosition(target, out hit, 25f, NavMesh.AllAreas))
            {
                target = hit.position;
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[AI] Day spawn: '" + source + "' is off the NavMesh: " + target.ToString("F1"));
                return;
            }

            transform.position = target;
            targetPosition = target;
            hasTargetPosition = true;

            // Detach the agent, move the transform, then re-attach: Warp alone can fail
            // and the agent would snap the body back to its old position next frame.
            var agent = GetEnemyAgent();
            if (agent != null)
            {
                bool wasEnabled = agent.enabled;
                if (wasEnabled) agent.enabled = false;

                transform.position = target;

                if (wasEnabled)
                {
                    agent.enabled = true;
                    if (!agent.Warp(target))
                    {
                        RakeMultiplayerPlugin.Log?.LogWarning("[AI] Day spawn: agent.Warp failed at " +
                                                           target.ToString("F1") + " (isOnNavMesh=" + agent.isOnNavMesh + ")");
                    }
                }
            }
            else
            {
                transform.position = target;
            }

            RakeMultiplayerPlugin.Log?.LogInfo("[AI] Day spawn: Rake moved to the forest at " +
                                               target.ToString("F1") + " (source: " + source + 
                                               ", now " + transform.position.ToString("F1") + ")");
        }

        private bool TryFindForestSpawn(out Vector3 position, out string source)
        {
            string[] names = { "mestorake", "rake place", "rakeplace", "rakespawn", "rake spawn", "foresttrigger" };

            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
            for (int n = 0; n < names.Length; n++)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    Transform candidate = all[i];
                    if (candidate == null) continue;

                    string name = candidate.gameObject.name.ToLowerInvariant();
                    if (!name.Contains(names[n])) continue;

                    // Only markers inside the playable area near the players. The level
                    // markers sit on void platforms far outside the map.
                    float distance = NearestPlayerDistance(candidate.position);
                    if (distance <= 0f || distance > 400f) continue;

                    position = candidate.position;
                    source = candidate.gameObject.name;
                    return true;
                }
            }

            MapSettings settings = MapSettings.Current;

            Vector3 origin;
            string originName;

            if (settings != null && settings.hasSafehouse)
            {
                origin = settings.safehousePosition;
                originName = "safehouse";
            }
            else if (settings != null && settings.spawnPoints != null && settings.spawnPoints.Length > 0)
            {
                origin = settings.spawnPoints[0];
                originName = "player spawn";
            }
            else
            {
                var spawner = PlayerSpawner.Instance;
                var local = spawner != null ? spawner.LocalPlayer : null;
                if (local != null)
                {
                    origin = local.transform.position;
                    originName = "player";
                }
                else
                {
                    origin = transform.position;
                    originName = "current position";
                }
            }

            Vector3 nearby;
            if (TryNearbyPoint(origin, out nearby))
            {
                position = nearby;
                source = originName + " + offset";
                return true;
            }

            if (settings != null && settings.hasCave)
            {
                position = settings.cavePosition;
                source = "MapSettings cave";
                return true;
            }

            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position + Vector3.up * 120f, out hit, 250f, NavMesh.AllAreas))
            {
                position = hit.position;
                source = "surface above current position";
                return true;
            }

            position = transform.position;
            source = "current position";
            return false;
        }

        private bool TryNearbyPoint(Vector3 origin, out Vector3 point)
        {
            Vector3[] offsets =
            {
                new Vector3(75f, 0f, 75f),
                new Vector3(-75f, 0f, 75f),
                new Vector3(75f, 0f, -75f),
                new Vector3(-75f, 0f, -75f),
                new Vector3(100f, 0f, 0f),
                new Vector3(-100f, 0f, 0f),
                new Vector3(0f, 0f, 100f),
                new Vector3(0f, 0f, -100f)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(origin + offsets[i], out hit, 30f, NavMesh.AllAreas))
                {
                    point = hit.position;
                    return true;
                }
            }

            point = origin;
            return false;
        }
        private void FindCaveTransform()
        {
            var caveObj = GameObject.Find("Cave");
            if (caveObj == null)
            {
                caveObj = GameObject.Find("RakeCave");
            }
            if (caveObj != null)
            {
                caveTransform = caveObj.transform;
                RakeMultiplayerPlugin.Log?.LogInfo($"[AI] Found cave transform: {caveTransform.name}");
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[AI] Cave transform not found - FeignedRetreat will use fallback");
            }
        }

        private void UpdateHunterList()
        {
            hunters.Clear();

            // Local player (host)
            if (PlayerSpawner.Instance != null && PlayerSpawner.Instance.LocalPlayer != null)
            {
                hunters.Add(PlayerSpawner.Instance.LocalPlayer.transform);
            }

            // Remote players - use public API instead of reflection
            if (PlayerSpawner.Instance != null)
            {
                foreach (var transform in PlayerSpawner.Instance.GetAllRemotePlayerTransforms())
                {
                    hunters.Add(transform);
                }
            }

            // Update nearest and isolated hunters
            UpdateHunterTargets();
        }

        private void UpdateHunterTargets()
        {
            nearestHunter = null;
            isolatedHunter = null;
            float nearestDist = float.MaxValue;
            float maxIsolationScore = -1f;

            foreach (var hunter in hunters)
            {
                if (hunter == null) continue;

                float dist = Vector3.Distance(transform.position, hunter.position);
                
                // Nearest hunter
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestHunter = hunter;
                }

                // Isolated hunter: furthest from other hunters
                float isolationScore = 0f;
                foreach (var other in hunters)
                {
                    if (other == hunter) continue;
                    isolationScore += Vector3.Distance(hunter.position, other.position);
                }
                
                if (isolationScore > maxIsolationScore)
                {
                    maxIsolationScore = isolationScore;
                    isolatedHunter = hunter;
                }
            }

            // Prefer isolated hunter for Follow/Stealth targeting
            currentTarget = nearestHunter ?? isolatedHunter;

            // The vanilla AI still runs on the host and only knows one player. Point it at
            // whoever we are hunting, otherwise it keeps dragging the Rake to the host.
            if (enemyAI != null && enemyAI.enabled && currentTarget != null)
            {
                enemyAI.playerTarget = currentTarget;
            }
        }

        private void UpdateStateMachine()
        {
            float dt = Time.deltaTime;
            stateTimer += dt;
            feignedRetreatCooldownTimer = Mathf.Max(0f, feignedRetreatCooldownTimer - dt);
            _lurkCooldownTimer = Mathf.Max(0f, _lurkCooldownTimer - dt);
            _noiseCooldown = Mathf.Max(0f, _noiseCooldown - dt);

            // Check for death
            float health = GetCurrentHealth();
            if (health <= 0f && currentState != RakeState.Dead)
            {
                TransitionToState(RakeState.Dead);
                return;
            }

            // State-specific updates
            switch (currentState)
            {
                case RakeState.Idle:
                    UpdateIdle();
                    break;
                case RakeState.Follow:
                    UpdateFollow();
                    break;
                case RakeState.Lurk:
                    UpdateLurk();
                    break;
                case RakeState.Stealth:
                    UpdateStealth();
                    break;
                case RakeState.Chase:
                    UpdateChase();
                    break;
                case RakeState.Attack:
                    UpdateAttack();
                    break;
                case RakeState.Flee:
                    UpdateFlee();
                    break;
                case RakeState.Blocking:
                    UpdateBlocking();
                    break;
                case RakeState.FeignedRetreat:
                    UpdateFeignedRetreat();
                    break;
                case RakeState.Search:
                    UpdateSearch();
                    break;
                case RakeState.Stunned:
                    UpdateStunned();
                    break;
                case RakeState.Dead:
                    UpdateDead();
                    break;
            }
        }

        /// <summary>
        /// Idle is the pause between lurk cycles. A hunter who walks right into the
        /// Rake interrupts it: close range means stalk, very close range means charge.
        /// </summary>
        private void UpdateIdle()
        {
            if (currentTarget == null) return;

            float distance = Vector3.Distance(transform.position, currentTarget.position);

            // Point blank: even between cycles the Rake will not ignore a hunter
            // standing in its face. Otherwise the lurk cycle owns the decision.
            if (distance < 6f)
            {
                TransitionToState(RakeState.Chase);
                return;
            }

            if (_lurkCooldownTimer > 0f) return;
            BeginLurk();
        }

        /// <summary>Legacy state: behave like a new lurk cycle.</summary>
        private void UpdateFollow()
        {
            BeginLurk();
        }

        /// <summary>
        /// Lurk: walk to a vantage point about 60 m from the hunter (on the line
        /// between them), stand still, freeze the idle/crawl animation and watch.
        /// After lurkWatchDuration it repositions or goes back to Idle.
        /// </summary>
        private void UpdateLurk()
        {
            var target = currentTarget;
            if (target == null)
            {
                _lurkCooldownTimer = UnityEngine.Random.Range(lurkIntervalMin, lurkIntervalMax);
                TransitionToState(RakeState.Idle);
                return;
            }

            float distance = Vector3.Distance(transform.position, target.position);

            // During the watch, a hunter closing to 30 m starts the silent approach.
            // While still walking to the vantage point the Rake keeps going.
            if (_lurkArrived && distance < sneakStartDistance)
            {
                TransitionToState(RakeState.Stealth);
                return;
            }

            // The hunter is actually looking at the Rake: break contact.
            if (IsRakeSeenCached())
            {
                TransitionToState(RakeState.Flee);
                return;
            }

            if (!_lurkArrived)
            {
                if (!_lurkPointValid)
                {
                    ArriveAtLurk();
                }
                else
                {
                    float speed = Vector3.Distance(transform.position, _lurkPoint) > 35f
                        ? GetRunSpeed() : GetWalkSpeed();
                    MoveToTarget(_lurkPoint, false, speed);

                    var agent = GetEnemyAgent();
                    bool arrived = agent != null && !agent.pathPending && agent.remainingDistance <= 2.5f;
                    if (arrived || Vector3.Distance(transform.position, _lurkPoint) < 2.5f || stateTimer > 60f)
                    {
                        ArriveAtLurk();
                    }
                }
            }

            if (_lurkArrived)
            {
                FaceLook(target.position, 2.5f);
                _lurkWatchTimer += Time.deltaTime;
                if (_lurkWatchTimer >= lurkWatchDuration)
                {
                    _lurkCooldownTimer = UnityEngine.Random.Range(lurkIntervalMin, lurkIntervalMax);
                    TransitionToState(RakeState.Idle);
                }
            }
        }

        /// <summary>Stop at the vantage point and start the watch timer.</summary>
        private void ArriveAtLurk()
        {
            _lurkArrived = true;
            _lurkWatchTimer = 0f;
            _lurkFreezeTimer = 0.35f;
            _lurkCrouch = UnityEngine.Random.value < lurkCrouchChance;

            var agent = GetEnemyAgent();
            if (agent != null && agent.isOnNavMesh)
            {
                agent.Stop();
                agent.ResetPath();
            }
        }

        /// <summary>Pick a standoff point ~60 m from the hunter, reachable and in the open.</summary>
        private bool TryFindLurkPoint(Transform target, out Vector3 point)
        {
            point = transform.position;
            if (target == null) return false;

            float currentDistance = Vector3.Distance(transform.position, target.position);
            if (currentDistance >= sneakStartDistance && currentDistance <= lurkStandoffDistance + 10f &&
                HasLineOfSight(transform.position + Vector3.up * 1.4f, target.position + Vector3.up * 1.2f))
            {
                // Already at a good watching distance with a clear view: stay put.
                return true;
            }

            Vector3 toRake = transform.position - target.position;
            toRake.y = 0f;
            if (toRake.sqrMagnitude < 1f) toRake = -target.forward;
            toRake.y = 0f;
            if (toRake.sqrMagnitude < 0.001f) toRake = Vector3.forward;
            toRake.Normalize();

            float[] angles = { 0f, 15f, -15f, 30f, -30f, 45f, -45f, 60f, -60f, 90f, -90f };
            Vector3 fallback = transform.position;
            bool hasFallback = false;

            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, angles[i], 0f) * toRake;
                Vector3 candidate = target.position + dir * lurkStandoffDistance;

                NavMeshHit hit;
                if (!NavMesh.SamplePosition(candidate, out hit, 12f, NavMesh.AllAreas)) continue;
                candidate = hit.position;

                NavMeshPath path = new NavMeshPath();
                if (!NavMesh.CalculatePath(transform.position, candidate, NavMesh.AllAreas, path)) continue;
                if (path.status != NavMeshPathStatus.PathComplete) continue;

                // Prefer a spot from which the hunter is actually visible: the whole
                // point of the watch is to be seen at a distance, not to stare at a hill.
                if (HasLineOfSight(candidate + Vector3.up * 1.4f, target.position + Vector3.up * 1.2f))
                {
                    point = candidate;
                    return true;
                }

                if (!hasFallback)
                {
                    fallback = candidate;
                    hasFallback = true;
                }
            }

            if (hasFallback)
            {
                point = fallback;
                return true;
            }

            return false;
        }

        /// <summary>Clear physical line between two points (triggers are ignored).</summary>
        private static bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.5f) return true;

            RaycastHit[] hits = Physics.RaycastAll(from, delta / distance, distance - 0.4f);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null) continue;
                if (hits[i].collider.isTrigger) continue;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Stealth: crouch and close the distance. Being noticed here does not make
        /// the Rake flee: it charges instead.
        /// </summary>
        private void UpdateStealth()
        {
            var target = currentTarget;
            if (target == null)
            {
                TransitionToState(RakeState.Idle);
                return;
            }

            float distance = Vector3.Distance(transform.position, target.position);

            if (distance <= chargeStartDistance)
            {
                TransitionToState(RakeState.Chase);
                return;
            }

            if (IsRakeSeenCached())
            {
                TransitionToState(RakeState.Chase);
                return;
            }

            if (distance > fleeStopDistance)
            {
                BeginLurk();
                return;
            }

            MoveToTarget(target.position, true, GetSneakSpeed());

            Vector3 lookDir = target.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                FaceTarget(lookDir.normalized, 4f);
            }
        }

        private void UpdateChase()
        {
            // Play chase vocalizations
            chaseVocalizationTimer += Time.deltaTime;
            if (chaseVocalizationTimer >= chaseVocalizationInterval)
            {
                PlayChaseVocalization();
                chaseVocalizationTimer = 0f;
            }

            if (currentTarget == null)
            {
                noTargetTimer += Time.deltaTime;
                if (noTargetTimer > 5f)
                {
                    _lurkCooldownTimer = UnityEngine.Random.Range(lurkIntervalMin, lurkIntervalMax);
                    TransitionToState(RakeState.Idle);
                }
                return;
            }
            noTargetTimer = 0f;

            float distance = Vector3.Distance(transform.position, currentTarget.position);

            MoveToTarget(currentTarget.position, false, GetRunSpeed());

            Vector3 lookDir = (currentTarget.position - transform.position).normalized;
            lookDir.y = 0f;
            if (lookDir != Vector3.zero)
            {
                FaceTarget(lookDir, 10f);
            }

            if (distance <= attackRange)
            {
                TransitionToState(RakeState.Attack);
                return;
            }

            if (distance > fleeStopDistance)
            {
                _lurkCooldownTimer = UnityEngine.Random.Range(lurkIntervalMin, lurkIntervalMax);
                TransitionToState(RakeState.Idle);
            }
        }

        private void UpdateAttack()
        {
            if (currentTarget == null)
            {
                TransitionToState(RakeState.Chase);
                return;
            }

            // Face target
            Vector3 lookDir = (currentTarget.position - transform.position).normalized;
            lookDir.y = 0f;
            if (lookDir != Vector3.zero)
            {
                FaceTarget(lookDir, 15f);
            }

            // Enemy_AI has no Attack() method - the attack is the "bAttack" animator
            // flag. Stop the agent and freeze the walk so the animation plays in place.
            var attackAgent = GetEnemyAgent();
            if (attackAgent != null && attackAgent.isOnNavMesh)
            {
                attackAgent.Stop();
            }

            if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
                animator.SetBool("bAttack", true);
            }

            // One hit per AttackInterval while the target stays in range and in front.
            _attackCooldown -= Time.deltaTime;
            if (_attackCooldown <= 0f)
            {
                _attackCooldown = AttackInterval;

                Vector3 toTarget = currentTarget.position - transform.position;
                float distance = toTarget.magnitude;
                toTarget.y = 0f;
                bool inFront = toTarget.sqrMagnitude < 0.01f ||
                               Vector3.Dot(transform.forward, toTarget.normalized) > 0.2f;

                if (distance <= attackRange + 1.5f && inFront)
                {
                    DealRakeDamage(currentTarget);
                }
            }

            // Attack -> Chase: target escaped (moved out of range)
            if (Vector3.Distance(transform.position, currentTarget.position) > attackRange * 2f)
            {
                TransitionToState(RakeState.Chase);
                return;
            }
        }

        /// <summary>
        /// Flee: run directly away from the hunter until it is farther than
        /// fleeStopDistance, then pause before the next lurk cycle.
        /// </summary>
        private void UpdateFlee()
        {
            var target = currentTarget;
            if (target == null)
            {
                _lurkCooldownTimer = UnityEngine.Random.Range(lurkIntervalMin, lurkIntervalMax);
                TransitionToState(RakeState.Idle);
                return;
            }

            float distance = Vector3.Distance(transform.position, target.position);
            if (distance > fleeStopDistance)
            {
                _lurkCooldownTimer = UnityEngine.Random.Range(lurkIntervalMin, lurkIntervalMax);
                TransitionToState(RakeState.Idle);
                return;
            }

            _fleeRepathTimer -= Time.deltaTime;
            if (_fleeRepathTimer <= 0f || !_lurkPointValid)
            {
                _fleeRepathTimer = 1.2f;

                Vector3 away = transform.position - target.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = -target.forward;
                away.y = 0f;
                if (away.sqrMagnitude < 0.001f) away = Vector3.forward;
                away.Normalize();

                Vector3 desired = target.position + away * fleeDestinationDistance;
                Vector3 sampled;
                if (!TrySampleNavMesh(desired, 18f, out sampled)) sampled = desired;
                _lurkPoint = sampled;
                _lurkPointValid = true;
            }

            if (_lurkPointValid)
            {
                MoveToTarget(_lurkPoint, false, GetRunSpeed());
            }
        }

        /// <summary>Start a lurk cycle against the current hunter.</summary>
        private void BeginLurk()
        {
            var target = currentTarget;
            if (target == null) return;

            Vector3 point = transform.position;
            _lurkPointValid = TryFindLurkPoint(target, out point);
            _lurkPoint = point;
            _lurkArrived = false;
            _lurkWatchTimer = 0f;
            _lurkFreezeTimer = 0f;
            _lurkCrouch = UnityEngine.Random.value < lurkCrouchChance;

            if (currentState != RakeState.Lurk)
            {
                TransitionToState(RakeState.Lurk);
            }
            else
            {
                stateTimer = 0f;
                ResetAgentPath();
            }
        }

        private static bool TrySampleNavMesh(Vector3 desired, float radius, out Vector3 result)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(desired, out hit, radius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }

            result = desired;
            return false;
        }

        /// <summary>Turn the whole body towards a world point (only used while standing).</summary>
        private void FaceLook(Vector3 worldPoint, float speed)
        {
            Vector3 direction = worldPoint - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;

            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(direction), Time.deltaTime * speed);
        }

        private bool IsRakeSeenCached()
        {
            _seenTimer -= Time.deltaTime;
            if (_seenTimer <= 0f)
            {
                _seenTimer = 0.2f;
                _seenCached = RakeIsSeenByAnyHunter();
            }
            return _seenCached;
        }

        private bool RakeIsSeenByAnyHunter()
        {
            for (int i = 0; i < hunters.Count; i++)
            {
                if (hunters[i] != null && IsRakeSeenByHunter(hunters[i])) return true;
            }
            return false;
        }

        /// <summary>
        /// A hunter sees the Rake when the screen centre is aimed at it and a ray from
        /// the camera reaches the Rake before anything else. Body facing is used for
        /// remote players whose camera is not simulated on this peer.
        /// </summary>
        private bool IsRakeSeenByHunter(Transform hunter)
        {
            if (hunter == null) return false;

            var fps = hunter.GetComponent<FPSController>();
            if (fps != null && fps.bHide) return false;

            Vector3 eye;
            Vector3 forward;
            float range = seenRange;

            Camera cam = null;
            var spawner = PlayerSpawner.Instance;
            bool isLocal = spawner != null && spawner.LocalPlayer != null &&
                           spawner.LocalPlayer.transform == hunter;

            if (isLocal)
            {
                // CameraMouseLook knows the real first-person camera (it is not always
                // Camera.main, e.g. while a scope camera is active).
                var look = hunter.GetComponentInChildren<CameraMouseLook>();
                if (look == null) look = hunter.GetComponentInParent<CameraMouseLook>();
                if (look != null && look.camera != null) cam = look.camera.GetComponent<Camera>();
                if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
            }

            if (cam == null || !cam.isActiveAndEnabled) cam = hunter.GetComponentInChildren<Camera>();

            if (cam != null && cam.isActiveAndEnabled)
            {
                eye = cam.transform.position;
                forward = cam.transform.forward;
            }
            else
            {
                eye = hunter.position + Vector3.up * 1.6f;
                forward = hunter.forward;
                range = Mathf.Min(range, 80f);
            }

            Vector3 rakePoint = transform.position + Vector3.up * 1.2f;
            Vector3 toRake = rakePoint - eye;
            float distance = toRake.magnitude;
            if (distance < 0.05f || distance > range) return false;

            Vector3 direction = toRake / distance;
            if (Vector3.Angle(forward, direction) > seenConeAngle) return false;

            // Unity 5.x has no RaycastNonAlloc: use RaycastAll and filter afterwards.
            RaycastHit[] hits = Physics.RaycastAll(eye, direction, distance + 2f);

            Collider nearest = null;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                var collider = hit.collider;
                if (collider == null) continue;
                if (collider.transform == hunter || collider.transform.IsChildOf(hunter)) continue;
                if (collider.isTrigger) continue; // only solid geometry blocks the view
                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearest = collider;
                }
            }

            return nearest != null && IsRakeCollider(nearest);
        }

        private bool IsRakeCollider(Collider collider)
        {
            if (collider == null) return false;
            if (collider.transform == transform || collider.transform.IsChildOf(transform)) return true;
            return enemyAI != null && collider.GetComponentInParent<Enemy_AI>() == enemyAI;
        }

        private float GetWalkSpeed()
        {
            if (enemyAI != null && enemyAI.walkSpeed > 0.1f) return enemyAI.walkSpeed;
            return 1.6f;
        }

        private float GetRunSpeed()
        {
            if (enemyAI != null && enemyAI.runSpeed > 0.1f) return enemyAI.runSpeed;
            if (originalAgentSpeed > 0.1f) return originalAgentSpeed;
            return 5f;
        }

        private float GetSneakSpeed()
        {
            if (enemyAI != null && enemyAI.hauntSpeed > 0.1f) return Mathf.Clamp(enemyAI.hauntSpeed, 1f, 2.2f);
            return 1.6f;
        }

        private void ResetAgentPath()
        {
            var agent = GetEnemyAgent();
            if (agent != null && agent.isOnNavMesh)
            {
                agent.Stop();
                agent.ResetPath();
            }
            _repathTimer = -999f;
            _lastDestination = Vector3.zero;
        }
        /// <summary>
        /// Apply one Rake hit. The host damages its own player directly; for a client the
        /// victim gets a PlayerDamaged packet and applies it locally, so the vanilla
        /// damage effects and the ragdoll hit reaction run on every peer.
        /// </summary>
        private void DealRakeDamage(Transform victim)
        {
            DealRakeDamage(victim, AttackDamage, AttackDamageType);
        }

        private void DealRakeDamage(Transform victim, int damage, string damageType)
        {
            if (victim == null) return;

            var net = victim.GetComponent<NetworkedPlayer>();
            if (net == null) return;

            Vector3 away = victim.position - transform.position;
            away.y = 0f;

            if (net.isLocalPlayer)
            {
                // The TakeDamage patch spawns our own ragdoll; observers need the packet.
                var fps = victim.GetComponent<FPSController>();
                if (fps != null) fps.TakeDamage(damage, damageType);

                PlayerRagdoll.BroadcastOnly(net.PlayerId, away);

                RakeMultiplayerPlugin.Log?.LogInfo("[AI] Rake hit the host for " + damage);
                return;
            }

            if (net.PlayerId <= 0) return;

            var packet = new PlayerDamagedPacket
            {
                PlayerId = net.PlayerId,
                Amount = damage,
                DamageType = damageType,
                SourcePosition = transform.position
            };
            RakeNetworkManager.Instance?.ServerBroadcast(packet.Serialize());

            // The victim plays its own pain sound; the host needs the clone's one.
            PlayerAudio.PlayPain(victim);

            // Ragdoll on the host and on every client
            PlayerRagdoll.TriggerNetworked(net.PlayerId, away);

            RakeMultiplayerPlugin.Log?.LogInfo("[AI] Rake hit player " + net.PlayerId +
                                              " for " + damage);
        }

        private void UpdateBlocking()
        {
            // Face the attacker during block
            if (lastDamageDirection != Vector3.zero)
            {
                Vector3 faceDir = -lastDamageDirection;
                faceDir.y = 0;
                if (faceDir != Vector3.zero)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, 
                        Quaternion.LookRotation(faceDir), Time.deltaTime * 15f);
                }
            }

            // Blocking -> Chase: timer expired
            if (Time.time >= blockingEndTime)
            {
                isBlocking = false;
                TransitionToState(RakeState.Chase);
                RakeMultiplayerPlugin.Log?.LogInfo("[AI] Blocking ended, returning to Chase");
            }
        }

        private void UpdateFeignedRetreat()
        {
            // Run to cave transform
            if (caveTransform != null)
            {
                MoveToTarget(caveTransform.position, silent: false);
                
                if (Vector3.Distance(transform.position, caveTransform.position) < 2f)
                {
                    // At cave - wait 5 seconds
                    if (stateTimer >= feignedRetreatWaitTime)
                    {
                        // Teleport behind nearest hunter
                        TeleportBehindNearestHunter();
                        
                        // Set cooldown
                        feignedRetreatCooldownTimer = feignedRetreatCooldown;
                        stateTimer = 0f;
                        
                        // Transition to Follow
                        TransitionToState(RakeState.Follow);
                    }
                }
            }
            else
            {
                // No cave found - just wait and teleport
                if (stateTimer >= feignedRetreatWaitTime)
                {
                    TeleportBehindNearestHunter();
                    feignedRetreatCooldownTimer = feignedRetreatCooldown;
                    stateTimer = 0f;
                    TransitionToState(RakeState.Follow);
                }
            }
        }

        private void UpdateSearch()
        {
            if (stateTimer > 10f)
            {
                _lurkCooldownTimer = UnityEngine.Random.Range(lurkIntervalMin, lurkIntervalMax);
                TransitionToState(RakeState.Idle);
            }
        }

        private void UpdateStunned()
        {
            // Stun is applied by traps (Task 3.11); auto-recover after stunDuration
            if (stateTimer > stunDuration)
            {
                SetStunnedPhysics(false);
                _lurkCooldownTimer = UnityEngine.Random.Range(lurkIntervalMin, lurkIntervalMax);
                TransitionToState(RakeState.Idle);
            }
        }

        /// <summary>Freeze/resume the vanilla Enemy_AI and its NavMeshAgent during a stun.</summary>
        private void SetStunnedPhysics(bool stunned)
        {
            var agent = GetEnemyAgent();
            if (agent != null && agent.isOnNavMesh)
            {
                if (stunned)
                {
                    agent.Stop();
                    agent.ResetPath();
                }
                else
                {
                    agent.Resume();
                }
            }

            // The mod brain owns the Rake while the AI is active; never switch the
            // vanilla brain back on from here (it would fight the state machine).
            if (enemyAI != null && !IsAiActive)
            {
                enemyAI.enabled = !stunned;
            }
        }

        private NavMeshAgent GetEnemyAgent()
        {
            if (enemyAI != null && enemyAI.nav != null) return enemyAI.nav;
            if (enemyAI != null && enemyAgentField != null)
            {
                var cached = enemyAgentField.GetValue(enemyAI) as NavMeshAgent;
                if (cached != null) return cached;
            }

            var own = GetComponent<NavMeshAgent>();
            if (own != null) return own;
            return GetComponentInChildren<NavMeshAgent>();
        }

        private bool _clientFrozeSimulation;
        private bool _loggedClientFreeze;

        /// <summary>
        /// Clients only render the Rake; the server owns its AI. The vanilla Enemy_AI and
        /// its NavMeshAgent keep writing the transform every frame, which undoes the
        /// network interpolation and leaves each client with the Rake somewhere else.
        /// </summary>
        private void EnforceClientAuthority()
        {
            bool changed = false;

            if (enemyAI != null && enemyAI.enabled)
            {
                enemyAI.enabled = false;
                changed = true;
            }

            var agent = ResolveAgent();
            if (agent != null && agent.enabled)
            {
                agent.enabled = false;
                changed = true;
            }

            if (animator != null && animator.applyRootMotion)
            {
                animator.applyRootMotion = false;
                changed = true;
            }

            _clientFrozeSimulation = true;

            if (changed && !_loggedClientFreeze)
            {
                _loggedClientFreeze = true;
                RakeMultiplayerPlugin.Log?.LogInfo("[AI] Client disabled the local Rake simulation (Enemy_AI + NavMeshAgent)");
            }
        }

        /// <summary>Give the Rake back to this peer when it turns out to be the server.</summary>
        private void RestoreServerSimulation()
        {
            _clientFrozeSimulation = false;

            if (enemyAI != null && !enemyAI.enabled) enemyAI.enabled = true;

            var agent = ResolveAgent();
            if (agent != null && !agent.enabled) agent.enabled = true;

            RakeMultiplayerPlugin.Log?.LogInfo("[AI] Rake simulation restored for the server role");
        }

        private NavMeshAgent ResolveAgent()
        {
            var agent = GetEnemyAgent();
            if (agent != null) return agent;

            agent = GetComponent<NavMeshAgent>();
            if (agent != null) return agent;

            return GetComponentInChildren<NavMeshAgent>();
        }

        private Vector3 _lastAnimPosition;
        private float _observedSpeed;
        private bool _lastAnimPositionValid;

        /// <summary>
        /// Clients drive the animator from the movement they actually observe. The old
        /// code fed the animator a speed derived from the AI state (Follow = 1,
        /// Chase = 2), which is never 0, so the Rake walked in place forever.
        /// </summary>
        /// <summary>
        /// Host-side animation for the mod brain. The vanilla Enemy_AI is disabled, so
        /// the stalk/crouch/freeze poses are driven from the state machine.
        /// </summary>
        private void UpdateAiAnimation()
        {
            if (animator == null) return;

            switch (currentState)
            {
                case RakeState.Lurk:
                    animator.speed = 1f;
                    animator.SetBool("bAttack", false);
                    animator.SetInteger("WalkType", 1);
                    if (!_lurkArrived)
                    {
                        animator.SetBool("bCrouch", false);
                        var agent = GetEnemyAgent();
                        bool running = agent != null && agent.speed >= GetRunSpeed() - 0.5f;
                        animator.SetFloat("Speed", running ? 2f : 1f);
                        _lurkFreezeTimer = 0f;
                    }
                    else
                    {
                        animator.SetBool("bCrouch", _lurkCrouch);
                        animator.SetFloat("Speed", 0f);
                        if (_lurkFreezeTimer > 0f)
                        {
                            _lurkFreezeTimer -= Time.deltaTime;
                        }
                        else
                        {
                            animator.speed = 0f; // fully frozen watching pose
                        }
                    }
                    break;

                case RakeState.Stealth:
                    animator.speed = 1f;
                    animator.SetBool("bCrouch", true);
                    animator.SetBool("bAttack", false);
                    animator.SetInteger("WalkType", 1);
                    animator.SetFloat("Speed", 1f);
                    break;

                case RakeState.Chase:
                case RakeState.Flee:
                    animator.speed = 1f;
                    animator.SetBool("bCrouch", false);
                    animator.SetBool("bAttack", false);
                    animator.SetInteger("WalkType", 1);
                    animator.SetFloat("Speed", 2f);
                    break;

                case RakeState.Attack:
                    animator.speed = 1f;
                    animator.SetBool("bCrouch", false);
                    animator.SetBool("bAttack", true);
                    animator.SetInteger("WalkType", 1);
                    animator.SetFloat("Speed", 0f);
                    break;

                case RakeState.Idle:
                    animator.speed = 1f;
                    animator.SetBool("bCrouch", false);
                    animator.SetBool("bAttack", false);
                    animator.SetInteger("WalkType", 1);
                    animator.SetFloat("Speed", 0f);
                    break;
            }
        }

        private void UpdateClientAnimation()
        {
            if (animator == null) return;

            float dt = Time.deltaTime;
            Vector3 position = transform.position;

            if (_lastAnimPositionValid && dt > 0.0001f)
            {
                float instant = (position - _lastAnimPosition).magnitude / dt;
                _observedSpeed = Mathf.Lerp(_observedSpeed, instant, 1f - Mathf.Exp(-10f * dt));
            }
            _lastAnimPosition = position;
            _lastAnimPositionValid = true;

            if (!hasAnimState)
            {
                // Before the first snapshot arrives, fall back to the observed movement.
                float derived = _observedSpeed < 0.2f ? 0f : (_observedSpeed < 3.5f ? 1f : 2f);
                animator.SetFloat("Speed", Mathf.MoveTowards(animator.GetFloat("Speed"), derived, dt * 4f));
                animator.SetInteger("WalkType", 1);
                animator.SetBool("bAttack", currentState == RakeState.Attack);
                return;
            }

            // The host freezes the crawl/idle pose while it watches from a distance.
            // Freeze after a short settle so the crouch transition is not cut in half.
            if (currentState == RakeState.Lurk && targetAnimSpeed < 0.1f)
            {
                _clientLurkFreezeTimer += dt;
                animator.SetBool("bCrouch", (targetAnimFlags & 1) != 0);
                animator.SetBool("bAttack", (targetAnimFlags & 2) != 0);
                animator.SetInteger("WalkType", targetWalkType);
                animator.SetFloat("Speed", 0f);
                animator.speed = _clientLurkFreezeTimer > 0.35f ? 0f : 1f;
                return;
            }

            animator.speed = 1f;
            _clientLurkFreezeTimer = 0f;

            // Mirror the host animator exactly: the crouch/sneak used while hunting
            // animals lives only in the vanilla parameters, not in our state enum.
            animator.SetBool("bCrouch", (targetAnimFlags & 1) != 0);
            animator.SetBool("bAttack", (targetAnimFlags & 2) != 0);
            animator.SetBool("bAnimalKilling", (targetAnimFlags & 4) != 0);
            animator.SetBool("bPlayerAttack", (targetAnimFlags & 8) != 0);
            animator.SetBool("bPlayerAppercut", (targetAnimFlags & 16) != 0);
            animator.SetBool("bCameraBreak", (targetAnimFlags & 32) != 0);
            animator.SetInteger("WalkType", targetWalkType);
            animator.SetFloat("Speed", Mathf.MoveTowards(animator.GetFloat("Speed"), targetAnimSpeed, dt * 6f));
        }

        /// <summary>
        /// Face a direction, but only while the NavMeshAgent is standing still: while it
        /// moves, the agent owns the rotation and the Rake should look where it walks.
        /// Overriding it made the model face sideways relative to its movement.
        /// </summary>
        /// <summary>
        /// Clients never run Enemy_AI, so none of the Rake sounds (footsteps, chase
        /// vocals, attack roars) are triggered there. Mirror them from the movement and
        /// the synced state, through the Rake's own 3D AudioSource.
        /// </summary>
        private void UpdateClientAudio()
        {
            var src = audioSource != null ? audioSource : (enemyAI != null ? enemyAI.audioSource : null);
            if (src == null) return;

            float dt = Time.deltaTime;
            Vector3 pos = transform.position;

            float speed = 0f;
            if (_clientAudioValid && dt > 0.0001f)
            {
                speed = (pos - _clientAudioPosition).magnitude / dt;
            }
            _clientAudioPosition = pos;
            _clientAudioValid = true;

            bool crouching = animator != null && animator.GetBool("bCrouch");

            if (speed > 0.35f && !crouching)
            {
                _clientStepTimer -= dt;
                if (_clientStepTimer <= 0f)
                {
                    _clientStepTimer = speed > 3.5f ? 0.34f : 0.52f;
                    if (enemyAI != null && enemyAI.walkSound != null)
                    {
                        src.PlayOneShot(enemyAI.walkSound, 0.8f);
                    }
                }
            }
            else
            {
                _clientStepTimer = 0f;
            }

            // On the host the state machine already handles chase vocalisations;
            // this mirror only adds the attack roars there.
            if (RakeMultiplayerPlugin.isServer && currentState == RakeState.Chase) return;

            if (currentState != _clientAudioState)
            {
                _clientAudioState = currentState;
                _clientVocalTimer = 0f;
            }

            _clientVocalTimer -= dt;
            if (_clientVocalTimer > 0f) return;

            if (currentState == RakeState.Chase)
            {
                _clientVocalTimer = UnityEngine.Random.Range(4f, 7f);
                PlayChaseVocalization();
            }
            else if (currentState == RakeState.Attack)
            {
                _clientVocalTimer = 2.5f;
                if (enemyAI != null && enemyAI.playerAttackRoars != null && enemyAI.playerAttackRoars.Length > 0)
                {
                    src.PlayOneShot(enemyAI.playerAttackRoars[UnityEngine.Random.Range(0, enemyAI.playerAttackRoars.Length)]);
                }
            }
            else
            {
                _clientVocalTimer = 1f;
            }
        }

        private void FaceTarget(Vector3 direction, float speed)
        {
            if (direction.sqrMagnitude < 0.0001f) return;

            var agent = ResolveAgent();
            if (agent != null && agent.velocity.sqrMagnitude > 0.04f) return;

            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(direction), Time.deltaTime * speed);
        }

        /// <summary>
        /// Server: stun the Rake (e.g. triggered trap, Task 3.11).
        /// </summary>
        public void Stun(float seconds = 3f)
        {
            if (!RakeMultiplayerPlugin.isServer) return;

            stunDuration = Mathf.Max(0.1f, seconds);

            if (currentState == RakeState.Stunned)
            {
                stateTimer = 0f; // refresh the stun
            }
            else
            {
                TransitionToState(RakeState.Stunned);
            }

            SetStunnedPhysics(true);

            RakeMultiplayerPlugin.Log?.LogInfo($"[3.11] Rake stunned for {stunDuration:F1}s");
        }

        /// <summary>
        /// The mod state machine is the only brain while the AI owns the Rake. The
        /// vanilla Enemy_AI keeps its own Patrol/PlayerHount/Escape logic and would
        /// fight every destination we set, so disable its Update.
        /// </summary>
        private void EnforceAiAuthority()
        {
            if (enemyAI != null && enemyAI.enabled)
            {
                enemyAI.enabled = false;
                RakeMultiplayerPlugin.Log?.LogInfo("[AI] Vanilla Enemy_AI disabled; stalk AI owns the Rake");
            }

            var agent = GetEnemyAgent();
            if (agent != null && !agent.enabled) agent.enabled = true;
        }

        /// <summary>Single-player: let the vanilla Rake AI run unchanged.</summary>
        private void RestoreVanillaBrain()
        {
            if (_playerControlled || _remoteOwner || RakeMultiplayerPlugin.isConnected) return;

            if (enemyAI != null && !enemyAI.enabled)
            {
                enemyAI.enabled = true;
                RakeMultiplayerPlugin.Log?.LogInfo("[AI] Vanilla Enemy_AI restored (no multiplayer match)");
            }
        }

        /// <summary>
        /// A shot was fired within earshot. While waiting between lurk cycles the Rake
        /// comes out to look; it does not interrupt a charge or a retreat.
        /// </summary>
        public void NotifyNoise(Vector3 position)
        {
            if (!IsAiActive) return;
            if (_noiseCooldown > 0f) return;
            if (Vector3.Distance(transform.position, position) > noiseHearRange) return;

            if (currentState == RakeState.Chase || currentState == RakeState.Attack ||
                currentState == RakeState.Flee || currentState == RakeState.Stunned ||
                currentState == RakeState.Dead)
            {
                return;
            }

            _noiseCooldown = 6f;

            if (currentState == RakeState.Idle)
            {
                _lurkCooldownTimer = 0f;
                BeginLurk();
            }
            else if (currentState == RakeState.Lurk && _lurkArrived)
            {
                // Something happened: look towards it and keep watching.
                _lurkWatchTimer = 0f;
                FaceLook(position, 3f);
            }

            RakeMultiplayerPlugin.Log?.LogInfo("[AI] Heard a shot at " + position.ToString("F0"));
        }

        /// <summary>One-line state dump for the "rake ai" console command.</summary>
        public string GetAiDebugInfo()
        {
            var target = currentTarget;
            float distance = target != null ? Vector3.Distance(transform.position, target.position) : -1f;
            var agent = GetEnemyAgent();

            return "state=" + currentState + " prev=" + previousState +
                   " target=" + (target != null ? target.name : "-") +
                   " dist=" + distance.ToString("F1") +
                   " seen=" + _seenCached +
                   " lurkArrived=" + _lurkArrived +
                   " watch=" + _lurkWatchTimer.ToString("F1") + "/" + lurkWatchDuration.ToString("F0") +
                   " cooldown=" + _lurkCooldownTimer.ToString("F1") +
                   " ai=" + IsAiActive +
                   " agent=" + (agent == null ? "null" : (agent.isOnNavMesh ? "onMesh" : "offMesh")) +
                   " path=" + (agent == null ? "-" : agent.pathStatus.ToString());
        }

        public void ForceLurk()
        {
            if (!IsAiActive) return;
            _lurkCooldownTimer = 0f;
            BeginLurk();
        }

        public void ForceFlee()
        {
            if (!IsAiActive || currentTarget == null) return;
            TransitionToState(RakeState.Flee);
        }

        private void UpdateDead()
        {
            // Dead state - disable AI
            if (enemyAI != null)
            {
                enemyAI.enabled = false;
            }
        }

        private void TransitionToState(RakeState newState)
        {
            if (currentState == newState) return;

            previousState = currentState;
            currentState = newState;
            stateTimer = 0f;
            _seenTimer = 0f;

            // Leaving a frozen lurk pose: let the animator run again.
            if (animator != null) animator.speed = 1f;
            ResetAgentPath();

            // State entry actions
            switch (newState)
            {
                case RakeState.Stealth:
                    if (animator != null) animator.SetBool("bCrouch", true);
                    break;

                case RakeState.Lurk:
                    if (animator != null) animator.SetBool("bCrouch", false);
                    _lurkArrived = false;
                    _lurkWatchTimer = 0f;
                    _lurkFreezeTimer = 0f;
                    break;

                case RakeState.Flee:
                    if (animator != null) animator.SetBool("bCrouch", false);
                    _fleeRepathTimer = 0f;
                    _lurkPointValid = false;
                    break;

                case RakeState.Attack:
                    if (animator != null) animator.SetBool("bCrouch", false);
                    // A short wind-up so the first hit is not applied on the same frame
                    // the state is entered.
                    _attackCooldown = 0.5f;
                    break;

                case RakeState.Follow:
                case RakeState.Idle:
                case RakeState.Patrol:
                case RakeState.Chase:
                case RakeState.Search:
                case RakeState.Stunned:
                case RakeState.Blocking:
                case RakeState.FeignedRetreat:
                case RakeState.Dead:
                    if (animator != null) animator.SetBool("bCrouch", false);
                    if (newState == RakeState.Chase)
                        chaseVocalizationTimer = 0f; // Reset vocalization timer on Chase entry
                    if (newState == RakeState.Blocking)
                    {
                        isBlocking = true;
                        blockingEndTime = Time.time + UnityEngine.Random.Range(blockingDurationMin, blockingDurationMax);
                    }
                    break;
            }

            // The vanilla attack animation is the bAttack flag, not a method call.
            if (animator != null)
            {
                animator.SetBool("bAttack", newState == RakeState.Attack);
            }

            RakeMultiplayerPlugin.Log?.LogInfo($"[AI] State transition: {previousState} -> {currentState}");
        }
        private void MoveToTarget(Vector3 targetPos, bool silent, float speed = -1f)
        {
            var agent = GetEnemyAgent();
            if (agent == null || !agent.isOnNavMesh) return;

            float desired = speed > 0f ? speed : (silent ? GetSneakSpeed() : GetRunSpeed());
            agent.speed = desired;
            agent.stoppingDistance = 0.5f;

            // Do not re-issue the path every frame: that cuts corners and makes the
            // agent stutter. Repath on a timer or when the destination really moved.
            bool destinationMoved = (targetPos - _lastDestination).sqrMagnitude > 4f;
            if (_repathTimer < 0f || Time.time - _repathTimer > 0.45f || destinationMoved)
            {
                agent.SetDestination(targetPos);
                _lastDestination = targetPos;
                _repathTimer = Time.time;
            }
        }

        private bool IsHunterLookingAtRake(Transform hunter)
        {
            if (hunter == null) return false;

            // Get hunter's camera (local player only; remote players won't have Camera component)
            var camera = hunter.GetComponentInChildren<Camera>();
            Vector3 lookOrigin;
            Vector3 lookForward;

            if (camera != null)
            {
                // Local player: use camera position and forward
                lookOrigin = camera.transform.position;
                lookForward = camera.transform.forward;
            }
            else
            {
                // Remote player: fallback to hunter's transform (head/body forward)
                lookOrigin = hunter.position + Vector3.up * 1.6f; // Approximate eye level
                lookForward = hunter.forward;
            }

            Vector3 toRake = (transform.position - lookOrigin).normalized;
            float angle = Vector3.Angle(lookForward, toRake);
            float distance = Vector3.Distance(lookOrigin, transform.position);

            return angle <= stealthLookConeAngle && distance <= stealthLookRange;
        }

        private bool WasHitFromFront()
        {
            // Check if we took damage recently from the front
            if (Time.time - lastDamageTime > DamageTimeWindow) return false; // Only check recent damage
            
            Vector3 toRake = -lastDamageDirection.normalized;
            Vector3 rakeForward = transform.forward;
            
            float angle = Vector3.Angle(rakeForward, toRake);
            return angle < blockingFrontalAngle;
        }

        public float CalculateDamageReduction(Vector3 hitDirection, Vector3 rakeForward)
        {
            // Only an active Blocking state reduces damage
            if (!isBlocking || currentState != RakeState.Blocking) return 1f;

            // hitDirection: direction FROM which the hit came (e.g., shooter to target)
            // rakeForward: Rake's forward vector

            Vector3 toRake = -hitDirection.normalized;
            float angle = Vector3.Angle(rakeForward, toRake);
            
            // Frontal: angle < 45° → 80% reduction (return 0.2f multiplier)
            // Side/back: angle >= 45° → normal damage (return 1.0f multiplier)
            if (angle < blockingFrontalAngle)
            {
                return 1f - blockingDamageReduction; // 0.2f for 80% reduction
            }
            return 1f;
        }

        private void TeleportBehindNearestHunter()
        {
            // Prefer isolated hunter for teleport (consistent with Follow/Stealth targeting)
            Transform targetHunter = isolatedHunter ?? nearestHunter;
            if (targetHunter == null) return;

            Vector3 behindDir = -targetHunter.forward;
            behindDir.y = 0;
            behindDir.Normalize();
            
            float offset = UnityEngine.Random.Range(feignedRetreatTeleportOffsetMin, feignedRetreatTeleportOffsetMax);
            Vector3 teleportPos = targetHunter.position + behindDir * offset;
            
            // Ensure position on NavMesh
            NavMeshHit hit;
            if (NavMesh.SamplePosition(teleportPos, out hit, NavMeshSampleRadius, NavMesh.AllAreas))
            {
                teleportPos = hit.position;
            }

            transform.position = teleportPos;
            targetPosition = teleportPos; // For client interpolation

            var agent = GetEnemyAgent();
            if (agent != null) agent.Warp(teleportPos);

            RakeMultiplayerPlugin.Log?.LogInfo($"[AI] FeignedRetreat teleport behind hunter at {teleportPos}");
        }

        private void PlayChaseVocalization()
        {
            var src = audioSource != null ? audioSource : (enemyAI != null ? enemyAI.audioSource : null);
            if (src == null) return;

            AudioClip[] clips = null;
            if (enemyChaseVocalsField != null)
            {
                clips = enemyChaseVocalsField.GetValue(enemyAI) as AudioClip[];
            }

            // Enemy_AI has no "chaseVocals" field; idle sounds are the closest
            // vanilla set and make the Rake audible while it stalks.
            if ((clips == null || clips.Length == 0) && enemyAI != null)
            {
                clips = enemyAI.idleSounds;
            }

            if (clips != null && clips.Length > 0)
            {
                src.PlayOneShot(clips[UnityEngine.Random.Range(0, clips.Length)], 0.7f);
            }
        }

        // Called by damage system when Rake takes damage
        public void OnDamageTaken(Vector3 hitDirection, int amount, string damageType)
        {
            lastDamageDirection = hitDirection;
            lastDamageTime = Time.time;

            // Player-controlled Rake: getting hit pushes the attack cooldown to 10 s.
            if (_playerControlled) RakePlayerController.NotifyDamageTaken(transform);

            // Apply damage reduction if blocking
            if (isBlocking && WasHitFromFront())
            {
                float reduction = CalculateDamageReduction(hitDirection, transform.forward);
                // The actual damage application happens in RakeNetworkManager.ApplyDamageToEnemy
                // This method is for AI reaction only
                RakeMultiplayerPlugin.Log?.LogInfo($"[AI] Blocking frontal damage! Reduction: {reduction * 100}%");
            }

            // Being hurt while watching from cover means the hunter spotted us:
            // break contact. Sneaking or already charging means attack instead.
            if (currentState == RakeState.Lurk)
            {
                TransitionToState(RakeState.Flee);
            }
            else if (currentState == RakeState.Idle)
            {
                float distance = currentTarget != null
                    ? Vector3.Distance(transform.position, currentTarget.position) : 0f;
                TransitionToState(distance > sneakStartDistance ? RakeState.Flee : RakeState.Chase);
            }
            else if (currentState == RakeState.Stealth || currentState == RakeState.Follow)
            {
                TransitionToState(RakeState.Chase);
            }
        }

        /// <summary>
        /// Snapshot the animator parameters the vanilla AI uses. The host Rake can be
        /// crouching (animal hunt), killing an animal or breaking a camera without any
        /// change in our own state enum, so clients cannot derive this from movement.
        /// </summary>
        private void ReadAnimatorSnapshot(out byte flags, out byte walkType, out float speed)
        {
            flags = 0;
            walkType = 1;
            speed = 0f;

            if (animator == null) return;

            if (animator.GetBool("bCrouch")) flags |= 1;
            if (animator.GetBool("bAttack")) flags |= 2;
            if (animator.GetBool("bAnimalKilling")) flags |= 4;
            if (animator.GetBool("bPlayerAttack")) flags |= 8;
            if (animator.GetBool("bPlayerAppercut")) flags |= 16;
            if (animator.GetBool("bCameraBreak")) flags |= 32;

            walkType = (byte)Mathf.Clamp(animator.GetInteger("WalkType"), 0, 255);
            speed = animator.GetFloat("Speed");
        }

        private void SendRakeState()
        {
            Vector3 currentPos = transform.position;
            byte currentStateByte = (byte)currentState; // Use our enum directly
            float currentHealth = GetCurrentHealth();

            // Delta compression check
            byte animFlags;
            byte walkType;
            float animSpeed;
            ReadAnimatorSnapshot(out animFlags, out walkType, out animSpeed);

            bool posChanged = Vector3.Distance(currentPos, lastSentPosition) > PositionDeltaThreshold;
            bool stateChanged = currentStateByte != lastSentState;
            bool healthChanged = Math.Abs(currentHealth - lastSentHealth) > HealthDeltaThreshold;
            bool animChanged = animFlags != lastSentAnimFlags ||
                               walkType != lastSentWalkType ||
                               Math.Abs(animSpeed - lastSentAnimSpeed) > 0.05f;

            if (!posChanged && !stateChanged && !healthChanged && !animChanged) return;

            var packet = new RakeStatePacket
            {
                Position = currentPos,
                Rotation = transform.rotation,
                State = currentStateByte,
                Health = currentHealth,
                AnimFlags = animFlags,
                WalkType = walkType,
                AnimSpeed = animSpeed
            };

            var data = packet.Serialize();

            // Server broadcasts; a client owner sends its state up to the server.
            var manager = RakeNetworkManager.Instance;
            if (manager != null)
            {
                if (RakeMultiplayerPlugin.isServer) manager.ServerBroadcast(data);
                else if (_localOwner) manager.ClientSend(data);
            }

            lastSentPosition = currentPos;
            lastSentState = currentStateByte;
            lastSentHealth = currentHealth;
            lastSentAnimFlags = animFlags;
            lastSentWalkType = walkType;
            lastSentAnimSpeed = animSpeed;
        }

        private byte GetCurrentStateByte()
        {
            return (byte)currentState;
        }

        private float GetCurrentHealth()
        {
            if (enemyAI == null) return 100f;
            if (enemyHealthField != null)
            {
                return Convert.ToSingle(enemyHealthField.GetValue(enemyAI));
            }
            if (!_loggedHealthWarning)
            {
                _loggedHealthWarning = true;
                RakeMultiplayerPlugin.Log?.LogWarning("[AI] Enemy_AI Health field not found via reflection - using fallback 100f");
            }
            return 100f;
        }

        private float GetMaxHealth()
        {
            if (enemyAI == null) return 100f;
            if (enemyMaxHealthField != null)
            {
                return Convert.ToSingle(enemyMaxHealthField.GetValue(enemyAI));
            }
            // Try common alternative names
            var altField = typeof(Enemy_AI).GetField("maxHealth", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (altField != null)
            {
                return Convert.ToSingle(altField.GetValue(enemyAI));
            }
            if (!_loggedMaxHealthWarning)
            {
                _loggedMaxHealthWarning = true;
                RakeMultiplayerPlugin.Log?.LogWarning("[AI] Enemy_AI MaxHealth field not found via reflection - using fallback 100f");
            }
            return 100f; // Default fallback
        }

        // Client-side apply (for 3.4)
        public void ApplySyncData(Vector3 position, Quaternion rotation, byte state, float health)
        {
            ApplySyncData(position, rotation, state, health, 0, 1, 0f);
            hasAnimState = false; // this overload carries no animator snapshot
        }

        public void ApplySyncData(Vector3 position, Quaternion rotation, byte state, float health,
                                  byte animFlags, byte walkType, float animSpeed)
        {
            if (RakeMultiplayerPlugin.isServer) return; // Don't apply on server
            if (_localOwner) return; // our own body is authoritative on this peer

            // Set interpolation targets instead of direct assignment
            if (!hasTargetPosition || Vector3.Distance(transform.position, position) > snapDistance)
            {
                transform.position = position;
                transform.rotation = rotation;
            }

            targetPosition = position;
            targetRotation = rotation;
            hasTargetPosition = true;
            hasTargetRotation = true;
            targetState = state;
            targetHealth = health;
            targetAnimFlags = animFlags;
            targetWalkType = walkType;
            targetAnimSpeed = animSpeed;
            hasAnimState = true;

            // Apply state/health immediately (they don't interpolate)
            ApplyState(state);
            ApplyHealth(health);
        }

        private void ApplyState(byte state)
        {
            // Update our state machine from network
            RakeState newState = (RakeState)state;
            bool stateChanged = currentState != newState;
            
            if (stateChanged)
            {
                previousState = currentState;
                currentState = newState;
                stateTimer = 0f;
            }

            // State entry effects for clients - ONLY fire triggers on actual state transitions
            // Track lastAppliedState to avoid re-firing triggers on repeated packets
            if (state != lastAppliedState)
            {
                lastAppliedState = state;
                
                switch (newState)
                {
                    case RakeState.Stealth:
                        if (animator != null) animator.SetBool("bCrouch", true);
                        isBlocking = false;
                        break;
                    case RakeState.Follow:
                    case RakeState.Idle:
                    case RakeState.Patrol:
                    case RakeState.Chase:
                    case RakeState.Attack:
                    case RakeState.Search:
                    case RakeState.Stunned:
                    case RakeState.Dead:
                        if (animator != null) animator.SetBool("bCrouch", false);
                        isBlocking = false;
                        break;
                    case RakeState.Blocking:
                        if (animator != null) animator.SetBool("bCrouch", true);
                        isBlocking = true;
                        break;
                    case RakeState.FeignedRetreat:
                        if (animator != null) animator.SetBool("bCrouch", false);
                        isBlocking = false;
                        break;
                    case RakeState.Lurk:
                        // The real pose comes from the animator flags in the snapshot;
                        // this is only the entry default.
                        if (animator != null) animator.SetBool("bCrouch", true);
                        isBlocking = false;
                        break;
                    case RakeState.Flee:
                        if (animator != null) animator.SetBool("bCrouch", false);
                        isBlocking = false;
                        break;
                }
            }
            else if (newState != RakeState.Blocking)
            {
                // Ensure isBlocking is cleared when not in Blocking state (handles packet loss/reordering)
                isBlocking = false;
            }

            // Speed is a real animator parameter on the game's Rake. Store it as a
            // target and let the client update blend towards it.
            targetSpeed = newState == RakeState.Chase ? 2f
                        : newState == RakeState.Follow ? 1f
                        : 0f;
        }

        public void ApplyHealth(float health)
        {
            if (enemyAI != null)
            {
                var healthField = typeof(Enemy_AI).GetField("Health", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (healthField != null)
                {
                    healthField.SetValue(enemyAI, health);
                }
            }
        }
    }
}