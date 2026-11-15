using System.Collections;
using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Turns off shadow casting for the level's terrain.
    ///
    /// A level can carry tens of thousands of trees, and the terrain draws them through its
    /// own renderer: the terrain is a single huge shadow caster, so the shadow pass walks
    /// every tree again. Dropping <c>Terrain.castShadows</c> removes that whole pass while
    /// leaving the visible terrain untouched.
    ///
    /// The switch lives in the BepInEx config as <c>[Graphics] TerrainShadows</c>:
    /// <c>false</c> (default) turns terrain shadows off, <c>true</c> keeps the level as is.
    /// The value is re-applied on every level load and after a graphics preset change,
    /// because a quality switch rebuilds the terrain renderer.
    /// </summary>
    public class TerrainShadowControl : MonoBehaviour
    {
        private int _lastQuality;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            _lastQuality = QualitySettings.GetQualityLevel();
            StartCoroutine(ApplyWhenReady("plugin start"));
        }

        private void OnLevelWasLoaded(int level)
        {
            StartCoroutine(ApplyWhenReady("scene " + Application.loadedLevelName));
        }

        private void Update()
        {
            int quality = QualitySettings.GetQualityLevel();
            if (quality == _lastQuality) return;

            _lastQuality = quality;
            Apply("quality level changed");
        }

        /// <summary>
        /// The level builds its terrain right after the scene is loaded, so retry for a
        /// couple of seconds instead of assuming it is there on the first frame.
        /// </summary>
        private IEnumerator ApplyWhenReady(string reason)
        {
            for (int attempt = 0; attempt < 120; attempt++)
            {
                if (Apply(reason) > 0 || HasTerrain()) yield break;
                yield return null;
            }
        }

        private static bool HasTerrain()
        {
            Terrain[] terrains = Terrain.activeTerrains;
            if (terrains != null && terrains.Length > 0) return true;
            return Terrain.activeTerrain != null;
        }

        /// <summary>Applies the configured value to every terrain in the level.</summary>
        public static int Apply(string reason)
        {
            Terrain[] terrains = Terrain.activeTerrains;
            if (terrains == null || terrains.Length == 0)
            {
                Terrain single = Terrain.activeTerrain;
                if (single == null) return 0;
                terrains = new[] { single };
            }

            bool castShadows = RakeMultiplayerPlugin.TerrainShadows != null &&
                               RakeMultiplayerPlugin.TerrainShadows.Value;

            int trees = 0;
            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrains[i] == null || terrains[i].terrainData == null) continue;
                trees += terrains[i].terrainData.treeInstances.Length;
            }

            int changed = 0;
            int wasCasting = 0;
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null) continue;
                if (terrain.castShadows) wasCasting++;
                if (terrain.castShadows == castShadows) continue;

                terrain.castShadows = castShadows;
                changed++;
            }

            string quality = QualitySettings.names.Length > 0
                ? QualitySettings.names[QualitySettings.GetQualityLevel()]
                : QualitySettings.GetQualityLevel().ToString();

            RakeMultiplayerPlugin.Log?.LogInfo("[Graphics] terrain shadows " +
                                               (castShadows ? "ON" : "OFF") +
                                               " on " + terrains.Length + " terrain(s)" +
                                               " (was ON for " + wasCasting + ", changed " + changed + ", " + trees + " tree instances)" +
                                               " [" + reason + ", quality " + quality + "]");
            return changed;
        }
    }
}
