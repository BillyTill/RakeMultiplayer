using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer.UI
{
    /// <summary>
    /// Builds the multiplayer menu at runtime in the game's horror style: near-black
    /// panels, a blood-red accent, thin separators and a lobby that shows the hunters
    /// and the single Rake slot.
    /// </summary>
    public class RuntimeUIBuilder : MonoBehaviour
    {
        private static readonly Color ColBg = new Color(0.035f, 0.035f, 0.05f, 0.97f);
        private static readonly Color ColPanel = new Color(0.075f, 0.07f, 0.085f, 0.99f);
        private static readonly Color ColPanelLight = new Color(0.11f, 0.105f, 0.12f, 1f);
        private static readonly Color ColAccent = new Color(0.72f, 0.16f, 0.12f, 1f);
        private static readonly Color ColAccentDim = new Color(0.35f, 0.10f, 0.08f, 1f);
        private static readonly Color ColText = new Color(0.93f, 0.90f, 0.84f, 1f);
        private static readonly Color ColMuted = new Color(0.55f, 0.52f, 0.46f, 1f);
        private static readonly Color ColOk = new Color(0.55f, 0.78f, 0.45f, 1f);
        private static readonly Color ColBad = new Color(0.85f, 0.28f, 0.22f, 1f);

        private Canvas _canvas;
        private GameObject _mainContainer;

        private GameObject _mainMenuPanel;
        private GameObject _multiplayerPanel;
        private GameObject _lobbyPanel;
        private GameObject _settingsPanel;
        private GameObject _joinGamePanel;

        private InputField _ipInputField;
        private InputField _portInputField;
        private InputField _playerNameInputField;
        private InputField _maxPlayersInputField;
        private Toggle _steamP2PToggle;
        private Button _connectButton;

        private Text _connectionStatusText;
        private Text _playerCountText;
        private Text _lobbyTitleText;
        private Text _lobbySubtitleText;
        private Text _playerListText;
        private Text _rakeSlotText;
        private Text _rakeRoleButtonText;
        private Text _lobbyHintText;
        private Button _rakeRoleButton;

        private Transform _playerListContainer;

        private GameObject _hudPanel;
        private Text _phaseText;
        private Text _nightText;
        private Text _timerText;

        private MultiplayerMenu _menu;
        private RakeNetworkManager _networkManager;

        public void Initialize(MultiplayerMenu menu)
        {
            _menu = menu;
            _networkManager = UnityEngine.Object.FindObjectOfType<RakeNetworkManager>();
            BuildUI();
            BuildHUD();
            BuildMapUI();
            RakeMultiplayerPlugin.Log?.LogInfo("[UI] RuntimeUIBuilder initialized. Canvas created.");
            menu.OnUIBuilt(this);
        }

        private void BuildUI()
        {
            var canvasObj = new GameObject("MultiplayerUICanvas");
            UnityEngine.Object.DontDestroyOnLoad(canvasObj);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32000;
            RakeMultiplayerPlugin.Log?.LogInfo("[UI] Canvas created (sortingOrder=32000)");

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            canvasObj.AddComponent<GraphicRaycaster>();

            _mainContainer = CreatePanel(canvasObj.transform, "MainContainer", new Vector2(560, 660), Vector2.zero,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _mainContainer.GetComponent<Image>().color = ColBg;
            _mainContainer.SetActive(false);

            AddTopBar(_mainContainer.transform, ColAccent, 4f);

            BuildMainMenu();
            BuildMultiplayerPanel();
            BuildJoinPanel();
            BuildLobbyPanel();
            BuildSettingsPanel();

            _menu.SetMainContainer(_mainContainer);
        }

        // ------------------------------------------------------------------ panels

        private void BuildMainMenu()
        {
            _mainMenuPanel = CreatePanel(_mainContainer.transform, "MainMenuPanel", new Vector2(0, 0), Vector2.zero, Vector2.zero, Vector2.one);
            _mainMenuPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            CreateText(_mainMenuPanel.transform, "Kicker", "RAKE", 13, new Vector2(0, 262), ColAccent, TextAnchor.MiddleCenter);
            CreateTitle(_mainMenuPanel.transform, "MULTIPLAYER", 34, new Vector2(0, 232));
            CreateText(_mainMenuPanel.transform, "Tagline", "THE HUNT BEGINS WITH YOU", 13, new Vector2(0, 200), ColMuted, TextAnchor.MiddleCenter);

            CreateSeparator(_mainMenuPanel.transform, 172f, 440f);

            CreateButton(_mainMenuPanel.transform, "HostGameBtn", "HOST GAME", new Vector2(380, 52), new Vector2(0, 118), OnHostGameClicked);
            CreateButton(_mainMenuPanel.transform, "JoinGameBtn", "JOIN GAME", new Vector2(380, 52), new Vector2(0, 56), OnJoinGameClicked);
            CreateButton(_mainMenuPanel.transform, "SettingsBtn", "SETTINGS", new Vector2(380, 52), new Vector2(0, -6), OnSettingsClicked);
            CreateButton(_mainMenuPanel.transform, "ExitBtn", "EXIT TO GAME", new Vector2(380, 52), new Vector2(0, -68), OnExitClicked, ColPanelLight);

            CreateSeparator(_mainMenuPanel.transform, -132f, 440f);

            CreateText(_mainMenuPanel.transform, "Hint",
                "F1 - menu   |   Tab - cameras   |   1 Rake + up to 3 hunters", 12, new Vector2(0, -160), ColMuted, TextAnchor.MiddleCenter);
            CreateText(_mainMenuPanel.transform, "Version", PluginInfo.NAME + "  v" + PluginInfo.VERSION, 11,
                new Vector2(0, -186), new Color(0.35f, 0.33f, 0.30f, 1f), TextAnchor.MiddleCenter);
        }

        private void BuildMultiplayerPanel()
        {
            _multiplayerPanel = CreatePanel(_mainContainer.transform, "MultiplayerPanel", new Vector2(0, 0), Vector2.zero, Vector2.zero, Vector2.one);
            _multiplayerPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            _multiplayerPanel.SetActive(false);

            CreateTitle(_multiplayerPanel.transform, "CONNECTION", 30, new Vector2(0, 250));
            CreateSeparator(_multiplayerPanel.transform, 214f, 440f);

            CreateButton(_multiplayerPanel.transform, "LANBtn", "LAN / DIRECT IP", new Vector2(380, 52), new Vector2(0, 120), OnLANClicked);
            CreateButton(_multiplayerPanel.transform, "OnlineBtn", "STEAM P2P (SOON)", new Vector2(380, 52), new Vector2(0, 58), OnOnlineClicked, ColPanelLight);
            CreateButton(_multiplayerPanel.transform, "BackToMainBtn", "BACK", new Vector2(380, 46), new Vector2(0, -60), OnBackToMainClicked, ColPanelLight);

            CreateText(_multiplayerPanel.transform, "Hint", "Radmin VPN / Hamachi work too: enter the host's VPN IP.",
                12, new Vector2(0, -140), ColMuted, TextAnchor.MiddleCenter);
        }

        private void BuildJoinPanel()
        {
            _joinGamePanel = CreatePanel(_mainContainer.transform, "JoinGamePanel", new Vector2(0, 0), Vector2.zero, Vector2.zero, Vector2.one);
            _joinGamePanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            _joinGamePanel.SetActive(false);

            CreateTitle(_joinGamePanel.transform, "JOIN GAME", 30, new Vector2(0, 250));
            CreateSeparator(_joinGamePanel.transform, 214f, 440f);

            CreateText(_joinGamePanel.transform, "IpLabel", "HOST ADDRESS", 12, new Vector2(0, 150), ColMuted, TextAnchor.MiddleLeft, 380);
            _ipInputField = CreateInputField(_joinGamePanel.transform, "IPInput", "192.168.1.10 / 26.x.x.x", "127.0.0.1",
                new Vector2(380, 44), new Vector2(0, 118));

            CreateText(_joinGamePanel.transform, "PortLabel", "PORT", 12, new Vector2(0, 76), ColMuted, TextAnchor.MiddleLeft, 380);
            _portInputField = CreateInputField(_joinGamePanel.transform, "PortInput", "7777", "7777", new Vector2(380, 44), new Vector2(0, 44));

            _connectButton = CreateButton(_joinGamePanel.transform, "ConnectBtn", "CONNECT", new Vector2(380, 52), new Vector2(0, -24), OnConnectClicked);
            CreateButton(_joinGamePanel.transform, "JoinBackBtn", "BACK", new Vector2(380, 46), new Vector2(0, -86), OnBackToMultiplayerClicked, ColPanelLight);

            CreateText(_joinGamePanel.transform, "Hint", "The host must allow TCP 7777 through Windows Firewall.",
                12, new Vector2(0, -160), ColMuted, TextAnchor.MiddleCenter);
        }

        private void BuildLobbyPanel()
        {
            _lobbyPanel = CreatePanel(_mainContainer.transform, "LobbyPanel", new Vector2(0, 0), Vector2.zero, Vector2.zero, Vector2.one);
            _lobbyPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            _lobbyPanel.SetActive(false);

            _lobbyTitleText = CreateTitle(_lobbyPanel.transform, "LOBBY", 30, new Vector2(0, 270));
            _lobbySubtitleText = CreateText(_lobbyPanel.transform, "LobbySubtitle", "HOST", 12, new Vector2(0, 244), ColMuted, TextAnchor.MiddleCenter);
            CreateSeparator(_lobbyPanel.transform, 222f, 460f);

            _connectionStatusText = CreateText(_lobbyPanel.transform, "ConnectionStatus", "Disconnected", 13,
                new Vector2(0, 194), ColBad, TextAnchor.MiddleCenter);
            _playerCountText = CreateText(_lobbyPanel.transform, "PlayerCount", "Players: 1/4", 13,
                new Vector2(0, 172), ColText, TextAnchor.MiddleCenter);

            // ---- hunters column ----
            CreateText(_lobbyPanel.transform, "HuntersLabel", "HUNTERS", 12, new Vector2(-118, 136), ColMuted, TextAnchor.MiddleLeft, 200);
            var huntersFrame = CreatePanel(_lobbyPanel.transform, "HuntersFrame", new Vector2(220, 190), new Vector2(-118, 28),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _playerListContainer = huntersFrame.transform;
            AddTopBar(huntersFrame.transform, ColAccentDim, 3f);

            _playerListText = CreateText(huntersFrame.transform, "PlayerList", "1. HOST", 14, Vector2.zero,
                ColText, TextAnchor.UpperLeft, 196);
            var listRt = _playerListText.GetComponent<RectTransform>();
            listRt.anchorMin = Vector2.zero;
            listRt.anchorMax = Vector2.one;
            listRt.offsetMin = new Vector2(12f, 10f);
            listRt.offsetMax = new Vector2(-12f, -10f);

            // ---- rake column ----
            CreateText(_lobbyPanel.transform, "RakeLabel", "RAKE (1 slot)", 12, new Vector2(118, 136), ColAccent, TextAnchor.MiddleLeft, 200);
            var rakeFrame = CreatePanel(_lobbyPanel.transform, "RakeFrame", new Vector2(220, 190), new Vector2(118, 28),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            AddTopBar(rakeFrame.transform, ColAccent, 3f);

            _rakeSlotText = CreateText(rakeFrame.transform, "RakeSlot", "-", 22, new Vector2(0, 10), ColAccent, TextAnchor.MiddleCenter);
            CreateText(rakeFrame.transform, "RakeHint", "one per session", 11, new Vector2(0, -34), ColMuted, TextAnchor.MiddleCenter);

            _rakeRoleButton = CreateButton(_lobbyPanel.transform, "RakeRoleBtn", "BECOME THE RAKE", new Vector2(220, 40),
                new Vector2(118, -84), OnRakeRoleClicked, ColAccentDim);
            _rakeRoleButtonText = _rakeRoleButton.GetComponentInChildren<Text>();

            // ---- actions ----
            CreateButton(_lobbyPanel.transform, "StartGameBtn", "START GAME", new Vector2(220, 48), new Vector2(-118, -168), OnStartGameClicked);
            CreateButton(_lobbyPanel.transform, "LeaveLobbyBtn", "LEAVE LOBBY", new Vector2(220, 48), new Vector2(118, -168), OnLeaveLobbyClicked, ColPanelLight);
            CreateButton(_lobbyPanel.transform, "CopyIPBtn", "COPY MY IP", new Vector2(460, 38), new Vector2(0, -222), OnCopyIPClicked, ColPanelLight);

            _lobbyHintText = CreateText(_lobbyPanel.transform, "LobbyHint",
                "Only one Rake per session. It cannot attack during the day.", 11, new Vector2(0, -258), ColMuted, TextAnchor.MiddleCenter);
        }

        private void BuildSettingsPanel()
        {
            _settingsPanel = CreatePanel(_mainContainer.transform, "SettingsPanel", new Vector2(0, 0), Vector2.zero, Vector2.zero, Vector2.one);
            _settingsPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            _settingsPanel.SetActive(false);

            CreateTitle(_settingsPanel.transform, "SETTINGS", 30, new Vector2(0, 250));
            CreateSeparator(_settingsPanel.transform, 214f, 440f);

            CreateText(_settingsPanel.transform, "NameLabel", "PLAYER NAME", 12, new Vector2(0, 150), ColMuted, TextAnchor.MiddleLeft, 380);
            _playerNameInputField = CreateInputField(_settingsPanel.transform, "PlayerNameInput", "Player", "Player", new Vector2(380, 44), new Vector2(0, 118));

            CreateText(_settingsPanel.transform, "MaxLabel", "MAX PLAYERS (2-4)", 12, new Vector2(0, 76), ColMuted, TextAnchor.MiddleLeft, 380);
            _maxPlayersInputField = CreateInputField(_settingsPanel.transform, "MaxPlayersInput", "4", "4", new Vector2(380, 44), new Vector2(0, 44));

            _steamP2PToggle = CreateToggle(_settingsPanel.transform, "SteamP2PToggle", "Use Steam P2P", false, new Vector2(0, -20));

            CreateButton(_settingsPanel.transform, "SettingsBackBtn", "BACK", new Vector2(380, 46), new Vector2(0, -100), OnBackToMultiplayerClicked, ColPanelLight);
        }

        // --------------------------------------------------------------- callbacks

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
        private void OnRakeRoleClicked() { if (_menu != null) _menu.OnToggleRakeRole(); }

        // ---------------------------------------------------------------- elements

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
            img.color = ColPanel;
            return obj;
        }

        private static void AddTopBar(Transform parent, Color color, float height)
        {
            var bar = new GameObject("TopBar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(parent, false);
            var rt = bar.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, height);
            bar.GetComponent<Image>().color = color;
        }

        private Text CreateTitle(Transform parent, string text, int fontSize, Vector2 anchoredPos)
        {
            return CreateText(parent, "Title", text, fontSize, anchoredPos, ColText, TextAnchor.MiddleCenter);
        }

        private Text CreateText(Transform parent, string name, string text, int fontSize, Vector2 anchoredPos, Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter, float width = 460f)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, fontSize + 14);
            rt.anchoredPosition = anchoredPos;
            var txt = obj.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = fontSize;
            txt.fontStyle = fontSize >= 20 ? FontStyle.Bold : FontStyle.Normal;
            txt.alignment = alignment;
            txt.color = color;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        private static void CreateSeparator(Transform parent, float y, float width)
        {
            var obj = new GameObject("Separator", typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            obj.GetComponent<Image>().color = new Color(0.72f, 0.16f, 0.12f, 0.35f);
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 size, Vector2 anchoredPos,
            UnityAction onClick, Color? background = null)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            var img = obj.GetComponent<Image>();
            Color baseColor = background.HasValue ? background.Value : new Color(0.16f, 0.14f, 0.15f, 1f);
            img.color = baseColor;

            var btn = obj.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = new Color(0.55f, 0.16f, 0.12f, 1f);
            colors.pressedColor = new Color(0.35f, 0.08f, 0.06f, 1f);
            colors.disabledColor = new Color(0.12f, 0.12f, 0.12f, 0.6f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var edge = new GameObject("Edge", typeof(RectTransform), typeof(Image));
            edge.transform.SetParent(obj.transform, false);
            var edgeRt = edge.GetComponent<RectTransform>();
            edgeRt.anchorMin = new Vector2(0f, 0f);
            edgeRt.anchorMax = new Vector2(0f, 1f);
            edgeRt.pivot = new Vector2(0f, 0.5f);
            edgeRt.sizeDelta = new Vector2(3f, 0f);
            edge.GetComponent<Image>().color = ColAccent;

            var txtObj = new GameObject("Text", typeof(RectTransform));
            txtObj.transform.SetParent(obj.transform, false);
            var txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(14f, 0f);
            txtRt.offsetMax = new Vector2(-14f, 0f);
            var txt = txtObj.AddComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 16;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.color = ColText;

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
            img.color = new Color(0.12f, 0.115f, 0.13f, 1f);

            var input = obj.GetComponent<InputField>();
            input.text = defaultValue;
            input.targetGraphic = img;

            var phObj = new GameObject("Placeholder", typeof(RectTransform));
            phObj.transform.SetParent(obj.transform, false);
            var phRt = phObj.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = new Vector2(12, 0);
            phRt.offsetMax = new Vector2(-12, 0);
            var phTxt = phObj.AddComponent<Text>();
            phTxt.text = placeholder;
            phTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            phTxt.fontSize = 15;
            phTxt.color = new Color(0.45f, 0.43f, 0.40f, 1f);
            phTxt.alignment = TextAnchor.MiddleLeft;
            input.placeholder = phTxt;

            var txtObj = new GameObject("Text", typeof(RectTransform));
            txtObj.transform.SetParent(obj.transform, false);
            var txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(12, 0);
            txtRt.offsetMax = new Vector2(-12, 0);
            var txt = txtObj.AddComponent<Text>();
            txt.text = defaultValue;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 15;
            txt.color = ColText;
            txt.alignment = TextAnchor.MiddleLeft;
            input.textComponent = txt;

            return input;
        }

        private Toggle CreateToggle(Transform parent, string name, string label, bool defaultValue, Vector2 anchoredPos)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(380, 30);
            rt.anchoredPosition = anchoredPos;

            var toggle = obj.GetComponent<Toggle>();
            toggle.isOn = defaultValue;

            var bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgObj.transform.SetParent(obj.transform, false);
            var bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.sizeDelta = new Vector2(20, 20);
            bgRt.anchorMin = new Vector2(0, 0.5f);
            bgRt.anchorMax = new Vector2(0, 0.5f);
            bgRt.anchoredPosition = new Vector2(10, 0);
            bgObj.GetComponent<Image>().color = new Color(0.12f, 0.115f, 0.13f, 1f);

            var checkObj = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkObj.transform.SetParent(bgObj.transform, false);
            var checkRt = checkObj.GetComponent<RectTransform>();
            checkRt.sizeDelta = new Vector2(14, 14);
            checkRt.anchorMin = Vector2.zero;
            checkRt.anchorMax = Vector2.one;
            checkRt.offsetMin = Vector2.zero;
            checkRt.offsetMax = Vector2.zero;
            var checkImg = checkObj.GetComponent<Image>();
            checkImg.color = ColAccent;
            checkImg.enabled = defaultValue;
            toggle.graphic = checkImg;
            toggle.targetGraphic = bgObj.GetComponent<Image>();

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
            lblTxt.fontSize = 15;
            lblTxt.color = ColText;
            lblTxt.alignment = TextAnchor.MiddleLeft;

            return toggle;
        }

        // --------------------------------------------------------------- accessors

        public GameObject MainMenuPanel { get { return _mainMenuPanel; } }
        public GameObject MultiplayerPanel { get { return _multiplayerPanel; } }
        public GameObject LobbyPanel { get { return _lobbyPanel; } }
        public GameObject SettingsPanel { get { return _settingsPanel; } }
        public GameObject JoinGamePanel { get { return _joinGamePanel; } }

        public InputField IPInputField { get { return _ipInputField; } }
        public InputField PortInputField { get { return _portInputField; } }
        public InputField PlayerNameInputField { get { return _playerNameInputField; } }
        public Toggle SteamP2PToggle { get { return _steamP2PToggle; } }
        public Button ConnectButton { get { return _connectButton; } }
        public InputField MaxPlayersInputField { get { return _maxPlayersInputField; } }

        public Text ConnectionStatusText { get { return _connectionStatusText; } }
        public Text PlayerCountText { get { return _playerCountText; } }
        public Text LobbyTitleText { get { return _lobbyTitleText; } }
        public Text LobbySubtitleText { get { return _lobbySubtitleText; } }
        public Text PlayerListText { get { return _playerListText; } }
        public Text RakeSlotText { get { return _rakeSlotText; } }
        public Text RakeRoleButtonText { get { return _rakeRoleButtonText; } }
        public Text LobbyHintText { get { return _lobbyHintText; } }
        public Button RakeRoleButton { get { return _rakeRoleButton; } }

        public Transform PlayerListContainer { get { return _playerListContainer; } }

        public Canvas Canvas { get { return _canvas; } }

        public void SetLobbyRole(bool isHost, bool hostIsRake)
        {
            if (_rakeRoleButton != null)
            {
                _rakeRoleButton.gameObject.SetActive(isHost);
                if (_rakeRoleButtonText != null)
                {
                    _rakeRoleButtonText.text = hostIsRake ? "RETURN TO HUNTERS" : "BECOME THE RAKE";
                }
            }

            if (_rakeSlotText != null)
            {
                _rakeSlotText.text = hostIsRake ? "HOST" : "-";
                _rakeSlotText.color = hostIsRake ? ColAccent : ColMuted;
            }

            if (_lobbyHintText != null)
            {
                _lobbyHintText.text = isHost
                    ? (hostIsRake
                        ? "You are the Rake. No attacks during the day - hunt at night."
                        : "Only one Rake per session. Press BECOME THE RAKE to play as it.")
                    : "Only one Rake per session. It cannot attack during the day.";
            }
        }

        public void SetHUDVisible(bool visible)
        {
            if (_hudPanel != null) _hudPanel.SetActive(visible);
        }

        public void UpdatePhaseText(string text)
        {
            if (_phaseText != null) _phaseText.text = text;
        }

        public void UpdateNightText(string text)
        {
            if (_nightText != null) _nightText.text = text;
        }

        public void UpdateTimerText(string text)
        {
            if (_timerText != null) _timerText.text = text;
        }

        private void BuildMapUI()
        {
            if (_canvas == null) return;

            var map = MapUI.Create(_canvas);
            if (map != null)
            {
                RakeMultiplayerPlugin.Log?.LogInfo("[UI] MapUI integrated (toggle with M)");
            }
        }

        private void BuildHUD()
        {
            if (_canvas == null) return;

            _hudPanel = CreatePanel(_canvas.transform, "HUDPanel", new Vector2(300, 100), new Vector2(0, -60),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            _hudPanel.SetActive(false);
            _hudPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            _phaseText = CreateTitle(_hudPanel.transform, "DAY", 24, new Vector2(0, 30));
            _phaseText.color = Color.yellow;

            _nightText = CreateTitle(_hudPanel.transform, "Night: 0/3", 18, new Vector2(0, 0));
            _nightText.color = Color.white;

            _timerText = CreateTitle(_hudPanel.transform, "05:00", 28, new Vector2(0, -30));
            _timerText.color = Color.white;
        }
    }
}
