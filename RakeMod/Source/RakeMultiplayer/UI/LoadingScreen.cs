using UnityEngine;
using UnityEngine.UI;

namespace RakeMultiplayer.UI
{
    /// <summary>
    /// Full-screen, horror-styled loading screen that matches the multiplayer menu:
    /// near-black backdrop, blood-red accents, cream typography, segmented progress
    /// bar, rotating tips and a slow scan line.
    ///
    /// The overlay lives on its own DontDestroyOnLoad canvas (above the menu canvas),
    /// so it survives the scene change it is announcing. Show() is safe from any
    /// main-thread code; all animation is driven internally with unscaled time.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        // Same palette as RuntimeUIBuilder, so both screens feel like one interface.
        private static readonly Color ColBg = new Color(0.02f, 0.02f, 0.03f, 1f);
        private static readonly Color ColPanel = new Color(0.055f, 0.05f, 0.065f, 0.97f);
        private static readonly Color ColAccent = new Color(0.72f, 0.16f, 0.12f, 1f);
        private static readonly Color ColAccentDim = new Color(0.35f, 0.10f, 0.08f, 1f);
        private static readonly Color ColText = new Color(0.93f, 0.90f, 0.84f, 1f);
        private static readonly Color ColMuted = new Color(0.55f, 0.52f, 0.46f, 1f);
        private static readonly Color ColTrack = new Color(0.12f, 0.11f, 0.12f, 1f);

        private const float FadeIn = 0.25f;
        private const float FadeOut = 0.35f;
        private const float TrackWidth = 520f;
        private const float MinShowTime = 0.9f;

        private static readonly string[] Tips =
        {
            "TIP: the Rake cannot attack during the day, but it never stops watching.",
            "TIP: one hunter can become the Rake in the lobby.",
            "TIP: the Rake senses every hunter, animal and trap within 100 m.",
            "TIP: sprinting is loud. Sneak (Ctrl) to stay unheard.",
            "TIP: frontal fire is absorbed while the Rake blocks.",
            "TIP: a hit throws you into a ragdoll and gives the Rake an opening.",
        };

        private static LoadingScreen _instance;
        private static bool _quitting;

        private CanvasGroup _canvasGroup;
        private Text _kicker;
        private Text _title;
        private Text _subtitle;
        private Text _status;
        private Text _percent;
        private Text _tip;
        private Text _version;
        private Image _topBar;
        private Image _bottomBar;
        private Image _separator;
        private Image _diamond;
        private RectTransform _diamondRt;
        private readonly System.Collections.Generic.List<Image> _corners =
            new System.Collections.Generic.List<Image>();
        private Image _scan;
        private Image _fill;
        private RectTransform _fillRt;
        private RectTransform _scanRt;

        private string _statusBase = "";
        private float _progress;
        private float _targetProgress;
        private bool _indeterminate;
        private bool _visible;
        private float _targetAlpha;
        private float _alpha;
        private float _time;
        private float _shownAt;
        private int _tipIndex;
        private float _tipTimer;
        private float _tipAlpha = 1f;
        private bool _fadeOutAfterShow;

        // ------------------------------------------------------------------ public API

        /// <summary>Show the overlay (fades in if it was hidden).</summary>
        public static void Show(string title, string subtitle, string status, bool indeterminate = false, bool closeMenu = true)
        {
            if (_quitting) return;

            var screen = Instance;
            if (screen == null) return;
            screen.ShowInternal(title, subtitle, status, indeterminate, closeMenu);
        }

        public static void SetStatus(string status)
        {
            if (_instance == null) return;
            _instance._statusBase = string.IsNullOrEmpty(status) ? "" : status;
        }

        /// <summary>0..1; the fill moves towards the value so it never jumps.</summary>
        public static void SetProgress(float progress)
        {
            if (_instance == null) return;
            _instance._targetProgress = Mathf.Clamp01(progress);
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance.StartFadeOut();
        }

        public static bool IsVisible
        {
            get { return _instance != null && _instance._visible; }
        }

        private static LoadingScreen Instance
        {
            get
            {
                if (_instance != null) return _instance;

                var go = new GameObject("RakeLoadingScreen");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<LoadingScreen>();
                return _instance;
            }
        }

        // ------------------------------------------------------------------- lifecycle

