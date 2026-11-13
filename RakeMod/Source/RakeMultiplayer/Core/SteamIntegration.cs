using System;
using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Minimal Steamworks bootstrap for Spacewar (app 480).
    ///
    /// The game is a non-Steam Unity build, so we initialise the Steam API ourselves.
    /// `steam_appid.txt` (480), `steam_api.dll` and `CSteamworks.dll` must sit next to
    /// Rake.exe. If Steam is not running (or the files are missing) LAN keeps working and
    /// the Steam menu reports why it is unavailable.
    /// </summary>
    public class SteamIntegration : MonoBehaviour
    {
        public static SteamIntegration Instance { get; private set; }

        public static bool Initialized { get; private set; }
        public static string Status { get; private set; } = "not started";
        public static ulong LocalSteamId { get; private set; }

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            TryInit();
        }

        private void Update()
        {
            if (!Initialized) return;

            // SteamAPI_RunCallbacks crashed inside steam_api.dll on this Unity 5 build.
            // Everything the mod needs is polled (TryGetCallResult + lobby member diffing),
            // so native callback dispatch is intentionally not used.
        }

        private void OnApplicationQuit()
        {
            if (!Initialized) return;

            try
            {
                if (SteamLobbyManager.Instance != null) SteamLobbyManager.Instance.LeaveLobby();
                SteamNative.Shutdown();
            }
            catch { }
            Initialized = false;
        }

        private void TryInit()
        {
            try
            {
                if (!SteamNative.IsSteamRunning())
                {
                    Status = "Steam is not running";
                    RakeMultiplayerPlugin.Log?.LogWarning("[Steam] " + Status);
                    return;
                }

                if (!SteamNative.Init())
                {
                    Status = "SteamAPI.Init failed (app 480 / steam_appid.txt / steam_api.dll)";
                    RakeMultiplayerPlugin.Log?.LogError("[Steam] " + Status);
                    return;
                }

                Initialized = true;
                LocalSteamId = SteamNative.GetSteamId();
                Status = "ready as " + SteamNative.PersonaName();

                RakeMultiplayerPlugin.Log?.LogInfo("[Steam] Initialised: " + Status +
                                                   " (" + LocalSteamId + ") app 480");
            }
            catch (Exception ex)
            {
                Status = "init exception: " + ex.Message;
                RakeMultiplayerPlugin.Log?.LogError("[Steam] " + Status + "\n" + ex);
            }
        }

        public static string PersonaName
        {
            get
            {
                if (!Initialized) return "Player";
                try { return SteamNative.PersonaName(); }
                catch { return "Player"; }
            }
        }
    }
}
