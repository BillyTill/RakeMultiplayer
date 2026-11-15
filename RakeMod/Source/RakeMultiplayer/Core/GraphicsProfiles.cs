using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Graphics presets measured on the forest levels.
    ///
    /// The frame is CPU-bound on the terrain trees, so the levers that matter are the LOD
    /// bias (distant trees switch to cheap levels earlier) and the draw distance. Shadows
    /// and anti-aliasing are worth almost nothing on this engine.
    /// </summary>
    public static class GraphicsProfiles
    {
        public struct Profile
        {
            public string Key;
            public string Label;
            public float LodBias;   // < 0 = keep the level's own value
            public float FarClip;   // <= 0 = keep the level's own value
            public bool Shadows;
        }

        public static readonly Profile[] All =
        {
            new Profile { Key = "off",        Label = "OFF (VANILLA)", LodBias = -1f,  FarClip = -1f,  Shadows = true  },
            new Profile { Key = "light",      Label = "LIGHT",         LodBias = 1.0f, FarClip = -1f,  Shadows = true  },
            new Profile { Key = "balanced",   Label = "BALANCED",      LodBias = 0.7f, FarClip = 220f, Shadows = false },
            new Profile { Key = "aggressive", Label = "AGGRESSIVE",    LodBias = 0.7f, FarClip = 120f, Shadows = false },
        };

        private static bool _captured;
        private static float _originalLodBias = 1.5f;
        private static float _originalFar = 300f;
        private static LightShadows _originalShadows = LightShadows.Soft;

        /// <summary>
        /// Remembers the level's own values so the "off" preset can restore them. Only a
        /// gameplay level is captured: the main menu camera has a 1000 m clip plane, which
        /// must never become the "vanilla" value for the forest.
        /// </summary>
        public static bool Capture(string sceneName)
        {
            if (_captured) return true;
            if (string.IsNullOrEmpty(sceneName)) return false;
            if (sceneName.IndexOf("level", System.StringComparison.OrdinalIgnoreCase) < 0) return false;

            _originalLodBias = QualitySettings.lodBias;
            Camera main = Camera.main;
            if (main != null) _originalFar = main.farClipPlane;

            Light[] lights = Object.FindObjectsOfType<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null || lights[i].type != LightType.Directional) continue;
                _originalShadows = lights[i].shadows;
                break;
            }

            _captured = true;
            return true;
        }

        public static bool IsCaptured { get { return _captured; } }

        public static int Index
        {
            get
            {
                if (RakeMultiplayerPlugin.GraphicsPreset == null) return 0;
                return IndexOf(RakeMultiplayerPlugin.GraphicsPreset.Value);
            }
        }

        public static Profile Current
        {
            get { return All[Mathf.Clamp(Index, 0, All.Length - 1)]; }
        }

        public static int IndexOf(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;

            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i].Key, key, System.StringComparison.OrdinalIgnoreCase)) return i;
            }
            return 0;
        }

        /// <summary>Applies a preset and stores it in the config.</summary>
        public static void Select(int index)
        {
            index = Mathf.Clamp(index, 0, All.Length - 1);

            if (RakeMultiplayerPlugin.GraphicsPreset != null)
            {
                RakeMultiplayerPlugin.GraphicsPreset.Value = All[index].Key;
                RakeMultiplayerPlugin.Instance?.Config.Save();
            }

            ApplyAll();
        }

        public static void Cycle(int direction)
        {
            int index = Mathf.Clamp(Index + direction, 0, All.Length - 1);
            if (index == Index) return;
            Select(index);
        }

        /// <summary>Applies the current preset to the whole scene.</summary>
        public static void ApplyAll()
        {
            // Nothing to measure against until a gameplay level is up; the preset is
            // applied right after that level loads.
            if (!Capture(Application.loadedLevelName)) return;

            Profile profile = Current;
            float lodBias = profile.LodBias >= 0f ? profile.LodBias : _originalLodBias;
            float farClip = profile.FarClip > 0f ? profile.FarClip : _originalFar;
            LightShadows shadows = profile.Shadows ? _originalShadows : LightShadows.None;

            QualitySettings.lodBias = lodBias;

            Camera[] cameras = Object.FindObjectsOfType<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] == null) continue;
                cameras[i].farClipPlane = farClip;
            }

            int shadowed = 0;
            Light[] lights = Object.FindObjectsOfType<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null || lights[i].type != LightType.Directional) continue;
                lights[i].shadows = shadows;
                shadowed++;
            }

            RakeMultiplayerPlugin.Log?.LogInfo("[Graphics] preset " + profile.Label +
                                               " (lod " + lodBias.ToString("0.##") +
                                               ", far " + farClip.ToString("0.#") +
                                               ", shadows " + (shadows == LightShadows.None ? "off" : shadows.ToString().ToLower()) +
                                               ", " + shadowed + " light(s), " + cameras.Length + " camera(s))");
        }
    }
}
