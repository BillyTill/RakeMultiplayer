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
            static void Prefix(string name)
            {
                if (!RakeMultiplayerPlugin.Enabled.Value) return;
                RakeMultiplayerPlugin.Log?.LogInfo($"[GameEntryPatch] Scene load requested: {name}");
                _isSceneLoading = true;
                OnSceneChangeStart(name);
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
            static void Prefix(int index)
            {
                if (!RakeMultiplayerPlugin.Enabled.Value) return;
                RakeMultiplayerPlugin.Log?.LogInfo($"[GameEntryPatch] Scene load requested (index): {index}");
                _isSceneLoading = true;
                OnSceneChangeStart(index.ToString());
            }
        }

        /// <summary>
        /// Called when scene change starts - cleanup networking state BEFORE new scene loads
        /// </summary>
        private static void OnSceneChangeStart(string sceneName)
        {
            // Snapshot world state before the scene unloads (cameras/traps/Rake HP)
            if (RakeMultiplayerPlugin.isServer && RakeMultiplayerPlugin.isConnected)
            {
                WorldPersistence.SaveWorld();
            }
            // Clear all networked objects but keep the network connection
            // NetIdentityManager removed - NetIdentity.Registry handles cleanup
            NetIdentity.Registry.Clear();
            NetIdentity.NextNetId = 1;

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