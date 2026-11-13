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
        private const float ClimbProbeDistance = 1.15f;
        private const float ClimbChestHeight = 1.25f;
        private const float ClimbKneeHeight = 0.55f;

        private const float SenseRange = 100f;

        private static RakePlayerController _instance;

        private Transform _rake;
        private NetworkedRakeAI _rakeAI;
        private Enemy_AI _enemyAI;
        private NavMeshAgent _agent;
        private Animator _animator;
        private AudioSource _audio;

        private Camera _camera;
        private float _yaw;
        private float _pitch;
        private float _distance = 4.6f;
        private bool _firstPerson;

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
        private Vector3 _climbMove;
        private float _mantleTimer;
        private float _climbVertical;

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
            _camera.enabled = true;

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

            if (Input.GetKeyDown(KeyCode.Space)) TryJump();

            Vector3 input = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) input += Vector3.forward;
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

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            if (_firstPerson)
            {
                SetRakeVisible(false);
                _camera.transform.position = _rake.position + Vector3.up * 1.72f;
                _camera.transform.rotation = rotation;
                return;
            }

            SetRakeVisible(true);

            Vector3 pivot = _rake.position + Vector3.up * 1.65f;
            Vector3 position = pivot - rotation * Vector3.forward * _distance;

            // Do not let the camera sink into the ground.
            if (position.y < _rake.position.y + 0.35f) position.y = _rake.position.y + 0.35f;

            _camera.transform.position = position;
            _camera.transform.rotation = rotation;
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
                        RakePosition = transform.position,
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
                // Space lets go. Releasing the movement keys does NOT: the Rake hangs.
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    StopClimbing(false);
                    return;
                }

                bool down = Input.GetKey(KeyCode.S);
                bool up = Input.GetKey(KeyCode.W);

                RaycastHit wall;
                if (FindWall(-_climbNormal, ClimbChestHeight, out wall))
                {
                    _climbNormal = wall.normal;
                    _mantleTimer = 0f;
                }
                else if (FindWall(-_climbNormal, ClimbKneeHeight, out wall))
                {
                    // Only the lower part still touches the wall. Going up: this is the
                    // top edge, pull the body over it. Otherwise just hang here.
                    _climbNormal = wall.normal;
                    if (up) _mantleTimer = 0.45f;
                }
                else
                {
                    StopClimbing(false);
                    return;
                }

                if (down && _rakeController != null && _rakeController.isGrounded)
                {
                    StopClimbing(false);
                    return;
                }

                Vector3 face = -_climbNormal;
                face.y = 0f;
                if (face.sqrMagnitude > 0.001f) _rake.rotation = Quaternion.LookRotation(face);

                if (_mantleTimer > 0f)
                {
                    _mantleTimer -= dt;
                    _climbMove = _rake.forward * 3.4f + Vector3.up * 1.1f;
                    return;
                }

                float climb = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
                _climbVertical = climb;
                _climbMove = Vector3.up * (climb * ClimbSpeed) - _climbNormal * 1.4f;

                float side = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
                if (side != 0f) _climbMove += _rake.right * (side * 2.8f);
                return;
            }

            if (!moving) return;
            if (_rakeController == null) return;

            RaycastHit hit;
            if (!FindWall(direction, ClimbChestHeight, out hit)) return;

            Vector3 toWall = hit.point - _rake.position;
            toWall.y = 0f;
            if (toWall.sqrMagnitude < 0.001f) return;

            Vector3 into = toWall.normalized;
            if (Vector3.Dot(direction, into) < 0.4f && Vector3.Dot(_rake.forward, into) < 0.4f) return;

            _climbing = true;
            _mantleTimer = 0f;
            _climbNormal = hit.normal;
            _climbMove = Vector3.zero;
            _verticalVelocity = 0f;
            _climbVertical = 0f;
            _jumpActive = false; // a grab cancels the leap

            RakeMultiplayerPlugin.Log?.LogInfo("[RakePlay] grabbed the wall: " + hit.collider.name);
        }

        private void StopClimbing(bool reachedTop)
        {
            if (!_climbing) return;

            _climbing = false;
            _mantleTimer = 0f;
            _climbMove = Vector3.zero;
            _climbVertical = 0f;
            _verticalVelocity = reachedTop ? 2f : 0f;

            if (_animator != null) _animator.SetFloat("Speed", 0f);

            RakeMultiplayerPlugin.Log?.LogInfo("[RakePlay] climb finished" +
                                               (reachedTop ? " (over the top)" : ""));
        }

        /// <summary>Nearest non-character vertical surface in front of the body.</summary>
        private bool FindWall(Vector3 direction, float height, out RaycastHit wall)
        {
            wall = new RaycastHit();
            if (_rake == null) return false;

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return false;
            direction.Normalize();

            Vector3 origin = _rake.position + Vector3.up * height;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, ClimbProbeDistance);

            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                Collider col = hit.collider;
                if (col == null || col.isTrigger) continue;
                if (col.transform.IsChildOf(_rake)) continue;
                if (col.GetComponentInParent<NetworkedPlayer>() != null) continue;
                if (col.GetComponentInParent<Animal_AI>() != null) continue;

                // Floors, roofs and gentle slopes are not climbable.
                if (Mathf.Abs(hit.normal.y) > 0.45f) continue;

                if (hit.distance >= bestDistance) continue;
                bestDistance = hit.distance;
                wall = hit;
                found = true;
            }

            return found;
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
                RakePosition = transform.position,
                FocusPosition = focusPosition,
                TargetPosition = targetPosition
            };

            manager.ClientSend(packet.Serialize());
            RakeMultiplayerPlugin.Log?.LogInfo("[RakePlay] sent attack request kind=" + kind +
                                               " target=" + targetPlayerId);
        }
        private void FaceInstant(Vector3 worldPosition)
        {
            Vector3 dir = worldPosition - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(dir);
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
        internal bool AttacksAllowedForHud { get { return AttacksAllowed(); } }

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
