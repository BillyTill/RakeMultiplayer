using System;
using UnityEngine;
using HarmonyLib;

namespace RakeMultiplayer
{
    /// <summary>
    /// Entry point that initializes multiplayer when a game scene's local player starts.
    ///
    /// This MUST run for every gameplay scene (not just once), because Rake reloads the
    /// level on "Skip day" / night transitions: the local player and every item/camera
    /// is recreated, so the mod has to rebind them and respawn remote players again.
    /// </summary>
    [HarmonyPatch(typeof(FPSController))]
    [HarmonyPatch("Start")]
    public class GameEntryPatch
    {
        static void Postfix(FPSController __instance)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (__instance == null) return;

            // Remote clones also carry FPSController; never run scene setup for them
            if (__instance.gameObject.name.StartsWith("RemotePlayer_")) return;

            RakeMultiplayerPlugin.Log?.LogInfo(
                $"[GameEntry] Local player started in scene '{Application.loadedLevelName}' - (re)binding multiplayer");

            AudioDiagnostics.EnsureListener();
            AudioDiagnostics.LogState();

            // Persistent managers (idempotent - found if they already survived the load)
            EnsurePersistentObject<MultiplayerSceneManager>("MultiplayerSceneManager");
            EnsurePersistentObject<RakeNetworkManager>("RakeNetworkManager");
            EnsurePersistentObject<PlayerSpawner>("PlayerSpawner");
            EnsurePersistentObject<DayNightManager>("DayNightManager");
            EnsurePersistentObject<SessionManager>("SessionManager");

            var netPlayer = __instance.GetComponent<NetworkedPlayer>();
            if (netPlayer == null)
            {
                netPlayer = __instance.gameObject.AddComponent<NetworkedPlayer>();
            }

            var spawner = PlayerSpawner.Instance;
            var manager = RakeNetworkManager.Instance;

            if (spawner != null)
            {
                // The scene player is the template for remote clones
                spawner.playerPrefab = __instance.gameObject;

                netPlayer.SetAsLocalPlayer();
                spawner.RegisterLocalPlayer(netPlayer);

                // Restore the client ID assigned earlier in the session
                if (!RakeMultiplayerPlugin.isServer && spawner.LastLocalPlayerId > 0)
                {
                    netPlayer.SetPlayerId(spawner.LastLocalPlayerId);
                }
            }

            DayNightManager.Instance?.RefreshSceneLighting();

            if (manager != null)
            {
                // Scene objects were recreated - rebuild their deterministic registries
                manager.PrepareForSceneLoad();
                RegisterSceneObjects(manager);

                // Restore persisted cameras/traps for the new scene
                if (RakeMultiplayerPlugin.isServer)
                {
                    WorldPersistence.RestoreWorld();
                }

                if (RakeMultiplayerPlugin.isServer)
                {
                    manager.UpdateHostPosition(__instance.transform.position);
                }

                // Recreate remote player proxies for the new scene
                manager.EnsureRemotePlayersSpawned();
            }

            if (netPlayer != null)
            {
                netPlayer.ForceSendState();
            }
        }

        private static void EnsurePersistentObject<T>(string name) where T : Component
        {
            if (UnityEngine.Object.FindObjectOfType<T>() != null) return;

            var obj = new GameObject(name);
            UnityEngine.Object.DontDestroyOnLoad(obj);
            obj.AddComponent<T>();
            RakeMultiplayerPlugin.Log?.LogInfo($"[GameEntry] Created persistent {name}");
        }

        private static void RegisterSceneObjects(RakeNetworkManager manager)
        {
            // Items: deterministic order (position, then name) so host and client agree
            var items = UnityEngine.Object.FindObjectsOfType<Item>();
            System.Array.Sort(items, (a, b) =>
            {
                if (a == null || b == null) return 0;
                var pa = a.transform.position;
                var pb = b.transform.position;
                int cmp = pa.x.CompareTo(pb.x); if (cmp != 0) return cmp;
                cmp = pa.y.CompareTo(pb.y); if (cmp != 0) return cmp;
                cmp = pa.z.CompareTo(pb.z); if (cmp != 0) return cmp;
                return string.CompareOrdinal(a.gameObject.name, b.gameObject.name);
            });

            for (int i = 0; i < items.Length; i++)
            {
                manager.RegisterItem(items[i], i + 1);
            }
            RakeMultiplayerPlugin.Log?.LogInfo($"[GameEntry] Registered {items.Length} networked items");

            // The level ships the Rake itself (no prefab is spawned because
            // PlayerSpawner.rakePrefab is never assigned), so make sure the networked
            // AI exists on every peer. Without it each client runs the vanilla AI
            // locally and every player sees the Rake in a different place.
            var enemies = UnityEngine.Object.FindObjectsOfType<Enemy_AI>();
            int rakeCount = 0;
            for (int i = 0; i < enemies.Length; i++)
            {
                var enemy = enemies[i];
                if (enemy == null) continue;

                if (enemy.GetComponent<NetworkedRakeAI>() == null)
                {
                    enemy.gameObject.AddComponent<NetworkedRakeAI>();
                }
                rakeCount++;
            }
            RakeMultiplayerPlugin.Log?.LogInfo($"[GameEntry] Networked Rake instances: {rakeCount}");

            DoorSync.RegisterAll();

            manager.RegisterCameras();
            manager.RegisterAnimals();
        }
    }
}
