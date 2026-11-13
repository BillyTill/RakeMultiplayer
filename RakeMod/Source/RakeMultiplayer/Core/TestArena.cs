using System.Collections.Generic;
using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Floating damage-test arena, built from primitives at runtime and reachable with
    /// the "test arena" console command after a match has started. The host builds it,
    /// broadcasts the centre to every peer, gives everyone weapons/ammo on the platform
    /// and parks the Rake at one end with its AI disabled.
    /// </summary>
    public static class TestArena
    {
        private static GameObject _root;
        private static Material _floor;
        private static Material _wall;
        private static Material _accent;

        public static Vector3 Center { get; private set; }

        public static void CreateOnHost()
        {
            if (RakeMultiplayerPlugin.isConnected && !RakeMultiplayerPlugin.isServer)
            {
                DebugConsole.Print("test arena: only the host can build it");
                return;
            }

            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;
            if (local == null)
            {
                DebugConsole.Print("test arena: no local player");
                return;
            }

            Vector3 center = local.transform.position + Vector3.up * 120f + local.transform.forward * 30f;

            // AFK Rake: stop any player control and the AI, restore full health.
            LobbyState.RakeOwnerId = 0;
            RakeControlSync.Apply("test arena");

            var rake = RakeHighlight.FindRake();
            if (rake != null)
            {
                var ai = rake.GetComponent<NetworkedRakeAI>();
                if (ai != null)
                {
                    ai.SetPlayerControlled(true);
                    ai.ApplyHealth(ai.MaxHealth);
                }

                SessionManager.Instance?.UpdateRakeHealth(ai != null ? ai.MaxHealth : 2500f);
                foreach (var arenaAgent in rake.GetComponentsInChildren<NavMeshAgent>(true)) arenaAgent.enabled = false;
                rake.position = center + new Vector3(0f, 0.7f, 10f);
                rake.rotation = Quaternion.Euler(0f, 180f, 0f);
                RakeMultiplayerPlugin.Log?.LogInfo("[Arena] rake now at " + rake.position.ToString("F1"));
            }

            Build(center);
            SpawnItems(center);

            var manager = RakeNetworkManager.Instance;
            if (manager != null) manager.BroadcastTestArena(center);

            TeleportPlayers(center);

            DebugConsole.Print("test arena: built at " + center.ToString("F0") +
                               " (host the match and use the console on each peer)");
            RakeMultiplayerPlugin.Log?.LogInfo("[Arena] built at " + center);
        }

        private static void TeleportPlayers(Vector3 center)
        {
            var manager = RakeNetworkManager.Instance;
            int count = 1;

            if (manager != null && RakeMultiplayerPlugin.isServer)
            {
                count = manager.GetConnectedClientCount() + 1;
            }

            Vector3[] offsets =
            {
                new Vector3(-6f, 0.7f, -4f),
                new Vector3(6f, 0.7f, -4f),
                new Vector3(-6f, 0.7f, 4f),
                new Vector3(6f, 0.7f, 4f)
            };

            if (manager == null || !RakeMultiplayerPlugin.isServer)
            {
                var singleSpawner = PlayerSpawner.Instance;
                if (singleSpawner != null && singleSpawner.LocalPlayer != null)
                {
                    singleSpawner.LocalPlayer.transform.position = center + offsets[0];
                    singleSpawner.LocalPlayer.transform.rotation =
                        Quaternion.LookRotation(center + new Vector3(0f, 1.2f, 10f) - (center + offsets[0]));
                }
                return;
            }

            for (int id = 1; id <= count; id++)
            {
                Vector3 pos = center + offsets[(id - 1) % offsets.Length];
                Quaternion rot = Quaternion.LookRotation(center + new Vector3(0f, 1.2f, 10f) - pos);

                if (manager != null) manager.ServerTeleportPlayer(id, pos, rot);
            }

            if (count == 1 && manager == null)
            {
                var spawner = PlayerSpawner.Instance;
                if (spawner != null && spawner.LocalPlayer != null)
                {
                    spawner.LocalPlayer.transform.position = center + offsets[0];
                }
            }
        }

        public static void Build(Vector3 center)
        {
            Center = center;

            if (_root != null) Object.Destroy(_root);
            _root = new GameObject("RakeTestArena");

            EnsureMaterials();

            // platform
            MakeBox("Floor", center, new Vector3(40f, 1f, 40f), _floor);
            // walls
            MakeBox("WallN", center + new Vector3(0f, 3f, 20f), new Vector3(40f, 5f, 1f), _wall);
            MakeBox("WallS", center + new Vector3(0f, 3f, -20f), new Vector3(40f, 5f, 1f), _wall);
            MakeBox("WallE", center + new Vector3(20f, 3f, 0f), new Vector3(1f, 5f, 40f), _wall);
            MakeBox("WallW", center + new Vector3(-20f, 3f, 0f), new Vector3(1f, 5f, 40f), _wall);

            // red centre stripe / spawn line
            MakeBox("Stripe", center + new Vector3(0f, 0.52f, -8f), new Vector3(16f, 0.02f, 0.25f), _accent);
            MakeBox("Stripe2", center + new Vector3(0f, 0.52f, 8f), new Vector3(16f, 0.02f, 0.25f), _accent);

            // lighting
            MakeLight(center + new Vector3(-10f, 8f, -8f));
            MakeLight(center + new Vector3(10f, 8f, 8f));

            RakeMultiplayerPlugin.Log?.LogInfo("[Arena] platform built at " + center.ToString("F0"));
        }

        private static void EnsureMaterials()
        {
            if (_floor != null) return;

            Shader standard = Shader.Find("Standard");
            if (standard == null) standard = Shader.Find("Diffuse");

            _floor = new Material(standard) { color = new Color(0.12f, 0.12f, 0.14f, 1f) };
            _wall = new Material(standard) { color = new Color(0.08f, 0.08f, 0.09f, 1f) };
            _accent = new Material(standard) { color = new Color(0.55f, 0.10f, 0.08f, 1f) };
        }

        private static void MakeBox(string name, Vector3 position, Vector3 scale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(_root.transform, false);
            box.transform.position = position;
            box.transform.localScale = scale;

            var renderer = box.GetComponent<Renderer>();
            if (renderer != null) renderer.material = material;
        }

        private static void MakeLight(Vector3 position)
        {
            var go = new GameObject("ArenaLight");
            go.transform.SetParent(_root.transform, false);
            go.transform.position = position;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.92f, 0.8f, 1f);
            light.intensity = 1.8f;
            light.range = 34f;
            light.shadows = LightShadows.None;
        }

        public static void SpawnItems(Vector3 center)
        {
            // Local clones from the level's own item prefabs: each peer can pick its own up.
            SpawnFromScene<Rifle_item>(center + new Vector3(-9f, 1.0f, -8f));
            SpawnFromScene<Rifle_item>(center + new Vector3(9f, 1.0f, -8f));
            SpawnFromScene<SniperRifle_item>(center + new Vector3(-3f, 1.0f, -8f));
            SpawnFromScene<SniperRifle_item>(center + new Vector3(3f, 1.0f, -8f));

            SpawnFromScene<RifleAmmo_item>(center + new Vector3(-12f, 1.0f, -6f));
            SpawnFromScene<RifleAmmo_item>(center + new Vector3(-12f, 1.0f, -3f));
            SpawnFromScene<SniperAmmo_item>(center + new Vector3(12f, 1.0f, -6f));
            SpawnFromScene<SniperAmmo_item>(center + new Vector3(12f, 1.0f, -3f));

            SpawnFromScene<Medkit_item>(center + new Vector3(0f, 1.0f, -12f));
            SpawnFromScene<Medkit_item>(center + new Vector3(0f, 1.0f, 12f));
        }

        private static void SpawnFromScene<T>(Vector3 position) where T : Component
        {
            T source = Object.FindObjectOfType<T>();
            if (source == null) return;

            var clone = Object.Instantiate(source.gameObject, position, Quaternion.identity) as GameObject;
            if (clone == null) return;

            clone.name = "Arena_" + typeof(T).Name;

            // Arena copies are local-only; remove network bookkeeping so they behave
            // exactly like single-player pickups.
            var identity = clone.GetComponent<NetIdentity>();
            if (identity != null) Object.Destroy(identity);
            var networked = clone.GetComponent<NetworkedItem>();
            if (networked != null) Object.Destroy(networked);
        }
    }
}
