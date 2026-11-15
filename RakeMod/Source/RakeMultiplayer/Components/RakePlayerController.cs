using System.Collections.Generic;
using UnityEngine;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    /// <summary>
    /// "Play as the Rake" mode (host or single-player).
    ///
    /// Third/first-person camera, WASD movement with a silent crouch and a run, and
    /// automatic attacks on whatever stands in front of the model. Attacks are only
    /// allowed at night: during the day the Rake spawns as a threat that cannot hit
    /// anything yet.
    ///
    /// The Rake player also gets a "sense": a large ring in the middle of the screen,
    /// top = forward, bottom = backward, with identical arrows for animals, players and
    /// traps within 100 m, so it cannot tell a trap from prey.
    /// </summary>
    public class RakePlayerController : MonoBehaviour
    {
        public static bool IsActive { get; private set; }
        public static Transform LocalHunter { get; private set; }

        private const float WalkSpeed = 6.75f;
        private const float RunSpeed = 15.0f;
        private const float StealthSpeed = 3.75f;

        private const float AttackRange = 3.4f;
        private const float AttackCone = 70f;

        private const float PlayerCooldown = 20f;
        private const float AnimalCooldown = 10f;
        private const float CameraCooldown = 50f;

        /// <summary>After taking damage every attack cooldown is raised to at least this.</summary>
        private const float DamageAttackLockout = 10f;

        private const float JumpDistance = 10f;
        private const float JumpHeight = 1.0f;
        private const float JumpCooldown = 50f;

        private const float ClimbSpeed = 3.6f;

        /// <summary>After a deliberate drop the Rake cannot re-grab the same wall for this long.</summary>
        private const float ClimbReleaseTime = 2f;

        /// <summary>Radius of the volumetric wall probe.</summary>
        private const float ClimbProbeRadius = 0.25f;
        private const float ClimbProbeDistance = 1.15f;
        private const float ClimbChestHeight = 1.25f;
        private const float ClimbKneeHeight = 0.55f;

        /// <summary>The knee probe reaches further: the edge of a ledge is often just out of range.</summary>
        private const float ClimbKneeProbeDistance = 1.45f;

        /// <summary>
        /// How fast the stored wall normal follows the measured one (1/s). A round trunk
        /// reports a normal that swings by ±15° from frame to frame; following it raw
        /// snapped the body left and right instead of climbing.
        /// </summary>
        private const float ClimbNormalSmoothing = 7f;

        /// <summary>A normal that jumps further than this belongs to another surface, not to the wall.</summary>
        private const float ClimbNormalMaxJump = 60f;

        /// <summary>How fast the body turns to face the wall while climbing (1/s).</summary>
        private const float ClimbTurnSpeed = 9f;

        /// <summary>
        /// Probe failures shorter than this do not drop the wall. Thin trunks and branch
        /// colliders blink in and out of the sphere, and every blink used to become a
        /// visible grab/drop/grab cycle.
        /// </summary>
        private const float ClimbLostGrace = 0.4f;

        /// <summary>Time spent pulling the body over a ledge.</summary>
        private const float ClimbMantleTime = 0.5f;

        private const float SenseRange = 100f;

        /// <summary>The Rake sees much wider than a hunter.</summary>
        private const float FirstPersonFov = 105f;

        /// <summary>How far in front of the head bone the eye sits, so it is not inside the skull.</summary>
        private const float FirstPersonForward = 0.38f;
        private const float ThirdPersonFov = 68f;

        /// <summary>
        /// Distance that counts as "outside the level". The day staging platform sits far
        /// from every hunter, so a player-controlled Rake beyond this must be moved back.
        /// </summary>
        private const float StagingPlatformDistance = 400f;

        /// <summary>
        /// Safety net during play: only a body this far from every hunter is certainly
        /// back on the level's staging platform rather than legitimately stalking.
        /// </summary>
        private const float StagingSafetyDistance = 1200f;

        private static RakePlayerController _instance;

        private Transform _rake;
        private NetworkedRakeAI _rakeAI;
        private Enemy_AI _enemyAI;
        private NavMeshAgent _agent;
        private Animator _animator;
        private AnimatorCullingMode _animatorCullingWas = AnimatorCullingMode.AlwaysAnimate;
        private AudioSource _audio;

        private Camera _camera;
        private float _yaw;
        private float _pitch;
        private float _distance = 4.6f;
        private bool _firstPerson;

        // First-person camera takes its position from the head bone (real bob) but keeps a
        // fixed world orientation, so the head's animated rotation does not swing the view.
        private Transform _head;

        private readonly List<Renderer> _rakeRenderers = new List<Renderer>();
        private bool _rakeVisible = true;

        private Transform _hunter;
        private CharacterController _hunterController;
        private bool _hunterControllerWasEnabled;
        private AudioListener _hunterListener;
        private bool _hunterListenerWasEnabled;
        private float _stepTimer;
        private float _hunterRetry;
        private bool _hunterFpsWasEnabled;
        private bool _hunterMovementWasEnabled;
        private bool _hunterFpsCameraWasEnabled;
        private bool _hunterInventoryWasEnabled;
        private GameObject _hunterCanvas;
        private bool _hunterCanvasWasActive;
        private Light _hunterFlashlight;
        private bool _hunterFlashlightWasEnabled;
        private bool _enemyWasEnabled;
        private bool _agentRotationWas = true;
        private bool _agentWasEnabled = true;
        private CharacterController _rakeController;
        private bool _rakeControllerAdded;
        private bool _rakeControllerWasEnabled;
        private float _verticalVelocity;
        private readonly List<Collider> _disabledColliders = new List<Collider>();
        private bool _rootMotionWas = true;

        private readonly List<Camera> _hiddenCameras = new List<Camera>();
        private readonly List<Renderer> _hiddenRenderers = new List<Renderer>();

        private float _playerCooldown;
        private float _animalCooldown;
        private float _cameraCooldown;
        private float _attackAnimation;
        private bool _actionLocked;

        private float _jumpCooldown;
        private float _jumpElapsed;
        private float _jumpSpeed;
        private Vector3 _jumpDirection;
        private bool _jumpActive;

        private bool _climbing;
        private UI.RakeHudUI _hudUi;
        private float _damageLockout;
        private bool _preyInReach;
        private Vector3 _climbNormal = Vector3.forward;
        private Vector3 _climbDir = Vector3.forward;
        private Vector3 _climbMove;
        private float _mantleTimer;
        private float _climbLostTimer;
        private float _climbReleaseTimer;
        private bool _bridgeForward;
        private bool _bridgeSpace;
        private float _climbVertical;

        private float _stagingCheck;
        private bool _spawnChecked;
        private float _scanTimer;
        private readonly List<NetworkedPlayer> _players = new List<NetworkedPlayer>();
        private readonly List<Animal_AI> _animals = new List<Animal_AI>();
        private readonly List<Trap_proj> _traps = new List<Trap_proj>();

        // ------------------------------------------------------------ static control

        public static void Toggle()
        {
            SetEnabled(!IsActive);
        }

        public static void SetEnabled(bool enable)
        {
            if (enable)
            {
                if (IsActive) return;

                if (RakeMultiplayerPlugin.isConnected &&
                    LobbyState.RakeOwnerId != LobbyState.LocalPlayerId)
                {
                    DebugConsole.Print("rake play: you are not the Rake in this session");
                    return;
                }

                Transform rake = RakeHighlight.FindRake();
                if (rake == null)
                {
                    DebugConsole.Print("rake play: Rake not found in this scene");
                    return;
                }

                var go = new GameObject("RakePlayerController");
                var controller = go.AddComponent<RakePlayerController>();
                if (!controller.Begin(rake))
                {
                    UnityEngine.Object.Destroy(go);
                    DebugConsole.Print("rake play: could not take control of the Rake");
                    return;
                }

                _instance = controller;
                IsActive = true;
                DebugConsole.Print("rake play ON: WASD move, Shift run, Ctrl sneak, V view. " +
                                   "Auto-attack in front of the Rake; attacks only work at night.");
            }
            else
            {
                if (_instance != null)
                {
                    _instance.End();
                    UnityEngine.Object.Destroy(_instance.gameObject);
                }
                _instance = null;
                IsActive = false;
                DebugConsole.Print("rake play OFF");
            }
        }

        // --------------------------------------------------------------- setup/teardown

        private bool Begin(Transform rake)
        {
            _rake = rake;
            _rakeAI = rake.GetComponent<NetworkedRakeAI>();
            _enemyAI = rake.GetComponent<Enemy_AI>();
            _animator = rake.GetComponent<Animator>();
            if (_animator == null) _animator = rake.GetComponentInChildren<Animator>();

            if (_animator != null)
            {
                // In first person the Rake's renderers are hidden; with "based on renderers"
                // culling the skeleton stopped updating and the head-locked camera froze in
                // place. Keep the pose alive so the head bob is real.
                _animatorCullingWas = _animator.cullingMode;
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            _audio = rake.GetComponent<AudioSource>();
            if (_audio == null && _enemyAI != null) _audio = _enemyAI.audioSource;

            _agent = rake.GetComponent<NavMeshAgent>();
            if (_agent == null) _agent = rake.GetComponentInChildren<NavMeshAgent>();

            if (_rakeAI == null || _animator == null) return false;

            _rakeRenderers.Clear();
            Renderer[] rakeRenderers = rake.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rakeRenderers.Length; i++)
            {
                if (rakeRenderers[i] != null) _rakeRenderers.Add(rakeRenderers[i]);
            }
            _rakeVisible = true;

            Camera source = Camera.main;
            if (source == null)
            {
                var spawner = PlayerSpawner.Instance;
                var local = spawner != null ? spawner.LocalPlayer : null;
                if (local != null) source = local.GetComponentInChildren<Camera>();
            }

            _enemyWasEnabled = _enemyAI != null && _enemyAI.enabled;
            if (_enemyAI != null) _enemyAI.enabled = false;

            // Attack clips carry root motion; with the AI off it would drag the body sideways.
            _rootMotionWas = _animator.applyRootMotion;
            _animator.applyRootMotion = false;

            if (_agent != null)
            {
                _agentWasEnabled = _agent.enabled;
                _agentRotationWas = _agent.updateRotation;
                _agent.updateRotation = false; // we rotate the body ourselves
                _agent.Stop();
                _agent.ResetPath();

                // The level NavMesh only covers the staging platform; the body is
                // driven by a CharacterController instead.
                _agent.enabled = false;
            }

            SetupCharacterController();

            _rakeAI.SetPlayerControlled(true);
            if (!RakeMultiplayerPlugin.isServer) _rakeAI.SetLocalOwner(true);

            // The level keeps its Rake on a staging platform under the map during the day.
            // The AI day-spawn routine is skipped once a player takes over, so the player
            // would otherwise be left standing on that white platform outside the forest.
            WarpOffStagingPlatform(StagingPlatformDistance);

            HideHunter();

            var camGo = new GameObject("RakePlayerCamera");
            camGo.AddComponent<AudioListener>();
            _camera = camGo.AddComponent<Camera>();
            if (source != null)
            {
                _camera.CopyFrom(source);
                _camera.depth = source.depth + 1f;
            }
            _camera.nearClipPlane = 0.05f;

            // The graphics preset owns the draw distance; the Rake camera is created after
            // the level loaded, so it would otherwise keep the vanilla clip plane.
            _camera.farClipPlane = GraphicsProfiles.Current.FarClip > 0f
                ? GraphicsProfiles.Current.FarClip
                : _camera.farClipPlane;

            _camera.enabled = true;

            _camera.fieldOfView = ThirdPersonFov;
            _head = FindHeadBone(rake);

            _yaw = rake.eulerAngles.y;
            _pitch = 12f;
            UpdateCamera(0f);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _playerCooldown = 0f;
            _animalCooldown = 0f;
            _cameraCooldown = 0f;

            _hudUi = UI.RakeHudUI.Create(this);

            RakeMultiplayerPlugin.Log?.LogInfo("[RakePlay] Player took control of the Rake at " +
                                               rake.position.ToString("F1"));
            return true;
        }

        /// <summary>
        /// Player-controlled Rake: move the body from the level's day staging platform
        /// onto playable ground. Called on takeover and, throttled, during play in case
        /// the level parks the body again after a phase change.
        /// </summary>
        private void WarpOffStagingPlatform(float maxDistance)
        {
            if (_rakeAI == null || _rake == null) return;

            if (!_spawnChecked)
            {
                _spawnChecked = true;
                RakeMultiplayerPlugin.Log?.LogInfo("[RakePlay] Spawn check: body " +
                                                   _rake.position.ToString("F1") + ", nearest hunter " +
                                                   _rakeAI.DistanceToNearestHunter().ToString("F0") +
                                                   " m, phase=" + CurrentPhaseName());
            }

            if (!_rakeAI.IsOutsidePlayableArea(maxDistance)) return;

            bool wasEnabled = _rakeController != null && _rakeController.enabled;
            if (wasEnabled) _rakeController.enabled = false;

            bool moved = _rakeAI.WarpToPlayableGround();

            if (wasEnabled && _rakeController != null) _rakeController.enabled = true;
            if (!moved) return;

            _verticalVelocity = 0f;
            _climbing = false;
            _jumpActive = false;
            _climbMove = Vector3.zero;
            _yaw = _rake.eulerAngles.y;
        }

        private static string CurrentPhaseName()
        {
            DayNightManager dayNight = DayNightManager.Instance;
            if (dayNight == null) return "none";
            return dayNight.CurrentPhase == Phase.Day ? "day" : "night";
        }

        private void SetupCharacterController()
        {
            _rakeController = _rake.GetComponent<CharacterController>();
            if (_rakeController == null)
            {
                _rakeController = _rake.gameObject.AddComponent<CharacterController>();
                _rakeControllerAdded = true;
            }

            _rakeControllerWasEnabled = _rakeController.enabled;
            _rakeController.height = 2.0f;
            _rakeController.radius = 0.45f;
            _rakeController.center = new Vector3(0f, 1.0f, 0f);
            _rakeController.slopeLimit = 60f;
            _rakeController.stepOffset = 0.55f;
            _rakeController.enabled = true;

            _verticalVelocity = 0f;

            // Avoid self-collision with the Rake's own trigger/colliders.
            Collider[] colliders = _rake.GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (col == null || col == _rakeController || !col.enabled) continue;
                col.enabled = false;
                _disabledColliders.Add(col);
            }
        }

        private void HideHunter()
        {
            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;
            if (local == null) return;

            _hunter = local.transform;
            LocalHunter = _hunter;

            var fps = _hunter.GetComponent<FPSController>();
            if (fps != null)
            {
                _hunterFpsWasEnabled = fps.enabled;
                fps.enabled = false;

                // The hunter's flashlight and weapon must not keep glowing/floating
                // around the level while its body is hidden.
                fps.HidePlayerWeapon();

                if (fps.flashlight != null)
                {
                    _hunterFlashlight = fps.flashlight;
                    _hunterFlashlightWasEnabled = fps.flashlight.enabled;
                    fps.flashlight.enabled = false;
                }

                if (fps.playerCanvas != null)
                {
                    _hunterCanvas = fps.playerCanvas;
                    _hunterCanvasWasActive = fps.playerCanvas.activeSelf;
                    fps.playerCanvas.SetActive(false);
                }
            }

            var movement = _hunter.GetComponent<PlayerMovement>();
            if (movement != null)
            {
                _hunterMovementWasEnabled = movement.enabled;
                movement.enabled = false;
            }

            var listeners = _hunter.GetComponentsInChildren<AudioListener>(true);
            _hunterListener = listeners.Length > 0 ? listeners[0] : null;
            if (_hunterListener != null)
            {
                _hunterListenerWasEnabled = _hunterListener.enabled;
                _hunterListener.enabled = false;
            }

            // The Rake must not pick items up: the hunter camera does the pickup raycast.
            var fpsCameras = _hunter.GetComponentsInChildren<FPSCamera>(true);
            if (fpsCameras.Length > 0 && fpsCameras[0] != null)
            {
                _hunterFpsCameraWasEnabled = fpsCameras[0].enabled;
                fpsCameras[0].enabled = false;
            }

            var inventories = _hunter.GetComponentsInChildren<Inventory>(true);
            if (inventories.Length > 0 && inventories[0] != null)
            {
                _hunterInventoryWasEnabled = inventories[0].enabled;
                inventories[0].enabled = false;
            }

            _hunterController = _hunter.GetComponent<CharacterController>();
            if (_hunterController != null)
            {
                _hunterControllerWasEnabled = _hunterController.enabled;
                _hunterController.enabled = false;
            }

            var cameras = _hunter.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] == null || !cameras[i].enabled) continue;
                cameras[i].enabled = false;
                _hiddenCameras.Add(cameras[i]);
            }

            var renderers = _hunter.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || !renderers[i].enabled) continue;
                renderers[i].enabled = false;
                _hiddenRenderers.Add(renderers[i]);
            }
        }

        private void End()
        {
            if (_hudUi != null)
            {
                UnityEngine.Object.Destroy(_hudUi.gameObject);
                _hudUi = null;
            }

            SetRakeVisible(true);

            if (_animator != null) _animator.cullingMode = _animatorCullingWas;

            if (_camera != null) UnityEngine.Object.Destroy(_camera.gameObject);
            _camera = null;

            for (int i = 0; i < _hiddenCameras.Count; i++)
            {
                if (_hiddenCameras[i] != null) _hiddenCameras[i].enabled = true;
            }
            _hiddenCameras.Clear();

            for (int i = 0; i < _hiddenRenderers.Count; i++)
            {
                if (_hiddenRenderers[i] != null) _hiddenRenderers[i].enabled = true;
            }
            _hiddenRenderers.Clear();

            if (_hunterListener != null) _hunterListener.enabled = _hunterListenerWasEnabled;

            if (_hunterFlashlight != null)
            {
                _hunterFlashlight.enabled = _hunterFlashlightWasEnabled;
                _hunterFlashlight = null;
            }

            if (_hunterCanvas != null)
            {
                _hunterCanvas.SetActive(_hunterCanvasWasActive);
                _hunterCanvas = null;
            }

            if (_hunter != null)
            {
                var fps = _hunter.GetComponent<FPSController>();
                if (fps != null) fps.enabled = _hunterFpsWasEnabled;

                var movement = _hunter.GetComponent<PlayerMovement>();
                if (movement != null) movement.enabled = _hunterMovementWasEnabled;

                if (_hunterController != null) _hunterController.enabled = _hunterControllerWasEnabled;

                var fpsCameras = _hunter.GetComponentsInChildren<FPSCamera>(true);
                if (fpsCameras.Length > 0 && fpsCameras[0] != null) fpsCameras[0].enabled = _hunterFpsCameraWasEnabled;

                var inventories = _hunter.GetComponentsInChildren<Inventory>(true);
                if (inventories.Length > 0 && inventories[0] != null) inventories[0].enabled = _hunterInventoryWasEnabled;
            }

            if (_enemyAI != null) _enemyAI.enabled = _enemyWasEnabled;
            if (_animator != null) _animator.applyRootMotion = _rootMotionWas;
            if (_rakeAI != null)
            {
                if (!RakeMultiplayerPlugin.isServer) _rakeAI.SetLocalOwner(false);
                _rakeAI.SetPlayerControlled(false);
            }
            if (_rakeController != null)
            {
                if (_rakeControllerAdded) UnityEngine.Object.Destroy(_rakeController);
                else _rakeController.enabled = _rakeControllerWasEnabled;
                _rakeController = null;
            }

            for (int i = 0; i < _disabledColliders.Count; i++)
            {
                if (_disabledColliders[i] != null) _disabledColliders[i].enabled = true;
            }
            _disabledColliders.Clear();

            if (_agent != null)
            {
                _agent.updateRotation = _agentRotationWas;
                _agent.enabled = _agentWasEnabled;
            }

            _hunter = null;
            LocalHunter = null;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                IsActive = false;
                LocalHunter = null;
            }
        }

        // -------------------------------------------------------------------- update

        private void Update()
        {
            if (_rake == null)
            {
                End();
                UnityEngine.Object.Destroy(gameObject);
                return;
            }

            // The F1 menu and the console own the input while they are open.
            if (DebugConsole.ConsoleOpen || UI.MultiplayerMenu.IsOpen) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _playerCooldown = Mathf.Max(0f, _playerCooldown - dt);
            _animalCooldown = Mathf.Max(0f, _animalCooldown - dt);
            _cameraCooldown = Mathf.Max(0f, _cameraCooldown - dt);
            _jumpCooldown = Mathf.Max(0f, _jumpCooldown - dt);
            _damageLockout = Mathf.Max(0f, _damageLockout - dt);
            // A stuck or NaN release timer used to lock the Rake out of every wall for the
            // rest of the match; clamp it instead of trusting the value.
            _climbReleaseTimer -= dt;
            if (!(_climbReleaseTimer > 0f)) _climbReleaseTimer = 0f;
            else if (_climbReleaseTimer > ClimbReleaseTime) _climbReleaseTimer = ClimbReleaseTime;
            _attackAnimation = Mathf.Max(0f, _attackAnimation - dt);

            if (!DebugConsole.ConsoleOpen && !_actionLocked)
            {
                if (Input.GetKeyDown(KeyCode.V)) _firstPerson = !_firstPerson;

                ReadLook();
                ReadMovement(dt);
            }
            else if (_actionLocked && _animator != null)
            {
                _animator.SetFloat("Speed", 0f);
            }

            if (_hunter == null)
            {
                _hunterRetry -= dt;
                if (_hunterRetry <= 0f)
                {
                    _hunterRetry = 0.5f;
                    HideHunter(); // the local player may not exist yet right after a scene load
                }
            }

            // The level may re-park the body on the staging platform between phases.
            _stagingCheck -= dt;
            if (_stagingCheck <= 0f)
            {
                _stagingCheck = 2f;
                WarpOffStagingPlatform(StagingSafetyDistance);
            }

            UpdateCamera(dt);

            _scanTimer -= dt;
            if (_scanTimer <= 0f)
            {
                _scanTimer = 0.35f;
                RefreshTargets();
            }

            if (AttacksAllowed() && !DebugConsole.ConsoleOpen && !_jumpActive && !_climbing) TryAutoAttack();
        }

        private void ReadLook()
        {
            const float sensitivity = 2.5f;
            _yaw += Input.GetAxis("Mouse X") * sensitivity;
            _pitch -= Input.GetAxis("Mouse Y") * sensitivity;
            _pitch = Mathf.Clamp(_pitch, -70f, 65f);
        }

        private void ReadMovement(float dt)
        {
            bool run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool stealth = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            float speed = stealth ? StealthSpeed : (run ? RunSpeed : WalkSpeed);

            bool spacePressed = Input.GetKeyDown(KeyCode.Space) || _bridgeSpace;
            _bridgeSpace = false;
            if (spacePressed)
            {
                // Space always lets go of a wall, even while the jump is cooling down.
                if (_climbing) DetachFromWall();
                else TryJump();
            }

            Vector3 input = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || _bridgeForward) input += Vector3.forward;
            if (Input.GetKey(KeyCode.S)) input += Vector3.back;
            if (Input.GetKey(KeyCode.A)) input += Vector3.left;
            if (Input.GetKey(KeyCode.D)) input += Vector3.right;

            Vector3 direction = Quaternion.Euler(0f, _yaw, 0f) * input;
            direction.y = 0f;

            bool moving = direction.sqrMagnitude > 0.001f;
            if (moving) direction.Normalize();

            Vector3 move = moving ? direction * speed : Vector3.zero;

            // Wall grabs can happen mid-leap too, so climbing is checked every frame.
            if (_rakeController != null && _rakeController.enabled)
            {
                UpdateClimbing(dt, moving, direction);
            }

            if (_climbing) move = _climbMove;
            else if (_jumpActive) move = _jumpDirection * _jumpSpeed;

            if (_rakeController != null && _rakeController.enabled)
            {
                if (_climbing)
                {
                    // On a wall the climb replaces gravity: the body is pinned to it.
                    _verticalVelocity = 0f;
                    _rakeController.Move(_climbMove * dt);
                }
                else
                {
                    // CharacterController handles terrain, steps and obstacles; gravity is
                    // applied by hand because the Rake has no Rigidbody.
                    if (_rakeController.isGrounded)
                    {
                        if (_verticalVelocity < 0f) _verticalVelocity = -2f;
                    }
                    else
                    {
                        _verticalVelocity += Physics.gravity.y * dt;
                    }

                    move.y = _verticalVelocity;
                    _rakeController.Move(move * dt);
                    UpdateJump(dt);
                }
            }
            else if (moving)
            {
                _rake.position += direction * speed * dt;
            }

            if (_climbing)
            {
                // Already turned towards the wall by UpdateClimbing.
            }
            else if (_firstPerson)
            {
                // In first person the body follows the view, not the movement direction.
                _rake.rotation = Quaternion.Euler(0f, _yaw, 0f);
            }
            else if (moving)
            {
                Quaternion look = Quaternion.LookRotation(direction);
                _rake.rotation = Quaternion.Slerp(_rake.rotation, look, dt * 16f);
            }

            // Running/walking is audible; the sneak must stay silent.
            if (moving && !stealth && !_climbing)
            {
                _stepTimer -= dt;
                if (_stepTimer <= 0f)
                {
                    _stepTimer = run ? 0.24f : 0.36f;
                    if (_audio != null && _enemyAI != null && _enemyAI.walkSound != null)
                    {
                        _audio.PlayOneShot(_enemyAI.walkSound, 0.8f);
                    }
                }
            }
            else
            {
                _stepTimer = 0f;
            }

            if (_animator != null)
            {
                if (_climbing)
                {
                    // No climb clip in the game: a walk cycle against the wall reads as climbing.
                    // Standing still on the wall is the idle hanging pose.
                    _animator.SetBool("bCrouch", false);
                    _animator.SetInteger("WalkType", 1);
                    _animator.SetFloat("Speed", Mathf.Abs(_climbVertical) > 0.01f ? 1f : 0f);
                }
                else if (stealth)
                {
                    // Vanilla sneak: crouched, WalkType 0, no footstep trigger.
                    _animator.SetBool("bCrouch", true);
                    _animator.SetInteger("WalkType", 0);
                    _animator.SetFloat("Speed", 0f);
                }
                else
                {
                    _animator.SetBool("bCrouch", false);
                    _animator.SetInteger("WalkType", 1);
                    float animSpeed = !moving ? 0f : (run ? 2f : 1f);
                    _animator.SetFloat("Speed", animSpeed);
                }

                _animator.SetBool("bAttack", _attackAnimation > 0f);
            }
        }

        private void SetRakeVisible(bool visible)
        {
            if (_rakeVisible == visible) return;
            _rakeVisible = visible;

            for (int i = 0; i < _rakeRenderers.Count; i++)
            {
                if (_rakeRenderers[i] != null) _rakeRenderers[i].enabled = visible;
            }
        }

        private void UpdateCamera(float dt)
        {
            if (_camera == null) return;

            // Wider eyes in first person; blended so the switch is not a jump cut.
            float targetFov = _firstPerson ? FirstPersonFov : ThirdPersonFov;
            _camera.fieldOfView = dt > 0f
                ? Mathf.MoveTowards(_camera.fieldOfView, targetFov, dt * 240f)
                : targetFov;

            if (_firstPerson)
            {
                // Looking down shows the creature's own chest and arms while walking, but on
                // a wall both arms are raised in front of the lens: hide the body for the
                // climb so the Rake never sees itself (or its face) while climbing.
                SetRakeVisible(!_climbing);

                // While climbing the body faces the wall; keep the view along the wall and
                // pull the eye closer so it never ends up behind the Rake's own face.
                float viewYaw = _climbing ? _rake.eulerAngles.y : _yaw;
                float forwardOffset = _climbing ? 0.16f : FirstPersonForward;
                Vector3 viewForward = Quaternion.Euler(0f, viewYaw, 0f) * Vector3.forward;

                Vector3 eye = _head != null
                    ? _head.position + Vector3.up * 0.12f + viewForward * forwardOffset
                    : _rake.position + Vector3.up * 1.72f;

                _camera.transform.position = eye;
                _camera.transform.rotation = Quaternion.Euler(_pitch, viewYaw, 0f);
                return;
            }

            SetRakeVisible(true);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pivot = _rake.position + Vector3.up * 1.65f;
            Vector3 position = pivot - rotation * Vector3.forward * _distance;

            // Do not let the camera sink into the ground.
            if (position.y < _rake.position.y + 0.35f) position.y = _rake.position.y + 0.35f;

            _camera.transform.position = position;
            _camera.transform.rotation = rotation;
        }

        private static Transform FindHeadBone(Transform rake)
        {
            if (rake == null) return null;

            var animator = rake.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                Transform humanoid = animator.GetBoneTransform(HumanBodyBones.Head);
                if (humanoid != null) return humanoid;
            }

            Transform[] all = rake.GetComponentsInChildren<Transform>(true);
            Transform fallback = null;
            for (int i = 0; i < all.Length; i++)
            {
                Transform candidate = all[i];
                if (candidate == null) continue;

                string name = candidate.name.ToLowerInvariant();
                if (name.IndexOf("head", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (name.IndexOf("nub", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

                // "Bip001 Head" is the skeleton's head; anything else is a fallback.
                if (name.EndsWith(" head")) return candidate;
                if (fallback == null) fallback = candidate;
            }

            return fallback;
        }

        /// <summary>Dev bridge: place the body somewhere for a test.</summary>
        public static string BridgeTeleport(float x, float y, float z)
        {
            if (_instance == null || _instance._rake == null) return "{\"ok\":false}";

            var c = _instance;
            bool wasEnabled = c._rakeController != null && c._rakeController.enabled;
            if (c._rakeController != null) c._rakeController.enabled = false;

            c._rake.position = new Vector3(x, y, z);
            c._verticalVelocity = 0f;
            c._climbReleaseTimer = 0f;

            if (c._rakeController != null) c._rakeController.enabled = wasEnabled;

            return "{\"ok\":true,\"pos\":\"" + c._rake.position.ToString("F2") + "\"}";
        }

        /// <summary>Dev bridge: dump every raw hit of the climbing sphere along a yaw.</summary>
        public static string BridgeProbe(float yaw, float height)
        {
            if (_instance == null || _instance._rake == null) return "{\"ok\":false}";

            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            dir.y = 0f;
            dir.Normalize();

            Vector3 origin = _instance._rake.position + Vector3.up * height - dir * 0.10f;
            RaycastHit[] hits = Physics.SphereCastAll(origin, ClimbProbeRadius, dir,
                                                      ClimbProbeDistance + 0.10f);

            var sb = new System.Text.StringBuilder();
            sb.Append("{\"ok\":true,\"origin\":\"").Append(origin.ToString("F2")).Append("\",\"hits\":[");
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"col\":\"").Append(h.collider == null ? "-" : h.collider.name);
                sb.Append("\",\"type\":\"").Append(h.collider == null ? "-" : h.collider.GetType().Name);
                sb.Append("\",\"dist\":").Append(h.distance.ToString("F2"));
                sb.Append(",\"n\":\"").Append(h.normal.ToString("F2"));
                sb.Append("\",\"p\":\"").Append(h.point.ToString("F2")).Append("\"}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>Dev bridge: live climbing state for diagnosis.</summary>
        public static string BridgeClimb()
        {
            if (_instance == null) return "{\"ok\":false}";

            var c = _instance;
            string state = "{\"ok\":true," +
                "\"climbing\":" + (c._climbing ? "true" : "false") + "," +
                "\"yaw\":" + c._rake.eulerAngles.y.ToString("F1") + "," +
                "\"normal\":\"" + c._climbNormal.ToString("F2") + "\"," +
                "\"dir\":\"" + c._climbDir.ToString("F2") + "\"," +
                "\"lost\":" + c._climbLostTimer.ToString("F2") + "," +
                "\"mantle\":" + c._mantleTimer.ToString("F2") + "," +
                "\"pos\":\"" + c._rake.position.ToString("F2") + "\"";

            RaycastHit hit;
            bool chest = c.FindWall(c._climbDir, ClimbChestHeight, out hit);
            state += ",\"chest\":" + (chest ? "true" : "false");
            state += ",\"chestCollider\":\"" + (chest ? hit.collider.name : "-") + "\"";
            if (chest) state += ",\"chestNormal\":\"" + hit.normal.ToString("F2") + "\"";

            bool knee = c.FindWall(c._climbDir, ClimbKneeHeight, out hit, ClimbKneeProbeDistance);
            state += ",\"knee\":" + (knee ? "true" : "false");
            state += ",\"kneeCollider\":\"" + (knee ? hit.collider.name : "-") + "\"";
            state += "}";
            return state;
        }

        /// <summary>Dev bridge: press Space for one frame.</summary>
        public static void BridgeSpace()
        {
            if (_instance != null) _instance._bridgeSpace = true;
        }

        /// <summary>Dev bridge: walk forward without a keyboard.</summary>
        public static void BridgeMove(bool forward, float yaw)
        {
            if (_instance == null) return;
            _instance._bridgeForward = forward;
            if (!float.IsNaN(yaw))
            {
                _instance._yaw = yaw;
                if (_instance._rake != null) _instance._rake.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        /// <summary>Dev bridge: switch first/third person and report the camera state.</summary>
        public static string BridgeCamera(string view, string pitch, string look)
        {
            if (_instance == null) return "{\"ok\":false,\"error\":\"rake camera is not active\"}";

            if (string.Equals(view, "fp", System.StringComparison.OrdinalIgnoreCase)) _instance._firstPerson = true;
            else if (string.Equals(view, "tp", System.StringComparison.OrdinalIgnoreCase)) _instance._firstPerson = false;

            float pitchValue;
            if (float.TryParse(pitch, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out pitchValue))
            {
                _instance._pitch = Mathf.Clamp(pitchValue, -70f, 65f);
            }

            if (!string.IsNullOrEmpty(look) && _instance._camera != null)
            {
                Vector3 eye = _instance._camera.transform.position;
                Vector3 target = eye;

                if (look.Equals("animal", System.StringComparison.OrdinalIgnoreCase))
                {
                    float best = float.MaxValue;
                    var animals = Object.FindObjectsOfType<Animal_AI>();
                    for (int i = 0; i < animals.Length; i++)
                    {
                        if (animals[i] == null) continue;
                        float d = Vector3.Distance(eye, animals[i].transform.position);
                        if (d < best) { best = d; target = animals[i].transform.position + Vector3.up * 0.6f; }
                    }
                }
                else if (look.Equals("trap", System.StringComparison.OrdinalIgnoreCase))
                {
                    float best = float.MaxValue;
                    var traps = Object.FindObjectsOfType<Trap_proj>();
                    for (int i = 0; i < traps.Length; i++)
                    {
                        if (traps[i] == null) continue;
                        float d = Vector3.Distance(eye, traps[i].transform.position);
                        if (d < best) { best = d; target = traps[i].transform.position + Vector3.up * 0.3f; }
                    }
                }

                Vector3 to = target - eye;
                _instance._yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                _instance._pitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
                if (_instance._rake != null) _instance._rake.rotation = Quaternion.Euler(0f, _instance._yaw, 0f);
            }

            string head = _instance._head != null ? _instance._head.name : string.Empty;
            string fov = _instance._camera != null
                ? Mathf.RoundToInt(_instance._camera.fieldOfView).ToString()
                : "0";

            string camPos = _instance._camera != null ? _instance._camera.transform.position.ToString("F2") : "-";
            string headPos = _instance._head != null ? _instance._head.position.ToString("F2") : "-";
            string rakePos = _instance._rake != null ? _instance._rake.position.ToString("F2") : "-";
            string localPos = _instance._camera != null ? _instance._camera.transform.localPosition.ToString("F2") : "-";
            string parent = _instance._camera != null && _instance._camera.transform.parent != null
                ? _instance._camera.transform.parent.name
                : "-";

            return "{\"ok\":true,\"view\":\"" + (_instance._firstPerson ? "fp" : "tp") + "\"," +
                   "\"fov\":" + fov + "," +
                   "\"onHead\":" + (_instance._head != null ? "true" : "false") + "," +
                   "\"parent\":\"" + parent + "\"," +
                   "\"head\":\"" + head + "\"," +
                   "\"cam\":\"" + camPos + "\"," +
                   "\"headPos\":\"" + headPos + "\"," +
                   "\"rakePos\":\"" + rakePos + "\"," +
                   "\"local\":\"" + localPos + "\"}";
        }

        // -------------------------------------------------------------------- combat

        private static bool AttacksAllowed()
        {
            DayNightManager dayNight = DayNightManager.Instance;
            if (dayNight == null || !dayNight.IsRunning) return true; // single-player / no cycle
            return dayNight.CurrentPhase == Phase.Night;
        }

        private void RefreshTargets()
        {
            _players.Clear();
            _animals.Clear();
            _traps.Clear();

            NetworkedPlayer[] players = UnityEngine.Object.FindObjectsOfType<NetworkedPlayer>();
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                if (_hunter != null && players[i].transform == _hunter) continue;
                _players.Add(players[i]);
            }

            Animal_AI[] animals = UnityEngine.Object.FindObjectsOfType<Animal_AI>();
            for (int i = 0; i < animals.Length; i++)
            {
                if (animals[i] == null || !animals[i].isAlive) continue;
                _animals.Add(animals[i]);
            }

            Trap_proj[] traps = UnityEngine.Object.FindObjectsOfType<Trap_proj>();
            for (int i = 0; i < traps.Length; i++)
            {
                if (traps[i] == null) continue;
                _traps.Add(traps[i]);
            }

            UpdatePreyInReach();
        }

        /// <summary>True when a hunter or animal is close enough and in front, regardless of cooldowns.</summary>
        private void UpdatePreyInReach()
        {
            _preyInReach = false;

            for (int i = 0; i < _players.Count; i++)
            {
                NetworkedPlayer player = _players[i];
                if (player == null) continue;
                float distance;
                if (InFront(player.transform.position, out distance)) { _preyInReach = true; break; }
            }

            if (_preyInReach) return;

            for (int i = 0; i < _animals.Count; i++)
            {
                Animal_AI animal = _animals[i];
                if (animal == null) continue;
                float distance;
                if (InFront(animal.transform.position, out distance)) { _preyInReach = true; break; }
            }
        }

        private void TryAutoAttack()
        {
            if (DebugConsole.ConsoleOpen || UI.MultiplayerMenu.IsOpen) return;

            Transform best = null;
            int bestType = -1; // 0 player, 1 animal, 2 camera
            float bestDistance = float.MaxValue;

            if (_playerCooldown <= 0f)
            {
                for (int i = 0; i < _players.Count; i++)
                {
                    NetworkedPlayer player = _players[i];
                    if (player == null) continue;
                    float distance;
                    if (!InFront(player.transform.position, out distance)) continue;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    best = player.transform;
                    bestType = 0;
                }
            }

            if (_animalCooldown <= 0f)
            {
                for (int i = 0; i < _animals.Count; i++)
                {
                    Animal_AI animal = _animals[i];
                    if (animal == null) continue;
                    float distance;
                    if (!InFront(animal.transform.position, out distance)) continue;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    best = animal.transform;
                    bestType = 1;
                }
            }

            if (_cameraCooldown <= 0f)
            {
                RecordCamera_obj[] cameras = UnityEngine.Object.FindObjectsOfType<RecordCamera_obj>();
                for (int i = 0; i < cameras.Length; i++)
                {
                    RecordCamera_obj camera = cameras[i];
                    if (camera == null) continue;
                    if (camera.cameraState != RecordCamera_obj.Camera_state.Setup) continue;
                    float distance;
                    if (!InFront(camera.transform.position, out distance)) continue;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    best = camera.transform;
                    bestType = 2;
                }
            }

            if (best == null) return;

            if (bestType == 0) StartPlayerGrab(best);
            else if (bestType == 1) StartAnimalKill(best);
            else StartCameraBreak(best);
        }

        // --- scripted attack sequences: the Rake stops, the victim stops, the camera
        // turns to the Rake, then the attack animation plays and the hit lands. ---

        private void StartPlayerGrab(Transform victim)
        {
            if (_actionLocked || victim == null) return;

            _actionLocked = true;
            _playerCooldown = PlayerCooldown;

            FaceInstant(victim.position);
            PlayAttackRoar();

            if (_animator != null)
            {
                _animator.SetFloat("Speed", 0f);
                _animator.SetBool("bCrouch", false);
                _animator.SetBool("bAttack", false);
                _animator.SetBool("bPlayerAttack", true);
            }

            Vector3 focus = transform.position + Vector3.up * 1.6f;
            if (_enemyAI != null && _enemyAI.focusPoint != null) focus = _enemyAI.focusPoint.position;

            NetworkedPlayer net = victim.GetComponent<NetworkedPlayer>();
            int victimId = net != null ? net.PlayerId : 0;

            if (RakeMultiplayerPlugin.isServer)
            {
                if (victimId > 0)
                {
                    var grab = new RakeGrabPacket
                    {
                        PlayerId = victimId,
                        RakePosition = _rake != null ? _rake.position : transform.position,
                        FocusPosition = focus
                    };
                    RakeNetworkManager.Instance?.ServerBroadcast(grab.Serialize());
                }
            }
            else
            {
                SendAttackRequest(0, victimId, victim.position, focus, 10, "RakeKick");
            }

            StartCoroutine(PlayerGrabRoutine(victim));
        }

        private System.Collections.IEnumerator PlayerGrabRoutine(Transform victim)
        {
            yield return new WaitForSeconds(1.2f);

            // Vanilla grab: kick sound, then the hit and the victim is released.
            // A client owner only animates; the server lands the hit and broadcasts.
            if (RakeMultiplayerPlugin.isServer)
            {
                RakeNetworkManager.Instance?.BroadcastRakeSound(1, transform.position);
                if (victim != null && _rakeAI != null) _rakeAI.AttackPlayer(victim, 10, "RakeKick");
            }

            if (_animator != null)
            {
                _animator.SetBool("bPlayerAttack", false);
                _animator.SetBool("bAttack", false);
            }

            _actionLocked = false;
        }

        private void StartAnimalKill(Transform animal)
        {
            if (_actionLocked || animal == null) return;

            _actionLocked = true;
            _animalCooldown = AnimalCooldown;

            FaceInstant(animal.position);
            if (_animator != null)
            {
                _animator.SetFloat("Speed", 0f);
                _animator.SetBool("bAnimalKilling", true);
            }

            if (!RakeMultiplayerPlugin.isServer)
            {
                SendAttackRequest(1, 0, animal.position, Vector3.zero, 250, "RakeAnimalKick");
            }

            StartCoroutine(AnimalKillRoutine(animal));
        }

        private System.Collections.IEnumerator AnimalKillRoutine(Transform animal)
        {
            yield return new WaitForSeconds(1.8f);

            if (RakeMultiplayerPlugin.isServer)
            {
                Animal_AI ai = animal != null ? animal.GetComponent<Animal_AI>() : null;
                if (ai != null) ai.TakeDamage(250, "RakeAnimalKick");
            }
            if (_animator != null) _animator.SetBool("bAnimalKilling", false);

            _actionLocked = false;
        }

        private void StartCameraBreak(Transform cameraTransform)
        {
            if (_actionLocked || cameraTransform == null) return;

            _actionLocked = true;
            _cameraCooldown = CameraCooldown;

            FaceInstant(cameraTransform.position);
            if (_animator != null)
            {
                _animator.SetFloat("Speed", 0f);
                _animator.SetBool("bCameraBreak", true);
            }

            if (!RakeMultiplayerPlugin.isServer)
            {
                SendAttackRequest(2, 0, cameraTransform.position, Vector3.zero, 0, "CameraBreak");
            }

            StartCoroutine(CameraBreakRoutine(cameraTransform));
        }

        private System.Collections.IEnumerator CameraBreakRoutine(Transform cameraTransform)
        {
            yield return new WaitForSeconds(1.6f);

            if (RakeMultiplayerPlugin.isServer)
            {
                RecordCamera_obj cam = cameraTransform != null ? cameraTransform.GetComponent<RecordCamera_obj>() : null;
                if (cam != null) cam.SetUnsetupState();
            }
            if (_animator != null) _animator.SetBool("bCameraBreak", false);

            _actionLocked = false;
        }

        // -------------------------------------------------------------------- jump

        /// <summary>
        /// A short forward leap: about 10 m along the view direction with a 1 m arc,
        /// on a 50 s cooldown. The body is ballistic for the whole flight; input does
        /// not steer it, so the leap is predictable for the hunters.
        /// </summary>
        // -------------------------------------------------------------------- climbing

        /// <summary>
        /// Climb any vertical surface like a ladder: hold W against a wall to go up,
        /// S to go down, A/D to slide sideways, Space to let go. The body is pinned to
        /// the wall and gravity is suspended until it reaches the top or releases.
        /// </summary>
        private void UpdateClimbing(float dt, bool moving, Vector3 direction)
        {
            if (_climbing)
            {
                // Space is handled in ReadMovement (DetachFromWall); releasing the movement
                // keys does NOT let go: the Rake hangs.
                bool down = Input.GetKey(KeyCode.S);
                bool up = Input.GetKey(KeyCode.W) || _bridgeForward;

                // Probe along the stored approach direction, NOT along the raw normal.
                // Feeding the normal back into the probe made it oscillate on round
                // trunks: the body turned, the probe missed, the grip blinked off.
                RaycastHit chestHit = new RaycastHit();
                RaycastHit kneeHit = new RaycastHit();
                bool chest = FindWall(_climbDir, ClimbChestHeight, out chestHit);
                bool knee = !chest && FindWall(_climbDir, ClimbKneeHeight, out kneeHit, ClimbKneeProbeDistance);

                if (chest || knee)
                {
                    _climbLostTimer = 0f;
                    RaycastHit wall = chest ? chestHit : kneeHit;

                    Vector3 n = wall.normal;
                    n.y = 0f;
                    if (n.sqrMagnitude > 0.001f)
                    {
                        n.Normalize();

                        // A branch or a second collider behind the trunk can report a
                        // wildly different normal for one frame. Ignore those outliers.
                        if (Vector3.Angle(_climbNormal, n) <= ClimbNormalMaxJump)
                        {
                            _climbNormal = Vector3.Slerp(_climbNormal, n,
                                1f - Mathf.Exp(-ClimbNormalSmoothing * dt));
                            _climbNormal.y = 0f;
                            if (_climbNormal.sqrMagnitude > 0.001f) _climbNormal.Normalize();
                        }
                    }

                    // Track the curvature of the trunk slowly along with the smoothed
                    // normal, so corners are still followed without snapping.
                    Vector3 steer = -_climbNormal;
                    steer.y = 0f;
                    if (chest && steer.sqrMagnitude > 0.001f)
                    {
                        steer.Normalize();
                        _climbDir = Vector3.Slerp(_climbDir, steer, 1f - Mathf.Exp(-3.5f * dt));
                        _climbDir.y = 0f;
                        if (_climbDir.sqrMagnitude > 0.001f) _climbDir.Normalize();
                    }

                    Vector3 face = -_climbNormal;
                    if (face.sqrMagnitude > 0.001f)
                    {
                        Quaternion look = Quaternion.LookRotation(face, Vector3.up);
                        _rake.rotation = Quaternion.Slerp(_rake.rotation, look,
                            1f - Mathf.Exp(-ClimbTurnSpeed * dt));
                    }

                    if (chest) _mantleTimer = 0f;
                    else if (up)
                    {
                        // Committed pull over the edge; the wall is behind us now.
                        _mantleTimer = ClimbMantleTime;
                        _climbLostTimer = 0f;
                    }
                }
                else if (_mantleTimer <= 0f)
                {
                    // One lost frame is not a lost wall.
                    _climbLostTimer += dt;
                    if (_climbLostTimer >= ClimbLostGrace)
                    {
                        RakeMultiplayerPlugin.Log?.LogInfo("[RakePlay] wall lost after " +
                            _climbLostTimer.ToString("F2") + " s at y=" +
                            _rake.position.y.ToString("F1") + " dir=" + _climbDir.ToString("F2"));
                        StopClimbing(false);
                        // Never re-grab in the same instant: the new grab would read a
                        // different normal and snap the body sideways again.
                        _climbReleaseTimer = Mathf.Max(_climbReleaseTimer, 0.25f);
                        return;
                    }
                }

                if (down && _rakeController != null && _rakeController.isGrounded)
                {
                    StopClimbing(false);
                    return;
                }

                if (_mantleTimer > 0f)
                {
                    _mantleTimer -= dt;
                    _climbMove = _climbDir * 3.2f + Vector3.up * 1.5f;
                    if (_mantleTimer <= 0f)
                    {
                        // Carry the body over the edge instead of letting it slide back.
                        StopClimbing(true);
                        _jumpActive = false;
                    }
                    return;
                }

                float climb = (up ? 1f : 0f) - (down ? 1f : 0f);
                _climbVertical = climb;
                _climbMove = Vector3.up * (climb * ClimbSpeed) - _climbNormal * 1.2f;

                float side = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
                if (side != 0f) _climbMove += _rake.right * (side * 2.8f);
                return;
            }

            if (_climbReleaseTimer > 0f) return;
            if (!moving) return;
            if (_rakeController == null) return;

            // Self-heal a degenerate approach direction: a zero or NaN vector silently
            // makes every probe fail, which looks exactly like "climbing stopped working".
            if (!IsFinite(_climbDir) || _climbDir.sqrMagnitude < 0.01f)
            {
                _climbDir = new Vector3(direction.x, 0f, direction.z);
                if (_climbDir.sqrMagnitude < 0.001f && _rake != null) _climbDir = _rake.forward;
                _climbDir.y = 0f;
                if (_climbDir.sqrMagnitude > 0.001f)
                {
                    _climbDir.Normalize();
                    RakeMultiplayerPlugin.Log?.LogInfo("[RakePlay] climb direction recovered");
                }
            }

            RaycastHit hit;
            if (!FindWall(direction, ClimbChestHeight, out hit)) return;

            // hit.point from a sphere cast is the contact on the sphere, not on the wall;
            // using it pointed the body metres away from the surface. The surface normal
            // is the reliable way to know which way the wall is.
            Vector3 into = -hit.normal;
            into.y = 0f;
            if (into.sqrMagnitude < 0.001f) return;
            into.Normalize();

            // Only grab a wall the Rake is actually walking into, not a tree beside him.
            if (Vector3.Dot(direction, into) < 0.5f) return;

            _climbing = true;
            _mantleTimer = 0f;
            _climbLostTimer = 0f;
            // Face the wall by the approach direction, not by the first contact normal:
            // that one is already rotated on a curved trunk.
            // The stored normal points AWAY from the wall (it is used to face it as
            // -normal and to press the body into it); the approach direction points into it.
            _climbNormal = -into;
            _climbDir = into;
            _climbMove = Vector3.zero;
            _verticalVelocity = 0f;
            _climbVertical = 0f;
            _jumpActive = false; // a grab cancels the leap

            RakeMultiplayerPlugin.Log?.LogInfo("[RakePlay] grabbed the wall: " + hit.collider.name);
        }

        /// <summary>Deliberate drop: push away from the wall and forbid re-grabbing for a moment.</summary>
        private void DetachFromWall()
        {
            if (!_climbing) return;

            Vector3 away = _climbNormal;
            away.y = 0f;
            if (away.sqrMagnitude < 0.001f) away = Vector3.zero;
            else away.Normalize();

            StopClimbing(false);
            _climbReleaseTimer = ClimbReleaseTime;
            _verticalVelocity = 0f;

            if (_rakeController != null && _rakeController.enabled && away != Vector3.zero)
            {
                _rakeController.Move(away * 0.55f);
            }

            RakeMultiplayerPlugin.Log?.LogInfo("[RakePlay] dropped from the wall (re-grab blocked for " +
                                               ClimbReleaseTime + " s)");
        }

        private void StopClimbing(bool reachedTop)
        {
            if (!_climbing) return;

            _climbing = false;
            _mantleTimer = 0f;
            _climbLostTimer = 0f;
            _climbMove = Vector3.zero;
            _climbVertical = 0f;
            _verticalVelocity = reachedTop ? 2f : 0f;

            // Keep the stored direction usable for the next grab even if this climb ended
            // in a corner where the smoothed values collapsed.
            if (!IsFinite(_climbDir) || _climbDir.sqrMagnitude < 0.01f) _climbDir = Vector3.forward;
            if (!IsFinite(_climbNormal) || _climbNormal.sqrMagnitude < 0.01f) _climbNormal = Vector3.back;

            if (_animator != null) _animator.SetFloat("Speed", 0f);

            RakeMultiplayerPlugin.Log?.LogInfo("[RakePlay] climb finished" +
                                               (reachedTop ? " (over the top)" : ""));
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
                   !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }

        /// <summary>Nearest non-character vertical surface in front of the body.</summary>
        private bool FindWall(Vector3 direction, float height, out RaycastHit wall)
        {
            return FindWall(direction, height, out wall, ClimbProbeDistance);
        }

        private bool FindWall(Vector3 direction, float height, out RaycastHit wall, float distance)
        {
            wall = new RaycastHit();
            if (_rake == null) return false;

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return false;
            direction.Normalize();

            // Volumetric probe: a ray slipped between faces and off ledges, so the Rake
            // let go of corners and thin fences. Start slightly behind the body so the
            // sphere never begins inside the wall (SphereCast ignores initial overlaps).
            const float backoff = 0.10f;
            Vector3 origin = _rake.position + Vector3.up * height - direction * backoff;

            float bestDistance = float.MaxValue;
            bool found = false;

            RaycastHit[] hits = Physics.SphereCastAll(origin, ClimbProbeRadius, direction,
                                                      distance + backoff);
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (!IsClimbSurface(hit)) continue;
                if (hit.distance >= bestDistance) continue;
                bestDistance = hit.distance;
                wall = hit;
                found = true;
            }

            if (found) return true;

            // When the sphere starts inside a root bulge or a knot it reports nothing
            // usable; a thin ray from the body centre still finds the surface and keeps
            // the grip alive instead of blinking the climb off.
            Vector3 rayOrigin = _rake.position + Vector3.up * height;
            RaycastHit[] rays = Physics.RaycastAll(rayOrigin, direction, distance + 0.4f);
            bestDistance = float.MaxValue;
            for (int i = 0; i < rays.Length; i++)
            {
                RaycastHit hit = rays[i];
                if (!IsClimbSurface(hit)) continue;
                if (hit.distance >= bestDistance) continue;
                bestDistance = hit.distance;
                wall = hit;
                found = true;
            }

            return found;
        }

        /// <summary>Filters shared by every climbing probe.</summary>
        private bool IsClimbSurface(RaycastHit hit)
        {
            Collider col = hit.collider;
            if (col == null || col.isTrigger) return false;
            if (col is TerrainCollider) return false; // slopes are not climbing surfaces

            // The Rake's own capsule was slipping through here: IsChildOf is false for the
            // object itself, and it reported distance 0 with a junk hit.point, which then
            // became the approach direction.
            if (col.transform == _rake || col.transform.IsChildOf(_rake)) return false;
            if (_rakeController != null && col == _rakeController) return false;
            if (col.GetComponentInParent<NetworkedPlayer>() != null) return false;
            if (col.GetComponentInParent<Animal_AI>() != null) return false;

            // Floors, roofs and gentle slopes are not climbable.
            if (Mathf.Abs(hit.normal.y) > 0.45f) return false;

            // A zero distance with a zero normal means the probe started inside the
            // collider (Unity's initial-overlap report), not a wall in front of us.
            if (hit.distance <= 0.001f) return false;

            return true;
        }

        private void TryJump()
        {
            if (_jumpActive || _jumpCooldown > 0f) return;
            if (_rakeController != null && !_rakeController.isGrounded) return;

            float gravity = Mathf.Abs(Physics.gravity.y);
            if (gravity < 0.01f) gravity = 9.81f;

            _jumpActive = true;
            _jumpCooldown = JumpCooldown;
            _jumpElapsed = 0f;

            _jumpDirection = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            _jumpDirection.y = 0f;
            if (_jumpDirection.sqrMagnitude < 0.001f) _jumpDirection = _rake.forward;
            _jumpDirection.y = 0f;
            _jumpDirection.Normalize();

            // Up to JumpHeight, then back down: airtime sets the horizontal speed
            // that carries the body the requested distance.
            float airtime = 2f * Mathf.Sqrt(2f * JumpHeight / gravity);
            _jumpSpeed = JumpDistance / Mathf.Max(airtime, 0.1f);

            _verticalVelocity = Mathf.Sqrt(2f * gravity * JumpHeight);

            _rake.rotation = Quaternion.LookRotation(_jumpDirection);
            if (_animator != null)
            {
                _animator.SetBool("bCrouch", false);
                _animator.SetFloat("Speed", 0f);
            }

            RakeMultiplayerPlugin.Log?.LogInfo("[RakePlay] jumped forward " + JumpDistance +
                                               " m (speed " + _jumpSpeed.ToString("F1") + " m/s)");
        }

        private void UpdateJump(float dt)
        {
            if (!_jumpActive) return;

            _jumpElapsed += dt;
            bool grounded = _rakeController == null || _rakeController.isGrounded;
            if ((grounded && _jumpElapsed > 0.15f) || _jumpElapsed > 2f)
            {
                _jumpActive = false;
                if (_rakeController != null && _rakeController.isGrounded && _verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f;
                }
            }
        }

        // ----------------------------------------------------------------- combat lock

        /// <summary>
        /// Called when the Rake takes a hit: attacks are locked for
        /// <see cref="DamageAttackLockout"/> seconds, but never shortened if the current
        /// cooldown is already longer.
        /// </summary>
        public static void NotifyDamageTaken(Transform rake)
        {
            if (_instance == null) return;
            if (rake != null && _instance._rake != rake) return;
            _instance.ApplyDamageLockout();
        }

        private void ApplyDamageLockout()
        {
            if (_playerCooldown < DamageAttackLockout) _playerCooldown = DamageAttackLockout;
            if (_animalCooldown < DamageAttackLockout) _animalCooldown = DamageAttackLockout;
            if (_cameraCooldown < DamageAttackLockout) _cameraCooldown = DamageAttackLockout;
            _damageLockout = DamageAttackLockout;

            if (_hudUi != null) _hudUi.PulseDamage();

            RakeMultiplayerPlugin.Log?.LogInfo("[RakePlay] Rake hit: attack locked for " +
                                               DamageAttackLockout + " s");
        }

        /// <summary>A client-owned Rake asks the server to land an attack.</summary>
        private void SendAttackRequest(byte kind, int targetPlayerId, Vector3 targetPosition,
                                        Vector3 focusPosition, int amount, string damageType)
        {
            var manager = RakeNetworkManager.Instance;
            if (manager == null) return;

            var packet = new RakeAttackRequestPacket
            {
                Kind = kind,
                TargetPlayerId = targetPlayerId,
                Amount = amount,
                DamageType = damageType ?? string.Empty,
                RakePosition = _rake != null ? _rake.position : transform.position,
                FocusPosition = focusPosition,
                TargetPosition = targetPosition
            };

            manager.ClientSend(packet.Serialize());
            RakeMultiplayerPlugin.Log?.LogInfo("[RakePlay] sent attack request kind=" + kind +
                                               " target=" + targetPlayerId);
        }
        private void FaceInstant(Vector3 worldPosition)
        {
            // The controller object is a plain root at the origin; the body is _rake.
            // Rotating the controller used to drag the HUD (a child of it) across the screen.
            if (_rake == null) return;

            Vector3 dir = worldPosition - _rake.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                _rake.rotation = Quaternion.LookRotation(dir);
            }
        }

        private void PlayAttackRoar()
        {
            var manager = RakeNetworkManager.Instance;
            if (manager != null && RakeMultiplayerPlugin.isConnected)
            {
                manager.BroadcastRakeSound(0, transform.position);
                return;
            }

            if (_audio != null && _enemyAI != null && _enemyAI.playerAttackRoars != null &&
                _enemyAI.playerAttackRoars.Length > 0)
            {
                _audio.PlayOneShot(_enemyAI.playerAttackRoars[Random.Range(0, _enemyAI.playerAttackRoars.Length)]);
            }
        }
        // ---------------------------------------------------------------- HUD data

        internal Transform RakeTransform { get { return _rake; } }
        internal float Health { get { return _enemyAI != null ? _enemyAI.Health : 0f; } }
        internal float MaxHealth { get { return _rakeAI != null ? _rakeAI.MaxHealth : 2500f; } }
        internal bool AttacksAllowedForHud
        {
            get
            {
                // The HUD must follow the session, not the attack permission: that one
                // defaults to "night" whenever no cycle is running, which is exactly the
                // state a client is in until the first phase packet arrives.
                DayNightManager dayNight = DayNightManager.Instance;
                if (dayNight != null && dayNight.IsRunning) return dayNight.CurrentPhase == Phase.Night;

                string scene = UnityEngine.Application.loadedLevelName;
                return scene != null &&
                       scene.IndexOf("night", System.StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        internal float GrabCooldownLeft { get { return _playerCooldown; } }
        internal float AnimalCooldownLeft { get { return _animalCooldown; } }
        internal float CameraCooldownLeft { get { return _cameraCooldown; } }
        internal float LeapCooldownLeft { get { return _jumpCooldown; } }

        internal float GrabCooldownTotal { get { return PlayerCooldown; } }
        internal float AnimalCooldownTotal { get { return AnimalCooldown; } }
        internal float CameraCooldownTotal { get { return CameraCooldown; } }
        internal float LeapCooldownTotal { get { return JumpCooldown; } }

        internal float DamageLockoutLeft { get { return _damageLockout; } }
        internal bool PreyInReach { get { return _preyInReach; } }
        internal bool IsClimbing { get { return _climbing; } }
        internal bool IsHanging { get { return _climbing && Mathf.Abs(_climbVertical) < 0.01f; } }

        internal System.Collections.Generic.IList<NetworkedPlayer> SensePlayers { get { return _players; } }
        internal System.Collections.Generic.IList<Animal_AI> SenseAnimals { get { return _animals; } }
        internal System.Collections.Generic.IList<Trap_proj> SenseTraps { get { return _traps; } }
        private bool InFront(Vector3 targetPosition, out float distance)
        {
            Vector3 to = targetPosition - _rake.position;
            to.y = 0f;
            distance = to.magnitude;
            if (distance > AttackRange || distance < 0.01f) return false;
            if (Vector3.Angle(_rake.forward, to) > AttackCone) return false;

            // Do not hit through walls or closed doors.
            Vector3 from = _rake.position + Vector3.up * 1.2f;
            Vector3 toChest = targetPosition + Vector3.up * 1.2f;
            Vector3 delta = toChest - from;
            float rayLength = delta.magnitude;
            if (rayLength > 0.5f)
            {
                var hits = Physics.RaycastAll(from, delta / rayLength, rayLength - 0.5f, ~0);
                for (int i = 0; i < hits.Length; i++)
                {
                    var collider = hits[i].collider;
                    if (collider == null || collider.isTrigger) continue;
                    if (collider.transform == _rake || collider.transform.IsChildOf(_rake)) continue;
                    return false;
                }
            }
            return true;
        }

    }
}
