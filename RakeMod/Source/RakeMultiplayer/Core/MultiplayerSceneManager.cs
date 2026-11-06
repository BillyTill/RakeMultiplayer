using System.Collections;
using UnityEngine;
using RakeMultiplayer.UI;

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

        private bool isInitialized = false;
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

            if (sceneName == gameScene)
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

        private void OnGameSceneLoaded()
        {
            if (playerSpawner != null && RakeMultiplayerPlugin.isServer)
            {
                playerSpawner.SpawnRake();
                playerSpawner.SpawnItems();
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
            Application.LoadLevel(gameScene);
        }

        public void LoadMainMenuScene()
        {
            Application.LoadLevel(mainMenuScene);
        }
    }
}