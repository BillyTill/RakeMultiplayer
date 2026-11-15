using System.Collections;
using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Applies the graphics preset and the terrain shadow switch to every loaded level.
    ///
    /// Both are re-applied on each level load and after a graphics preset change, because a
    /// new scene brings a new directional light and new cameras, and a quality switch
    /// rebuilds the terrain renderer.
    ///
    /// Config: <c>[Graphics] Preset</c> (off / light / balanced / aggressive) and
    /// <c>[Graphics] TerrainShadows</c> (false = no terrain shadow pass).
    /// </summary>
    public class GraphicsSettingsController : MonoBehaviour
    {
        private int _lastQuality;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            GraphicsProfiles.Capture(Application.loadedLevelName);
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
            RakeMultiplayerPlugin.Log?.LogInfo("[Graphics] graphics quality changed to " +
                                               (QualitySettings.names.Length > 0
                                                   ? QualitySettings.names[quality]
                                                   : quality.ToString()));
            GraphicsProfiles.ApplyAll();
            ApplyTerrainShadows("quality level changed");
        }

        /// <summary>
        /// The level builds its terrain right after the scene is loaded, so retry for a
        /// couple of seconds instead of assuming it is there on the first frame.
        /// </summary>
        private IEnumerator ApplyWhenReady(string reason)
        {
            GraphicsProfiles.Capture(Application.loadedLevelName);
            GraphicsProfiles.ApplyAll();

            for (int attempt = 0; attempt < 120; attempt++)
            {
                if (ApplyTerrainShadows(reason) > 0 || HasTerrain()) yield break;
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
        public static int ApplyTerrainShadows(string reason)
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
                                               " (was ON for " + wasCasting + ", changed " + changed + ", " +
                                               trees + " tree instances)" +
                                               " [" + reason + ", quality " + quality + "]");
            return changed;
        }
    }
}
