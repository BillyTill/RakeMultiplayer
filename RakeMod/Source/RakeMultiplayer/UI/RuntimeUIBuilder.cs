using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace RakeMultiplayer.UI
{
    /// <summary>
    /// Builds multiplayer UI programmatically at runtime (no Inspector references needed)
    /// </summary>
    public class RuntimeUIBuilder : MonoBehaviour
    {
        private Canvas _canvas;
        private GameObject _mainMenuPanel;
        private GameObject _multiplayerPanel;
        private GameObject _lobbyPanel;
        private GameObject _settingsPanel;
        private GameObject _joinGamePanel;

        private InputField _ipInputField;
        private InputField _portInputField;
        private InputField _playerNameInputField;
        private Toggle _steamP2PToggle;
        private Button _connectButton;
        private InputField _maxPlayersInputField;

        private Text _connectionStatusText;
        private Text _playerCountText;
        private Text _lobbyTitleText;

        private Transform _playerListContainer;

        private RakeNetworkManager _networkManager;
        private MultiplayerMenu _menu;

        public void Initialize(MultiplayerMenu menu)
        {
            _menu = menu;
            _networkManager = UnityEngine.Object.FindObjectOfType<RakeNetworkManager>();
            BuildUI();
            RakeMultiplayerPlugin.Log?.LogInfo("[UI] RuntimeUIBuilder initialized. Canvas created.");
            menu.OnUIBuilt(this);
        }

        private void BuildUI()
        {
            // Create Canvas
            var canvasObj = new GameObject("MultiplayerUICanvas");
            UnityEngine.Object.DontDestroyOnLoad(canvasObj);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32000; // Above all game UI
            RakeMultiplayerPlugin.Log?.LogInfo("[UI] Canvas created (sortingOrder=32000)");

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Main container
            var mainContainer = CreatePanel(canvasObj.transform, "MainContainer", new Vector2(400, 500), new Vector2(0, 0), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            mainContainer.SetActive(false);

            // ===== MAIN MENU PANEL =====
            _mainMenuPanel = CreatePanel(mainContainer.transform, "MainMenuPanel", new Vector2(380, 480), new Vector2(0, 0), Vector2.zero, Vector2.one);
            CreateTitle(_mainMenuPanel.transform, "Rake Multiplayer", 28, new Vector2(0, 180));

            CreateButton(_mainMenuPanel.transform, "HostGameBtn", "Host Game (LAN)", new Vector2(300, 50), new Vector2(0, 60), OnHostGameClicked);
            CreateButton(_mainMenuPanel.transform, "JoinGameBtn", "Join Game", new Vector2(300, 50), new Vector2(0, 0), OnJoinGameClicked);
            CreateButton(_mainMenuPanel.transform, "SettingsBtn", "Settings", new Vector2(300, 50), new Vector2(0, -60), OnSettingsClicked);
            CreateButton(_mainMenuPanel.transform, "ExitBtn", "Exit to Game", new Vector2(300, 50), new Vector2(0, -180), OnExitClicked);

            // ===== MULTIPLAYER PANEL (LAN/Online choice) =====
            _multiplayerPanel = CreatePanel(mainContainer.transform, "MultiplayerPanel", new Vector2(380, 480), new Vector2(0, 0), Vector2.zero, Vector2.one);
            _multiplayerPanel.SetActive(false);
            CreateTitle(_multiplayerPanel.transform, "Multiplayer Mode", 28, new Vector2(0, 180));
            CreateButton(_multiplayerPanel.transform, "LANBtn", "LAN (Direct IP)", new Vector2(300, 50), new Vector2(0, 60), OnLANClicked);
            CreateButton(_multiplayerPanel.transform, "OnlineBtn", "Online (Steam P2P)", new Vector2(300, 50), new Vector2(0, 0), OnOnlineClicked);
            CreateButton(_multiplayerPanel.transform, "BackToMainBtn", "Back", new Vector2(300, 50), new Vector2(0, -120), OnBackToMainClicked);

            // ===== JOIN GAME PANEL =====
            _joinGamePanel = CreatePanel(mainContainer.transform, "JoinGamePanel", new Vector2(380, 480), new Vector2(0, 0), Vector2.zero, Vector2.one);
            _joinGamePanel.SetActive(false);
            CreateTitle(_joinGamePanel.transform, "Join Game", 28, new Vector2(0, 180));

            _ipInputField = CreateInputField(_joinGamePanel.transform, "IPInput", "Server IP", "127.0.0.1", new Vector2(300, 40), new Vector2(0, 80));
            _portInputField = CreateInputField(_joinGamePanel.transform, "PortInput", "Port", "7777", new Vector2(300, 40), new Vector2(0, 20));
            _connectButton = CreateButton(_joinGamePanel.transform, "ConnectBtn", "Connect", new Vector2(300, 50), new Vector2(0, -40), OnConnectClicked);
            CreateButton(_joinGamePanel.transform, "JoinBackBtn", "Back", new Vector2(300, 50), new Vector2(0, -120), OnBackToMultiplayerClicked);

            // ===== LOBBY PANEL =====
            _lobbyPanel = CreatePanel(mainContainer.transform, "LobbyPanel", new Vector2(380, 480), new Vector2(0, 0), Vector2.zero, Vector2.one);
            _lobbyPanel.SetActive(false);
            _lobbyTitleText = CreateTitle(_lobbyPanel.transform, "Lobby", 28, new Vector2(0, 180));

            _connectionStatusText = CreateText(_lobbyPanel.transform, "ConnectionStatus", "Disconnected", 16, new Vector2(0, 120), Color.red);
            _playerCountText = CreateText(_lobbyPanel.transform, "PlayerCount", "Players: 1/4", 16, new Vector2(0, 90), Color.white);

            _playerListContainer = CreatePanel(_lobbyPanel.transform, "PlayerListContainer", new Vector2(340, 180), new Vector2(0, -30), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).transform;

            CreateButton(_lobbyPanel.transform, "StartGameBtn", "Start Game", new Vector2(300, 50), new Vector2(0, -160), OnStartGameClicked);
            CreateButton(_lobbyPanel.transform, "LeaveLobbyBtn", "Leave Lobby", new Vector2(300, 50), new Vector2(0, -220), OnLeaveLobbyClicked);
            CreateButton(_lobbyPanel.transform, "CopyIPBtn", "Copy My IP", new Vector2(300, 50), new Vector2(0, -280), OnCopyIPClicked);

            // ===== SETTINGS PANEL =====
            _settingsPanel = CreatePanel(mainContainer.transform, "SettingsPanel", new Vector2(380, 480), new Vector2(0, 0), Vector2.zero, Vector2.one);
            _settingsPanel.SetActive(false);
            CreateTitle(_settingsPanel.transform, "Settings", 28, new Vector2(0, 180));

            _playerNameInputField = CreateInputField(_settingsPanel.transform, "PlayerNameInput", "Player Name", "Player", new Vector2(300, 40), new Vector2(0, 80));
            _steamP2PToggle = CreateToggle(_settingsPanel.transform, "SteamP2PToggle", "Use Steam P2P", false, new Vector2(0, 20));
            _maxPlayersInputField = CreateInputField(_settingsPanel.transform, "MaxPlayersInput", "Max Players (2-4)", "4", new Vector2(300, 40), new Vector2(0, -40));

            CreateButton(_settingsPanel.transform, "SettingsBackBtn", "Back", new Vector2(300, 50), new Vector2(0, -120), OnBackToMultiplayerClicked);

            // Store reference to main container for ToggleMenu
            _menu.SetMainContainer(mainContainer);
        }

        // Button callback methods (using UnityAction compatible signatures)
        private void OnHostGameClicked() { if (_menu != null) _menu.OnHostGame(); }
        private void OnJoinGameClicked() { if (_menu != null) _menu.ShowMultiplayerPanel(); }
        private void OnSettingsClicked() { if (_menu != null) _menu.ShowSettings(); }
        private void OnExitClicked() { if (_menu != null) _menu.ToggleMenu(); }
        private void OnLANClicked() { if (_menu != null) _menu.OnLAN(); }
        private void OnOnlineClicked() { if (_menu != null) _menu.OnOnline(); }
        private void OnBackToMainClicked() { if (_menu != null) _menu.OnBackToMain(); }
        private void OnBackToMultiplayerClicked() { if (_menu != null) _menu.OnBackToMultiplayer(); }
        private void OnConnectClicked() { if (_menu != null) _menu.OnConnect(); }
        private void OnStartGameClicked() { if (_menu != null) _menu.OnStartGame(); }
        private void OnLeaveLobbyClicked() { if (_menu != null) _menu.OnLeaveLobby(); }
        private void OnCopyIPClicked() { if (_menu != null) _menu.OnCopyIP(); }

        private GameObject CreatePanel(Transform parent, string name, Vector2 size, Vector2 anchoredPos, Vector2 anchorMin, Vector2 anchorMax)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            var img = obj.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            return obj;
        }

        private Text CreateTitle(Transform parent, string text, int fontSize, Vector2 anchoredPos)
        {
            var obj = new GameObject("Title", typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(360, 40);
            rt.anchoredPosition = anchoredPos;
            var txt = obj.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 10;
            txt.resizeTextMaxSize = fontSize;
            return txt;
        }

        private Text CreateText(Transform parent, string name, string text, int fontSize, Vector2 anchoredPos, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(360, 30);
            rt.anchoredPosition = anchoredPos;
            var txt = obj.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = fontSize;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            return txt;
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 size, Vector2 anchoredPos, UnityAction onClick)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            var btn = obj.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.2f, 0.3f, 0.5f, 1f);
            colors.highlightedColor = new Color(0.3f, 0.4f, 0.7f, 1f);
            colors.pressedColor = new Color(0.15f, 0.25f, 0.45f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            var txtObj = new GameObject("Text", typeof(RectTransform));
            txtObj.transform.SetParent(obj.transform, false);
            var txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
            var txt = txtObj.AddComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 18;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;

            return btn;
        }

        private InputField CreateInputField(Transform parent, string name, string placeholder, string defaultValue, Vector2 size, Vector2 anchoredPos)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            var img = obj.GetComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.2f, 1f);

            var input = obj.GetComponent<InputField>();
            input.text = defaultValue;
            input.targetGraphic = img;

            // Placeholder
            var phObj = new GameObject("Placeholder", typeof(RectTransform));
            phObj.transform.SetParent(obj.transform, false);
            var phRt = phObj.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = new Vector2(10, 0);
            phRt.offsetMax = new Vector2(-10, 0);
            var phTxt = phObj.AddComponent<Text>();
            phTxt.text = placeholder;
            phTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            phTxt.fontSize = 16;
            phTxt.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            phTxt.alignment = TextAnchor.MiddleLeft;
            input.placeholder = phTxt;

            // Text component
            var txtObj = new GameObject("Text", typeof(RectTransform));
            txtObj.transform.SetParent(obj.transform, false);
            var txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(10, 0);
            txtRt.offsetMax = new Vector2(-10, 0);
            var txt = txtObj.AddComponent<Text>();
            txt.text = defaultValue;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 16;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleLeft;
            input.textComponent = txt;

            return input;
        }

        private Toggle CreateToggle(Transform parent, string name, string label, bool defaultValue, Vector2 anchoredPos)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 30);
            rt.anchoredPosition = anchoredPos;

            var toggle = obj.GetComponent<Toggle>();
            toggle.isOn = defaultValue;

            // Background
            var bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgObj.transform.SetParent(obj.transform, false);
            var bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.sizeDelta = new Vector2(20, 20);
            bgRt.anchorMin = new Vector2(0, 0.5f);
            bgRt.anchorMax = new Vector2(0, 0.5f);
            bgRt.anchoredPosition = new Vector2(10, 0);
            var bgImg = bgObj.GetComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.3f, 1f);

            // Checkmark
            var checkObj = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkObj.transform.SetParent(bgObj.transform, false);
            var checkRt = checkObj.GetComponent<RectTransform>();
            checkRt.sizeDelta = new Vector2(14, 14);
            checkRt.anchorMin = Vector2.zero;
            checkRt.anchorMax = Vector2.one;
            checkRt.offsetMin = Vector2.zero;
            checkRt.offsetMax = Vector2.zero;
            var checkImg = checkObj.GetComponent<Image>();
            checkImg.color = new Color(0.2f, 0.6f, 0.2f, 1f);
            checkImg.enabled = defaultValue;
            toggle.graphic = checkImg;
            toggle.targetGraphic = bgImg;

            // Label
            var lblObj = new GameObject("Label", typeof(RectTransform));
            lblObj.transform.SetParent(obj.transform, false);
            var lblRt = lblObj.GetComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = new Vector2(40, 0);
            lblRt.offsetMax = Vector2.zero;
            var lblTxt = lblObj.AddComponent<Text>();
            lblTxt.text = label;
            lblTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            lblTxt.fontSize = 16;
            lblTxt.color = Color.white;
            lblTxt.alignment = TextAnchor.MiddleLeft;

            return toggle;
        }

        // Public accessors for MultiplayerMenu
        public GameObject MainMenuPanel => _mainMenuPanel;
        public GameObject MultiplayerPanel => _multiplayerPanel;
        public GameObject LobbyPanel => _lobbyPanel;
        public GameObject SettingsPanel => _settingsPanel;
        public GameObject JoinGamePanel => _joinGamePanel;

        public InputField IPInputField => _ipInputField;
        public InputField PortInputField => _portInputField;
        public InputField PlayerNameInputField => _playerNameInputField;
        public Toggle SteamP2PToggle => _steamP2PToggle;
        public Button ConnectButton => _connectButton;
        public InputField MaxPlayersInputField => _maxPlayersInputField;

        public Text ConnectionStatusText => _connectionStatusText;
        public Text PlayerCountText => _playerCountText;
        public Text LobbyTitleText => _lobbyTitleText;

        public Transform PlayerListContainer => _playerListContainer;

        public Canvas Canvas => _canvas;
    }
}