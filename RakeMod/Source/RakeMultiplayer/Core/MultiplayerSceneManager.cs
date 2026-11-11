using System.Collections;
using UnityEngine;
using RakeMultiplayer.UI;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    /// <summary>
    /// Manages scene transitions for multiplayer
    /// </summary>
    public class MultiplayerSceneManager : MonoBehaviour
    {
        public static MultiplayerSceneManager Instance { get; private set; }

        [Header("Scene Names")]
        public string mainMenuScene = "MainMenu";
        public string lobbyScene = "Lobby";
        public string gameScene = "GameScene";

        [Header("References")]
        public RakeNetworkManager networkManager;
        public PlayerSpawner playerSpawner;
        public MultiplayerMenu multiplayerMenu;

        // Core systems that persist across scenes
        private DayNightManager _dayNightManager;
        private SessionManager _sessionManager;

        private string lastSceneName;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            lastSceneName = Application.loadedLevelName;
            StartCoroutine(CheckSceneChanges());
        }

        private IEnumerator CheckSceneChanges()
        {
            while (true)
            {
                yield return null;
                string currentScene = Application.loadedLevelName;
                if (currentScene != lastSceneName)
                {
                    OnSceneChanged(currentScene);
                    lastSceneName = currentScene;
                }
            }
        }

        private void OnSceneChanged(string sceneName)
        {
            RakeMultiplayerPlugin.Log?.LogInfo($"[SceneManager] Scene changed to: {sceneName}");

            // Re-bind persistent managers after scene load
            RebindPersistentManagers();

            // "Skip day" and night transitions reload the level: every remote proxy was
            // destroyed with the old scene, so recreate them for the new one.
            if (RakeMultiplayerPlugin.isConnected)
            {
                if (playerSpawner != null)
                {
                    playerSpawner.PruneDestroyedRemotePlayers();
                }
                RakeNetworkManager.Instance?.EnsureRemotePlayersSpawned();
            }

            // Rake uses level0..level5 scene names; treat any level scene as gameplay
            if (sceneName == gameScene || sceneName.StartsWith("level"))
            {
                OnGameSceneLoaded();
            }
            else if (sceneName == lobbyScene)
            {
                OnLobbySceneLoaded();
            }
            else if (sceneName == mainMenuScene)
            {
                OnMainMenuSceneLoaded();
            }
        }

        private void RebindPersistentManagers()
        {
            // Find DayNightManager and SessionManager (they have DontDestroyOnLoad)
            _dayNightManager = UnityEngine.Object.FindObjectOfType<DayNightManager>();
            _sessionManager = UnityEngine.Object.FindObjectOfType<SessionManager>();

            // Re-bind network manager and player spawner references
            if (networkManager == null)
                networkManager = UnityEngine.Object.FindObjectOfType<RakeNetworkManager>();
            if (playerSpawner == null)
                playerSpawner = UnityEngine.Object.FindObjectOfType<PlayerSpawner>();
            if (multiplayerMenu == null)
                multiplayerMenu = UnityEngine.Object.FindObjectOfType<MultiplayerMenu>();

            // Refresh network state after scene load
            if (networkManager != null)
            {
                networkManager.ReregisterHandlers();
                networkManager.Refresh();
            }

            if (playerSpawner != null)
            {
                playerSpawner.Refresh();
            }

            RakeMultiplayerPlugin.Log?.LogInfo("[SceneManager] Persistent managers re-bound after scene load");
        }

        private void OnGameSceneLoaded()
        {
            // Re-bind in case not done yet
            if (_dayNightManager == null) _dayNightManager = UnityEngine.Object.FindObjectOfType<DayNightManager>();
            if (_sessionManager == null) _sessionManager = UnityEngine.Object.FindObjectOfType<SessionManager>();

            // Discover per-scene POIs (Task 4.1) and apply spawn configuration (Task 4.2)
            var settings = MapSettings.FindOrCreate(UnityEngine.Application.loadedLevelName);
            if (playerSpawner != null)
            {
                int nightIndex = _sessionManager != null ? Mathf.Max(0, _sessionManager.CurrentNight - 1) : 0;
                playerSpawner.ApplyMapSettings(settings, nightIndex);
            }

            // Spawn any remote players that connected before the scene loaded
            RakeNetworkManager.Instance?.EnsureRemotePlayersSpawned();

            // Restore world/inventory after a scene reload (Tasks 4.3 / 4.4)
            WorldPersistence.RestoreWorld();
            if (playerSpawner != null && playerSpawner.LocalPlayer != null)
            {
                WorldPersistence.RestoreInventory(playerSpawner.LocalPlayer);
            }

            if (playerSpawner != null && RakeMultiplayerPlugin.isServer)
            {
                playerSpawner.SpawnRake();
                playerSpawner.SpawnItems();
            }

            // If session is running, ensure DayNightManager is synced
            if (_sessionManager != null && _sessionManager.IsRunning && _dayNightManager != null)
            {
                // DayNightManager already persists, just ensure it's initialized
                if (!_dayNightManager.IsRunning && _sessionManager.CurrentState == SessionState.Active)
                {
                    // Session is active but day/night cycle isn't running - restart it
                    bool isNight = _dayNightManager.CurrentPhase == Phase.Night;
                    int nightNum = _sessionManager.CurrentNight > 0 ? _sessionManager.CurrentNight - 1 : 0;
                    _dayNightManager.StartCycle(isNight, nightNum);
                }
            }
        }

        private void OnLobbySceneLoaded()
        {
            if (multiplayerMenu != null)
            {
                multiplayerMenu.ShowLobby();
            }
        }

        private void OnMainMenuSceneLoaded()
        {
            if (multiplayerMenu != null)
            {
                multiplayerMenu.ShowMainMenu();
            }
        }

        public void LoadLobbyScene()
        {
            Application.LoadLevel(lobbyScene);
        }

        public void LoadGameScene()
        {
            // Snapshot persistent state before the scene unloads (Tasks 4.3 / 4.4)
            if (playerSpawner != null && playerSpawner.LocalPlayer != null)
            {
                WorldPersistence.SaveInventory(playerSpawner.LocalPlayer);
            }
            WorldPersistence.SaveWorld();

            Application.LoadLevel(gameScene);
        }

        public void LoadMainMenuScene()
        {
            Application.LoadLevel(mainMenuScene);
        }
    }
}