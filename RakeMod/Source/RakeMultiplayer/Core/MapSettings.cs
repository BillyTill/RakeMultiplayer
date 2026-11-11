using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RakeMultiplayer
{
    /// <summary>
    /// Per-scene configuration discovered at runtime (Task 4.1).
    /// Stored as a ScriptableObject so it can also be authored in the editor later;
    /// at runtime <see cref="FindOrCreate"/> builds it from the loaded scene.
    /// </summary>
    public class MapSettings : ScriptableObject
    {
        [Header("Scene")]
        public string sceneName;

        [Header("Spawn Points")]
        public Vector3[] spawnPoints;

        [Header("World POIs")]
        public Vector3[] cameraPositions;
        public Vector3[] trapPositions;
        public bool hasCave;
        public Vector3 cavePosition;
        public bool hasSafehouse;
        public Vector3 safehousePosition;

        private static MapSettings _current;
        public static MapSettings Current => _current;

        /// <summary>
        /// Discover scene POIs and build/refresh the settings object.
        /// Safe to call on every game-scene load.
        /// </summary>
        public static MapSettings FindOrCreate(string sceneName)
        {
            if (_current == null)
            {
                _current = CreateInstance<MapSettings>();
                _current.name = "RuntimeMapSettings";
            }

            _current.sceneName = sceneName;
            _current.Discover();
            return _current;
        }

        public void Discover()
        {
            // Player spawn points: prefer the configured spawner, otherwise look for named objects
            var spawner = PlayerSpawner.Instance;
            if (spawner != null && spawner.playerSpawnPoints != null && spawner.playerSpawnPoints.Length > 0)
            {
                var pts = new List<Vector3>();
                foreach (var t in spawner.playerSpawnPoints)
                {
                    if (t != null) pts.Add(t.position);
                }
                spawnPoints = pts.ToArray();
            }
            else
            {
                spawnPoints = FindPositionsByKeywords(new[] { "spawn", "playerstart", "startpoint" });
            }

            // Cameras (RecordCamera_obj scene objects)
            var recordCameras = Object.FindObjectsOfType<RecordCamera_obj>();
            if (recordCameras != null && recordCameras.Length > 0)
            {
                var cams = new List<Vector3>();
                foreach (var cam in recordCameras)
                {
                    if (cam != null) cams.Add(cam.transform.position);
                }
                cameraPositions = cams.ToArray();
            }
            else
            {
                cameraPositions = new Vector3[0];
            }

            // Traps already placed in the scene
            var levelItems = LevelItemsManager.instance;
            if (levelItems != null && levelItems.trapObjects != null)
            {
                var traps = new List<Vector3>();
                foreach (var trap in levelItems.trapObjects)
                {
                    if (trap != null) traps.Add(trap.transform.position);
                }
                trapPositions = traps.ToArray();
            }
            else
            {
                trapPositions = new Vector3[0];
            }

            // Cave (Rake spawn)
            hasCave = TryFindKeywordPosition(new[] { "cave", "пещер", "rake_spawn", "rakespawn" }, out var cave);
            cavePosition = hasCave ? cave : Vector3.zero;

            // Safehouse (day respawn)
            hasSafehouse = TryFindKeywordPosition(new[] { "safe", "house", "shed", "cabin", "дом", "убежищ" }, out var safe);
            safehousePosition = hasSafehouse ? safe : Vector3.zero;

            RakeMultiplayerPlugin.Log?.LogInfo(
                $"[4.1] MapSettings for '{sceneName}': spawns={spawnPoints.Length}, cameras={cameraPositions.Length}, " +
                $"traps={trapPositions.Length}, cave={hasCave}, safehouse={hasSafehouse}");
        }

        private static Vector3[] FindPositionsByKeywords(string[] keywords)
        {
            var result = new List<Vector3>();
            var transforms = Object.FindObjectsOfType<Transform>();
            foreach (var t in transforms)
            {
                if (t == null) continue;
                var n = t.gameObject.name.ToLowerInvariant();
                foreach (var kw in keywords)
                {
                    if (n.Contains(kw))
                    {
                        result.Add(t.position);
                        break;
                    }
                }
            }
            return result.ToArray();
        }

        private static bool TryFindKeywordPosition(string[] keywords, out Vector3 position)
        {
            var transforms = Object.FindObjectsOfType<Transform>();
            foreach (var t in transforms)
            {
                if (t == null) continue;
                var n = t.gameObject.name.ToLowerInvariant();
                foreach (var kw in keywords)
                {
                    if (n.Contains(kw))
                    {
                        position = t.position;
                        return true;
                    }
                }
            }
            position = Vector3.zero;
            return false;
        }

        /// <summary>Closest configured spawn point to a world position (or fallback).</summary>
        public Vector3 GetClosestSpawn(Vector3 position)
        {
            if (spawnPoints == null || spawnPoints.Length == 0) return position;

            Vector3 best = spawnPoints[0];
            float bestDist = (best - position).sqrMagnitude;
            for (int i = 1; i < spawnPoints.Length; i++)
            {
                float d = (spawnPoints[i] - position).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = spawnPoints[i];
                }
            }
            return best;
        }
    }
}
