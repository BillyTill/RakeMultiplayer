using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using RakeMultiplayer.Networking;
using RakeMultiplayer;

namespace RakeMultiplayer.UI
{
    /// <summary>
    /// Multiplayer menu UI for lobby and connection management
    /// UI is built programmatically at runtime via RuntimeUIBuilder
    /// </summary>
    public class MultiplayerMenu : MonoBehaviour
    {
        private RuntimeUIBuilder _uiBuilder;
        private RakeNetworkManager _networkManager;
        private GameObject _mainContainer;
        private bool _menuVisible = false;
        private bool _waitingForConnection = false;
        private float _connectionTimer = 0f;
        private bool _lastIsServer = false;
        private bool _lastIsConnected = false;
        private string _lastSceneName;
        private SteamLobbyManager.LobbyInfo _steamSelectedLobby;
        private float _steamUiTimer;

        /// <summary>True while the F1 menu (or any of its panels) is on screen.</summary>
        public static bool IsOpen
        {
            get
            {
                var menu = FindObjectOfType<MultiplayerMenu>();
                return menu != null && menu._menuVisible;
            }
        }

        private void Awake()
        {
            _networkManager = UnityEngine.Object.FindObjectOfType<RakeNetworkManager>();
            
            // Build UI at runtime
            _uiBuilder = gameObject.AddComponent<RuntimeUIBuilder>();
            _uiBuilder.Initialize(this);
        }

        public void OnUIBuilt(RuntimeUIBuilder builder)
        {
            _uiBuilder = builder;
            // Initially hide the main container
            if (_mainContainer != null)
                _mainContainer.SetActive(false);
        }

        public void SetMainContainer(GameObject container)
        {
            _mainContainer = container;
            if (_mainContainer != null)
                _mainContainer.SetActive(false);
        }

        private void Update()
        {
            // A freshly loaded match closes the menu that started it.
            string sceneName = UnityEngine.Application.loadedLevelName;
            if (sceneName != _lastSceneName)
            {
                _lastSceneName = sceneName;
                if (_menuVisible && !string.IsNullOrEmpty(sceneName) && sceneName.StartsWith("level"))
                {
                    ToggleMenu();
                }
            }

            if (Input.GetKeyDown(KeyCode.F1))
            {
                ToggleMenu();
            }

            // Runtime diagnostics: dump weapon/inventory state to the BepInEx log
            if (Input.GetKeyDown(KeyCode.F8))
            {
                WeaponDiagnostics.Dump("F8 pressed");
            }

            // Dump the local player prefab hierarchy (third-person model availability)
            if (Input.GetKeyDown(KeyCode.F9))
            {
                PlayerHierarchyDiagnostics.Dump("F9 pressed");
            }

            // Debug: preview the baked remote player body without a second game instance
            if (Input.GetKeyDown(KeyCode.F10))
            {
                RemotePlayerPreview.Next();
            }

            if (Input.GetKeyDown(KeyCode.F11))
            {
                RemotePlayerPreview.Remove();
            }

            // Ждем подключения клиента перед показом лобби
            if (_waitingForConnection)
            {
                _connectionTimer += Time.deltaTime;
                
                if (RakeMultiplayerPlugin.isConnected)
                {
                    _waitingForConnection = false;
                    LoadingScreen.Hide();
                    
                    // Восстановить кнопку
                    if (_uiBuilder != null && _uiBuilder.ConnectButton != null)
                    {
                        _uiBuilder.ConnectButton.interactable = true;
                        var btnText = _uiBuilder.ConnectButton.GetComponentInChildren<Text>();
                        if (btnText != null) btnText.text = "Connect";
                    }
                    
                    ShowLobby();
                }
                else if (_connectionTimer > 5.0f) // Таймаут 5 секунд
                {
                    _waitingForConnection = false;
                    LoadingScreen.Hide();
                    RakeMultiplayerPlugin.Log?.LogError("[Menu] Connection timed out!");
                    
                    // Восстановить кнопку с ошибкой
                    if (_uiBuilder != null && _uiBuilder.ConnectButton != null)
                    {
                        _uiBuilder.ConnectButton.interactable = true;
                        var btnText = _uiBuilder.ConnectButton.GetComponentInChildren<Text>();
                        if (btnText != null) btnText.text = "Connect (Failed)";
                    }
                }
            }

            UpdateConnectionStatus();
            UpdateHUD();
            UpdateRakeHealthBar();

            if (_uiBuilder != null && _uiBuilder.SteamBrowsePanel != null &&
                _uiBuilder.SteamBrowsePanel.activeSelf)
            {
                _steamUiTimer -= Time.deltaTime;
                if (_steamUiTimer <= 0f)
                {
                    _steamUiTimer = 0.4f;
                    UpdateSteamBrowse();
                }
            }
        }

