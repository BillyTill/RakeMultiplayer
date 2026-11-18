using System;
using UnityEngine;
using HarmonyLib;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer.Patches
{
    /// <summary>
    /// Harmony patches for scene loading - only cleans up networking state BEFORE scene load
    /// Post-load initialization is handled by MultiplayerSceneManager (via coroutine)
    /// </summary>
    public class GameEntryPatch
    {
        private static bool _isSceneLoading = false;

        /// <summary>
        /// Patch Application.LoadLevel (Unity 5.x legacy) - string overload, parameter name is "name"
        /// </summary>
        [HarmonyPatch(typeof(Application), "LoadLevel", new Type[] { typeof(string) })]
        public class ApplicationLoadLevelPatch
        {
            static bool Prefix(string name)
            {
                if (!RakeMultiplayerPlugin.Enabled.Value) return true;
                if (BlockClientLevelLoad(name)) return false;

                RakeMultiplayerPlugin.Log?.LogInfo($"[GameEntryPatch] Scene load requested: {name}");
                _isSceneLoading = true;
                OnSceneChangeStart(name);
                return true;
            }

            static void Postfix(string name)
            {
                if (_loadBlocked)
                {
                    _loadBlocked = false;
                    return;
                }

                // If this was a local (non-network) load, tell the other players.
                // Loading by name (level transitions do that) used to be swallowed here
                // because only numeric names were broadcast, so peers stayed on the old level.
                int index;
                if (int.TryParse(name, out index))
                {
                    RakeNetworkManager.Instance?.NotifyLocalLevelLoad(index);
                    return;
                }

                RakeNetworkManager.Instance?.NotifyLocalLevelLoad(UnityEngine.Application.loadedLevel);
            }
        }

        /// <summary>
        /// Patch Application.LoadLevelAsync (Unity 5.x legacy) - parameter name is "levelName"
        /// </summary>
        [HarmonyPatch(typeof(Application), "LoadLevelAsync", new Type[] { typeof(string) })]
        public class ApplicationLoadLevelAsyncPatch
        {
            static void Prefix(string levelName)
            {
                if (!RakeMultiplayerPlugin.Enabled.Value) return;
                RakeMultiplayerPlugin.Log?.LogInfo($"[GameEntryPatch] Async scene load requested: {levelName}");
                _isSceneLoading = true;
                OnSceneChangeStart(levelName);
            }
        }

        /// <summary>
        /// Patch Application.LoadLevel (int overload - Unity 5.x) - parameter name is "index"
        /// </summary>
        [HarmonyPatch(typeof(Application), "LoadLevel", new Type[] { typeof(int) })]
        public class ApplicationLoadLevelIntPatch
        {
            static bool Prefix(int index)
            {
                if (!RakeMultiplayerPlugin.Enabled.Value) return true;
                if (BlockClientLevelLoad(index.ToString())) return false;

                RakeMultiplayerPlugin.Log?.LogInfo($"[GameEntryPatch] Scene load requested (index): {index}");
                _isSceneLoading = true;
                OnSceneChangeStart(index.ToString());
                return true;
            }

            static void Postfix(int index)
            {
                if (_loadBlocked)
                {
                    _loadBlocked = false;
                    return;
                }

                RakeNetworkManager.Instance?.NotifyLocalLevelLoad(index);
            }
        }

        /// <summary>
        /// Called when scene change starts - cleanup networking state BEFORE new scene loads
        /// </summary>
        /// <summary>
        /// In a session only the host decides which level is loaded. A client that loads a
        /// level locally - the vanilla GameTimer, a stray menu path, anything - ends up in a
        /// different level than everybody else, which reads as "the day was skipped only for
        /// me". The main menu is always allowed, so leaving still works.
        /// </summary>
        // Harmony runs the postfix even when a prefix returns false, so a refused load would
        // still be reported to the host as "this client changed the level". That is exactly
        // the "ignored LevelChange from client" line that looked like an old build.
        private static bool _loadBlocked;

        private static bool BlockClientLevelLoad(string what)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return false;
            if (!RakeMultiplayerPlugin.isConnected) return false;
            if (RakeMultiplayerPlugin.isServer) return false;
            if (RakeNetworkManager.IsApplyingNetworkLevelChange) return false;
            if (what == "0" || what.StartsWith("MainMenu", StringComparison.OrdinalIgnoreCase) ||
                what.StartsWith("Menu", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string caller = "?";
            try
            {
                var frames = new System.Diagnostics.StackTrace(1, false);
                if (frames.FrameCount > 0 && frames.GetFrame(0).GetMethod() != null)
                {
                    caller = frames.GetFrame(0).GetMethod().DeclaringType.Name + "." +
                             frames.GetFrame(0).GetMethod().Name;
                }
            }
            catch { }

            RakeMultiplayerPlugin.Log?.LogWarning("[Level] client refused a local load of '" + what +
                                                  "' (asked by " + caller + "); the host owns level changes");
            _loadBlocked = true;
            return true;
        }

        private static void OnSceneChangeStart(string sceneName)
        {
            // A load can start while someone is paused (Time.timeScale = 0). The new
            // level then came up alive but frozen, and only a second ESC press fixed it.
            Time.timeScale = 1f;

            // The free camera is HideAndDontSave: it would survive and render the new
            // level on top of the player camera with doubled WASD/mouse.
            if (DebugFreeCamera.IsActive) DebugFreeCamera.SetEnabled(false);

            // Snapshot world state before the scene unloads (cameras/traps/Rake HP)
            if (RakeMultiplayerPlugin.isServer && RakeMultiplayerPlugin.isConnected)
            {
                WorldPersistence.SaveWorld();
            }

            // Both sides: which items are already taken. The new level rebuilds its items.
            WorldPersistence.SaveConsumedItems();
            // Every process-wide subsystem that belongs to the old level. Input locks,
            // network ids, cameras, traps and deaths used to be reset by hand in different
            // patches; one missed line left state in the next level. The snapshot above must
            // be taken first, because SceneScope clears the registries it reads.
            SceneScope.ResetAll("load " + sceneName);

            RakeMultiplayerPlugin.Log?.LogInfo($"[GameEntryPatch] Networking state cleared for scene transition");
        }

        /// <summary>
        /// Called by MultiplayerSceneManager AFTER scene is fully loaded
        /// </summary>
        public static void OnSceneLoadedPost()
        {
            if (!_isSceneLoading) return;

            _isSceneLoading = false;
            
            try
            {
                RakeMultiplayerPlugin.Log?.LogInfo("[GameEntryPatch] Scene loaded - reinitializing networking");

                // Re-register network event handlers
                if (RakeNetworkManager.Instance != null)
                {
                    RakeNetworkManager.Instance.ReregisterHandlers();
                }

                // Refresh PlayerSpawner to discover new scene prefabs/spawn points
                if (PlayerSpawner.Instance != null)
                {
                    PlayerSpawner.Instance.Refresh();
                }

                // If we're server, respawn persistent objects (Rake, items)
                if (RakeMultiplayerPlugin.isServer)
                {
                    RakeMultiplayerPlugin.Log?.LogInfo("[GameEntryPatch] Server: respawning persistent objects");
                    
                    // Small delay to let scene fully initialize
                    CoroutineHelper.DelayedCall(0.5f, () =>
                    {
                        if (PlayerSpawner.Instance != null)
                        {
                            PlayerSpawner.Instance.SpawnRake();
                            PlayerSpawner.Instance.SpawnItems();
                            
                            // Broadcast host position to clients
                            if (RakeNetworkManager.Instance != null && PlayerSpawner.Instance.LocalPlayer != null)
                            {
                                Vector3 hostPos = PlayerSpawner.Instance.LocalPlayer.transform.position;
                                RakeNetworkManager.Instance.UpdateHostPosition(hostPos);
                                RakeMultiplayerPlugin.Log?.LogInfo($"[GameEntryPatch] Broadcast host position: {hostPos}");
                            }
                        }
                    });
                }

                RakeMultiplayerPlugin.Log?.LogInfo("[GameEntryPatch] Networking reinitialized after scene load");
            }
            catch (Exception ex)
            {
                RakeMultiplayerPlugin.Log?.LogError($"[GameEntryPatch] Error reinitializing networking: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if currently in a scene transition
        /// </summary>
        public static bool IsSceneLoading => _isSceneLoading;
    }

    /// <summary>
    /// Helper for delayed coroutine calls without MonoBehaviour
    /// </summary>
    public static class CoroutineHelper
    {
        private static GameObject _helperObject;
        private static CoroutineRunner _runner;

        public static void DelayedCall(float delay, Action callback)
        {
            if (_helperObject == null)
            {
                _helperObject = new GameObject("RakeMultiplayer_CoroutineHelper");
                UnityEngine.Object.DontDestroyOnLoad(_helperObject);
                _runner = _helperObject.AddComponent<CoroutineRunner>();
            }
            _runner.StartDelayedCall(delay, callback);
        }

        private class CoroutineRunner : MonoBehaviour
        {
            public void StartDelayedCall(float delay, Action callback)
            {
                StartCoroutine(DelayedCallRoutine(delay, callback));
            }

            private System.Collections.IEnumerator DelayedCallRoutine(float delay, Action callback)
            {
                yield return new WaitForSeconds(delay);
                try { callback?.Invoke(); }
                catch (Exception ex) { RakeMultiplayerPlugin.Log?.LogError($"[CoroutineHelper] Error: {ex.Message}"); }
            }
        }
    }
}