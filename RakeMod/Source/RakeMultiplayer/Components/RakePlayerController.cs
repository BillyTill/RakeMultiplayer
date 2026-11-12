using System.Collections.Generic;
using UnityEngine;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    /// <summary>
    /// "Play as the Rake" mode (host or single-player).
    ///
    /// Third-person camera, WASD movement with a silent crouch and a run, and automatic
    /// attacks on whatever stands in front of the model. Attacks are only allowed at
    /// night: during the day the Rake spawns as a threat that cannot hit anything yet.
    ///
    /// The Rake player also gets a "sense": identical edge-of-screen markers for animals,
    /// players and traps within 100 m, so it cannot tell a trap from prey.
    /// </summary>
    public class RakePlayerController : MonoBehaviour
    {
        public static bool IsActive { get; private set; }
        public static Transform LocalHunter { get; private set; }

        private const float WalkSpeed = 2.2f;
        private const float RunSpeed = 5.0f;
        private const float StealthSpeed = 1.25f;

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

        private Transform _hunter;
        private CharacterController _hunterController;
        private bool _hunterControllerWasEnabled;
        private AudioListener _hunterListener;
        private bool _hunterListenerWasEnabled;
        private float _stepTimer;
        private float _hunterRetry;
        private bool _hunterFpsWasEnabled;
        private bool _hunterMovementWasEnabled;
        private bool _enemyWasEnabled;
        private bool _agentRotationWas = true;

        private readonly List<Camera> _hiddenCameras = new List<Camera>();
        private readonly List<Renderer> _hiddenRenderers = new List<Renderer>();

        private float _playerCooldown;
        private float _animalCooldown;
        private float _cameraCooldown;
        private float _attackAnimation;
        private string _lastAction = "—";

        private float _scanTimer;
        private readonly List<NetworkedPlayer> _players = new List<NetworkedPlayer>();
        private readonly List<Animal_AI> _animals = new List<Animal_AI>();
        private readonly List<Trap_proj> _traps = new List<Trap_proj>();

        private static Texture2D _pixel;
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
                DebugConsole.Print("rake play ON: WASD move, Shift run, Ctrl sneak (silent), mouse look. " +
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

            Camera source = Camera.main;
            if (source == null)
            {
                var spawner = PlayerSpawner.Instance;
                var local = spawner != null ? spawner.LocalPlayer : null;
                if (local != null) source = local.GetComponentInChildren<Camera>();
            }

            _enemyWasEnabled = _enemyAI != null && _enemyAI.enabled;
            if (_enemyAI != null) _enemyAI.enabled = false;

            if (_agent != null)
            {
                _agent.enabled = true;
                _agentRotationWas = _agent.updateRotation;
                _agent.updateRotation = false; // we rotate the body ourselves
                _agent.Stop();
                _agent.ResetPath();
            }

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
            }

            if (_enemyAI != null) _enemyAI.enabled = _enemyWasEnabled;
            if (_rakeAI != null) _rakeAI.SetPlayerControlled(false);
            if (_agent != null) _agent.updateRotation = _agentRotationWas;

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

            if (moving)
            {
                if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                {
                    _agent.Move(direction * speed * dt);
                }
                else
                {
                    _rake.position += direction * speed * dt;
                }

                Quaternion look = Quaternion.LookRotation(direction);
                _rake.rotation = Quaternion.Slerp(_rake.rotation, look, dt * 10f);
            }

            // Running/walking is audible; the sneak must stay silent.
            if (moving && !stealth)
            {
                _stepTimer -= dt;
                if (_stepTimer <= 0f)
                {
                    _stepTimer = run ? 0.34f : 0.52f;
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

        private void UpdateCamera(float dt)
        {
            if (_camera == null) return;

            Vector3 pivot = _rake.position + Vector3.up * 1.65f;
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
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
                _lastAction = "игрок";
            }
            else if (bestType == 1)
            {
                Animal_AI animal = best.GetComponent<Animal_AI>();
                if (animal != null) animal.TakeDamage(250, "RakeAnimalKick");
                _animalCooldown = AnimalCooldown;
                _lastAction = "животное";
            }
            else
            {
                RecordCamera_obj camera = best.GetComponent<RecordCamera_obj>();
                if (camera != null) camera.SetUnsetupState();
                _cameraCooldown = CameraCooldown;
                _lastAction = "камера";
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
            GUI.Label(new Rect(16f, 12f, 520f, 22f), "RAKE MODE  " + (night ? "НОЧЬ — атака доступна" : "ДЕНЬ — атака заблокирована"), _hud);

            _small.normal.textColor = Color.white;
            GUI.Label(new Rect(16f, 36f, 560f, 20f),
                "WASD — движение, Shift — бег, Ctrl — красться (без звука), мышь — камера", _small);
            GUI.Label(new Rect(16f, 56f, 560f, 20f),
                "Кулдауны: игрок " + Mathf.CeilToInt(_playerCooldown) + "с / животное " +
                Mathf.CeilToInt(_animalCooldown) + "с / камера " + Mathf.CeilToInt(_cameraCooldown) +
                "с   последняя цель: " + _lastAction, _small);

            DrawSense();
        }

        /// <summary>
        /// Edge-of-screen markers for animals, players and traps within 100 m. The marker is
        /// identical for every type on purpose: the Rake player cannot tell a trap from prey.
        /// </summary>
        private void DrawSense()
        {
            if (_camera == null) return;

            DrawSenseList(_players.Count, i => _players[i] != null ? _players[i].transform.position + Vector3.up : Vector3.zero);
            DrawSenseList(_animals.Count, i => _animals[i] != null ? _animals[i].transform.position + Vector3.up * 0.8f : Vector3.zero);
            DrawSenseList(_traps.Count, i => _traps[i] != null ? _traps[i].transform.position + Vector3.up * 0.4f : Vector3.zero);
        }

        private void DrawSenseList(int count, System.Func<int, Vector3> position)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 world = position(i);
                if (world == Vector3.zero) continue;

                Vector3 delta = world - _rake.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > SenseRange * SenseRange) continue;

                DrawMarker(world);
            }
        }

        private void DrawMarker(Vector3 world)
        {
            Vector3 screen = _camera.WorldToScreenPoint(world);
            bool behind = screen.z <= 0.05f;

            Vector2 point = new Vector2(screen.x, Screen.height - screen.y);
            if (behind)
            {
                point = new Vector2(Screen.width - point.x, Screen.height - point.y);
            }

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 direction = point - center;
            if (behind) direction = -direction;

            float margin = 46f;
            float maxX = Screen.width * 0.5f - margin;
            float maxY = Screen.height * 0.5f - margin;

            bool onScreen = !behind &&
                            point.x > margin && point.x < Screen.width - margin &&
                            point.y > margin && point.y < Screen.height - margin;

            Vector2 drawAt;
            if (onScreen)
            {
                drawAt = point;
            }
            else
            {
                float scaleX = maxX / Mathf.Max(Mathf.Abs(direction.x), 0.001f);
                float scaleY = maxY / Mathf.Max(Mathf.Abs(direction.y), 0.001f);
                float scale = Mathf.Min(scaleX, scaleY);
                drawAt = center + direction * scale;
            }

            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.9f);

            float size = onScreen ? 6f : 9f;
            GUI.DrawTexture(new Rect(drawAt.x - size * 0.5f, drawAt.y - size * 0.5f, size, size), Pixel);

            if (!onScreen)
            {
                Vector2 outward = direction.normalized;
                float length = 14f;
                float endX = drawAt.x + outward.x * length;
                float endY = drawAt.y + outward.y * length;
                GUI.DrawTexture(new Rect(Mathf.Min(drawAt.x, endX), Mathf.Min(drawAt.y, endY),
                    Mathf.Max(Mathf.Abs(endX - drawAt.x), 1f), Mathf.Max(Mathf.Abs(endY - drawAt.y), 1f)), Pixel);
            }

            GUI.color = old;
        }

        private static Texture2D Pixel
        {
            get
            {
                if (_pixel == null)
                {
                    _pixel = new Texture2D(1, 1);
                    _pixel.SetPixel(0, 0, Color.white);
                    _pixel.Apply();
                    _pixel.hideFlags = HideFlags.HideAndDontSave;
                }
                return _pixel;
            }
        }
    }
}