        private NetworkedRakeAI _rakeHealthTarget;
        private float _rakeHealthScan;

        /// <summary>Boss-style Rake health bar, identical on host and clients.</summary>
        private void UpdateRakeHealthBar()
        {
            if (_uiBuilder == null) return;

            _rakeHealthScan -= Time.deltaTime;
            if (_rakeHealthScan <= 0f || _rakeHealthTarget == null)
            {
                _rakeHealthScan = 0.5f;
                var rake = RakeHighlight.FindRake();
                _rakeHealthTarget = rake != null ? rake.GetComponent<NetworkedRakeAI>() : null;
            }

            if (RakePlayerController.IsActive)
            {
                _uiBuilder.HideRakeHealth();
                return;
            }

            if (_rakeHealthTarget == null)
            {
                _uiBuilder.HideRakeHealth();
                return;
            }

            float health = _rakeHealthTarget.CurrentHealth;
            if (health <= 0f)
            {
                _uiBuilder.HideRakeHealth();
                return;
            }

            _uiBuilder.UpdateRakeHealth(health, _rakeHealthTarget.MaxHealth);
        }
        private void UpdateHUD()
        {
            if (_uiBuilder == null) return;

            // The Rake has its own full HUD (health, phase, timer, abilities).
            if (RakePlayerController.IsActive)
            {
                _uiBuilder.SetHUDVisible(false);
                _uiBuilder.HideRakeHealth();
                return;
            }

            var dayNight = DayNightManager.Instance;
            var session = SessionManager.Instance;

            if (dayNight == null || session == null)
            {
                // Hide HUD if managers not ready
                _uiBuilder.SetHUDVisible(false);
                return;
            }

            // Show HUD only during active session
            bool sessionActive = session.IsRunning && (session.CurrentState == SessionState.Active);
            _uiBuilder.SetHUDVisible(sessionActive);

            if (!sessionActive) return;

            // Phase text
            string phaseText = dayNight.CurrentPhase == Phase.Day ? "ДЕНЬ" : "НОЧЬ";
            _uiBuilder.UpdatePhaseText(phaseText);

            // Night counter (1-based, TotalNights from config)
            int currentNight = Mathf.Max(1, session.CurrentNight);
            int totalNights = session.TotalNightsConfig > 0 ? session.TotalNightsConfig : 3;
            _uiBuilder.UpdateNightText($"Ночь: {currentNight}/{totalNights}");

            // Timer
            float remaining = dayNight.GetRemainingTime();
            int minutes = Mathf.FloorToInt(remaining / 60f);
            int seconds = Mathf.FloorToInt(remaining % 60f);
            _uiBuilder.UpdateTimerText($"{minutes:00}:{seconds:00}");
        }

