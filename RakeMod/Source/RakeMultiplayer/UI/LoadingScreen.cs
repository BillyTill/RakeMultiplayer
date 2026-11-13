using UnityEngine;
using UnityEngine.UI;

namespace RakeMultiplayer.UI
{
    /// <summary>
    /// Full-screen loading screen in the same physical-paper style as the
    /// multiplayer menu: dark board, torn paper note, hand-written labels, a
    /// paper progress strip and a slowly turning reel.
    ///
    /// The overlay lives on its own DontDestroyOnLoad canvas (above the menu
    /// canvas), so it survives the scene change it is announcing.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        private static readonly Color ColBg = new Color(0.025f, 0.020f, 0.015f, 1f);
        private static readonly Color ColBoard = new Color(0.19f, 0.17f, 0.14f, 0.96f);
        private static readonly Color ColText = new Color(0.94f, 0.90f, 0.81f, 1f);
        private static readonly Color ColMuted = new Color(0.66f, 0.60f, 0.50f, 1f);
        private static readonly Color ColInk = new Color(0.15f, 0.11f, 0.06f, 1f);
        private static readonly Color ColInkMuted = new Color(0.34f, 0.27f, 0.17f, 1f);
        private static readonly Color ColAmber = new Color(1.00f, 0.70f, 0.38f, 1f);
        private static readonly Color ColGreen = new Color(0.72f, 0.82f, 0.57f, 1f);

        private const float FadeIn = 0.25f;
        private const float FadeOut = 0.35f;
        private const float TrackWidth = 560f;
        private const float MinShowTime = 0.9f;

        private static readonly string[] Tips =
        {
            "Tip — the Rake cannot attack during the day, but it never stops watching.",
            "Tip — one hunter can become the Rake in the lobby.",
            "Tip — the Rake senses every hunter, animal and trap within 100 m.",
            "Tip — sprinting is loud. Sneak (Ctrl) to stay unheard.",
            "Tip — frontal fire is absorbed while the Rake blocks.",
            "Tip — a hit throws you into a ragdoll and gives the Rake an opening.",
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
        private Image _glow;
        private Image _accentLine;
        private Image _reel;
        private RectTransform _reelRt;
        private Image _fill;
        private RectTransform _fillRt;

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
            UpdateGlow();

            RakeMultiplayerPlugin.Log?.LogInfo("[UI] Loading screen: " + _title.text +
                                               " (" + _subtitle.text + ")");
        }

        private void StartFadeOut()
        {
            if (!_visible) return;

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

            if (_indeterminate)
            {
                float sweep = (Mathf.Sin(_time * 2.2f) + 1f) * 0.5f;
                _progress = Mathf.Lerp(_progress, 0.18f + sweep * 0.34f, dt * 8f);
            }
            else
            {
                if (_targetProgress < 0.9f) _targetProgress = Mathf.Min(0.9f, _targetProgress + dt * 0.32f);
                _progress = Mathf.MoveTowards(_progress, _targetProgress, dt * 0.6f);
            }
            UpdateProgressVisual();

            int dots = 1 + (int)(_time * 2.2f) % 3;
            _status.text = _statusBase + new string('.', dots);

            _tipTimer += dt;
            if (_tipTimer > 6f)
            {
                _tipTimer = 0f;
                _tipIndex = (_tipIndex + 1) % Tips.Length;
                _tip.text = Tips[_tipIndex];
                _tipAlpha = 0f;
            }
            _tipAlpha = Mathf.MoveTowards(_tipAlpha, 1f, dt * 2.5f);
            var tipColor = ColInk;
            tipColor.a *= _tipAlpha;
            _tip.color = tipColor;

            if (_reelRt != null) _reelRt.localRotation = Quaternion.Euler(0f, 0f, -_time * 11f);

            UpdateGlow();
        }

        private void UpdateGlow()
        {
            float pulse = 0.62f + 0.38f * (Mathf.Sin(_time * 1.7f) + 1f) * 0.5f;

            if (_glow != null)
                _glow.color = new Color(1f, 0.55f, 0.27f, 0.10f + 0.05f * pulse);

            if (_accentLine != null)
                _accentLine.color = new Color(1f, 0.70f, 0.38f, 0.28f + 0.18f * pulse);

            if (_kicker != null)
                _kicker.color = new Color(ColGreen.r, ColGreen.g, ColGreen.b, 0.62f + 0.28f * pulse);
        }

