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
        private GameObject _hostPanel;
        private GameObject _multiplayerPanel;
        private GameObject _lobbyPanel;
        private GameObject _settingsPanel;
        private GameObject _joinGamePanel;

        private GameObject _steamPanel;
        private GameObject _steamCreatePanel;
        private GameObject _steamBrowsePanel;
        private GameObject _steamPasswordPanel;

        private Text _steamMainStatus;
        private Text _steamCreateStatus;
        private Text _steamBrowseStatus;
        private Text _steamPasswordStatus;
        private Text _steamPasswordTitle;
        private InputField _steamNameInput;
        private InputField _steamPasswordInput;
        private InputField _steamJoinPasswordInput;
        private readonly Button[] _steamRows = new Button[6];
        private readonly Text[] _steamRowTexts = new Text[6];

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
        private Button _startGameButton;
        private Text _waitingText;
        private GameObject _rakeHpPanel;
        private Image _rakeHpFill;
        private Text _rakeHpLabel;
        private Text _deathText;
        private float _deathTimer;

        /// <summary>The one UI builder that lives for the whole session.</summary>
        public static RuntimeUIBuilder Instance { get; private set; }

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
            Instance = this;
            _networkManager = UnityEngine.Object.FindObjectOfType<RakeNetworkManager>();
            BuildUI();
            BuildHUD();
            BuildRakeHealthBar();
            BuildDeathBanner();
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

            _mainContainer = CreatePanel(canvasObj.transform, "MainContainer", new Vector2(760, 840), Vector2.zero,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _mainContainer.GetComponent<Image>().color = ColBg;
            _mainContainer.SetActive(false);

            AddTopBar(_mainContainer.transform, ColAccent, 4f);
            AddPanelAnimation(_mainContainer, 0f, true);

            BuildMainMenu();
            BuildHostPanel();
            BuildMultiplayerPanel();
            BuildJoinPanel();
            BuildLobbyPanel();
            BuildSettingsPanel();
            BuildSteamPanels();

            _menu.SetMainContainer(_mainContainer);
        }

        // ------------------------------------------------------------------ panels

        private void BuildMainMenu()
        {
            _mainMenuPanel = CreatePanel(_mainContainer.transform, "MainMenuPanel", new Vector2(0, 0), Vector2.zero, Vector2.zero, Vector2.one);
            _mainMenuPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            AddPanelAnimation(_mainMenuPanel, 16f);

            CreateText(_mainMenuPanel.transform, "Kicker", "RAKE", 14, new Vector2(0, 344), ColAccent, TextAnchor.MiddleCenter);
            CreateTitle(_mainMenuPanel.transform, "MULTIPLAYER", 40, new Vector2(0, 306));
            CreateText(_mainMenuPanel.transform, "Tagline", "THE HUNT BEGINS WITH YOU", 14, new Vector2(0, 266), ColMuted, TextAnchor.MiddleCenter);

            CreateSeparator(_mainMenuPanel.transform, 232f, 560f);

            CreateButton(_mainMenuPanel.transform, "HostGameBtn", "HOST GAME", new Vector2(460, 60), new Vector2(0, 152), OnHostGameClicked);
            CreateButton(_mainMenuPanel.transform, "JoinGameBtn", "JOIN GAME", new Vector2(460, 60), new Vector2(0, 80), OnJoinGameClicked);
            CreateButton(_mainMenuPanel.transform, "SettingsBtn", "SETTINGS", new Vector2(460, 60), new Vector2(0, 8), OnSettingsClicked);
            CreateButton(_mainMenuPanel.transform, "ExitBtn", "EXIT TO GAME", new Vector2(460, 60), new Vector2(0, -64), OnExitClicked, ColPanelLight);

            CreateSeparator(_mainMenuPanel.transform, -140f, 560f);

            CreateText(_mainMenuPanel.transform, "Hint",
                "F1 - menu   |   Tab - cameras   |   1 Rake + up to 3 hunters", 12, new Vector2(0, -184), ColMuted, TextAnchor.MiddleCenter, 620);
            CreateText(_mainMenuPanel.transform, "Version", PluginInfo.NAME + "  v" + PluginInfo.VERSION, 11,
                new Vector2(0, -216), new Color(0.35f, 0.33f, 0.30f, 1f), TextAnchor.MiddleCenter);
        }

        private void BuildHostPanel()
        {
            _hostPanel = CreatePanel(_mainContainer.transform, "HostPanel", new Vector2(0, 0), Vector2.zero, Vector2.zero, Vector2.one);
            _hostPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            _hostPanel.SetActive(false);
            AddPanelAnimation(_hostPanel, 16f);

            CreateTitle(_hostPanel.transform, "CREATE LOBBY", 32, new Vector2(0, 322));
            CreateSeparator(_hostPanel.transform, 282f, 560f);

            CreateText(_hostPanel.transform, "HostSubtitle", "CHOOSE HOW THE HUNT IS HOSTED", 12,
                new Vector2(0, 246), ColMuted, TextAnchor.MiddleCenter, 620);

            CreateButton(_hostPanel.transform, "LanHostBtn", "CREATE LAN LOBBY", new Vector2(460, 62),
                new Vector2(0, 168), OnCreateLanClicked);
            CreateButton(_hostPanel.transform, "SteamHostBtn", "CREATE STEAM LOBBY", new Vector2(460, 62),
                new Vector2(0, 96), OnCreateSteamClicked);
            CreateButton(_hostPanel.transform, "HostBackBtn", "BACK", new Vector2(460, 48),
                new Vector2(0, 8), OnBackToMainClicked, ColPanelLight);

            CreateText(_hostPanel.transform, "HostHint",
                "LAN: direct TCP, no Steam needed.  STEAM: lobby browser, invites and no port forwarding.", 11,
                new Vector2(0, -110), ColMuted, TextAnchor.MiddleCenter, 660);
        }

        private void BuildMultiplayerPanel()
        {
            _multiplayerPanel = CreatePanel(_mainContainer.transform, "MultiplayerPanel", new Vector2(0, 0), Vector2.zero, Vector2.zero, Vector2.one);
            _multiplayerPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            _multiplayerPanel.SetActive(false);
            AddPanelAnimation(_multiplayerPanel, 16f);

            CreateTitle(_multiplayerPanel.transform, "CONNECTION", 32, new Vector2(0, 320));
            CreateSeparator(_multiplayerPanel.transform, 282f, 560f);

            CreateButton(_multiplayerPanel.transform, "LANBtn", "LAN / DIRECT IP", new Vector2(460, 60), new Vector2(0, 190), OnLANClicked);
            CreateButton(_multiplayerPanel.transform, "OnlineBtn", "STEAM LOBBY", new Vector2(460, 60), new Vector2(0, 118), OnOnlineClicked, ColPanelLight);
            CreateButton(_multiplayerPanel.transform, "BackToMainBtn", "BACK", new Vector2(460, 48), new Vector2(0, 30), OnBackToMainClicked, ColPanelLight);

            CreateText(_multiplayerPanel.transform, "Hint", "Radmin VPN / Hamachi work too: enter the host's VPN IP.",
                12, new Vector2(0, -128), ColMuted, TextAnchor.MiddleCenter, 620);
        }

        private void BuildJoinPanel()
        {
            _joinGamePanel = CreatePanel(_mainContainer.transform, "JoinGamePanel", new Vector2(0, 0), Vector2.zero, Vector2.zero, Vector2.one);
            _joinGamePanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            _joinGamePanel.SetActive(false);
            AddPanelAnimation(_joinGamePanel, 16f);

            CreateTitle(_joinGamePanel.transform, "JOIN GAME", 32, new Vector2(0, 320));
            CreateSeparator(_joinGamePanel.transform, 282f, 560f);

            CreateText(_joinGamePanel.transform, "IpLabel", "HOST ADDRESS", 12, new Vector2(0, 224), ColMuted, TextAnchor.MiddleLeft, 460);
            _ipInputField = CreateInputField(_joinGamePanel.transform, "IPInput", "192.168.1.10 / 26.x.x.x", "127.0.0.1",
                new Vector2(460, 50), new Vector2(0, 188));

            CreateText(_joinGamePanel.transform, "PortLabel", "PORT", 12, new Vector2(0, 138), ColMuted, TextAnchor.MiddleLeft, 460);
            _portInputField = CreateInputField(_joinGamePanel.transform, "PortInput", "7777", "7777", new Vector2(460, 50), new Vector2(0, 102));

            _connectButton = CreateButton(_joinGamePanel.transform, "ConnectBtn", "CONNECT", new Vector2(460, 60), new Vector2(0, 22), OnConnectClicked);
            CreateButton(_joinGamePanel.transform, "JoinBackBtn", "BACK", new Vector2(460, 48), new Vector2(0, -50), OnBackToMultiplayerClicked, ColPanelLight);

            CreateText(_joinGamePanel.transform, "Hint", "The host must allow TCP 7777 through Windows Firewall.",
                12, new Vector2(0, -156), ColMuted, TextAnchor.MiddleCenter, 620);
        }

        private void BuildLobbyPanel()
        {
            _lobbyPanel = CreatePanel(_mainContainer.transform, "LobbyPanel", new Vector2(0, 0), Vector2.zero, Vector2.zero, Vector2.one);
            _lobbyPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            _lobbyPanel.SetActive(false);
            AddPanelAnimation(_lobbyPanel, 16f);

            _lobbyTitleText = CreateTitle(_lobbyPanel.transform, "LOBBY", 32, new Vector2(0, 352));
            _lobbySubtitleText = CreateText(_lobbyPanel.transform, "LobbySubtitle", "HOST", 12, new Vector2(0, 324), ColMuted, TextAnchor.MiddleCenter);
            CreateSeparator(_lobbyPanel.transform, 300f, 620f);

            _connectionStatusText = CreateText(_lobbyPanel.transform, "ConnectionStatus", "Disconnected", 13,
                new Vector2(0, 262), ColBad, TextAnchor.MiddleCenter);
            _playerCountText = CreateText(_lobbyPanel.transform, "PlayerCount", "Players: 1/4", 13,
                new Vector2(0, 236), ColText, TextAnchor.MiddleCenter);

            // ---- hunters column ----
            CreateText(_lobbyPanel.transform, "HuntersLabel", "HUNTERS", 12, new Vector2(-172, 196), ColMuted, TextAnchor.MiddleLeft, 320);
            var huntersFrame = CreatePanel(_lobbyPanel.transform, "HuntersFrame", new Vector2(320, 240), new Vector2(-172, 52),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _playerListContainer = huntersFrame.transform;
            AddTopBar(huntersFrame.transform, ColAccentDim, 3f);

            _playerListText = CreateText(huntersFrame.transform, "PlayerList", "1. HOST", 14, Vector2.zero,
                ColText, TextAnchor.UpperLeft, 292);
            var listRt = _playerListText.GetComponent<RectTransform>();
            listRt.anchorMin = Vector2.zero;
            listRt.anchorMax = Vector2.one;
            listRt.offsetMin = new Vector2(16f, 14f);
            listRt.offsetMax = new Vector2(-16f, -14f);

            // ---- rake column ----
            CreateText(_lobbyPanel.transform, "RakeLabel", "RAKE (1 slot)", 12, new Vector2(172, 196), ColAccent, TextAnchor.MiddleLeft, 320);
            var rakeFrame = CreatePanel(_lobbyPanel.transform, "RakeFrame", new Vector2(320, 240), new Vector2(172, 52),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            AddTopBar(rakeFrame.transform, ColAccent, 3f);

            _rakeSlotText = CreateText(rakeFrame.transform, "RakeSlot", "-", 24, new Vector2(0, 14), ColAccent, TextAnchor.MiddleCenter);
            CreateText(rakeFrame.transform, "RakeHint", "one per session", 11, new Vector2(0, -40), ColMuted, TextAnchor.MiddleCenter);

            _rakeRoleButton = CreateButton(_lobbyPanel.transform, "RakeRoleBtn", "BECOME THE RAKE", new Vector2(320, 48),
                new Vector2(172, -112), OnRakeRoleClicked, ColAccentDim);
            _rakeRoleButtonText = _rakeRoleButton.GetComponentInChildren<Text>();

            // ---- actions ----
            _startGameButton = CreateButton(_lobbyPanel.transform, "StartGameBtn", "START GAME", new Vector2(320, 56), new Vector2(-172, -206), OnStartGameClicked);
            CreateButton(_lobbyPanel.transform, "LeaveLobbyBtn", "LEAVE LOBBY", new Vector2(320, 56), new Vector2(172, -206), OnLeaveLobbyClicked, ColPanelLight);
            _waitingText = CreateText(_lobbyPanel.transform, "WaitingText", "WAITING FOR THE HOST...", 12,
                new Vector2(-172, -206), ColMuted, TextAnchor.MiddleCenter, 320f);
            _waitingText.gameObject.SetActive(false);

            CreateButton(_lobbyPanel.transform, "CopyIPBtn", "COPY MY IP", new Vector2(660, 42), new Vector2(0, -274), OnCopyIPClicked, ColPanelLight);

            _lobbyHintText = CreateText(_lobbyPanel.transform, "LobbyHint",
                "Only one Rake per session. It cannot attack during the day.", 11, new Vector2(0, -318), ColMuted, TextAnchor.MiddleCenter, 660);
        }

        private void BuildSettingsPanel()
        {
            _settingsPanel = CreatePanel(_mainContainer.transform, "SettingsPanel", new Vector2(0, 0), Vector2.zero, Vector2.zero, Vector2.one);
            _settingsPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            _settingsPanel.SetActive(false);
            AddPanelAnimation(_settingsPanel, 16f);

            CreateTitle(_settingsPanel.transform, "SETTINGS", 32, new Vector2(0, 320));
            CreateSeparator(_settingsPanel.transform, 282f, 560f);

            CreateText(_settingsPanel.transform, "NameLabel", "PLAYER NAME", 12, new Vector2(0, 224), ColMuted, TextAnchor.MiddleLeft, 460);
            _playerNameInputField = CreateInputField(_settingsPanel.transform, "PlayerNameInput", "Player", "Player", new Vector2(460, 50), new Vector2(0, 188));

            CreateText(_settingsPanel.transform, "MaxLabel", "MAX PLAYERS (2-4)", 12, new Vector2(0, 138), ColMuted, TextAnchor.MiddleLeft, 460);
            _maxPlayersInputField = CreateInputField(_settingsPanel.transform, "MaxPlayersInput", "4", "4", new Vector2(460, 50), new Vector2(0, 102));

            _steamP2PToggle = CreateToggle(_settingsPanel.transform, "SteamP2PToggle", "Use Steam P2P", false, new Vector2(0, 30));

            CreateButton(_settingsPanel.transform, "SettingsBackBtn", "BACK", new Vector2(460, 48), new Vector2(0, -62), OnBackToMultiplayerClicked, ColPanelLight);
        }
        // ------------------------------------------------------------- steam panels

        private void BuildSteamPanels()
        {
            // main
            _steamPanel = CreatePanel(_mainContainer.transform, "SteamPanel", Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.one);
            _steamPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            _steamPanel.SetActive(false);
            AddPanelAnimation(_steamPanel, 16f);

            CreateTitle(_steamPanel.transform, "STEAM", 32, new Vector2(0, 330));
            _steamMainStatus = CreateText(_steamPanel.transform, "SteamStatus", "Steam ready", 12,
                new Vector2(0, 290), ColMuted, TextAnchor.MiddleCenter, 620);
            CreateSeparator(_steamPanel.transform, 256f, 560f);

            CreateButton(_steamPanel.transform, "SteamCreateBtn", "CREATE LOBBY", new Vector2(460, 60),
                new Vector2(0, 186), OnSteamCreateClicked);
            CreateButton(_steamPanel.transform, "SteamBrowseBtn", "BROWSE LOBBIES", new Vector2(460, 60),
                new Vector2(0, 114), OnSteamBrowseClicked);
            CreateButton(_steamPanel.transform, "SteamBackBtn", "BACK", new Vector2(460, 48),
                new Vector2(0, 26), OnBackToMultiplayerClicked, ColPanelLight);

            CreateText(_steamPanel.transform, "SteamHint", "Spacewar app 480 · Steam must be running", 11,
                new Vector2(0, -160), ColMuted, TextAnchor.MiddleCenter, 660);

            // create
            _steamCreatePanel = CreatePanel(_mainContainer.transform, "SteamCreatePanel", Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.one);
            _steamCreatePanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            _steamCreatePanel.SetActive(false);
            AddPanelAnimation(_steamCreatePanel, 16f);

            CreateTitle(_steamCreatePanel.transform, "CREATE LOBBY", 32, new Vector2(0, 330));
            CreateSeparator(_steamCreatePanel.transform, 292f, 560f);

            CreateText(_steamCreatePanel.transform, "SteamNameLabel", "LOBBY NAME", 12, new Vector2(0, 232),
                ColMuted, TextAnchor.MiddleLeft, 460);
            _steamNameInput = CreateInputField(_steamCreatePanel.transform, "SteamNameInput", "Night Hunt",
                "Night Hunt", new Vector2(460, 50), new Vector2(0, 196));

            CreateText(_steamCreatePanel.transform, "SteamPassLabel", "PASSWORD (OPTIONAL)", 12,
                new Vector2(0, 142), ColMuted, TextAnchor.MiddleLeft, 460);
            _steamPasswordInput = CreateInputField(_steamCreatePanel.transform, "SteamPassInput", "empty = public",
                "", new Vector2(460, 50), new Vector2(0, 106));

            CreateText(_steamCreatePanel.transform, "SteamSlots", "SLOTS: 4 (1 Rake + 3 hunters)", 12,
                new Vector2(0, 42), ColMuted, TextAnchor.MiddleCenter, 560);

            CreateButton(_steamCreatePanel.transform, "SteamCreateOk", "CREATE", new Vector2(460, 60),
                new Vector2(0, -6), OnSteamCreateConfirmed);
            CreateButton(_steamCreatePanel.transform, "SteamCreateBack", "BACK", new Vector2(460, 48),
                new Vector2(0, -70), OnSteamBackClicked, ColPanelLight);

            _steamCreateStatus = CreateText(_steamCreatePanel.transform, "SteamCreateStatus", "", 12,
                new Vector2(0, -176), ColBad, TextAnchor.MiddleCenter, 660);

            // browse
            _steamBrowsePanel = CreatePanel(_mainContainer.transform, "SteamBrowsePanel", Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.one);
            _steamBrowsePanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            _steamBrowsePanel.SetActive(false);
            AddPanelAnimation(_steamBrowsePanel, 16f);

            CreateTitle(_steamBrowsePanel.transform, "LOBBIES", 32, new Vector2(0, 330));
            _steamBrowseStatus = CreateText(_steamBrowsePanel.transform, "SteamBrowseStatus", "", 12,
                new Vector2(0, 292), ColMuted, TextAnchor.MiddleCenter, 660);
            CreateSeparator(_steamBrowsePanel.transform, 264f, 620f);

            for (int i = 0; i < 6; i++)
            {
                int index = i;
                var row = CreateButton(_steamBrowsePanel.transform, "SteamRow" + i, "", new Vector2(620, 44),
                    new Vector2(0, 212 - i * 50), () => OnSteamRowClicked(index), ColPanelLight);
                _steamRows[i] = row;
                _steamRowTexts[i] = row.GetComponentInChildren<Text>();
                row.gameObject.SetActive(false);
            }

            CreateButton(_steamBrowsePanel.transform, "SteamRefresh", "REFRESH", new Vector2(300, 46),
                new Vector2(-160, -116), OnSteamRefreshClicked);
            CreateButton(_steamBrowsePanel.transform, "SteamBrowseBack", "BACK", new Vector2(300, 46),
                new Vector2(160, -116), OnSteamBackClicked, ColPanelLight);

            // password
            _steamPasswordPanel = CreatePanel(_mainContainer.transform, "SteamPasswordPanel", Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.one);
            _steamPasswordPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            _steamPasswordPanel.SetActive(false);
            AddPanelAnimation(_steamPasswordPanel, 16f);

            CreateTitle(_steamPasswordPanel.transform, "JOIN LOBBY", 32, new Vector2(0, 330));
            _steamPasswordTitle = CreateText(_steamPasswordPanel.transform, "SteamJoinTitle", "", 13,
                new Vector2(0, 290), ColAccent, TextAnchor.MiddleCenter, 620);
            CreateSeparator(_steamPasswordPanel.transform, 262f, 560f);

            CreateText(_steamPasswordPanel.transform, "SteamJoinPassLabel", "PASSWORD", 12, new Vector2(0, 200),
                ColMuted, TextAnchor.MiddleLeft, 460);
            _steamJoinPasswordInput = CreateInputField(_steamPasswordPanel.transform, "SteamJoinPassInput",
                "lobby password", "", new Vector2(460, 50), new Vector2(0, 164));

            CreateButton(_steamPasswordPanel.transform, "SteamJoinOk", "JOIN", new Vector2(460, 60),
                new Vector2(0, 86), OnSteamJoinConfirmed);
            CreateButton(_steamPasswordPanel.transform, "SteamJoinBack", "BACK", new Vector2(460, 48),
                new Vector2(0, 16), OnSteamRowBackClicked, ColPanelLight);

            _steamPasswordStatus = CreateText(_steamPasswordPanel.transform, "SteamJoinStatus", "", 12,
                new Vector2(0, -92), ColBad, TextAnchor.MiddleCenter, 660);
        }

        // -------------------------------------------------- steam callbacks

        private void OnSteamCreateClicked() { if (_menu != null) _menu.ShowSteamCreate(); }
        private void OnSteamBrowseClicked() { if (_menu != null) _menu.ShowSteamBrowse(); }
        private void OnSteamBackClicked() { if (_menu != null) _menu.ShowSteamMain(); }
        private void OnSteamCreateConfirmed() { if (_menu != null) _menu.OnSteamCreate(); }
        private void OnSteamRefreshClicked() { if (_menu != null) _menu.OnSteamRefresh(); }
        private void OnSteamJoinConfirmed() { if (_menu != null) _menu.OnSteamJoin(); }
        private void OnSteamRowBackClicked() { if (_menu != null) _menu.ShowSteamBrowse(); }
        private void OnSteamRowClicked(int index) { if (_menu != null) _menu.OnSteamLobbyClicked(index); }

        // -------------------------------------------------- steam accessors

        public GameObject HostPanel { get { return _hostPanel; } }
        public GameObject SteamPanel { get { return _steamPanel; } }
        public GameObject SteamCreatePanel { get { return _steamCreatePanel; } }
        public GameObject SteamBrowsePanel { get { return _steamBrowsePanel; } }
        public GameObject SteamPasswordPanel { get { return _steamPasswordPanel; } }
        public InputField SteamNameInput { get { return _steamNameInput; } }
        public InputField SteamPasswordInput { get { return _steamPasswordInput; } }
        public InputField SteamJoinPasswordInput { get { return _steamJoinPasswordInput; } }

        public void SetSteamStatus(string text, bool error = false)
        {
            if (_steamMainStatus != null)
            {
                _steamMainStatus.text = text;
                _steamMainStatus.color = error ? ColBad : ColMuted;
            }
        }

        public void SetSteamCreateStatus(string text)
        {
            if (_steamCreateStatus != null) _steamCreateStatus.text = text;
        }

        public void SetSteamBrowseStatus(string text)
        {
            if (_steamBrowseStatus != null) _steamBrowseStatus.text = text;
        }

        public void SetSteamPasswordStatus(string text)
        {
            if (_steamPasswordStatus != null) _steamPasswordStatus.text = text;
        }

        public void SetSteamPasswordLobby(string text)
        {
            if (_steamPasswordTitle != null) _steamPasswordTitle.text = text;
        }

        public void UpdateSteamRows(System.Collections.Generic.List<SteamLobbyManager.LobbyInfo> lobbies)
        {
            for (int i = 0; i < _steamRows.Length; i++)
            {
                bool has = i < lobbies.Count;
                _steamRows[i].gameObject.SetActive(has);
                if (!has) continue;

                var info = lobbies[i];
                string lockIcon = info.HasPassword ? "  [LOCK]" : "";
                _steamRowTexts[i].text = info.Name + "   " + info.Host + "   " +
                                         info.Members + "/" + info.Max + lockIcon;
            }
        }
        // --------------------------------------------------------------- callbacks

        private void OnHostGameClicked() { if (_menu != null) _menu.ShowCreateLobby(); }
        private void OnCreateLanClicked() { if (_menu != null) _menu.OnHostGame(); }
        private void OnCreateSteamClicked() { if (_menu != null) _menu.ShowSteamCreate(); }
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

        private static void AddPanelAnimation(GameObject panel, float slide, bool scale = false)
        {
            if (panel == null) return;

            var animator = panel.GetComponent<PanelAnimator>();
            if (animator == null) animator = panel.AddComponent<PanelAnimator>();
            animator.Slide = slide;
            animator.AnimateScale = scale;
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
            obj.AddComponent<ButtonHoverScale>();

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

        public void SetLobbyRole(bool isHost, int rakeOwnerId, bool localIsOwner)
        {
            if (_startGameButton != null) _startGameButton.gameObject.SetActive(isHost);
            if (_waitingText != null) _waitingText.gameObject.SetActive(!isHost);

            if (_rakeRoleButton != null)
            {
                // Any player may claim the single Rake slot.
                _rakeRoleButton.gameObject.SetActive(true);
                if (_rakeRoleButtonText != null)
                {
                    _rakeRoleButtonText.text = localIsOwner ? "RETURN TO HUNTERS" : "BECOME THE RAKE";
                }
            }

            if (_rakeSlotText != null)
            {
                if (rakeOwnerId <= 0)
                {
                    _rakeSlotText.text = "-";
                    _rakeSlotText.color = ColMuted;
                }
                else if (rakeOwnerId == 1)
                {
                    _rakeSlotText.text = "HOST";
                    _rakeSlotText.color = ColAccent;
                }
                else
                {
                    _rakeSlotText.text = "PLAYER " + rakeOwnerId;
                    _rakeSlotText.color = ColAccent;
                }
            }

            if (_lobbyHintText != null)
            {
                if (localIsOwner)
                {
                    _lobbyHintText.text = "You are the Rake. No attacks during the day - hunt at night.";
                }
                else if (rakeOwnerId > 0)
                {
                    _lobbyHintText.text = "The Rake slot is taken. Press BECOME THE RAKE to take it over.";
                }
                else
                {
                    _lobbyHintText.text = "Only one Rake per session. Press BECOME THE RAKE to play as it.";
                }
            }
        }

        /// <summary>Big red announcement when the Rake dies.</summary>
        public void ShowRakeDeathBanner()
        {
            if (_deathText == null) return;
            _deathText.gameObject.SetActive(true);
            _deathTimer = 6f;
        }

        public void UpdateRakeHealth(float health, float max)
        {
            if (_rakeHpPanel == null || _rakeHpFill == null) return;

            if (max <= 0f || health <= 0f)
            {
                _rakeHpPanel.SetActive(false);
                return;
            }

            _rakeHpPanel.SetActive(true);
            float ratio = Mathf.Clamp01(health / max);
            var rt = _rakeHpFill.rectTransform;
            rt.sizeDelta = new Vector2(460f * ratio, 0f);

            _rakeHpFill.color = ratio > 0.5f
                ? ColAccent
                : (ratio > 0.2f ? new Color(0.85f, 0.45f, 0.12f, 1f) : new Color(0.90f, 0.20f, 0.15f, 1f));

            if (_rakeHpLabel != null)
            {
                _rakeHpLabel.text = "THE RAKE   " + Mathf.CeilToInt(health) + " / " + Mathf.CeilToInt(max);
            }
        }

        public void HideRakeHealth()
        {
            if (_rakeHpPanel != null) _rakeHpPanel.SetActive(false);
        }

        private void BuildRakeHealthBar()
        {
            if (_canvas == null) return;

            _rakeHpPanel = CreatePanel(_canvas.transform, "RakeHealthPanel", new Vector2(460, 40),
                new Vector2(0f, -122f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            _rakeHpPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            _rakeHpLabel = CreateText(_rakeHpPanel.transform, "RakeName", "THE RAKE", 12,
                new Vector2(0f, 10f), ColAccent, TextAnchor.MiddleCenter, 460f);

            var track = CreatePanel(_rakeHpPanel.transform, "RakeHpTrack", new Vector2(460, 10),
                new Vector2(0f, -8f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            track.GetComponent<Image>().color = new Color(0.10f, 0.09f, 0.10f, 0.95f);

            var fillObj = new GameObject("RakeHpFill", typeof(RectTransform), typeof(Image));
            fillObj.transform.SetParent(track.transform, false);
            var frt = fillObj.GetComponent<RectTransform>();
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.anchoredPosition = Vector2.zero;
            frt.sizeDelta = new Vector2(460f, 0f);
            _rakeHpFill = fillObj.GetComponent<Image>();
            _rakeHpFill.color = ColAccent;

            _rakeHpPanel.SetActive(false);
        }

        private void BuildDeathBanner()
        {
            if (_canvas == null) return;

            _deathText = CreateText(_canvas.transform, "RakeDeathBanner", "THE RAKE IS DEAD", 46,
                new Vector2(0f, 120f), new Color(0.90f, 0.15f, 0.12f, 1f), TextAnchor.MiddleCenter, 1200f);
            _deathText.fontStyle = FontStyle.Bold;
            _deathText.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_deathTimer <= 0f) return;

            _deathTimer -= Time.unscaledDeltaTime;
            if (_deathTimer > 0f) return;

            if (_deathText != null) _deathText.gameObject.SetActive(false);
        }

        public static void ShowRakeDeath()
        {
            if (Instance != null) Instance.ShowRakeDeathBanner();
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