        public void ToggleMenu()
        {
            _menuVisible = !_menuVisible;
            
            RakeMultiplayerPlugin.Log?.LogInfo($"[F1] ToggleMenu. visible={_menuVisible} mainContainer={(_mainContainer != null)} uiBuilder={(_uiBuilder != null)}");

            if (_mainContainer != null)
                _mainContainer.SetActive(_menuVisible);
            
            if (_menuVisible)
            {
                ShowMainMenu();
            }
            else
            {
                HideAllPanels();
            }
        }

        public void ShowMainMenu()
        {
            HideAllPanels();
            if (_uiBuilder != null && _uiBuilder.MainMenuPanel != null)
                _uiBuilder.MainMenuPanel.SetActive(true);
        }

        public void ShowMultiplayerPanel()
        {
            HideAllPanels();
            if (_uiBuilder != null && _uiBuilder.MultiplayerPanel != null)
                _uiBuilder.MultiplayerPanel.SetActive(true);
        }

        public void ShowLobby()
        {
            HideAllPanels();
            if (_uiBuilder != null && _uiBuilder.LobbyPanel != null)
            {
                _uiBuilder.LobbyPanel.SetActive(true);
                if (_uiBuilder.LobbyTitleText != null)
                    _uiBuilder.LobbyTitleText.text = RakeMultiplayerPlugin.isServer ? "Host Lobby" : "Client Lobby";
            }
            UpdatePlayerList();
        }

        public void ShowSettings()
        {
            HideAllPanels();
            if (_uiBuilder != null && _uiBuilder.SettingsPanel != null)
                _uiBuilder.SettingsPanel.SetActive(true);
        }

        public void ShowJoinGamePanel()
        {
            HideAllPanels();
            if (_uiBuilder != null && _uiBuilder.JoinGamePanel != null)
                _uiBuilder.JoinGamePanel.SetActive(true);
        }

        private void HideAllPanels()
        {
            if (_uiBuilder == null) return;
            if (_uiBuilder.MainMenuPanel) _uiBuilder.MainMenuPanel.SetActive(false);
            if (_uiBuilder.MultiplayerPanel) _uiBuilder.MultiplayerPanel.SetActive(false);
            if (_uiBuilder.LobbyPanel) _uiBuilder.LobbyPanel.SetActive(false);
            if (_uiBuilder.SettingsPanel) _uiBuilder.SettingsPanel.SetActive(false);
            if (_uiBuilder.JoinGamePanel) _uiBuilder.JoinGamePanel.SetActive(false);
            if (_uiBuilder.SteamPanel) _uiBuilder.SteamPanel.SetActive(false);
            if (_uiBuilder.SteamCreatePanel) _uiBuilder.SteamCreatePanel.SetActive(false);
            if (_uiBuilder.SteamBrowsePanel) _uiBuilder.SteamBrowsePanel.SetActive(false);
            if (_uiBuilder.SteamPasswordPanel) _uiBuilder.SteamPasswordPanel.SetActive(false);
        }

