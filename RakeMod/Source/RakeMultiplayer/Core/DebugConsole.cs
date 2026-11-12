using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// In-game debug console. Toggle with the backquote key (` / ~).
    ///
    /// Type "help" for the command list. Commands are registered by name; a name may
    /// contain spaces (e.g. "debug camera") and is matched greedily, so
    /// "debug camera speed 20" resolves to "debug camera" with args ["speed", "20"].
    /// </summary>
    public class DebugConsole : MonoBehaviour
    {
        public delegate string CommandHandler(string[] args);

        private sealed class Command
        {
            public string Name;
            public string Usage;
            public string Description;
            public CommandHandler Handler;
        }

        public static DebugConsole Instance { get; private set; }

        /// <summary>True while the console window is open (other input handlers check this).</summary>
        public static bool ConsoleOpen
        {
            get { return Instance != null && Instance._open; }
        }

        private static readonly Dictionary<string, Command> Commands =
            new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);

        private static bool _registered;

        private readonly List<string> _log = new List<string>();
        private readonly List<string> _history = new List<string>();
        private int _historyPos = -1;

        private string _input = string.Empty;
        private bool _open;
        private Vector2 _scroll;
        private bool _scrollToBottom;

        private GUIStyle _labelStyle;
        private GUIStyle _inputStyle;
        private GUIStyle _boxStyle;
        private Texture2D _background;

        // ---------------------------------------------------------------- lifecycle

        private void Awake()
        {
            Instance = this;
            EnsureRegistered();
            Print("Rake Multiplayer debug console. Type 'help'.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_background != null) Destroy(_background);
        }

        private void Update()
        {
            // Open with `; close with Escape or 'quit' inside the console.
            if (!_open && Input.GetKeyDown(KeyCode.BackQuote))
            {
                Open();
            }

            // Keep the player frozen while the console or the debug camera is active.
            PlayerInputLock.SetConsole(_open);
        }

        // ---------------------------------------------------------------- commands

        public static void Register(string name, string usage, string description, CommandHandler handler)
        {
            if (string.IsNullOrEmpty(name) || handler == null) return;

            string key = name.Trim().ToLowerInvariant();
            Commands[key] = new Command
            {
                Name = key,
                Usage = string.IsNullOrEmpty(usage) ? key : usage,
                Description = description ?? string.Empty,
                Handler = handler,
            };
        }

        /// <summary>Add a line to the console log (also visible while the console is closed).</summary>
        public static void Print(string message)
        {
            if (message == null) return;

            var target = Instance;
            if (target == null)
            {
                // No console yet: keep it in the BepInEx log only.
                RakeMultiplayerPlugin.Log?.LogInfo("[Console] " + message);
                return;
            }

            string[] lines = message.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                target._log.Add(lines[i]);
                RakeMultiplayerPlugin.Log?.LogInfo("[Console] " + lines[i]);
            }

            // Keep the buffer bounded
            const int maxLines = 400;
            if (target._log.Count > maxLines)
            {
                target._log.RemoveRange(0, target._log.Count - maxLines);
            }

            target._scrollToBottom = true;
        }

        private void Execute(string rawLine)
        {
            string line = (rawLine ?? string.Empty).Trim();
            if (line.Length == 0) return;

            Print("> " + line);

            if (_history.Count == 0 || _history[_history.Count - 1] != line)
            {
                _history.Add(line);
                if (_history.Count > 64) _history.RemoveAt(0);
            }
            _historyPos = -1;

            string[] tokens = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return;

            Command command = null;
            int used = 0;
            int maxTokens = Mathf.Min(tokens.Length, 3);
            for (int n = maxTokens; n >= 1; n--)
            {
                string candidate = string.Join(" ", tokens, 0, n).ToLowerInvariant();
                if (Commands.TryGetValue(candidate, out command))
                {
                    used = n;
                    break;
                }
                command = null;
            }

            if (command == null)
            {
                Print("Unknown command '" + tokens[0] + "'. Type 'help'.");
                return;
            }

            var args = new string[tokens.Length - used];
            if (args.Length > 0) Array.Copy(tokens, used, args, 0, args.Length);

            try
            {
                string result = command.Handler(args);
                if (!string.IsNullOrEmpty(result)) Print(result);
            }
            catch (Exception ex)
            {
                Print("ERROR: " + ex.Message);
            }
        }

        // ---------------------------------------------------------------- built-in commands

        private static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;

            Register("help", "help [command]", "List commands, or describe one", args =>
            {
                if (args.Length > 0)
                {
                    string key = string.Join(" ", args).ToLowerInvariant();
                    Command c;
                    if (Commands.TryGetValue(key, out c))
                    {
                        return c.Usage + "\n" + c.Description;
                    }
                    return "No such command: " + key;
                }

                var sb = new StringBuilder();
                sb.AppendLine("Commands:");
                foreach (var kv in Commands)
                {
                    sb.Append("  ").Append(kv.Value.Usage);
                    int pad = 34 - kv.Value.Usage.Length;
                    if (pad > 0) sb.Append(' ', pad);
                    sb.AppendLine(kv.Value.Description);
                }
                return sb.ToString();
            });

            Register("clear", "clear", "Clear the console output", args =>
            {
                if (Instance != null) Instance._log.Clear();
                return null;
            });

            Register("echo", "echo <text>", "Print text back", args => string.Join(" ", args));

            Register("quit", "quit", "Close the console", args =>
            {
                if (Instance != null) Instance.Close();
                return null;
            });

            Register("debug camera", "debug camera [on|off|toggle] [speed <n>]",
                "Free-fly spectator camera (WASD, Q/E down/up, Shift = fast)", args =>
            {
                if (args.Length >= 2 && args[0].Equals("speed", StringComparison.OrdinalIgnoreCase))
                {
                    float speed;
                    if (!float.TryParse(args[1], out speed) || speed <= 0f) return "usage: debug camera speed <n>";
                    DebugFreeCamera.SetSpeed(speed);
                    return "debug camera speed = " + speed;
                }

                string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "toggle";
                bool want;
                if (mode == "on") want = true;
                else if (mode == "off") want = false;
                else want = !DebugFreeCamera.IsActive;

                DebugFreeCamera.SetEnabled(want);
                return want
                    ? "debug camera ON  (WASD move, Q/E down/up, Shift fast, 'debug camera off' to stop)"
                    : "debug camera OFF";
            });

            Register("tp", "tp <x> <y> <z>", "Teleport the local player", args =>
            {
                if (args.Length < 3) return "usage: tp <x> <y> <z>";

                float x, y, z;
                if (!float.TryParse(args[0], out x) ||
                    !float.TryParse(args[1], out y) ||
                    !float.TryParse(args[2], out z))
                {
                    return "tp: coordinates must be numbers";
                }

                var player = LocalPlayer();
                if (player == null) return "tp: no local player";

                var position = new Vector3(x, y, z);
                var controller = player.GetComponent<CharacterController>();
                bool wasEnabled = controller != null && controller.enabled;
                if (wasEnabled) controller.enabled = false;
                player.position = position;
                if (wasEnabled) controller.enabled = true;

                return "teleported to " + position.ToString("F2");
            });

            Register("players", "players", "List local and remote players", args =>
            {
                var spawner = PlayerSpawner.Instance;
                if (spawner == null) return "players: PlayerSpawner not available";

                var sb = new StringBuilder();
                var local = spawner.LocalPlayer;
                sb.AppendLine(local != null
                    ? "local : " + local.name + "  id=" + local.PlayerId + "  pos=" + local.transform.position.ToString("F2")
                    : "local : <none>");

                int count = 0;
                foreach (var t in spawner.GetAllRemotePlayerTransforms())
                {
                    if (t == null) continue;
                    count++;
                    sb.AppendLine("remote: " + t.name + "  pos=" + t.position.ToString("F2"));
                }
                if (count == 0) sb.AppendLine("remote: <none>");
                return sb.ToString();
            });

            Register("net", "net", "Show connection state", args =>
            {
                return "server=" + RakeMultiplayerPlugin.isServer +
                       " connected=" + RakeMultiplayerPlugin.isConnected +
                       " clients=" + RakeMultiplayerPlugin.GetConnectedClientCount() +
                       " scene=" + Application.loadedLevelName;
            });

            Register("model", "model", "Show the loaded player body model", args =>
            {
                if (!RemotePlayerModelData.EnsureLoaded())
                {
                    return "model: not loaded (" + RemotePlayerModelData.Error + ")";
                }

                var sb = new StringBuilder();
                sb.AppendLine("model: nodes=" + RemotePlayerModelData.Nodes.Length +
                              " bones=" + RemotePlayerModelData.BoneNodes.Length +
                              " animNodes=" + RemotePlayerModelData.AnimNodes.Length +
                              " verts=" + RemotePlayerModelData.Mesh.vertexCount);
                sb.AppendLine("clips: " + RemotePlayerModelData.ClipNames());
                sb.AppendLine("weapon: " + (RemotePlayerWeaponData.EnsureLoaded()
                    ? "loaded" : "missing (" + RemotePlayerWeaponData.Error + ")"));
                return sb.ToString();
            });

            Register("preview", "preview [next|remove]", "Spawn / cycle / remove the body preview (same as F10/F11)", args =>
            {
                string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "next";
                if (sub == "remove")
                {
                    RemotePlayerPreview.Remove();
                    return "preview removed";
                }
                RemotePlayerPreview.Next();
                return "preview advanced";
            });

            Register("anim", "anim <clip>", "Force a clip on the body preview (spawn it first)", args =>
            {
                if (args.Length < 1) return "usage: anim <clip>  (e.g. anim Walk)";
                if (!RemotePlayerPreview.PlayClip(args[0]))
                {
                    return "anim: no preview. Use 'preview' first, or check the clip name with 'model'.";
                }
                return "preview clip = " + args[0];
            });

            Register("audio", "audio", "Show audio state (listener volume, weapon sound sources)", args =>
                AudioDiagnostics.Describe());

            Register("rake hit", "rake hit [damage]",
                "Simulate a Rake hit on the local player (default 0 = all effects, no health loss)", args =>
            {
                var player = LocalPlayer();
                if (player == null) return "rake hit: no local player";

                var fps = player.GetComponent<FPSController>();
                if (fps == null) return "rake hit: no FPSController on the local player";

                if (PlayerRagdoll.IsActive) return "rake hit: reaction already playing";

                int damage = 0;
                if (args.Length > 0) int.TryParse(args[0], out damage);

                bool triggered = false;
                try
                {
                    // Goes through the real damage path: pain sound, effects and the
                    // ragdoll hit reaction. 0 damage by default, so health is untouched.
                    fps.TakeDamage(damage, "RakeAttack");
                    triggered = PlayerRagdoll.IsActive;
                }
                catch (Exception ex)
                {
                    Print("rake hit: TakeDamage failed (" + ex.Message + "), triggering the reaction directly");
                }

                if (!triggered)
                {
                    var rake = RakeHighlight.FindRake();
                    Vector3 away = rake != null ? player.position - rake.position : Vector3.zero;
                    triggered = PlayerRagdoll.Trigger(away);
                }

                return triggered
                    ? "rake hit simulated (" + damage + " damage)"
                    : "rake hit: could not start the reaction";
            });


            Register("ragdoll", "ragdoll [playerId|all]",
                "Play the hit reaction for yourself, for one player id, or for everyone", args =>
            {
                var spawner = PlayerSpawner.Instance;
                if (spawner == null) return "ragdoll: no PlayerSpawner";

                var local = spawner.LocalPlayer;
                if (local == null) return "ragdoll: no local player";

                var rake = RakeHighlight.FindRake();
                Vector3 away = rake != null ? local.transform.position - rake.position : Vector3.zero;

                if (args.Length > 0 && args[0].Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    int count = 0;
                    if (PlayerRagdoll.TriggerNetworked(local.PlayerId, away)) count++;

                    foreach (var t in spawner.GetAllRemotePlayerTransforms())
                    {
                        if (t == null) continue;
                        var np = t.GetComponent<NetworkedPlayer>();
                        if (np == null) continue;
                        if (PlayerRagdoll.TriggerNetworked(np.PlayerId, away)) count++;
                    }

                    return "ragdoll for " + count + " player(s)";
                }

                int id = local.PlayerId;
                if (args.Length > 0) int.TryParse(args[0], out id);

                return PlayerRagdoll.TriggerNetworked(id, away)
                    ? "ragdoll started for player " + id
                    : "ragdoll: could not start for player " + id;
            });


            Register("loading", "loading [connect|off]", "Preview the styled loading screen (6 s)", args =>
            {
                if (args.Length > 0 && args[0].Equals("off", StringComparison.OrdinalIgnoreCase))
                {
                    RakeMultiplayer.UI.LoadingScreen.Hide();
                    return "loading: hidden";
                }

                bool connect = args.Length > 0 && args[0].Equals("connect", StringComparison.OrdinalIgnoreCase);

                if (connect)
                {
                    RakeMultiplayer.UI.LoadingScreen.Show("CONTACTING THE HOST", "ONLINE",
                        "Handshaking with the host", true);
                }
                else
                {
                    RakeMultiplayer.UI.LoadingScreen.Show("THE HUNT BEGINS", "HOST",
                        "Preparing the forest", false);
                    RakeMultiplayer.UI.LoadingScreen.SetProgress(0.45f);
                    Patches.CoroutineHelper.DelayedCall(2.0f, () =>
                        RakeMultiplayer.UI.LoadingScreen.SetStatus("Loading the level"));
                    Patches.CoroutineHelper.DelayedCall(2.8f, () =>
                        RakeMultiplayer.UI.LoadingScreen.SetProgress(1f));
                }

                Patches.CoroutineHelper.DelayedCall(5.5f, RakeMultiplayer.UI.LoadingScreen.Hide);
                return "loading: preview shown for 6 s";
            });
            Register("host", "host", "Start hosting (same as HOST GAME in the menu)", args =>
            {
                var menu = UnityEngine.Object.FindObjectOfType<RakeMultiplayer.UI.MultiplayerMenu>();
                if (menu == null) return "host: menu not found";
                menu.OnHostGame();
                return "host: started";
            });

            Register("role", "role", "Take/leave the Rake slot in the lobby", args =>
            {
                var menu = UnityEngine.Object.FindObjectOfType<RakeMultiplayer.UI.MultiplayerMenu>();
                if (menu == null) return "role: menu not found";
                menu.OnToggleRakeRole();
                return "role: owner=P" + LobbyState.RakeOwnerId;
            });

            Register("start", "start", "Start the match (host only)", args =>
            {
                var menu = UnityEngine.Object.FindObjectOfType<RakeMultiplayer.UI.MultiplayerMenu>();
                if (menu == null) return "start: menu not found";
                menu.OnStartGame();
                return "start: requested";
            });
            Register("cams", "cams", "Open/close the camera monitor from anywhere (same as Tab)", args =>
            {
                if (RakePlayerController.IsActive) return "cams: not while playing the Rake";
                return CameraViewHotkey.Toggle();
            });
            Register("rake play", "rake play [on|off]",
                "Play as the Rake (host/single-player): third-person, sneak/run, auto-attack at night", args =>
            {
                bool want = !RakePlayerController.IsActive;
                if (args.Length > 0)
                {
                    if (args[0].Equals("on", StringComparison.OrdinalIgnoreCase)) want = true;
                    else if (args[0].Equals("off", StringComparison.OrdinalIgnoreCase)) want = false;
                }

                RakePlayerController.SetEnabled(want);
                return RakePlayerController.IsActive ? "rake play: ON" : "rake play: OFF";
            });

            Register("rake info", "rake info",
                "Log the Rake transform, animator and NavMeshAgent state", args =>
            {
                var rake = RakeHighlight.FindRake();
                if (rake == null) return "rake: not found in this scene";

                var rakeAnimator = rake.GetComponent<Animator>();
                if (rakeAnimator == null) rakeAnimator = rake.GetComponentInChildren<Animator>();

                var rakeAgent = rake.GetComponent<NavMeshAgent>();
                if (rakeAgent == null) rakeAgent = rake.GetComponentInChildren<NavMeshAgent>();

                var sb = new StringBuilder();
                sb.AppendLine("pos=" + rake.position.ToString("F2") + "  euler=" + rake.eulerAngles.ToString("F1"));
                sb.AppendLine("forward=" + rake.forward.ToString("F2"));
                sb.AppendLine("animator=" + (rakeAnimator != null ? rakeAnimator.name : "null") +
                              "  rootMotion=" + (rakeAnimator != null && rakeAnimator.applyRootMotion) +
                              "  speed=" + (rakeAnimator != null ? rakeAnimator.GetFloat("Speed") : 0f));
                sb.AppendLine("agent=" + (rakeAgent != null ? (rakeAgent.enabled ? "on" : "off") : "null") +
                              "  vel=" + (rakeAgent != null ? rakeAgent.velocity.ToString("F2") : "-"));

                var smr = rake.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    sb.AppendLine("model=" + smr.transform.name +
                                  "  forward=" + smr.transform.forward.ToString("F2") +
                                  "  euler=" + smr.transform.eulerAngles.ToString("F1"));
                }
                return sb.ToString();
            });


            Register("rake stun", "rake stun [seconds]", "Stun the Rake (host only)", args =>
            {
                var rake = UnityEngine.Object.FindObjectOfType<NetworkedRakeAI>();
                if (rake == null) return "rake: not found in this scene";
                if (!RakeMultiplayerPlugin.isServer) return "rake: only the host can stun it";

                float seconds = 3f;
                if (args.Length > 0) float.TryParse(args[0], out seconds);
                rake.Stun(seconds);
                return "Rake stunned for " + seconds.ToString("F1") + "s";
            });

            Register("noclip", "noclip", "Toggle CharacterController collision for the local player", args =>
            {
                var player = LocalPlayer();
                if (player == null) return "noclip: no local player";

                var controller = player.GetComponent<CharacterController>();
                if (controller == null) return "noclip: no CharacterController";

                controller.enabled = !controller.enabled;
                return "noclip " + (controller.enabled ? "OFF" : "ON");
            });

            Register("rake highlight", "rake highlight [on|off|toggle]",
                "Highlight the Rake with a bright light and a marker above its head", args =>
            {
                string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "toggle";
                bool want;
                if (mode == "on") want = true;
                else if (mode == "off") want = false;
                else want = !RakeHighlight.IsActive;

                RakeHighlight.SetEnabled(want);
                return want ? "rake highlight ON" : "rake highlight OFF";
            });

            CommandHandler focusRake = args =>
            {
                if (!DebugFreeCamera.IsActive)
                {
                    return "debug camera is not active - run 'debug camera on' first";
                }

                var rake = RakeHighlight.FindRake();
                if (rake == null) return "rake: not found in this scene";

                return DebugFreeCamera.FocusOn(rake)
                    ? "debug camera moved to the Rake at " + rake.position.ToString("F1")
                    : "debug camera: could not move";
            };

            Register("debug camera rake", "debug camera rake",
                "Move the debug camera to the Rake (needs 'debug camera on')", focusRake);
            Register("rake camera", "rake camera",
                "Alias of 'debug camera rake'", focusRake);
        }


        private static Transform LocalPlayer()
        {
            var spawner = PlayerSpawner.Instance;
            if (spawner == null) return null;
            var local = spawner.LocalPlayer;
            return local != null ? local.transform : null;
        }

        // ---------------------------------------------------------------- window state

        private void Open()
        {
            _open = true;
            _scrollToBottom = true;
            Print("console opened - 'help' for commands, Escape to close");
        }

        private void Close()
        {
            _open = false;
            _input = string.Empty;
        }

        private void HistoryPrev()
        {
            if (_history.Count == 0) return;
            if (_historyPos < 0) _historyPos = _history.Count;
            _historyPos = Mathf.Max(0, _historyPos - 1);
            _input = _history[_historyPos];
        }

        private void HistoryNext()
        {
            if (_history.Count == 0 || _historyPos < 0) return;
            _historyPos++;
            if (_historyPos >= _history.Count)
            {
                _historyPos = -1;
                _input = string.Empty;
                return;
            }
            _input = _history[_historyPos];
        }

        // ---------------------------------------------------------------- rendering

        private void EnsureStyles()
        {
            if (_labelStyle != null) return;

            _background = new Texture2D(1, 1);
            _background.SetPixel(0, 0, new Color(0.02f, 0.02f, 0.03f, 0.88f));
            _background.Apply();
            _background.hideFlags = HideFlags.HideAndDontSave;

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = _background;

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 12;
            _labelStyle.normal.textColor = new Color(0.85f, 0.92f, 0.85f);
            _labelStyle.wordWrap = false;

            _inputStyle = new GUIStyle(GUI.skin.textField);
            _inputStyle.fontSize = 13;
        }

        private void OnGUI()
        {
            if (!_open) return;
            EnsureStyles();

            Event e = Event.current;
            if (e != null && e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    Submit();
                    e.Use();
                    return;
                }
                if (e.keyCode == KeyCode.Escape)
                {
                    Close();
                    e.Use();
                    return;
                }
                if (e.keyCode == KeyCode.UpArrow)
                {
                    HistoryPrev();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.DownArrow)
                {
                    HistoryNext();
                    e.Use();
                }
            }

            float height = Mathf.Clamp(Screen.height * 0.45f, 200f, Screen.height - 60f);
            var area = new Rect(8f, Screen.height - height - 8f, Screen.width - 16f, height);
            GUI.Box(area, GUIContent.none, _boxStyle);

            GUILayout.BeginArea(new Rect(area.x + 10f, area.y + 8f, area.width - 20f, area.height - 16f));

            if (_scrollToBottom)
            {
                _scroll.y = float.MaxValue;
                _scrollToBottom = false;
            }

            _scroll = GUILayout.BeginScrollView(_scroll, false, true);
            for (int i = 0; i < _log.Count; i++)
            {
                GUILayout.Label(_log[i], _labelStyle);
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            GUILayout.Label(">", _labelStyle, GUILayout.Width(12f));
            GUI.SetNextControlName("rakeConsoleInput");
            _input = GUILayout.TextField(_input, _inputStyle);
            GUILayout.EndHorizontal();

            GUILayout.EndArea();

            GUI.FocusControl("rakeConsoleInput");
        }

        private void Submit()
        {
            string line = _input;
            _input = string.Empty;
            Execute(line);
            _scrollToBottom = true;
        }
    }

    /// <summary>
    /// Freezes the local player's look/movement while the console or the debug camera is
    /// active, and restores whatever state they had before.
    /// </summary>
    internal static class PlayerInputLock
    {
        private static bool _console;
        private static bool _camera;
        private static bool _ragdoll;
        private static bool _applied;

        private static CameraMouseLook _look;
        private static PlayerMovement _movement;
        private static bool _savedControl;
        private static bool _savedCanMove;

        public static void SetConsole(bool active)
        {
            _console = active;
            Apply();
        }

        public static void SetRagdoll(bool active)
        {
            _ragdoll = active;
            Apply();
        }

        public static void SetCamera(bool active)
        {
            _camera = active;
            Apply();
        }

        /// <summary>
        /// Called when a ragdoll reaction ends. The vanilla Rake attack freezes the player
        /// *before* dealing damage and unfreezes it right after, so the flags captured when
        /// the ragdoll started are stale (false). Restoring them would leave the player
        /// frozen for good, so standing up always hands control back.
        /// </summary>
        public static void ReleaseRagdoll()
        {
            _ragdoll = false;
            Apply();

            // Another lock (console / debug camera) still owns the input.
            if (_console || _camera) return;

            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;
            if (local == null) return;

            var fps = local.GetComponent<FPSController>();
            if (fps != null && fps.Health < 0) return; // dead: the game owns the input

            var movement = local.GetComponent<PlayerMovement>();
            if (movement != null) movement.UnFreezePlayer();

            var look = local.GetComponentInChildren<CameraMouseLook>();
            if (look != null) look.bControl = true;
        }

        private static void Apply()
        {
            bool locked = _console || _camera || _ragdoll;
            if (locked == _applied) return;

            if (locked)
            {
                var spawner = PlayerSpawner.Instance;
                var local = spawner != null ? spawner.LocalPlayer : null;
                if (local == null) return; // try again on the next call

                _look = local.GetComponentInChildren<CameraMouseLook>();
                _movement = local.GetComponent<PlayerMovement>();

                _savedControl = _look != null ? _look.bControl : true;
                _savedCanMove = _movement != null ? _movement.bCanMove : true;

                if (_look != null) _look.bControl = false;
                if (_movement != null) _movement.FreezePlayer();

                _applied = true;
            }
            else
            {
                if (_look != null) _look.bControl = _savedControl;
                if (_movement != null)
                {
                    if (_savedCanMove) _movement.UnFreezePlayer();
                    else _movement.FreezePlayer();
                }

                _look = null;
                _movement = null;
                _applied = false;
            }
        }
    }
}
