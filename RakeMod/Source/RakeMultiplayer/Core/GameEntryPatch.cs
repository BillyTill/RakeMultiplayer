using System;
using System.Collections;
using UnityEngine;
using BepInEx;
using HarmonyLib;

namespace RakeMultiplayer
{
    /// <summary>
    /// Entry point that initializes multiplayer when the game starts
    /// </summary>
    [HarmonyPatch(typeof(FPSController))]
    [HarmonyPatch("Start")]
    public class GameEntryPatch
    {
        static bool initialized = false;

        static void Postfix(FPSController __instance)
        {
            if (initialized) return;
            
            if (!RakeMultiplayerPlugin.Enabled.Value) return;

            initialized = true;
            RakeMultiplayerPlugin.Log?.LogInfo("[GameEntry] FPSController started, initializing multiplayer...");

            // Add MultiplayerSceneManager to the scene
            var sceneManagerObj = new GameObject("MultiplayerSceneManager");
            UnityEngine.Object.DontDestroyOnLoad(sceneManagerObj);
            sceneManagerObj.AddComponent<MultiplayerSceneManager>();

            // MultiplayerMenu already created in Plugin.Awake (DontDestroyOnLoad)
            // Just ensure it exists and log
            var menuObj = GameObject.Find("MultiplayerMenu");
            if (menuObj != null)
            {
                RakeMultiplayerPlugin.Log?.LogInfo("[GameEntry] MultiplayerMenu found in scene");
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[GameEntry] MultiplayerMenu NOT found!");
            }

            // Get NetworkedPlayer added in Awake patch
            var netPlayer = __instance.GetComponent<NetworkedPlayer>();
            if (netPlayer != null) 
            {
                netPlayer.SetAsLocalPlayer();
            }

            // Add RakeNetworkManager if not exists
            var netManagerObj = GameObject.Find("RakeNetworkManager");
            if (netManagerObj == null)
            {
                netManagerObj = new GameObject("RakeNetworkManager");
                UnityEngine.Object.DontDestroyOnLoad(netManagerObj);
                netManagerObj.AddComponent<RakeNetworkManager>();
            }

            // Add PlayerSpawner if not exists
            var spawnerObj = GameObject.Find("PlayerSpawner");
            if (spawnerObj == null)
            {
                spawnerObj = new GameObject("PlayerSpawner");
                UnityEngine.Object.DontDestroyOnLoad(spawnerObj);
                spawnerObj.AddComponent<PlayerSpawner>();
            }

            // Add DayNightManager if not exists
            var dayNightObj = GameObject.Find("DayNightManager");
            if (dayNightObj == null)
            {
                dayNightObj = new GameObject("DayNightManager");
                UnityEngine.Object.DontDestroyOnLoad(dayNightObj);
                dayNightObj.AddComponent<DayNightManager>();
            }

            // Add SessionManager if not exists
            var sessionObj = GameObject.Find("SessionManager");
            if (sessionObj == null)
            {
                sessionObj = new GameObject("SessionManager");
                UnityEngine.Object.DontDestroyOnLoad(sessionObj);
                sessionObj.AddComponent<SessionManager>();
            }

            // Assign playerPrefab from the existing scene player (FPSController) so SpawnRemotePlayer works
            var spawner = PlayerSpawner.Instance;
            if (spawner != null && spawner.playerPrefab == null)
            {
                spawner.playerPrefab = __instance.gameObject;
                RakeMultiplayerPlugin.Log?.LogInfo("[GameEntry] Assigned playerPrefab from scene FPSController");
            }

            // Register local player in PlayerSpawner (Fix: гарантирует localPlayer != null)
            if (spawner != null && netPlayer != null)
            {
                spawner.RegisterLocalPlayer(netPlayer);  // Вызовет SetAsLocalPlayer() повторно — идемпотентно
            }

            // Register networked items (Stage 3.5)
            if (RakeNetworkManager.Instance != null)
            {
                var items = UnityEngine.Object.FindObjectsOfType<Item>();
                for (int i = 0; i < items.Length; i++)
                {
                    // Присваиваем детерминированный ID на основе индекса (1-based)
                    RakeNetworkManager.Instance.RegisterItem(items[i], i + 1);
                }
                RakeMultiplayerPlugin.Log?.LogInfo($"[GameEntry] Registered {items.Length} networked items with deterministic IDs");

                // Register networked cameras with deterministic IDs (Stage 3.4)
                RakeNetworkManager.Instance.RegisterCameras();

                // Register networked animals with deterministic IDs (Stage 3.8)
                RakeNetworkManager.Instance.RegisterAnimals();
            }

            // If host, update and broadcast host position now that local player is initialized
            if (RakeMultiplayerPlugin.isServer && RakeNetworkManager.Instance != null && netPlayer != null)
            {
                RakeNetworkManager.Instance.UpdateHostPosition(netPlayer.transform.position);
            }
        }
    }

    /// <summary>
    /// Patch to handle player death - DISABLED: Die method not found in this game version
    /// </summary>
    /*
    [HarmonyPatch(typeof(FPSController))]
    [HarmonyPatch("Die")]
    public class PlayerDeathPatch
    {
        static void Postfix(FPSController __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;

            var netPlayer = __instance.GetComponent<NetworkedPlayer>();
            if (netPlayer != null)
            {
                RakeMultiplayerPlugin.Log?.LogInfo("[GameEntry] Local player died");
            }
        }
    }
    */

    /// <summary>
    /// Patch to handle health changes - DISABLED: TakeDamage method not found
    /// </summary>
    /*
    [HarmonyPatch(typeof(FPSController))]
    [HarmonyPatch("TakeDamage")]
    public class PlayerDamagePatch
    {
        static void Postfix(FPSController __instance, float damage)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;

            var netPlayer = __instance.GetComponent<NetworkedPlayer>();
            if (netPlayer != null)
            {
            }
        }
    }
    */

    /// <summary>
    /// Patch to handle stamina changes - DISABLED: UpdateStamina method not found
    /// </summary>
    /*
    [HarmonyPatch(typeof(PlayerMovement))]
    [HarmonyPatch("UpdateStamina")]
    public class PlayerStaminaPatch
    {
        static void Postfix(PlayerMovement __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;

            var netPlayer = __instance.GetComponent<NetworkedPlayer>();
            if (netPlayer != null)
            {
            }
        }
    }
    */

    /// <summary>
    /// Patch to handle flashlight toggle - DISABLED: ToggleFlashlight method not found
    /// </summary>
    /*
    [HarmonyPatch(typeof(FPSController))]
    [HarmonyPatch("ToggleFlashlight")]
    public class FlashlightPatch
    {
        static void Postfix(FPSController __instance, bool enabled)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;

            var netPlayer = __instance.GetComponent<NetworkedPlayer>();
            if (netPlayer != null)
            {
            }
        }
    }
    */
}