        private void Awake()
        {
            BuildUI();
            _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        private void OnApplicationQuit()
        {
            _quitting = true;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void ShowInternal(string title, string subtitle, string status, bool indeterminate, bool closeMenu)
        {
            // Level loads close the menu; short connection screens leave it alone.
            if (closeMenu) UI.MultiplayerMenu.CloseForLoad();

            _title.text = string.IsNullOrEmpty(title) ? "LOADING" : title.ToUpperInvariant();
            _subtitle.text = string.IsNullOrEmpty(subtitle) ? "" : subtitle.ToUpperInvariant();
            _statusBase = string.IsNullOrEmpty(status) ? "" : status;

            _indeterminate = indeterminate;
            _progress = 0f;
            _targetProgress = 0f;
            _tipIndex = Random.Range(0, Tips.Length);
            _tip.text = Tips[_tipIndex];
            _tipAlpha = 1f;
            _tipTimer = 0f;
            _time = 0f;
            _shownAt = Time.unscaledTime;
            _fadeOutAfterShow = false;

            _visible = true;
            _targetAlpha = 1f;
            gameObject.SetActive(true);

            UpdateProgressVisual();
            UpdateAccentPulse();

            RakeMultiplayerPlugin.Log?.LogInfo("[UI] Loading screen: " + _title.text +
                                               " (" + _subtitle.text + ")");
        }

        private void StartFadeOut()
        {
            if (!_visible) return;

            // Never flash away before the screen has actually been seen.
            float shownFor = Time.unscaledTime - _shownAt;
            if (shownFor < MinShowTime)
            {
                _fadeOutAfterShow = true;
                return;
            }

            if (!_indeterminate && _targetProgress < 1f) _targetProgress = 1f;
            _visible = false;
            _targetAlpha = 0f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) dt = 0.016f;

            _time += dt;

            // ---- fade ----
            float fadeSpeed = _targetAlpha > _alpha ? 1f / FadeIn : 1f / FadeOut;
            _alpha = Mathf.MoveTowards(_alpha, _targetAlpha, dt * fadeSpeed);
            _canvasGroup.alpha = _alpha;

            if (!_visible && _alpha <= 0.001f)
            {
                gameObject.SetActive(false);
                return;
            }

            if (_fadeOutAfterShow && Time.unscaledTime - _shownAt >= MinShowTime)
            {
                _fadeOutAfterShow = false;
                StartFadeOut();
            }

            // ---- progress ----
            if (_indeterminate)
            {
                float sweep = (Mathf.Sin(_time * 2.2f) + 1f) * 0.5f;
                _progress = Mathf.Lerp(_progress, 0.18f + sweep * 0.34f, dt * 8f);
            }
            else
            {
                // Keep the bar alive on its own until someone reports real progress.
                if (_targetProgress < 0.9f) _targetProgress = Mathf.Min(0.9f, _targetProgress + dt * 0.32f);
                _progress = Mathf.MoveTowards(_progress, _targetProgress, dt * 0.6f);
            }
            UpdateProgressVisual();

            // ---- status dots ----
            int dots = 1 + (int)(_time * 2.2f) % 3;
            _status.text = _statusBase + new string('.', dots);

            // ---- tips ----
            _tipTimer += dt;
            if (_tipTimer > 6f)
            {
                _tipTimer = 0f;
                _tipIndex = (_tipIndex + 1) % Tips.Length;
                _tip.text = Tips[_tipIndex];
                _tipAlpha = 0f;
            }
            _tipAlpha = Mathf.MoveTowards(_tipAlpha, 1f, dt * 2.5f);
            var tipColor = ColMuted;
            tipColor.a *= _tipAlpha;
            _tip.color = tipColor;

            // ---- spinning marker ----
            if (_diamondRt != null) _diamondRt.localRotation = Quaternion.Euler(0f, 0f, -_time * 40f);

            // ---- scan line ----
            float sweepY = Mathf.Lerp(560f, -560f, Mathf.Repeat(_time * 0.22f, 1f));
            _scanRt.anchoredPosition = new Vector2(0f, sweepY);

            UpdateAccentPulse();
        }

        private void UpdateAccentPulse()
        {
            float pulse = 0.62f + 0.38f * (Mathf.Sin(_time * 1.9f) + 1f) * 0.5f;

            var accent = ColAccent;
            accent.a = pulse;
            _topBar.color = accent;

            var dim = ColAccentDim;
            dim.a = pulse;
            _bottomBar.color = dim;

            var sep = new Color(ColAccent.r, ColAccent.g, ColAccent.b, 0.35f * pulse);
            _separator.color = sep;

            var corner = new Color(ColAccent.r, ColAccent.g, ColAccent.b, 0.28f + 0.22f * pulse);
            for (int i = 0; i < _corners.Count; i++) _corners[i].color = corner;

            var diamond = ColAccent;
            diamond.a = 0.55f + 0.4f * pulse;
            _diamond.color = diamond;

            var kicker = ColAccent;
            kicker.a = 0.7f + 0.3f * pulse;
            _kicker.color = kicker;

            var scan = ColAccent;
            scan.a = 0.05f + 0.03f * pulse;
            _scan.color = scan;
        }

        private void UpdateProgressVisual()
        {
            float p = Mathf.Clamp01(_progress);
            _fillRt.sizeDelta = new Vector2(TrackWidth * p, 0f);

            if (_indeterminate)
            {
                _percent.text = "CONNECTING";
                return;
            }

            int pct = Mathf.RoundToInt(p * 100f);
            _percent.text = pct.ToString("00") + "%";
        }

        // ---------------------------------------------------------------------- build

        private void BuildUI()
        {
            var canvasObj = gameObject;
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 33000; // above the multiplayer menu (32000)

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            canvasObj.AddComponent<GraphicRaycaster>();
            _canvasGroup = canvasObj.AddComponent<CanvasGroup>();

            // ---- backdrop ----
            var backdrop = CreateImage(canvasObj.transform, "Backdrop", ColBg);
            Stretch(backdrop.rectTransform);

            var vignette = CreateImage(canvasObj.transform, "Vignette", Color.white);
            Stretch(vignette.rectTransform);
            vignette.sprite = Sprite.Create(BuildVignette(), new Rect(0f, 0f, 128f, 128f),
                new Vector2(0.5f, 0.5f), 100f);
            vignette.type = Image.Type.Simple;

            // ---- thin frame ----
            _topBar = CreateImage(canvasObj.transform, "TopBar", ColAccent);
            var topRt = _topBar.rectTransform;
            topRt.anchorMin = new Vector2(0f, 1f);
            topRt.anchorMax = new Vector2(1f, 1f);
            topRt.pivot = new Vector2(0.5f, 1f);
            topRt.anchoredPosition = Vector2.zero;
            topRt.sizeDelta = new Vector2(0f, 4f);

            _bottomBar = CreateImage(canvasObj.transform, "BottomBar", ColAccentDim);
            var bottomRt = _bottomBar.rectTransform;
            bottomRt.anchorMin = new Vector2(0f, 0f);
            bottomRt.anchorMax = new Vector2(1f, 0f);
            bottomRt.pivot = new Vector2(0.5f, 0f);
            bottomRt.anchoredPosition = Vector2.zero;
            bottomRt.sizeDelta = new Vector2(0f, 2f);

            _scan = CreateImage(canvasObj.transform, "ScanLine", new Color(ColAccent.r, ColAccent.g, ColAccent.b, 0.06f));
            _scanRt = _scan.rectTransform;
            _scanRt.anchorMin = new Vector2(0f, 0.5f);
            _scanRt.anchorMax = new Vector2(1f, 0.5f);
            _scanRt.pivot = new Vector2(0.5f, 0.5f);
            _scanRt.sizeDelta = new Vector2(0f, 2f);

            // ---- central panel ----
            var panel = CreateImage(canvasObj.transform, "Panel", ColPanel);
            var panelRt = panel.rectTransform;
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(760f, 360f);
            panelRt.anchoredPosition = new Vector2(0f, 20f);

            var panelTop = CreateImage(panel.transform, "PanelTopBar", ColAccent);
            var panelTopRt = panelTop.rectTransform;
            panelTopRt.anchorMin = new Vector2(0f, 1f);
            panelTopRt.anchorMax = new Vector2(1f, 1f);
            panelTopRt.pivot = new Vector2(0.5f, 1f);
            panelTopRt.anchoredPosition = Vector2.zero;
            panelTopRt.sizeDelta = new Vector2(0f, 3f);

            AddCorners(canvasObj.transform);
            _diamond = CreateImage(panel.transform, "Diamond", ColAccent);
            _diamondRt = _diamond.rectTransform;
            _diamondRt.anchorMin = new Vector2(0.5f, 0.5f);
            _diamondRt.anchorMax = new Vector2(0.5f, 0.5f);
            _diamondRt.pivot = new Vector2(0.5f, 0.5f);
            _diamondRt.sizeDelta = new Vector2(9f, 9f);
            _diamondRt.anchoredPosition = new Vector2(0f, 154f);

            // ---- texts ----
            _kicker = CreateText(panel.transform, "Kicker", "RAKE  /  MULTIPLAYER", 12,
                new Vector2(0f, 128f), ColAccent, FontStyle.Bold);
            _title = CreateText(panel.transform, "Title", "LOADING", 38,
                new Vector2(0f, 84f), ColText, FontStyle.Bold);
            _subtitle = CreateText(panel.transform, "Subtitle", "", 12,
                new Vector2(0f, 48f), ColMuted, FontStyle.Normal);

            _separator = CreateImage(panel.transform, "Separator", new Color(ColAccent.r, ColAccent.g, ColAccent.b, 0.35f));
            var sepRt = _separator.rectTransform;
            sepRt.anchorMin = new Vector2(0.5f, 0.5f);
            sepRt.anchorMax = new Vector2(0.5f, 0.5f);
            sepRt.sizeDelta = new Vector2(520f, 1f);
            sepRt.anchoredPosition = new Vector2(0f, 24f);

            _status = CreateText(panel.transform, "Status", "Preparing", 13,
                new Vector2(0f, -6f), ColMuted, FontStyle.Normal);

            // ---- progress ----
            var track = CreateImage(panel.transform, "Track", ColTrack);
            var trackRt = track.rectTransform;
            trackRt.anchorMin = new Vector2(0.5f, 0.5f);
            trackRt.anchorMax = new Vector2(0.5f, 0.5f);
            trackRt.sizeDelta = new Vector2(TrackWidth, 8f);
            trackRt.anchoredPosition = new Vector2(0f, -52f);

            _fill = CreateImage(track.transform, "Fill", ColAccent);
            _fillRt = _fill.rectTransform;
            _fillRt.anchorMin = new Vector2(0f, 0f);
            _fillRt.anchorMax = new Vector2(0f, 1f);
            _fillRt.pivot = new Vector2(0f, 0.5f);
            _fillRt.anchoredPosition = Vector2.zero;
            _fillRt.sizeDelta = new Vector2(0f, 0f);

            // Segment ticks drawn over the fill: a meter, not a plain bar.
            const int segments = 24;
            for (int i = 1; i < segments; i++)
            {
                var tick = CreateImage(track.transform, "Tick" + i, new Color(0.03f, 0.03f, 0.04f, 0.9f));
                var tickRt = tick.rectTransform;
                tickRt.anchorMin = new Vector2(0f, 0f);
                tickRt.anchorMax = new Vector2(0f, 1f);
                tickRt.pivot = new Vector2(0.5f, 0.5f);
                tickRt.sizeDelta = new Vector2(2f, 0f);
                tickRt.anchoredPosition = new Vector2(TrackWidth * i / segments, 0f);
            }

            _percent = CreateText(panel.transform, "Percent", "00%", 12,
                new Vector2(260f, -78f), ColAccent, FontStyle.Bold);

            // ---- footer ----
            _tip = CreateText(canvas.transform, "Tip", Tips[0], 12,
                new Vector2(0f, -258f), ColMuted, FontStyle.Normal, 1100f);

            _version = CreateText(canvas.transform, "Version",
                PluginInfo.NAME + "  v" + PluginInfo.VERSION, 11,
                new Vector2(0f, 0f), new Color(0.35f, 0.33f, 0.30f, 1f), FontStyle.Normal, 400f);
            var versionRt = _version.rectTransform;
            versionRt.anchorMin = new Vector2(1f, 0f);
            versionRt.anchorMax = new Vector2(1f, 0f);
            versionRt.pivot = new Vector2(1f, 0f);
            versionRt.anchoredPosition = new Vector2(-28f, 18f);
            _version.alignment = TextAnchor.LowerRight;
        }

        /// <summary>Thin red brackets in the four screen corners: a framed, filmic feel.</summary>
        private void AddCorners(Transform parent)
        {
            AddCorner(parent, new Vector2(0f, 1f), 1f, -1f);
            AddCorner(parent, new Vector2(1f, 1f), -1f, -1f);
            AddCorner(parent, new Vector2(0f, 0f), 1f, 1f);
            AddCorner(parent, new Vector2(1f, 0f), -1f, 1f);
        }

        private void AddCorner(Transform parent, Vector2 anchor, float dx, float dy)
        {
            var h = CreateImage(parent, "CornerH", ColAccent);
            var hRt = h.rectTransform;
            hRt.anchorMin = anchor;
            hRt.anchorMax = anchor;
            hRt.pivot = new Vector2(dx > 0f ? 0f : 1f, 0.5f);
            hRt.sizeDelta = new Vector2(46f, 2f);
            hRt.anchoredPosition = new Vector2(dx * 10f, dy * 10f);

            var v = CreateImage(parent, "CornerV", ColAccent);
            var vRt = v.rectTransform;
            vRt.anchorMin = anchor;
            vRt.anchorMax = anchor;
            vRt.pivot = new Vector2(0.5f, dy > 0f ? 0f : 1f);
            vRt.sizeDelta = new Vector2(2f, 46f);
            vRt.anchoredPosition = new Vector2(dx * 10f, dy * 10f);

            _corners.Add(h);
            _corners.Add(v);
        }
        private static Texture2D BuildVignette()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x / (size - 1f)) * 2f - 1f;
                    float dy = (y / (size - 1f)) * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;
                    float alpha = Mathf.Clamp01((d - 0.42f) / 0.58f);
                    alpha = alpha * alpha * 0.82f;
                    pixels[y * size + x] = new Color32(0, 0, 0, (byte)(alpha * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        // ---------------------------------------------------------------- ui helpers

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var img = obj.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Text CreateText(Transform parent, string name, string text, int fontSize,
            Vector2 anchoredPos, Color color, FontStyle style, float width = 760f)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, fontSize + 16f);
            rt.anchoredPosition = anchoredPos;

            var txt = obj.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }
    }
}
