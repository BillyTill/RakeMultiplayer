using UnityEngine;

namespace RakeMultiplayer.UI
{
    /// <summary>
    /// Detailed Rake AI state overlay. Toggle with F7. It is intentionally read-only:
    /// it never takes input or changes the simulation, so it is safe on host and client.
    /// </summary>
    public class RakeStatusPanel : MonoBehaviour
    {
        public static KeyCode ToggleKey = KeyCode.F7;

        private bool _visible;
        private GUIStyle _box;
        private GUIStyle _title;
        private GUIStyle _body;
        private Texture2D _background;
        private Texture2D _accent;

        private Transform _rake;
        private NetworkedRakeAI _ai;
        private float _refreshTimer;
        private string _report = "";

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
            {
                _visible = !_visible;
                if (_visible) _refreshTimer = 0f;
            }

            if (!_visible) return;

            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f) return;
            _refreshTimer = 0.2f;

            if (_rake == null) _rake = RakeHighlight.FindRake();
            _ai = _rake != null ? _rake.GetComponent<NetworkedRakeAI>() : null;

            if (_ai == null)
            {
                _report = _rake == null
                    ? "No Rake in this scene.\nBegin a match or load a level."
                    : "The Rake has no NetworkedRakeAI component.";
                return;
            }

            _report = _ai.GetAiStatusReport();
        }

        private void EnsureStyles()
        {
            if (_box != null) return;

            _background = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            _background.SetPixel(0, 0, new Color(0.035f, 0.045f, 0.055f, 0.93f));
            _background.Apply();
            _background.hideFlags = HideFlags.HideAndDontSave;

            _accent = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            _accent.SetPixel(0, 0, new Color(0.85f, 0.62f, 0.22f, 1f));
            _accent.Apply();
            _accent.hideFlags = HideFlags.HideAndDontSave;

            _box = new GUIStyle(GUI.skin.box);
            _box.normal.background = _background;
            _box.border = new RectOffset(1, 1, 1, 1);

            _title = new GUIStyle(GUI.skin.label);
            _title.fontSize = 13;
            _title.fontStyle = FontStyle.Bold;
            _title.normal.textColor = new Color(0.95f, 0.78f, 0.42f, 1f);
            _title.padding = new RectOffset(10, 10, 6, 2);

            _body = new GUIStyle(GUI.skin.label);
            _body.fontSize = 11;
            _body.richText = false;
            _body.wordWrap = false;
            _body.normal.textColor = new Color(0.82f, 0.87f, 0.9f, 1f);
            _body.padding = new RectOffset(10, 10, 4, 8);
        }

        private void OnGUI()
        {
            if (!_visible) return;
            EnsureStyles();

            const float width = 470f;
            float height = 286f;
            var rect = new Rect(Screen.width - width - 14f, 14f, width, height);

            GUI.Box(rect, GUIContent.none, _box);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 3f), _accent, ScaleMode.StretchToFill);

            GUI.Label(new Rect(rect.x, rect.y + 3f, rect.width, 22f),
                      "RAKE // AI STATE      [F7]", _title);

            GUI.Label(new Rect(rect.x + 2f, rect.y + 26f, rect.width - 4f, rect.height - 30f),
                      _report, _body);
        }

        private void OnDestroy()
        {
            if (_background != null) Destroy(_background);
            if (_accent != null) Destroy(_accent);
        }
    }
}
