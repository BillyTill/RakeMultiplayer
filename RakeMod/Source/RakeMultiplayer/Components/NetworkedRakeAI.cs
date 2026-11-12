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
            FeignedRetreat = 10
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
            enemyAgentField = type.GetField("agent", flags);
            enemyChaseVocalsField = type.GetField("chaseVocals", flags);
            enemyAttackMethod = type.GetMethod("Attack", flags);
            enemyTakeDamageMethod = type.GetMethod("TakeDamage", flags);

            // Cache original agent speed for restoration after silent movement
            if (enemyAgentField != null)
            {
                var agent = enemyAgentField.GetValue(enemyAI) as NavMeshAgent;
                if (agent != null)
                {
                    originalAgentSpeed = agent.speed;
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

            // Initial state: Follow at session start
            if (RakeMultiplayerPlugin.isServer)
            {
                TransitionToState(RakeState.Follow);
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

                UpdateDaySpawn();

                if (!RakeMultiplayerPlugin.isConnected) return;

                if (!_playerControlled)
                {
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

                // Client: interpolate to target values
                if (!RakeMultiplayerPlugin.isConnected) return;

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

        public void SetPlayerControlled(bool value)
        {
            if (_playerControlled == value) return;
            _playerControlled = value;

            if (value)
            {
                if (enemyAI != null) enemyAI.enabled = false;
                var agent = GetEnemyAgent();
                if (agent != null)
                {
                    agent.Stop();
                    agent.ResetPath();
                }
                currentState = RakeState.Chase;
                RakeMultiplayerPlugin.Log?.LogInfo("[RakePlay] Rake switched to player control");
            }
            else
            {
                if (RakeMultiplayerPlugin.isServer && enemyAI != null) enemyAI.enabled = true;
                RakeMultiplayerPlugin.Log?.LogInfo("[RakePlay] Rake returned to AI");
            }
        }

        /// <summary>Player-controlled Rake hit a player (same damage path as the AI).</summary>
        public bool AttackPlayer(Transform victim)
        {
            if (victim == null) return false;
            DealRakeDamage(victim);
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

            // Include inactive objects: the level keeps its markers disabled.
            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();

            for (int n = 0; n < names.Length; n++)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    Transform candidate = all[i];
                    if (candidate == null) continue;

                    string name = candidate.gameObject.name.ToLowerInvariant();
                    if (!name.Contains(names[n])) continue;

                    // Skip markers outside the playable map (the staging platform).
                    float distance = NearestPlayerDistance(candidate.position);
                    if (distance > 0f && distance > 1000f) continue;

                    position = candidate.position;
                    source = candidate.gameObject.name;
                    return true;
                }
            }

            MapSettings settings = MapSettings.Current;
            if (settings != null && settings.hasCave)
            {
                position = settings.cavePosition;
                source = "MapSettings cave";
                return true;
            }

            // Interior of the map, away from the player spawn.
            if (settings != null && settings.spawnPoints != null && settings.spawnPoints.Length > 0)
            {
                position = settings.spawnPoints[0] + new Vector3(60f, 0f, 60f);
                source = "player spawn + 60m";
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
            stateTimer += Time.deltaTime;
            feignedRetreatCooldownTimer = Mathf.Max(0f, feignedRetreatCooldownTimer - Time.deltaTime);

            // Check for death
            float health = GetCurrentHealth();
            if (health <= 0f && currentState != RakeState.Dead)
            {
                TransitionToState(RakeState.Dead);
                return;
            }

            // Check for stunned (trap trigger) - could be expanded with actual trap detection
            // For now, we'll assume external code sets a stunned flag on Enemy_AI

            // State-specific updates
            switch (currentState)
            {
                case RakeState.Idle:
                    UpdateIdle();
                    break;
                case RakeState.Follow:
                    UpdateFollow();
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

            // Animator is driven by the game's own Enemy_AI parameters (WalkType/Speed/bCrouch).
        }

        private void UpdateIdle()
        {
            // Idle -> Follow (session start)
            TransitionToState(RakeState.Follow);
        }

        private void UpdateFollow()
        {
            if (currentTarget == null)
            {
                // No hunters found, stay idle
                return;
            }

            // Silent movement - no footstep sounds
            // Maintain 15-25m distance from nearest hunter
            float distToNearest = Vector3.Distance(transform.position, currentTarget.position);
            
            Vector3 targetPos;
            if (distToNearest < followMinDistance)
            {
                // Too close - move away
                Vector3 awayDir = (transform.position - currentTarget.position).normalized;
                targetPos = currentTarget.position + awayDir * followMaxDistance;
            }
            else if (distToNearest > followMaxDistance)
            {
                // Too far - move closer
                Vector3 towardDir = (currentTarget.position - transform.position).normalized;
                targetPos = transform.position + towardDir * (distToNearest - followMaxDistance * 0.5f);
            }
            else
            {
                // In range - circle around the current target
                Vector3 toTarget = (currentTarget.position - transform.position).normalized;
                Vector3 perpendicular = Vector3.Cross(toTarget, Vector3.up).normalized;
                targetPos = currentTarget.position + perpendicular * followMinDistance;
            }

            // Avoid line of sight using raycasts
            if (Physics.Raycast(transform.position, (targetPos - transform.position).normalized, 
                out RaycastHit hit, Vector3.Distance(transform.position, targetPos), 
                LayerMask.GetMask("Default", "Terrain")))
            {
                // Obstacle in way - adjust path
                targetPos = hit.point + hit.normal * 2f;
            }

            // Move towards target position silently
            MoveToTarget(targetPos, silent: true);

            // Face the current target
            if (currentTarget != null)
            {
                Vector3 lookDir = (currentTarget.position - transform.position).normalized;
                lookDir.y = 0;
                if (lookDir != Vector3.zero)
                {
                    FaceTarget(lookDir, 5f);
                }
            }

            // Transition checks
            float distToIsolated = currentTarget != null ?
                Vector3.Distance(transform.position, currentTarget.position) : float.MaxValue;

            // Follow -> Stealth: close enough (< 20m) and not seen
            if (distToIsolated < stealthTransitionDistance && !IsHunterLookingAtRake(currentTarget))
            {
                TransitionToState(RakeState.Stealth);
                return;
            }

            // Follow -> Chase: spotted by any hunter
            foreach (var hunter in hunters)
            {
                if (IsHunterLookingAtRake(hunter))
                {
                    TransitionToState(RakeState.Chase);
                    return;
                }
            }
        }

        private void UpdateStealth()
        {
            // Cache currentTarget to avoid race condition with UpdateHunterList
            var target = currentTarget;
            if (target == null)
            {
                TransitionToState(RakeState.Follow);
                return;
            }

            // Maintain stealth position behind/around target
            Vector3 toTarget = (target.position - transform.position).normalized;
            Vector3 stealthPos = target.position - toTarget * 10f + Vector3.up * 0.5f;
            
            // Silent movement
            MoveToTarget(stealthPos, silent: true);

            // Face target
            Vector3 lookDir = (target.position - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                FaceTarget(lookDir, 3f);
            }

            // Hunter look detection: raycast from Camera.main, 30° cone, 20m range
            if (IsHunterLookingAtRake(currentTarget))
            {
                TransitionToState(RakeState.Chase);
                return;
            }

            // Stealth -> Follow: losing track of hunters
            if (currentTarget == null || Vector3.Distance(transform.position, currentTarget.position) > stealthLookRange * 1.5f)
            {
                TransitionToState(RakeState.Follow);
                return;
            }

            // Stealth -> Chase: close enough to attack
            if (Vector3.Distance(transform.position, currentTarget.position) < attackRange)
            {
                TransitionToState(RakeState.Attack);
                return;
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
                
                // FeignedRetreat trigger: health < 30% AND no target for 3s
                float health = GetCurrentHealth();
                float maxHealth = GetMaxHealth();
                if (maxHealth > 0f && health / maxHealth < feignedRetreatHealthThreshold && noTargetTimer >= feignedRetreatNoTargetTime)
                {
                    if (feignedRetreatCooldownTimer <= 0f)
                    {
                        TransitionToState(RakeState.FeignedRetreat);
                        return;
                    }
                }
                
                // Search for target
                if (noTargetTimer > 5f)
                {
                    TransitionToState(RakeState.Search);
                }
                return;
            }
            else
            {
                noTargetTimer = 0f;
            }

            // Direct pathfinding to target
            MoveToTarget(currentTarget.position, silent: false);

            // Face target
            Vector3 lookDir = (currentTarget.position - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                FaceTarget(lookDir, 10f);
            }

            // Check for frontal damage (Blocking trigger)
            if (WasHitFromFront())
            {
                TransitionToState(RakeState.Blocking);
                return;
            }

            // Chase -> Attack: close to target
            if (Vector3.Distance(transform.position, currentTarget.position) < attackRange)
            {
                TransitionToState(RakeState.Attack);
                return;
            }

            // Chase -> FeignedRetreat: health < 30% & no target for 3s (handled above)
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
            lookDir.y = 0;
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

            // One hit per AttackInterval while the target stays in range
            _attackCooldown -= Time.deltaTime;
            if (_attackCooldown <= 0f)
            {
                _attackCooldown = AttackInterval;
                DealRakeDamage(currentTarget);
            }

            // Attack -> Chase: target escaped (moved out of range)
            if (Vector3.Distance(transform.position, currentTarget.position) > attackRange * 2f)
            {
                TransitionToState(RakeState.Chase);
                return;
            }
        }

        /// <summary>
        /// Apply one Rake hit. The host damages its own player directly; for a client the
        /// victim gets a PlayerDamaged packet and applies it locally, so the vanilla
        /// damage effects and the ragdoll hit reaction run on every peer.
        /// </summary>
        private void DealRakeDamage(Transform victim)
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
                if (fps != null) fps.TakeDamage(AttackDamage, AttackDamageType);

                PlayerRagdoll.BroadcastOnly(net.PlayerId, away);

                RakeMultiplayerPlugin.Log?.LogInfo("[AI] Rake hit the host for " + AttackDamage);
                return;
            }

            if (net.PlayerId <= 0) return;

            var packet = new PlayerDamagedPacket
            {
                PlayerId = net.PlayerId,
                Amount = AttackDamage,
                DamageType = AttackDamageType,
                SourcePosition = transform.position
            };
            RakeNetworkManager.Instance?.ServerBroadcast(packet.Serialize());

            // The victim plays its own pain sound; the host needs the clone's one.
            PlayerAudio.PlayPain(victim);

            // Ragdoll on the host and on every client
            PlayerRagdoll.TriggerNetworked(net.PlayerId, away);

            RakeMultiplayerPlugin.Log?.LogInfo("[AI] Rake hit player " + net.PlayerId +
                                              " for " + AttackDamage);
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
            // Simple search pattern - could be expanded
            if (stateTimer > 10f)
            {
                TransitionToState(RakeState.Follow);
            }
        }

        private void UpdateStunned()
        {
            // Stun is applied by traps (Task 3.11); auto-recover after stunDuration
            if (stateTimer > stunDuration)
            {
                SetStunnedPhysics(false);
                TransitionToState(RakeState.Follow);
            }
        }

        /// <summary>Freeze/resume the vanilla Enemy_AI and its NavMeshAgent during a stun.</summary>
        private void SetStunnedPhysics(bool stunned)
        {
            var agent = GetEnemyAgent();
            if (agent != null && agent.isOnNavMesh)
            {
                if (stunned) agent.Stop();
                else agent.Resume();
            }

            if (enemyAI != null)
            {
                enemyAI.enabled = !stunned;
            }
        }

        private NavMeshAgent GetEnemyAgent()
        {
            if (enemyAI == null || enemyAgentField == null) return null;
            return enemyAgentField.GetValue(enemyAI) as NavMeshAgent;
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

            // State entry actions
            switch (newState)
            {
                case RakeState.Stealth:
                    if (animator != null) animator.SetBool("bCrouch", true);
                    break;
                case RakeState.Follow:
                case RakeState.Idle:
                case RakeState.Patrol:
                case RakeState.Chase:
                case RakeState.Attack:
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

        private void MoveToTarget(Vector3 targetPos, bool silent)
        {
            if (enemyAI == null) return;

            // Use Enemy_AI's movement system via reflection (cached)
            if (enemyAgentField != null)
            {
                var agent = enemyAgentField.GetValue(enemyAI) as NavMeshAgent;
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.SetDestination(targetPos);
                    
                    // Silent movement: reduce speed to minimize footstep sounds
                    if (silent)
                    {
                        agent.speed = Mathf.Min(originalAgentSpeed, 2f); // Slow, quiet movement
                    }
                    else
                    {
                        // Restore original speed from Enemy_AI
                        agent.speed = originalAgentSpeed;
                    }
                }
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
            if (audioSource != null && enemyChaseVocalsField != null)
            {
                var clips = enemyChaseVocalsField.GetValue(enemyAI) as AudioClip[];
                if (clips != null && clips.Length > 0)
                {
                    audioSource.PlayOneShot(clips[UnityEngine.Random.Range(0, clips.Length)]);
                }
            }
        }

        // Called by damage system when Rake takes damage
        public void OnDamageTaken(Vector3 hitDirection, int amount, string damageType)
        {
            lastDamageDirection = hitDirection;
            lastDamageTime = Time.time;

            // Apply damage reduction if blocking
            if (isBlocking && WasHitFromFront())
            {
                float reduction = CalculateDamageReduction(hitDirection, transform.forward);
                // The actual damage application happens in RakeNetworkManager.ApplyDamageToEnemy
                // This method is for AI reaction only
                RakeMultiplayerPlugin.Log?.LogInfo($"[AI] Blocking frontal damage! Reduction: {reduction * 100}%");
            }

            // Stealth -> Chase on damage
            if (currentState == RakeState.Stealth)
            {
                TransitionToState(RakeState.Chase);
            }
            // Follow -> Chase on damage
            else if (currentState == RakeState.Follow)
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

            // Broadcast to all clients
            var manager = RakeNetworkManager.Instance;
            if (manager != null)
            {
                manager.ServerBroadcast(data);
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