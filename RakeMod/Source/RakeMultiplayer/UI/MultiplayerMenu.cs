using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RakeMultiplayer.Networking;

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
            if (Input.GetKeyDown(KeyCode.F1))
            {
                ToggleMenu();
            }

            // Ждем подключения клиента перед показом лобби
            if (_waitingForConnection)
            {
                _connectionTimer += Time.deltaTime;
                
                if (RakeMultiplayerPlugin.isConnected)
                {
                    _waitingForConnection = false;
                    
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
            RakeMultiplayerPlugin.Log?.LogInfo("[Menu] Online mode selected");
            RakeMultiplayerPlugin.UseSteamP2P.Value = true;
            ShowJoinGamePanel();
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
                UnityEngine.Application.LoadLevel(1); // Хост грузит СРАЗУ
            }
        }
        
        private bool _startGameSent = false;

        public void OnLeaveLobby()
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[Menu] Leaving lobby");
            if (_networkManager != null)
            {
                _networkManager.Disconnect();
            }
            _startGameSent = false; // Allow starting a new game
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

            RakeMultiplayerPlugin.Log?.LogInfo($"[Menu] Connecting to {ip}:{port}");
            RakeMultiplayerPlugin.NetworkAddress.Value = ip;
            RakeMultiplayerPlugin.NetworkPort.Value = (ushort)port;

            EnsureNetworkManager();
            RakeMultiplayerPlugin.Log?.LogInfo($"[Menu] Calling StartClient on {_networkManager.gameObject.name}");
            _networkManager.StartClient();
            
            // UI feedback: disable button, show connecting
            _waitingForConnection = true;
            _connectionTimer = 0f;
            
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
                _uiBuilder.LobbyTitleText.text = RakeMultiplayerPlugin.isServer ? "Host Lobby" : "Client Lobby";
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
        }

        private void UpdatePlayerList()
        {
            // TODO: Populate player list container
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