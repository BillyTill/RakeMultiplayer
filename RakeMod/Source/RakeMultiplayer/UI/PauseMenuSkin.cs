using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RakeMultiplayer.UI
{
    /// <summary>
    /// Reskins the vanilla pause menu (GamePauseMenu) to the Rake's dark organic style:
    /// a torn plate, bone type, ember accents. The vanilla panel is only hidden; its
    /// Continue / Exit methods still do the actual work.
    /// </summary>
    public class PauseMenuSkin : MonoBehaviour
    {
        private static readonly Color Bone = new Color(0.847f, 0.816f, 0.745f, 1f);
        private static readonly Color BoneDim = new Color(0.588f, 0.569f, 0.518f, 1f);
        private static readonly Color Ember = new Color(0.776f, 0.463f, 0.141f, 1f);

        private GamePauseMenu _game;
        private CanvasGroup _vanillaGroup;
        private GameObject _canvas;
        private CanvasGroup _canvasGroup;
        private Font _font;
        private bool _shown;
        private bool _built;

        private void Update()
        {
            if (_game == null)
            {
                _game = FindObjectOfType<GamePauseMenu>();
                if (_game == null) return;
            }

            bool paused = _game.isPaused;
            if (paused == _shown) return;

            _shown = paused;
            if (paused) Show();
            else Hide();
        }

        private void Show()
        {
            if (_game == null) return;

            if (!_built) Build();

            // hide the vanilla black panel but keep its state machine alive
            if (_game.menu != null && _vanillaGroup == null)
            {
                _vanillaGroup = _game.menu.GetComponent<CanvasGroup>();
                if (_vanillaGroup == null) _vanillaGroup = _game.menu.AddComponent<CanvasGroup>();
            }

            if (_vanillaGroup != null)
            {
                _vanillaGroup.alpha = 0f;
                _vanillaGroup.blocksRaycasts = false;
                _vanillaGroup.interactable = false;
            }

            if (_canvas != null) _canvas.SetActive(true);

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void Hide()
        {
            if (_canvas != null) _canvas.SetActive(false);

            if (_vanillaGroup != null)
            {
                _vanillaGroup.alpha = 1f;
                _vanillaGroup.blocksRaycasts = true;
                _vanillaGroup.interactable = true;
            }

            if (RakePlayerController.IsActive)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
        }

        // ---------------------------------------------------------------- build

        private void Build()
        {
            _built = true;
            _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Bahnschrift SemiCondensed", "Bahnschrift", "Segoe UI Semibold", "Arial" }, 32);
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Without a GraphicRaycaster the canvas never receives pointer events, so
            // both buttons were dead: the plate only reacted to Escape.
            _canvas = new GameObject("RakePauseCanvas", typeof(Canvas), typeof(CanvasScaler),
                typeof(CanvasGroup), typeof(GraphicRaycaster));
            DontDestroyOnLoad(_canvas);
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32500;

            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _canvasGroup = _canvas.GetComponent<CanvasGroup>();

            var dim = NewImage("Dim", _canvas.transform, new Color(0f, 0f, 0f, 0.62f), Skin.Solid());
            Stretch(dim.rectTransform);

            var plate = NewImage("Plate", _canvas.transform,
                new Color(0.027f, 0.035f, 0.031f, 0.96f), Skin.Plate(780, 470, 91));
            TopLeft(plate.rectTransform, 570f, 305f, 780f, 470f);
            plate.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -0.7f);

            var title = NewText("Title", plate.transform, "PAUSED", 54, Bone, TextAnchor.MiddleCenter);
            TopLeft(title.rectTransform, 40f, 44f, 700f, 60f);

            var sub = NewText("Sub", plate.transform, "THE FOREST WAITS", 17, BoneDim, TextAnchor.MiddleCenter);
            TopLeft(sub.rectTransform, 40f, 108f, 700f, 24f);

            var line = NewImage("Line", plate.transform, new Color(Ember.r, Ember.g, Ember.b, 0.55f), Skin.Solid());
            TopLeft(line.rectTransform, 290f, 148f, 200f, 2f);

            MakeButton(plate.transform, "CONTINUE", 200f, ContinueGame, true);
            MakeButton(plate.transform, "EXIT TO MENU", 288f, ExitToMenu, false);

            var hint = NewText("Hint", plate.transform, "ESC  ·  RESUME", 15, BoneDim, TextAnchor.MiddleCenter);
            TopLeft(hint.rectTransform, 40f, 400f, 700f, 22f);
        }

        /// <summary>
        /// Resume the game. GamePauseMenu.Continue() hides the vanilla panel but leaves
        /// isPaused true, and this skin is driven by isPaused, so the plate stayed on top
        /// of a running game and the button looked dead.
        /// </summary>
        private void ContinueGame()
        {
            if (_game == null)
            {
                _shown = false;
                Hide();
                return;
            }

            _game.isPaused = false;
            _game.Continue();
        }

        /// <summary>
        /// The vanilla exit only calls Application.LoadLevel(0). In a modded session that
        /// left the peer connected, kept Time.timeScale at 0 and left this plate (it is
        /// DontDestroyOnLoad) on top of the main menu.
        /// </summary>
        private void ExitToMenu()
        {
            _shown = false;
            Hide();

            if (_game != null) _game.Continue();

            UI.MultiplayerMenu.ExitToMainMenu("pause menu");
        }

        private void MakeButton(Transform parent, string label, float y, Action action, bool primary)
        {
            var go = new GameObject("Btn" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            TopLeft(rt, 90f, y, 600f, 70f);

            var img = go.GetComponent<Image>();
            img.sprite = Skin.Plate(300, 70, primary ? 17 : 29);
            img.type = Image.Type.Simple;
            img.color = new Color(0.055f, 0.063f, 0.055f, 1f);

            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.86f, 0.62f, 1f);
            colors.pressedColor = new Color(0.8f, 0.62f, 0.42f, 1f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;
            button.onClick.AddListener(delegate { if (action != null) action(); });

            var text = NewText("Text", go.transform, label, 24, primary ? Ember : Bone, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
        }

        // ---------------------------------------------------------------- helpers

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static Image NewImage(string name, Transform parent, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            return img;
        }

        private Text NewText(string name, Transform parent, string text, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // ---------------------------------------------------------------- textures

        internal static class Skin
        {
            private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

            private static Sprite Get(string key, Func<Sprite> make)
            {
                Sprite s;
                if (Cache.TryGetValue(key, out s) && s != null) return s;
                s = make();
                Cache[key] = s;
                return s;
            }

            private static Sprite From(Texture2D tex)
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }

            internal static Sprite Solid()
            {
                return Get("solid", delegate
                {
                    var tex = new Texture2D(4, 4, TextureFormat.ARGB32, false);
                    var px = new Color32[16];
                    for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    return From(tex);
                });
            }

            internal static Sprite Plate(int w, int h, int seed)
            {
                return Get("plate" + w + "x" + h + "_" + seed, delegate
                {
                    var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
                    var px = new Color32[w * h];
                    float radius = Mathf.Min(w, h) * 0.16f;

                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            float nx = x / (float)w, ny = y / (float)h;
                            float wobble = Mathf.Sin(nx * 17.3f + seed) * 3.2f
                                           + Mathf.Sin(ny * 13.7f - seed * 0.7f) * 2.6f
                                           + ((x * 73856093 ^ y * 19349663 ^ seed * 83492791) % 1000) / 1000f * 4.6f - 2.3f;

                            float dx = Mathf.Max(Mathf.Abs(x - w * 0.5f) - (w * 0.5f - radius), 0f);
                            float dy = Mathf.Max(Mathf.Abs(y - h * 0.5f) - (h * 0.5f - radius), 0f);
                            float d = Mathf.Sqrt(dx * dx + dy * dy) - radius + wobble;

                            float a = Mathf.Clamp01(0.5f - d) * 0.98f;
                            if (a <= 0.01f) continue;

                            float shade = 0.92f + 0.08f * Mathf.Sin(nx * 23f) * Mathf.Sin(ny * 19f);
                            byte v = (byte)(Mathf.Clamp01(shade) * 255f);
                            px[y * w + x] = new Color32(v, v, v, (byte)(a * 255f));
                        }
                    }

                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    return From(tex);
                });
            }
        }
    }
}
