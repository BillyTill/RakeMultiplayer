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
        private string _lastAction = "-";

        private float _scanTimer;
        private readonly List<NetworkedPlayer> _players = new List<NetworkedPlayer>();
        private readonly List<Animal_AI> _animals = new List<Animal_AI>();
        private readonly List<Trap_proj> _traps = new List<Trap_proj>();

        private static Texture2D _ring;
        private static Texture2D _arrow;
        private static GUIStyle _hud;
        private static GUIStyle _small;

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

                if (RakeMultiplayerPlugin.isConnected && !RakeMultiplayerPlugin.isServer)
                {
                    DebugConsole.Print("rake play: only the host can control the Rake");
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
            if (_rakeAI != null) _rakeAI.SetPlayerControlled(false);
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

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _playerCooldown = Mathf.Max(0f, _playerCooldown - dt);
            _animalCooldown = Mathf.Max(0f, _animalCooldown - dt);
            _cameraCooldown = Mathf.Max(0f, _cameraCooldown - dt);
            _attackAnimation = Mathf.Max(0f, _attackAnimation - dt);

            if (!DebugConsole.ConsoleOpen)
            {
                if (Input.GetKeyDown(KeyCode.V)) _firstPerson = !_firstPerson;

                ReadLook();
                ReadMovement(dt);
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

            if (AttacksAllowed() && !DebugConsole.ConsoleOpen) TryAutoAttack();
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

            if (_rakeController != null && _rakeController.enabled)
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
            }
            else if (moving)
            {
                _rake.position += direction * speed * dt;
            }

            if (_firstPerson)
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
            if (moving && !stealth)
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
                if (stealth)
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
        }

        private void TryAutoAttack()
        {
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

            if (bestType == 0)
            {
                NetworkedPlayer player = best.GetComponent<NetworkedPlayer>();
                if (player != null && _rakeAI != null) _rakeAI.AttackPlayer(best);
                _playerCooldown = PlayerCooldown;
                _lastAction = "player";
            }
            else if (bestType == 1)
            {
                Animal_AI animal = best.GetComponent<Animal_AI>();
                if (animal != null) animal.TakeDamage(250, "RakeAnimalKick");
                _animalCooldown = AnimalCooldown;
                _lastAction = "animal";
            }
            else
            {
                RecordCamera_obj camera = best.GetComponent<RecordCamera_obj>();
                if (camera != null) camera.SetUnsetupState();
                _cameraCooldown = CameraCooldown;
                _lastAction = "camera";
            }

            _attackAnimation = 1.1f;
            if (_animator != null) _animator.SetBool("bAttack", true);

            if (_audio != null && _enemyAI != null && _enemyAI.playerAttackRoars != null &&
                _enemyAI.playerAttackRoars.Length > 0)
            {
                _audio.PlayOneShot(_enemyAI.playerAttackRoars[Random.Range(0, _enemyAI.playerAttackRoars.Length)]);
            }

            RakeMultiplayerPlugin.Log?.LogInfo("[RakePlay] attacked " + best.name + " (" + _lastAction + ")");
        }

        private bool InFront(Vector3 targetPosition, out float distance)
        {
            Vector3 to = targetPosition - _rake.position;
            to.y = 0f;
            distance = to.magnitude;
            if (distance > AttackRange || distance < 0.01f) return false;
            return Vector3.Angle(_rake.forward, to) <= AttackCone;
        }

        // ----------------------------------------------------------------------- HUD

        private void OnGUI()
        {
            if (_rake == null) return;

            if (_hud == null)
            {
                _hud = new GUIStyle(GUI.skin.label);
                _hud.fontSize = 14;
                _hud.fontStyle = FontStyle.Bold;
                _small = new GUIStyle(GUI.skin.label);
                _small.fontSize = 12;
            }

            bool night = AttacksAllowed();
            _hud.normal.textColor = night ? new Color(1f, 0.45f, 0.15f) : new Color(0.65f, 0.75f, 1f);

            GUI.depth = -900;
            GUI.Label(new Rect(16f, 12f, 560f, 22f),
                "RAKE MODE  " + (night ? "NIGHT - attacks enabled" : "DAY - attacks disabled"), _hud);

            _small.normal.textColor = Color.white;
            GUI.Label(new Rect(16f, 36f, 620f, 20f),
                "WASD move, Shift run, Ctrl sneak (silent), V view (" + (_firstPerson ? "1st" : "3rd") + "), mouse look", _small);
            GUI.Label(new Rect(16f, 56f, 620f, 20f),
                "Cooldowns: player " + Mathf.CeilToInt(_playerCooldown) + "s / animal " +
                Mathf.CeilToInt(_animalCooldown) + "s / camera " + Mathf.CeilToInt(_cameraCooldown) +
                "s   last: " + _lastAction, _small);

            DrawSense();
        }

        /// <summary>
        /// WoT-style sense: a ring in the middle of the screen (top = forward, bottom =
        /// backward) with an arrow on the ring for every target within 100 m. The arrow
        /// is identical for players, animals and traps on purpose.
        /// </summary>
        private void DrawSense()
        {
            if (_camera == null) return;

            float radius = Mathf.Min(Screen.width, Screen.height) * 0.38f;
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            Color oldColor = GUI.color;
            Matrix4x4 oldMatrix = GUI.matrix;

            DrawSenseList(_players.Count, i => _players[i] != null ? _players[i].transform.position : Vector3.zero, center, radius);
            DrawSenseList(_animals.Count, i => _animals[i] != null ? _animals[i].transform.position : Vector3.zero, center, radius);
            DrawSenseList(_traps.Count, i => _traps[i] != null ? _traps[i].transform.position : Vector3.zero, center, radius);

            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private void DrawSenseList(int count, System.Func<int, Vector3> position, Vector2 center, float radius)
        {
            Vector3 forward = _rake.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, forward);

            for (int i = 0; i < count; i++)
            {
                Vector3 world = position(i);
                if (world == Vector3.zero) continue;

                Vector3 delta = world - _rake.position;
                delta.y = 0f;
                if (delta.sqrMagnitude < 0.01f || delta.sqrMagnitude > SenseRange * SenseRange) continue;

                delta.Normalize();

                // 0 = straight ahead, positive = to the right (clockwise on the ring).
                float angle = Mathf.Atan2(Vector3.Dot(delta, right), Vector3.Dot(delta, forward));

                Vector2 outward = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
                Vector2 point = center + outward * radius;

                float degrees = Mathf.Atan2(outward.x, -outward.y) * Mathf.Rad2Deg;

                Matrix4x4 oldMatrix = GUI.matrix;
                GUI.color = new Color(1f, 0.12f, 0.08f, 0.95f);
                GUIUtility.RotateAroundPivot(degrees, point);
                GUI.DrawTexture(new Rect(point.x - 9f, point.y - 12f, 18f, 20f), Arrow);
                GUI.matrix = oldMatrix;
            }
        }

        private static Texture2D Ring
        {
            get
            {
                if (_ring == null)
                {
                    const int size = 256;
                    _ring = new Texture2D(size, size);
                    float center = (size - 1) * 0.5f;
                    float outer = center - 1f;
                    float inner = outer - 3f;

                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dx = x - center;
                            float dy = y - center;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            float alpha = (d <= outer && d >= inner) ? 0.6f : 0f;
                            _ring.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                        }
                    }

                    _ring.Apply();
                    _ring.hideFlags = HideFlags.HideAndDontSave;
                }
                return _ring;
            }
        }

        private static Texture2D Arrow
        {
            get
            {
                if (_arrow == null)
                {
                    const int size = 32;
                    _arrow = new Texture2D(size, size);

                    for (int y = 0; y < size; y++)
                    {
                        float t = y / (float)(size - 1);
                        float half = 2f + (1f - t) * 11f; // wide base, narrow tip at the top

                        for (int x = 0; x < size; x++)
                        {
                            bool inside = y >= 3 && y <= 29 &&
                                          Mathf.Abs(x - (size - 1) * 0.5f) <= half;
                            _arrow.SetPixel(x, y, inside ? Color.white : new Color(1f, 1f, 1f, 0f));
                        }
                    }

                    _arrow.Apply();
                    _arrow.hideFlags = HideFlags.HideAndDontSave;
                }
                return _arrow;
            }
        }
    }
}