        private void UpdateProgressVisual()
        {
            float p = Mathf.Clamp01(_progress);
            _fillRt.sizeDelta = new Vector2(TrackWidth * p, 0f);

            if (_indeterminate)
            {
                _percent.text = "WAITING";
                return;
            }

            int pct = Mathf.RoundToInt(p * 100f);
            _percent.text = pct.ToString("00") + "%";
        }

        // ---------------------------------------------------------------------- build

        private void BuildUI()
        {
            MenuSkin.Init();

            var canvasObj = gameObject;
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32001;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            canvasObj.AddComponent<GraphicRaycaster>();
            _canvasGroup = canvasObj.AddComponent<CanvasGroup>();

            // ---- backdrop ----
            var backdrop = CreateImage(canvasObj.transform, "Backdrop", ColBg);
            Stretch(backdrop.rectTransform);

            _glow = CreateImage(canvasObj.transform, "WarmGlow", Color.white);
            Stretch(_glow.rectTransform);
            _glow.sprite = Sprite.Create(BuildRadial(), new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f), 100f);
            _glow.type = Image.Type.Simple;
            _glow.color = new Color(1f, 0.55f, 0.27f, 0.14f);

            var vignette = CreateImage(canvasObj.transform, "Vignette", Color.white);
            Stretch(vignette.rectTransform);
            vignette.sprite = Sprite.Create(BuildVignette(), new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f), 100f);
            vignette.type = Image.Type.Simple;

            // ---- dark board ----
            var board = CreateImage(canvasObj.transform, "Board", Color.white);
            board.sprite = MenuSkin.Plate;
            board.color = ColBoard;
            var boardRt = board.rectTransform;
            boardRt.anchorMin = new Vector2(0.5f, 0.5f);
            boardRt.anchorMax = new Vector2(0.5f, 0.5f);
            boardRt.pivot = new Vector2(0.5f, 0.5f);
            boardRt.sizeDelta = new Vector2(920f, 580f);
            boardRt.anchoredPosition = new Vector2(0f, 30f);
            boardRt.localEulerAngles = new Vector3(0f, 0f, -0.6f);
            var boardShadow = board.gameObject.AddComponent<Shadow>();
            boardShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            boardShadow.effectDistance = new Vector2(12f, -14f);

            _accentLine = CreateImage(board.transform, "AccentLine", ColAmber);
            var lineRt = _accentLine.rectTransform;
            lineRt.anchorMin = new Vector2(0f, 1f);
            lineRt.anchorMax = new Vector2(1f, 1f);
            lineRt.pivot = new Vector2(0.5f, 1f);
            lineRt.offsetMin = new Vector2(40f, 0f);
            lineRt.offsetMax = new Vector2(-40f, 0f);
            lineRt.sizeDelta = new Vector2(lineRt.sizeDelta.x, 2f);
            lineRt.anchoredPosition = Vector2.zero;

            // ---- texts on the board ----
            _kicker = CreateText(board.transform, "Kicker", "RAKE  ·  FIELD INTERMISSION", 14,
                new Vector2(0f, 245f), ColGreen, 820f);

            _title = CreateText(board.transform, "Title", "LOADING", 58,
                new Vector2(0f, 178f), ColText, 860f);

            _subtitle = CreateText(board.transform, "Subtitle", "", 24,
                new Vector2(0f, 122f), ColMuted, 760f);

            _status = CreateText(board.transform, "Status", "Preparing", 22,
                new Vector2(0f, 72f), ColText, 760f);

            // ---- paper progress strip ----
            var track = CreateImage(board.transform, "Track", Color.white);
            track.sprite = MenuSkin.Paper;
            track.color = new Color(0.90f, 0.84f, 0.70f, 1f);
            var trackRt = track.rectTransform;
            trackRt.anchorMin = new Vector2(0.5f, 0.5f);
            trackRt.anchorMax = new Vector2(0.5f, 0.5f);
            trackRt.pivot = new Vector2(0.5f, 0.5f);
            trackRt.sizeDelta = new Vector2(TrackWidth, 20f);
            trackRt.anchoredPosition = new Vector2(0f, 8f);
            var trackShadow = track.gameObject.AddComponent<Shadow>();
            trackShadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            trackShadow.effectDistance = new Vector2(3f, -4f);

            _fill = CreateImage(track.transform, "Fill", Color.white);
            _fill.sprite = MenuSkin.Gold;
            var fillRt = _fill.rectTransform;
            _fillRt = fillRt;
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = new Vector2(0f, 0f);

            const int segments = 20;
            for (int i = 1; i < segments; i++)
            {
                var tick = CreateImage(track.transform, "Tick" + i, new Color(0.15f, 0.11f, 0.06f, 0.30f));
                var tickRt = tick.rectTransform;
                tickRt.anchorMin = new Vector2(0f, 0f);
                tickRt.anchorMax = new Vector2(0f, 1f);
                tickRt.pivot = new Vector2(0.5f, 0.5f);
                tickRt.sizeDelta = new Vector2(2f, 0f);
                tickRt.anchoredPosition = new Vector2(TrackWidth * i / segments, 0f);
            }

            _percent = CreateText(board.transform, "Percent", "00%", 22,
                new Vector2(342f, 8f), ColAmber, 150f);

            // ---- reel ----
            _reel = CreateImage(board.transform, "Reel", Color.white);
            _reel.sprite = MenuSkin.Reel;
            _reel.color = new Color(0.34f, 0.29f, 0.23f, 1f);
            _reelRt = _reel.rectTransform;
            _reelRt.anchorMin = new Vector2(0.5f, 0.5f);
            _reelRt.anchorMax = new Vector2(0.5f, 0.5f);
            _reelRt.pivot = new Vector2(0.5f, 0.5f);
            _reelRt.sizeDelta = new Vector2(116f, 116f);
            _reelRt.anchoredPosition = new Vector2(405f, -180f);
            _reel.gameObject.AddComponent<SlowSpin>().DegreesPerSecond = -11f;

            // ---- tip paper ----
            var tipPaper = CreateImage(canvasObj.transform, "TipNote", Color.white);
            tipPaper.sprite = MenuSkin.Paper;
            tipPaper.color = new Color(0.96f, 0.90f, 0.76f, 1f);
            var tipRt = tipPaper.rectTransform;
            tipRt.anchorMin = new Vector2(0.5f, 0.5f);
            tipRt.anchorMax = new Vector2(0.5f, 0.5f);
            tipRt.pivot = new Vector2(0.5f, 0.5f);
            tipRt.sizeDelta = new Vector2(820f, 120f);
            tipRt.anchoredPosition = new Vector2(0f, -365f);
            tipRt.localEulerAngles = new Vector3(0f, 0f, -1.2f);
            var tipShadow = tipPaper.gameObject.AddComponent<Shadow>();
            tipShadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            tipShadow.effectDistance = new Vector2(8f, -10f);

            CreateText(tipPaper.transform, "TipKicker", "FIELD NOTE", 12,
                new Vector2(0f, 34f), ColInkMuted, 700f);
            _tip = CreateText(tipPaper.transform, "Tip", Tips[0], 20,
                new Vector2(0f, -8f), ColInk, 730f);

            // tape strips on the note
            AddTape(tipPaper.transform, new Vector2(-360f, 42f), -16f);
            AddTape(tipPaper.transform, new Vector2(360f, 42f), 14f);

            // ---- version ----
            _version = CreateText(canvasObj.transform, "Version",
                PluginInfo.NAME + "  v" + PluginInfo.DISPLAY_VERSION, 12,
                new Vector2(-30f, 20f), new Color(0.45f, 0.40f, 0.33f, 1f), 400f);
            var versionRt = _version.rectTransform;
            versionRt.anchorMin = new Vector2(1f, 0f);
            versionRt.anchorMax = new Vector2(1f, 0f);
            versionRt.pivot = new Vector2(1f, 0f);
            versionRt.anchoredPosition = new Vector2(-30f, 20f);
            _version.alignment = TextAnchor.LowerRight;
        }

        private static void AddTape(Transform parent, Vector2 position, float rotation)
        {
            var tape = CreateImage(parent, "Tape", Color.white);
            tape.sprite = MenuSkin.Paper;
            tape.color = new Color(0.80f, 0.74f, 0.58f, 0.65f);
            var rt = tape.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(70f, 22f);
            rt.anchoredPosition = position;
            rt.localEulerAngles = new Vector3(0f, 0f, rotation);
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
                    float alpha = Mathf.Clamp01((d - 0.38f) / 0.62f);
                    alpha = alpha * alpha * 0.86f;
                    pixels[y * size + x] = new Color32(0, 0, 0, (byte)(alpha * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private static Texture2D BuildRadial()
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
                    float alpha = Mathf.Clamp01(1f - d);
                    alpha = alpha * alpha;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
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
            Vector2 anchoredPos, Color color, float width = 760f)
        {
            MenuSkin.Init();
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
            txt.font = MenuSkin.HandFont;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Normal;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }
    }
}
