using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer.UI
{
    /// <summary>
    /// Minimap with live markers (Task 3.12/3.13).
    /// Marker colors: cameras = green, traps = red, bait = yellow, safehouse = blue, cave = red.
    /// Toggle with the configured key (default: M).
    /// </summary>
    public class MapUI : MonoBehaviour
    {
        public static MapUI Instance { get; private set; }

        [Header("Map")]
        public KeyCode toggleKey = KeyCode.F3;
        public float mapSize = 220f;

        [Tooltip("World-space X/Z bounds shown on the map")]
        public Vector2 worldMin = new Vector2(-500f, -500f);
        public Vector2 worldMax = new Vector2(500f, 500f);

        private GameObject _panel;
        private RectTransform _mapRect;
        private RectTransform _markerRoot;
        private readonly Dictionary<uint, Image> _markerImages = new Dictionary<uint, Image>();
        private readonly Dictionary<uint, byte> _markerTypes = new Dictionary<uint, byte>();
        private bool _visible = false;
        private bool _wasGameplay = false;
        private float _refreshTimer;
        private const float RefreshInterval = 0.1f; // 10Hz is enough for a minimap

        private static readonly Color CameraColor = new Color(0.2f, 1f, 0.3f, 0.95f);   // green
        private static readonly Color TrapColor = new Color(1f, 0.2f, 0.2f, 0.95f);     // red
        private static readonly Color BaitColor = new Color(1f, 0.9f, 0.2f, 0.95f);     // yellow
        private static readonly Color SafehouseColor = new Color(0.3f, 0.6f, 1f, 0.95f); // blue
        private static readonly Color CaveColor = new Color(0.8f, 0.1f, 0.1f, 0.95f);   // red

        /// <summary>
        /// Build the minimap inside an existing canvas.
        /// </summary>
        public static MapUI Create(Canvas canvas)
        {
            if (Instance != null) return Instance;

            var go = new GameObject("MapUI");
            go.transform.SetParent(canvas != null ? canvas.transform : null, false);

            // Root must be a full-screen RectTransform so child anchors resolve correctly
            var rootRect = go.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.pivot = new Vector2(0.5f, 0.5f);

            var map = go.AddComponent<MapUI>();
            map.Build(canvas);
            Instance = map;
            return map;
        }

        private void Build(Canvas canvas)
        {
            _panel = new GameObject("MapPanel");
            _panel.transform.SetParent(transform, false);

            _mapRect = _panel.AddComponent<RectTransform>();
            _mapRect.sizeDelta = new Vector2(mapSize, mapSize);
            _mapRect.anchorMin = new Vector2(1f, 0f);
            _mapRect.anchorMax = new Vector2(1f, 0f);
            _mapRect.pivot = new Vector2(1f, 0f);
            _mapRect.anchoredPosition = new Vector2(-20f, 20f);

            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);

            var markerRootGo = new GameObject("Markers");
            markerRootGo.transform.SetParent(_panel.transform, false);
            _markerRoot = markerRootGo.AddComponent<RectTransform>();
            _markerRoot.sizeDelta = new Vector2(mapSize, mapSize);
            _markerRoot.anchorMin = Vector2.zero;
            _markerRoot.anchorMax = Vector2.one;
            _markerRoot.offsetMin = Vector2.zero;
            _markerRoot.offsetMax = Vector2.zero;

            RakeMultiplayerPlugin.Log?.LogInfo("[MapUI] Minimap created (hidden until gameplay)");
            SetVisible(false);
        }

        /// <summary>True when a connected session is running in a gameplay level (level0..level5).</summary>
        private static bool IsGameplayScene()
        {
            if (!RakeMultiplayerPlugin.isConnected) return false;
            string scene = UnityEngine.Application.loadedLevelName;
            return !string.IsNullOrEmpty(scene) && scene.StartsWith("level");
        }

        private void Update()
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;

            // Show only while playing; hide in menus/lobby
            bool gameplay = IsGameplayScene();
            if (gameplay != _wasGameplay)
            {
                _wasGameplay = gameplay;
                SetVisible(gameplay);
            }

            if (gameplay && !DebugConsole.ConsoleOpen && !MultiplayerMenu.IsOpen &&
                Input.GetKeyDown(toggleKey))
            {
                SetVisible(!_visible);
            }

            _refreshTimer += Time.deltaTime;
            if (_visible && _refreshTimer >= RefreshInterval)
            {
                _refreshTimer = 0f;
                RefreshMarkers();
            }
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_panel != null) _panel.SetActive(visible);
        }

        private void RefreshMarkers()
        {
            var manager = RakeNetworkManager.Instance;
            if (manager == null || _markerRoot == null) return;

            var markers = manager.GetMapMarkers();
            var traps = manager.GetTrapStates();

            var live = new HashSet<uint>();

            // Map markers (cameras, traps, bait, safehouse, cave)
            foreach (var kvp in markers)
            {
                var marker = kvp.Value;
                if (marker == null) continue;

                uint key = MakeKey(marker.MarkerType, marker.MarkerId);
                live.Add(key);

                if (!marker.IsActive)
                {
                    RemoveMarker(key);
                    continue;
                }

                var image = GetOrCreateMarker(key, marker.MarkerType);
                PositionMarker(image, marker.Position);
                image.color = GetColor(marker.MarkerType, false);
            }

            // Trap states (authoritative armed/triggered flags)
            foreach (var kvp in traps)
            {
                var trap = kvp.Value;
                if (trap == null) continue;

                uint key = MakeKey(1, trap.TrapId);
                live.Add(key);

                if (trap.IsTriggered)
                {
                    RemoveMarker(key);
                    continue;
                }

                var image = GetOrCreateMarker(key, 1);
                PositionMarker(image, trap.Position);
                image.color = TrapColor;
            }

            // Cleanup markers that disappeared
            var stale = new List<uint>();
            foreach (var key in _markerImages.Keys)
            {
                if (!live.Contains(key)) stale.Add(key);
            }
            foreach (var key in stale) RemoveMarker(key);
        }

        private static uint MakeKey(byte markerType, uint markerId)
        {
            // Top 4 bits = type, low 28 bits = id (avoids collisions between systems)
            return ((uint)markerType << 28) | (markerId & 0x0FFFFFFFu);
        }

        private Image GetOrCreateMarker(uint key, byte markerType)
        {
            if (_markerImages.TryGetValue(key, out var existing) && existing != null)
            {
                return existing;
            }

            var go = new GameObject($"Marker_{markerType}_{key}");
            go.transform.SetParent(_markerRoot, false);

            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(8f, 8f);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var image = go.AddComponent<Image>();
            image.color = GetColor(markerType, false);

            _markerImages[key] = image;
            _markerTypes[key] = markerType;
            return image;
        }

        private void PositionMarker(Image image, Vector3 worldPosition)
        {
            if (image == null) return;

            float x = Mathf.InverseLerp(worldMin.x, worldMax.x, worldPosition.x);
            float z = Mathf.InverseLerp(worldMin.y, worldMax.y, worldPosition.z);

            var rect = image.rectTransform;
            rect.anchoredPosition = new Vector2(
                Mathf.Clamp01(x) * mapSize,
                Mathf.Clamp01(z) * mapSize);
        }

        private void RemoveMarker(uint key)
        {
            if (_markerImages.TryGetValue(key, out var image))
            {
                if (image != null) Destroy(image.gameObject);
                _markerImages.Remove(key);
            }
            _markerTypes.Remove(key);
        }

        private static Color GetColor(byte markerType, bool broken)
        {
            if (broken)
            {
                return new Color(0.5f, 0.5f, 0.5f, 0.6f); // broken camera -> dim grey
            }

            switch (markerType)
            {
                case 0: return CameraColor;     // camera
                case 1: return TrapColor;       // trap
                case 2: return BaitColor;       // bait
                case 3: return SafehouseColor;  // safehouse
                case 4: return CaveColor;       // cave
                default: return Color.white;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