        private void EnsureNetworkManager()
        {
            if (_networkManager == null)
            {
                _networkManager = UnityEngine.Object.FindObjectOfType<RakeNetworkManager>();
            }
            if (_networkManager == null)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Menu] RakeNetworkManager not found. Creating new one...");
                var nmObj = new GameObject("RakeNetworkManager");
                UnityEngine.Object.DontDestroyOnLoad(nmObj);
                _networkManager = nmObj.AddComponent<RakeNetworkManager>();
            }
        }

        public void OnHostGame()
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[Menu] Host game clicked");
            RakeMultiplayerPlugin.UseSteamP2P.Value = false;
            EnsureNetworkManager();
            RakeMultiplayerPlugin.Log?.LogInfo($"[Menu] Calling StartHost on {_networkManager.gameObject.name}");
            _networkManager.StartHost();
            ShowLobby();
        }

        public void OnJoinGame()
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[Menu] Join game clicked");
            ShowMultiplayerPanel();
        }

        public void OnLAN()
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[Menu] LAN mode selected");
            RakeMultiplayerPlugin.UseSteamP2P.Value = false;
            ShowJoinGamePanel();
        }

        public void OnOnline()
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[Menu] Steam mode selected");
            RakeMultiplayerPlugin.UseSteamP2P.Value = true;
            ShowSteamMain();
        }

        public void OnBackToMain()
        {
            ShowMainMenu();
        }

        public void OnBackToMultiplayer()
        {
            ShowMultiplayerPanel();
        }

        public void OnStartGame()
        {
            if (_networkManager != null && RakeMultiplayerPlugin.isServer)
            {
                // Guard against duplicate sends
                if (_startGameSent)
                {
                    RakeMultiplayerPlugin.Log?.LogWarning("[Menu] StartGame already sent, ignoring duplicate");
                    return;
                }
                _startGameSent = true;

                RakeMultiplayerPlugin.Log?.LogInfo("[Menu] Starting game - broadcasting StartGame to clients");
                
                // Game uses Application.LoadLevel(1) for New Game (see MainMenu.StartNewGame)
                // Send index as string "1"
                var packet = new StartGamePacket("1");
                _networkManager.ServerBroadcast(packet.Serialize());
                
                RakeMultiplayerPlugin.Log?.LogInfo("[Menu] Sent StartGame with level index: 1");
                RakeMultiplayerPlugin.Log?.LogInfo("[Menu] Host loading level index 1 NOW");

                _menuVisible = false;
                if (_mainContainer != null) _mainContainer.SetActive(false);
                HideAllPanels();

                StartCoroutine(HostStartGameRoutine());
            }
        }

        private System.Collections.IEnumerator HostStartGameRoutine()
        {
            LoadingScreen.Show("THE HUNT BEGINS", "HOST", "Preparing the forest", false);
            LoadingScreen.SetProgress(0.2f);
            yield return null;
            yield return new WaitForSeconds(1.05f);

            LoadingScreen.SetStatus("Loading the level");
            LoadingScreen.SetProgress(1f);
            yield return new WaitForSeconds(0.35f);

            UnityEngine.Application.LoadLevel(1);
            LoadingScreen.SetProgress(1f);
            LoadingScreen.Hide();
        }
        
        private bool _startGameSent = false;

        public void OnLeaveLobby()
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[Menu] Leaving lobby");
            if (SteamLobbyManager.Instance != null) SteamLobbyManager.Instance.LeaveLobby();
            if (_networkManager != null)
            {
                _networkManager.Disconnect();
            }
            LoadingScreen.Hide();
            _startGameSent = false; // Allow starting a new game
            LobbyState.HostIsRake = false;
            ShowMainMenu();
        }

        public void OnCopyIP()
        {
            // Copy local IP to clipboard (Unity 5.x compatible)
            string ip = GetLocalIPAddress();
            if (!string.IsNullOrEmpty(ip))
            {
                // Unity 5.x doesn't have GUIUtility.systemCopyBuffer
                // Use TextEditor instead
                TextEditor te = new TextEditor();
                te.content = new GUIContent(ip);
                te.SelectAll();
                te.Copy();
                RakeMultiplayerPlugin.Log?.LogInfo($"[Menu] Copied IP: {ip}");
            }
        }

        public void OnConnect()
        {
            string ip = "127.0.0.1";
            int port = 7777;

            if (_uiBuilder != null)
            {
                if (_uiBuilder.IPInputField != null)
                    ip = _uiBuilder.IPInputField.text;
                if (_uiBuilder.PortInputField != null && int.TryParse(_uiBuilder.PortInputField.text, out int p))
                    port = p;
            }

            RakeMultiplayerPlugin.UseSteamP2P.Value = false;
            RakeMultiplayerPlugin.Log?.LogInfo($"[Menu] Connecting to {ip}:{port}");
            RakeMultiplayerPlugin.NetworkAddress.Value = ip;
            RakeMultiplayerPlugin.NetworkPort.Value = (ushort)port;

            EnsureNetworkManager();
            RakeMultiplayerPlugin.Log?.LogInfo($"[Menu] Calling StartClient on {_networkManager.gameObject.name}");
            _networkManager.StartClient();
            
            // UI feedback: disable button, show connecting
            _waitingForConnection = true;
            _connectionTimer = 0f;

            LoadingScreen.Show("CONTACTING THE HOST", "ONLINE", "Handshaking with the host", true);
            
            if (_uiBuilder != null && _uiBuilder.ConnectButton != null)
            {
                _uiBuilder.ConnectButton.interactable = false;
                var btnText = _uiBuilder.ConnectButton.GetComponentInChildren<Text>();
                if (btnText != null) btnText.text = "Connecting...";
            }
        }

        private void UpdateConnectionStatus()
        {
            if (_uiBuilder == null) return;

            // Лог только при смене состояния (избегаем спама)
            if (_lastIsServer != RakeMultiplayerPlugin.isServer || _lastIsConnected != RakeMultiplayerPlugin.isConnected)
            {
                RakeMultiplayerPlugin.Log?.LogInfo($"[Menu] UpdateStatus: isServer={RakeMultiplayerPlugin.isServer}, isConnected={RakeMultiplayerPlugin.isConnected}");
                _lastIsServer = RakeMultiplayerPlugin.isServer;
                _lastIsConnected = RakeMultiplayerPlugin.isConnected;
            }

            // Обновляем заголовок динамически (важно для хоста/клиента)
            if (_uiBuilder.LobbyTitleText != null)
            {
                _uiBuilder.LobbyTitleText.text = "LOBBY";
            }
            if (_uiBuilder.LobbySubtitleText != null)
            {
                _uiBuilder.LobbySubtitleText.text = RakeMultiplayerPlugin.isServer ? "HOST" : "CLIENT";
            }

            if (_uiBuilder.ConnectionStatusText != null)
            {
                if (RakeMultiplayerPlugin.isServer)
                {
                    _uiBuilder.ConnectionStatusText.text = "Hosting (Port " + RakeMultiplayerPlugin.NetworkPort.Value + ")";
                    _uiBuilder.ConnectionStatusText.color = Color.green;
                }
                else if (RakeMultiplayerPlugin.isConnected)
                {
                    _uiBuilder.ConnectionStatusText.text = "Connected";
                    _uiBuilder.ConnectionStatusText.color = Color.green;
                }
                else
                {
                    _uiBuilder.ConnectionStatusText.text = "Disconnected";
                    _uiBuilder.ConnectionStatusText.color = Color.red;
                }
            }

            if (_uiBuilder.PlayerCountText != null)
            {
                int count = 1;
                if (RakeNetworkManager.Instance != null)
                {
                    count = RakeNetworkManager.Instance.GetKnownPlayerCount();
                }
                else if (RakeMultiplayerPlugin.isServer)
                {
                    count = RakeMultiplayerPlugin.GetConnectedClientCount() + 1;
                }
                _uiBuilder.PlayerCountText.text = $"Players: {count}/{RakeMultiplayerPlugin.MaxPlayers.Value}";
            }

            if (_uiBuilder.LobbyPanel != null && _uiBuilder.LobbyPanel.activeSelf)
            {
                UpdatePlayerList();
            }
        }

        private void UpdatePlayerList()
        {
            if (_uiBuilder == null) return;

            int count = 1;
            if (RakeNetworkManager.Instance != null)
            {
                count = RakeNetworkManager.Instance.GetKnownPlayerCount();
            }
            else if (RakeMultiplayerPlugin.isServer)
            {
                count = RakeMultiplayerPlugin.GetConnectedClientCount() + 1;
            }

            int max = RakeMultiplayerPlugin.MaxPlayers.Value;

            StringBuilder sb = new StringBuilder();

            if (SteamLobbyManager.InLobby)
            {
                var steamNames = SteamLobbyManager.GetMemberNames();
                for (int i = 0; i < steamNames.Count; i++)
                {
                    if (sb.Length > 0) sb.Append((char)10);
                    sb.Append(steamNames[i]);
                }
                count = steamNames.Count;
            }
            else
            {
                int firstHunter = LobbyState.RakeOwnerId == 1 ? 2 : 1;
                for (int i = firstHunter; i <= count; i++)
                {
                    if (i == LobbyState.RakeOwnerId) continue;
                    if (sb.Length > 0) sb.Append((char)10);
                    sb.Append(i == 1 ? "1. HOST" : (i + ". PLAYER " + i));
                }
            }
            if (sb.Length == 0) sb.Append("no hunters");

            if (_uiBuilder.PlayerListText != null) _uiBuilder.PlayerListText.text = sb.ToString();
            if (_uiBuilder.PlayerCountText != null)
                _uiBuilder.PlayerCountText.text = "Players: " + count + "/" + max;

            _uiBuilder.SetLobbyRole(RakeMultiplayerPlugin.isServer, LobbyState.RakeOwnerId,
                LobbyState.LocalIsRakeOwner);
        }

        public void OnToggleRakeRole()
        {
            if (_networkManager == null) EnsureNetworkManager();

            if (RakeMultiplayerPlugin.isServer)
            {
                // Host: take the slot if free, otherwise give it back (next click a client can take it).
                int target = LobbyState.RakeOwnerId == 1 ? 0 : 1;
                _networkManager?.SetRakeOwner(target);
                UpdatePlayerList();
                return;
            }

            // Client: ask the server for the single Rake slot.
            var request = new RakeRoleRequestPacket
            {
                TakeRole = !LobbyState.LocalIsRakeOwner
            };
            _networkManager?.ClientSend(request.Serialize());
            RakeMultiplayerPlugin.Log?.LogInfo("[Lobby] Requested Rake role: " + request.TakeRole);
        }

        /// <summary>Close the F1 menu (used when a level starts loading).</summary>
        public static void CloseForLoad()
        {
            var menu = FindObjectOfType<MultiplayerMenu>();
            if (menu == null) return;

            menu._menuVisible = false;
            if (menu._mainContainer != null) menu._mainContainer.SetActive(false);
            menu.HideAllPanels();
        }

        // ------------------------------------------------------------- steam lobby UI

        public void ShowSteamMain()
        {
            HideAllPanels();
            if (_uiBuilder == null) return;

            if (_uiBuilder.SteamPanel != null) _uiBuilder.SteamPanel.SetActive(true);

            if (SteamLobbyManager.Instance != null)
            {
                _uiBuilder.SetSteamStatus(SteamLobbyManager.Instance.SteamStatus,
                    !SteamIntegration.Initialized);
            }
            else
            {
                _uiBuilder.SetSteamStatus("Steam unavailable", true);
            }
        }

        public void ShowSteamCreate()
        {
            HideAllPanels();
            if (_uiBuilder == null) return;

            if (_uiBuilder.SteamCreatePanel != null) _uiBuilder.SteamCreatePanel.SetActive(true);

            if (_uiBuilder.SteamNameInput != null)
            {
                _uiBuilder.SteamNameInput.text = SteamIntegration.PersonaName + "'s Hunt";
            }
            if (_uiBuilder.SteamPasswordInput != null) _uiBuilder.SteamPasswordInput.text = "";
            _uiBuilder.SetSteamCreateStatus("");
        }

        public void ShowSteamBrowse()
        {
            HideAllPanels();
            if (_uiBuilder == null) return;

            if (_uiBuilder.SteamBrowsePanel != null) _uiBuilder.SteamBrowsePanel.SetActive(true);

            if (SteamLobbyManager.Instance != null) SteamLobbyManager.Instance.RefreshLobbies();

            _steamUiTimer = 0f;
            UpdateSteamBrowse();
        }

        public void ShowSteamPassword(SteamLobbyManager.LobbyInfo lobby)
        {
            if (lobby == null) return;

            _steamSelectedLobby = lobby;
            HideAllPanels();
            if (_uiBuilder == null) return;

            if (_uiBuilder.SteamPasswordPanel != null) _uiBuilder.SteamPasswordPanel.SetActive(true);
            if (_uiBuilder.SteamJoinPasswordInput != null) _uiBuilder.SteamJoinPasswordInput.text = "";
            _uiBuilder.SetSteamPasswordLobby(lobby.Name + "  ·  " + lobby.Host);
            _uiBuilder.SetSteamPasswordStatus("");
        }

        public void ShowSteamMainNoHide()
        {
            if (_uiBuilder == null) return;
            HideAllPanels();
            if (_uiBuilder.SteamPanel != null) _uiBuilder.SteamPanel.SetActive(true);
        }

        public void OnSteamCreate()
        {
            if (_uiBuilder == null || SteamLobbyManager.Instance == null) return;

            string name = _uiBuilder.SteamNameInput != null ? _uiBuilder.SteamNameInput.text : "Hunt";
            string password = _uiBuilder.SteamPasswordInput != null ? _uiBuilder.SteamPasswordInput.text : "";

            SteamLobbyManager.Instance.CreateLobby(name, password, 4);
            _uiBuilder.SetSteamCreateStatus(SteamLobbyManager.LastError);
        }

        public void OnSteamRefresh()
        {
            if (SteamLobbyManager.Instance == null) return;
            SteamLobbyManager.Instance.RefreshLobbies();
            UpdateSteamBrowse();
        }

        public void OnSteamLobbyClicked(int index)
        {
            if (SteamLobbyManager.Instance == null) return;
            if (index < 0 || index >= SteamLobbyManager.Lobbies.Count) return;

            var lobby = SteamLobbyManager.Lobbies[index];
            if (lobby.HasPassword)
            {
                ShowSteamPassword(lobby);
                return;
            }

            SteamLobbyManager.Instance.JoinLobby(lobby, "");
            if (_uiBuilder != null) _uiBuilder.SetSteamBrowseStatus(SteamLobbyManager.LastError);
        }

        public void OnSteamJoin()
        {
            if (SteamLobbyManager.Instance == null || _steamSelectedLobby == null) return;

            string password = _uiBuilder != null && _uiBuilder.SteamJoinPasswordInput != null
                ? _uiBuilder.SteamJoinPasswordInput.text
                : "";

            SteamLobbyManager.Instance.JoinLobby(_steamSelectedLobby, password);

            if (_uiBuilder != null)
            {
                _uiBuilder.SetSteamPasswordStatus(SteamLobbyManager.LastError);
                if (string.IsNullOrEmpty(SteamLobbyManager.LastError)) _uiBuilder.SetSteamPasswordStatus("Joining...");
            }
        }

        private void UpdateSteamBrowse()
        {
            if (_uiBuilder == null) return;
            if (_uiBuilder.SteamBrowsePanel == null || !_uiBuilder.SteamBrowsePanel.activeSelf) return;

            _uiBuilder.UpdateSteamRows(SteamLobbyManager.Lobbies);

            string status = SteamLobbyManager.LastError;
            if (string.IsNullOrEmpty(status))
            {
                status = SteamLobbyManager.Lobbies.Count == 0
                    ? "No lobbies found - create one or press REFRESH"
                    : SteamLobbyManager.Lobbies.Count + " lobby(ies) - pick one";
            }
            _uiBuilder.SetSteamBrowseStatus(status);
        }
        private string GetLocalIPAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch (Exception e)
            {
                RakeMultiplayerPlugin.Log?.LogError($"[Menu] Failed to get local IP: {e.Message}");
            }
            return "127.0.0.1";
        }
    }
}