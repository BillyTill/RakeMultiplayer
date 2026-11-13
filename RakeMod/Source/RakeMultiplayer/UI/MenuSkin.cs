using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RakeMultiplayer.UI
{
    public enum MenuCardStyle
    {
        Paper,
        Plate,
        Gold,
        Tag,
        Radio,
        Camera,
        Reel
    }

    /// <summary>
    /// Runtime sprites, hand-written font and small components that give the
    /// multiplayer menu its physical, desk-object look. No external asset bundle:
    /// the camera/reel PNGs are embedded in the DLL and the paper/plate/gold
    /// textures are generated once at start.
    /// </summary>
    public static class MenuSkin
    {
        private static bool _ready;

        public static Font HandFont;
        public static Sprite Paper;
        public static Sprite Plate;
        public static Sprite Gold;
        public static Sprite Circle;
        public static Sprite Grille;
        public static Sprite Camera;
        public static Sprite Reel;

        public static void Init()
        {
            if (_ready) return;
            _ready = true;

            HandFont = LoadHandFont();
            Paper = MakePaper();
            Plate = MakePlate("plate", new Color(0.23f, 0.21f, 0.17f), new Color(0.06f, 0.05f, 0.04f), 0.045f);
            Gold = MakePlate("gold", new Color(1.00f, 0.86f, 0.63f), new Color(0.52f, 0.30f, 0.10f), 0.025f);
            Circle = MakeCircle();
            Grille = MakeGrille();
            Camera = LoadEmbedded("camera.png");
            Reel = LoadEmbedded("reel.png");
        }

        public static MenuCardStyle StyleFor(string name)
        {
            if (string.IsNullOrEmpty(name)) return MenuCardStyle.Plate;

            if (name.IndexOf("HostGame", StringComparison.OrdinalIgnoreCase) >= 0) return MenuCardStyle.Radio;
            if (name.IndexOf("JoinGame", StringComparison.OrdinalIgnoreCase) >= 0) return MenuCardStyle.Camera;
            if (name.Equals("SettingsBtn", StringComparison.OrdinalIgnoreCase)) return MenuCardStyle.Reel;
            if (name.IndexOf("StartGame", StringComparison.OrdinalIgnoreCase) >= 0) return MenuCardStyle.Gold;
            if (name.IndexOf("RakeRole", StringComparison.OrdinalIgnoreCase) >= 0) return MenuCardStyle.Gold;
            if (name.IndexOf("Connect", StringComparison.OrdinalIgnoreCase) >= 0) return MenuCardStyle.Gold;
            if (name.IndexOf("SteamCreateOk", StringComparison.OrdinalIgnoreCase) >= 0) return MenuCardStyle.Gold;
            if (name.IndexOf("SteamJoinOk", StringComparison.OrdinalIgnoreCase) >= 0) return MenuCardStyle.Gold;
            if (name.IndexOf("Apply", StringComparison.OrdinalIgnoreCase) >= 0) return MenuCardStyle.Gold;
            if (name.IndexOf("Back", StringComparison.OrdinalIgnoreCase) >= 0) return MenuCardStyle.Tag;
            if (name.IndexOf("Exit", StringComparison.OrdinalIgnoreCase) >= 0) return MenuCardStyle.Tag;
            if (name.IndexOf("Copy", StringComparison.OrdinalIgnoreCase) >= 0) return MenuCardStyle.Tag;
            if (name.IndexOf("Leave", StringComparison.OrdinalIgnoreCase) >= 0) return MenuCardStyle.Tag;
            return MenuCardStyle.Plate;
        }

        public static Sprite SpriteFor(MenuCardStyle style)
        {
            switch (style)
            {
                case MenuCardStyle.Paper:
                case MenuCardStyle.Tag:
                    return Paper;
                case MenuCardStyle.Gold:
                    return Gold;
                case MenuCardStyle.Camera:
                case MenuCardStyle.Radio:
                    return Plate;
                case MenuCardStyle.Reel:
                    return Reel;
                default:
                    return Plate;
            }
        }

        public static Color TextColorFor(MenuCardStyle style)
        {
            switch (style)
            {
                case MenuCardStyle.Paper:
                case MenuCardStyle.Tag:
                case MenuCardStyle.Camera:
                case MenuCardStyle.Reel:
                case MenuCardStyle.Gold:
                    return new Color(0.14f, 0.10f, 0.05f, 1f);
                default:
                    return new Color(0.95f, 0.91f, 0.82f, 1f);
            }
        }

        public static Color KickerColorFor(MenuCardStyle style)
        {
            switch (style)
            {
                case MenuCardStyle.Paper:
                case MenuCardStyle.Tag:
                case MenuCardStyle.Camera:
                case MenuCardStyle.Reel:
                case MenuCardStyle.Gold:
                    return new Color(0.36f, 0.28f, 0.17f, 0.8f);
                default:
                    return new Color(0.75f, 0.68f, 0.52f, 0.75f);
            }
        }

        public static int FontSizeFor(MenuCardStyle style, float height)
        {
            if (style == MenuCardStyle.Tag) return 24;
            if (style == MenuCardStyle.Reel) return 29;
            if (style == MenuCardStyle.Camera) return 26;
            return Mathf.Clamp(Mathf.RoundToInt(height * 0.19f), 22, 34);
        }

        public static Vector3 RestAnglesFor(string name)
        {
            // Overlay canvases have no perspective: X/Y rotation reads as a skew and
            // makes the hand-written labels look bent. Only a small Z tilt remains.
            return Vector3.zero;
        }

        // ---------------------------------------------------------------- scene dressing

        public static void DecorateFace(Transform face, MenuCardStyle style)
        {
            Init();

            if (style == MenuCardStyle.Radio)
            {
                AddImage(face, "Grille", Grille, new Vector2(0.04f, 0.12f), new Vector2(0.44f, 0.88f),
                    new Color(0.85f, 0.82f, 0.72f, 0.95f), Image.Type.Tiled);
                AddKnob(face, new Vector2(0.47f, 0.15f), 30f);
                AddKnob(face, new Vector2(0.56f, 0.15f), 30f);
            }
            else if (style == MenuCardStyle.Camera)
            {
                var cam = AddImage(face, "CameraBody", Camera, new Vector2(0.16f, 0.28f), new Vector2(0.84f, 0.98f),
                    Color.white, Image.Type.Simple);
                cam.preserveAspect = true;
            }
        }

        public static void DecorateLabel(RectTransform labelRt, MenuCardStyle style)
        {
            if (style == MenuCardStyle.Camera)
            {
                var strip = AddImage(labelRt, "LabelPaper", Paper, Vector2.zero, Vector2.one,
                    new Color(0.96f, 0.90f, 0.76f, 1f), Image.Type.Simple);
                var st = strip.rectTransform;
                st.localEulerAngles = new Vector3(0f, 0f, -1.4f);
            }
            else if (style == MenuCardStyle.Reel)
            {
                var strip = AddImage(labelRt, "LabelTape", Paper, Vector2.zero, Vector2.one,
                    new Color(0.96f, 0.90f, 0.76f, 1f), Image.Type.Simple);
                var st = strip.rectTransform;
                st.localEulerAngles = new Vector3(0f, 0f, -4.5f);
            }
        }

        public static void PlaceLabel(RectTransform rt, MenuCardStyle style)
        {
            switch (style)
            {
                case MenuCardStyle.Radio:
                    rt.anchorMin = new Vector2(0.45f, 0f);
                    rt.anchorMax = new Vector2(0.98f, 1f);
                    rt.offsetMin = new Vector2(0f, 6f);
                    rt.offsetMax = new Vector2(-4f, -6f);
                    break;
                case MenuCardStyle.Camera:
                    rt.anchorMin = new Vector2(0.05f, 0.03f);
                    rt.anchorMax = new Vector2(0.95f, 0.42f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    break;
                case MenuCardStyle.Reel:
                    rt.anchorMin = new Vector2(0.10f, 0.34f);
                    rt.anchorMax = new Vector2(0.90f, 0.64f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    break;
                case MenuCardStyle.Tag:
                    rt.anchorMin = new Vector2(0.06f, 0.06f);
                    rt.anchorMax = new Vector2(0.94f, 0.94f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    break;
                default:
                    rt.anchorMin = new Vector2(0.05f, 0.05f);
                    rt.anchorMax = new Vector2(0.95f, 0.95f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    break;
            }
        }

        private static Image AddImage(Transform parent, string name, Sprite sprite, Vector2 min, Vector2 max, Color color, Image.Type type)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = obj.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = type;
            return img;
        }

        private static void AddKnob(Transform parent, Vector2 anchor, float size)
        {
            var obj = new GameObject("Knob", typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var img = obj.GetComponent<Image>();
            img.sprite = Circle;
            img.color = new Color(0.55f, 0.50f, 0.40f, 1f);
        }

        // ---------------------------------------------------------------- textures

        private static Font LoadHandFont()
        {
            try
            {
                var font = Font.CreateDynamicFontFromOSFont(
                    new[] { "Ink Free", "Segoe Print", "Comic Sans MS", "Arial" }, 32);
                if (font != null) return font;
            }
            catch (Exception)
            {
                // fall through to Arial
            }
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static Sprite LoadEmbedded(string suffix)
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var names = asm.GetManifestResourceNames();
                for (int i = 0; i < names.Length; i++)
                {
                    if (!names[i].EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
                    using (var stream = asm.GetManifestResourceStream(names[i]))
                    {
                        if (stream == null) continue;
                        var data = new byte[stream.Length];
                        stream.Read(data, 0, data.Length);
                        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (!tex.LoadImage(data)) continue;
                        tex.wrapMode = TextureWrapMode.Clamp;
                        tex.filterMode = FilterMode.Bilinear;
                        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    }
                }
            }
            catch (Exception ex)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Skin] embedded texture failed: " + suffix + " - " + ex.Message);
            }
            return MakePlate("missing", new Color(.5f, .45f, .38f), new Color(.2f, .18f, .15f), 0.02f);
        }

        private static Sprite MakePaper()
        {
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var rnd = new System.Random(1937);
            var px = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                    float wave = 4.5f * Mathf.Sin(x * 0.045f) + 3.0f * Mathf.Sin(y * 0.07f) +
                                 2.0f * Mathf.Sin((x + y) * 0.11f);
                    float a = Mathf.Clamp01((edge + wave) / 5f);
                    if (edge < 2f) a *= edge / 2f;

                    float grain = (float)rnd.NextDouble() - 0.5f;
                    float fiber = ((x * 13 + y * 7) % 29 == 0) ? 0.05f : 0f;
                    float shade = 0.90f + grain * 0.06f - fiber;
                    float darkEdge = Mathf.Clamp01(1f - edge / 26f) * 0.12f;
                    shade -= darkEdge;

                    px[y * size + x] = new Color(shade, shade * 0.90f, shade * 0.74f, a);
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakePlate(string name, Color top, Color bottom, float noise)
        {
            const int w = 512, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var rnd = new System.Random(7151);
            var px = new Color32[w * h];

            for (int y = 0; y < h; y++)
            {
                float t = (float)y / (h - 1);
                Color c = Color.Lerp(bottom, top, t);
                for (int x = 0; x < w; x++)
                {
                    float n = ((float)rnd.NextDouble() - 0.5f) * noise;
                    float bevel = 0f;
                    if (y > h - 5) bevel = 0.22f;
                    if (y < 4) bevel = -0.18f;
                    if (x < 3 || x > w - 4) bevel = -0.08f;
                    px[y * w + x] = new Color(
                        Mathf.Clamp01(c.r + n + bevel),
                        Mathf.Clamp01(c.g + n + bevel),
                        Mathf.Clamp01(c.b + n + bevel), 1f);
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = name;
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeCircle()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - size * 0.5f) / (size * 0.5f);
                    float dy = (y - size * 0.5f) / (size * 0.5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - d) * 14f);
                    float shade = 0.35f + Mathf.Clamp01(1f - d) * 0.5f;
                    px[y * size + x] = new Color(shade, shade * 0.93f, shade * 0.82f, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeGrille()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float v = (x % 6 < 2) ? 0.10f : 0.32f;
                    float n = ((x * 31 + y * 17) % 11) * 0.004f;
                    px[y * size + x] = new Color(v + n, v + n, v * 0.9f + n, 1f);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    /// <summary>Subtle 3D tilt + scale feedback for menu cards (unscaled time).</summary>
    public class MenuCardTilt : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Vector3 RestAngles = Vector3.zero;
        public float HoverScale = 1.045f;
        public float PressScale = 0.985f;
        public float Speed = 13f;

        private Vector3 _angles;
        private Vector3 _scale;
        private Vector3 _targetAngles;
        private Vector3 _targetScale;
        private bool _hover;

        private void Awake()
        {
            _angles = RestAngles;
            _scale = Vector3.one;
            _targetAngles = RestAngles;
            _targetScale = Vector3.one;
            transform.localEulerAngles = RestAngles;
        }

        private void OnEnable()
        {
            _angles = RestAngles;
            _scale = Vector3.one;
            _targetAngles = RestAngles;
            _targetScale = Vector3.one;
            transform.localEulerAngles = RestAngles;
            transform.localScale = Vector3.one;
        }

        public void SetRestAngles(Vector3 angles)
        {
            RestAngles = angles;
            _angles = angles;
            _targetAngles = angles;
            if (_scale == Vector3.zero) _scale = Vector3.one;
            transform.localEulerAngles = angles;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hover = true;
            _targetAngles = RestAngles * 0.35f;
            _targetScale = Vector3.one * HoverScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hover = false;
            _targetAngles = RestAngles;
            _targetScale = Vector3.one;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _targetScale = Vector3.one * PressScale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _targetScale = Vector3.one * (_hover ? HoverScale : 1f);
        }

        private void Update()
        {
            float k = 1f - Mathf.Exp(-Speed * Time.unscaledDeltaTime);
            _angles = Vector3.Lerp(_angles, _targetAngles, k);
            _scale = Vector3.Lerp(_scale, _targetScale, k);
            transform.localEulerAngles = _angles;
            transform.localScale = _scale;
        }
    }

    /// <summary>Slow rotation for reels and other physical decorations.</summary>
    public class SlowSpin : MonoBehaviour
    {
        public float DegreesPerSecond = -9f;

        private void Update()
        {
            transform.Rotate(0f, 0f, DegreesPerSecond * Time.unscaledDeltaTime);
        }
    }
}
