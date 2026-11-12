using System.Collections.Generic;
using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Wallhack-style ESP for the Rake: a screen-space box with a distance label drawn
    /// over everything (GUI ignores depth), instead of a light stuck above its head.
    /// </summary>
    internal static class RakeHighlight
    {
        private static RakeEspOverlay _overlay;

        /// <summary>True while the ESP overlay exists.</summary>
        public static bool IsActive { get { return _overlay != null; } }

        /// <summary>Find the Rake in the current scene.</summary>
        public static Transform FindRake()
        {
            var network = UnityEngine.Object.FindObjectOfType<NetworkedRakeAI>();
            if (network != null) return network.transform;

            var enemy = UnityEngine.Object.FindObjectOfType<Enemy_AI>();
            if (enemy != null) return enemy.transform;

            return null;
        }

        public static void Toggle()
        {
            SetEnabled(!IsActive);
        }

        public static void SetEnabled(bool enable)
        {
            if (!enable)
            {
                Clear();
                return;
            }

            if (IsActive) return;

            Transform rake = FindRake();
            if (rake == null)
            {
                DebugConsole.Print("rake esp: Rake not found in this scene");
                return;
            }

            var go = new GameObject("RakeEspOverlay");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _overlay = go.AddComponent<RakeEspOverlay>();
            _overlay.Rake = rake;

            DebugConsole.Print("rake esp ON - box + distance through walls. 'rake highlight off' to stop.");
        }

        public static void Clear()
        {
            if (_overlay != null) UnityEngine.Object.Destroy(_overlay.gameObject);
            _overlay = null;
        }
    }

    /// <summary>
    /// Draws the ESP. Screen-space on purpose: a box around the projected bounds of the
    /// Rake is visible through geometry and does not need a special shader or a clone.
    /// </summary>
    internal class RakeEspOverlay : MonoBehaviour
    {
        public Transform Rake;

        private static Texture2D _pixel;
        private static GUIStyle _label;

        private readonly List<Renderer> _renderers = new List<Renderer>();
        private float _rescan;

        private static Texture2D Pixel
        {
            get
            {
                if (_pixel == null)
                {
                    _pixel = new Texture2D(1, 1);
                    _pixel.SetPixel(0, 0, Color.white);
                    _pixel.Apply();
                    _pixel.hideFlags = HideFlags.HideAndDontSave;
                }
                return _pixel;
            }
        }

        private void Update()
        {
            _rescan -= Time.deltaTime;
            if (Rake == null) Rake = RakeHighlight.FindRake();
            if (Rake == null || _rescan > 0f) return;

            _rescan = 2f;
            _renderers.Clear();
            var all = Rake.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null) _renderers.Add(all[i]);
            }
        }

        private void OnGUI()
        {
            if (Rake == null) return;

            var cam = Camera.main;
            if (cam == null) return;

            Bounds bounds;
            if (!TryGetBounds(out bounds)) return;

            Vector2 min, max;
            if (!Project(cam, bounds, out min, out max)) return;

            float x0 = Mathf.Clamp(min.x, 0f, Screen.width);
            float y0 = Mathf.Clamp(min.y, 0f, Screen.height);
            float x1 = Mathf.Clamp(max.x, 0f, Screen.width);
            float y1 = Mathf.Clamp(max.y, 0f, Screen.height);
            if (x1 - x0 < 4f || y1 - y0 < 4f) return;

            var rect = new Rect(x0, y0, x1 - x0, y1 - y0);
            Color color = new Color(1f, 0.32f, 0.08f, 0.95f);

            GUI.depth = -1000;
            DrawBox(rect, 2f, color);

            float distance = Vector3.Distance(cam.transform.position, bounds.center);
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label);
                _label.fontSize = 14;
                _label.fontStyle = FontStyle.Bold;
            }
            _label.normal.textColor = color;
            GUI.Label(new Rect(rect.x, rect.y - 20f, 200f, 20f),
                "RAKE  " + distance.ToString("0") + " m", _label);
        }

        private bool TryGetBounds(out Bounds bounds)
        {
            bounds = new Bounds(Rake.position + Vector3.up * 1f, new Vector3(1.2f, 2f, 1.2f));
            bool has = false;

            for (int i = 0; i < _renderers.Count; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;

                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }

            return true;
        }

        private static bool Project(Camera cam, Bounds bounds, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);

            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3(
                    ((i & 1) == 0 ? -e.x : e.x),
                    ((i & 2) == 0 ? -e.y : e.y),
                    ((i & 4) == 0 ? -e.z : e.z));

                Vector3 screen = cam.WorldToScreenPoint(corner);
                if (screen.z <= 0.05f) return false;

                var point = new Vector2(screen.x, Screen.height - screen.y);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            return true;
        }

        private static void DrawBox(Rect rect, float thickness, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;

            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, thickness), Pixel);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), Pixel);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, thickness, rect.height), Pixel);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), Pixel);

            GUI.color = old;
        }
    }
}