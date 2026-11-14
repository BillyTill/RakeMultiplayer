using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace RakeMultiplayer
{
    /// <summary>
    /// Optics for the Rake's first-person camera.
    ///
    /// The game already ships the CameraFilterPack image effects, so the look is assembled
    /// from them instead of bundling a second post-processing stack:
    ///  - a wide eye with a dirty-glass vignette;
    ///  - three vision modes: natural colour, thermal (grayscale + amber tint + heat
    ///    shimmer) and night vision (green intensifier);
    ///  - dynamic grain that spikes when the Rake is hit and sits low but visible when a
    ///    hunter is in sight (chasing or being watched).
    ///
    /// Nothing here draws a HUD; it only changes the camera image.
    /// </summary>
    public class RakeCameraEffects : MonoBehaviour
    {
        public enum VisionMode
        {
            Color = 0,
            Thermal = 1,
            Night = 2
        }

        public static RakeCameraEffects Active;

        /// <summary>Requested first-person field of view. The Rake sees much more than a hunter.</summary>
        public static float FirstPersonFov = 105f;

        public const float ThirdPersonFov = 68f;

        private const float DefaultGrain = 0.016f;

        private static Sprite _vignetteSprite;

        private Camera _camera;
        private VisionMode _mode = VisionMode.Color;

        private float _thermalBrightness = 0.75f;
        private float _nightBrightness = 1.15f;
        private float _colorBrightness = 1.00f;
        private float _thermalHeat = 0.30f;
        private float _thermalContrast = 0.50f;
        private float _nightContrast = 1.15f;

        private float _grainBase = DefaultGrain;

        private CameraFilterPack_Color_GrayScale _gray;
        private CameraFilterPack_Color_RGB _tint;
        private CameraFilterPack_Distortion_Heat _heat;
        private CameraFilterPack_Color_Contrast _contrast;
        private CameraFilterPack_Colors_Brightness _brightness;
        private CameraFilterPack_Color_Noise _grain;

        private GameObject _overlay;
        private Image _vignette;
        private Image _hitFlash;

        private float _damage;
        private float _sight;
        private float _sightTarget;

        public VisionMode Mode { get { return _mode; } }
        public bool HasAnyEffect { get; private set; }

        // ---------------------------------------------------------------- setup

        public static RakeCameraEffects Attach(Camera camera)
        {
            if (camera == null) return null;

            var fx = camera.GetComponent<RakeCameraEffects>();
            if (fx == null) fx = camera.gameObject.AddComponent<RakeCameraEffects>();
            fx.Build(camera);
            return fx;
        }

        private void Build(Camera camera)
        {
            _camera = camera;
            Active = this;

            // Order matters: lens first, grain last, so the noise is not distorted away.
            _gray = AddEffect<CameraFilterPack_Color_GrayScale>("CameraFilterPack/Color_GrayScale");
            _tint = AddEffect<CameraFilterPack_Color_RGB>("CameraFilterPack/Color_RGB");
            _heat = AddEffect<CameraFilterPack_Distortion_Heat>("CameraFilterPack/Distortion_Heat");
            _contrast = AddEffect<CameraFilterPack_Color_Contrast>("CameraFilterPack/Color_Contrast");
            _brightness = AddEffect<CameraFilterPack_Colors_Brightness>("CameraFilterPack/Colors_Brightness");
            _grain = AddEffect<CameraFilterPack_Color_Noise>("CameraFilterPack/Color_Noise");

            HasAnyEffect = _grain != null;

            BuildVignette();
            SetMode(VisionMode.Color, false);
            ApplyValues();

            RakeMultiplayerPlugin.Log?.LogInfo("[RakeFX] optics ready: grain=" + (_grain != null) +
                                               " thermal=" + (_gray != null && _tint != null && _heat != null) +
                                               " brightness=" + (_brightness != null) +
                                               " contrast=" + (_contrast != null));
        }

        private T AddEffect<T>(string shaderName) where T : MonoBehaviour
        {
            // The pack's scripts look their shader up themselves and silently fall back to a
            // plain blit when it is missing, so check first and skip dead weight.
            if (Shader.Find(shaderName) == null)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[RakeFX] shader not in this build: " + shaderName);
                return null;
            }

            return gameObject.AddComponent<T>();
        }

        private void BuildVignette()
        {
            // A screen-space overlay canvas has to be a root object: as a child of the camera
            // (which itself rides the head bone) it is never rendered.
            _overlay = new GameObject("RakeLensOverlay");

            var canvas = _overlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30500; // under the HUD (31000), above the world

            var go = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_overlay.transform, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _vignette = go.GetComponent<Image>();
            _vignette.sprite = VignetteSprite();
            _vignette.color = Color.white;

            // Hit flash reuses the vignette shape: an edge-lit red pulse, not a flat filter.
            var flashGo = new GameObject("HitFlash", typeof(RectTransform), typeof(Image));
            flashGo.transform.SetParent(_overlay.transform, false);

            var flashRt = (RectTransform)flashGo.transform;
            flashRt.anchorMin = Vector2.zero;
            flashRt.anchorMax = Vector2.one;
            flashRt.offsetMin = Vector2.zero;
            flashRt.offsetMax = Vector2.zero;

            _hitFlash = flashGo.GetComponent<Image>();
            _hitFlash.sprite = VignetteSprite();
            _hitFlash.color = new Color(0.85f, 0.06f, 0.03f, 0f);
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
            if (_overlay != null) Destroy(_overlay);
        }

        // ---------------------------------------------------------------- runtime

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            if (RakeThermalTargets.Active != null)
            {
                RakeThermalTargets.Active.SetVisible(_mode == VisionMode.Thermal);
            }

            _damage = Mathf.MoveTowards(_damage, 0f, dt * 1.6f);

            if (_sightTarget > 0.001f)
                _sight = Mathf.Lerp(_sight, _sightTarget, 1f - Mathf.Exp(-3.5f * dt));
            else
                _sight = Mathf.MoveTowards(_sight, 0f, dt * 0.9f);

            ApplyValues();
        }

        /// <summary>Strong grain burst; amount is the damage that was dealt.</summary>
        public void PulseDamage(int amount)
        {
            float impact = Mathf.Clamp(amount / 90f, 0.3f, 1f);
            _damage = Mathf.Clamp01(_damage + impact);
        }

        /// <summary>
        /// Small rotational jolt while the hit is fresh. The walking bob comes from the head
        /// bone itself; this is only the impact reaction.
        /// </summary>
        public Vector3 ShakeEuler()
        {
            if (_damage <= 0.01f) return Vector3.zero;

            float strength = _damage * 1.7f;
            return new Vector3(
                Mathf.Sin(Time.unscaledTime * 71f) * strength,
                Mathf.Cos(Time.unscaledTime * 63f) * strength,
                Mathf.Sin(Time.unscaledTime * 89f) * strength * 0.6f);
        }

        /// <summary>0..1: a hunter is in front of the Rake or is looking at it.</summary>
        public void SetSightTension(float value)
        {
            _sightTarget = Mathf.Clamp01(value);
        }

        public void SetMode(VisionMode mode, bool announce = true)
        {
            _mode = mode;

            // Colour is the plain look with the optics on top; the other two are monochrome
            // with a tint, which is also how a real thermal / intensifier tube reads.
            bool mono = mode != VisionMode.Color;

            if (_gray != null) _gray.enabled = mono;
            if (_tint != null) _tint.enabled = mode == VisionMode.Night;
            if (_heat != null) _heat.enabled = mode == VisionMode.Thermal;
            if (_contrast != null) _contrast.enabled = true;
            if (_brightness != null) _brightness.enabled = true;

            if (mode == VisionMode.Thermal)
            {
                // Black and white: the background is flattened so scene lighting cannot
                // decide the image, and the targets stay pure white on top of it.
                SetHeat(_thermalHeat);
                _grainBase = 0.008f;
                SetContrast(_thermalContrast);
                SetBrightness(_thermalBrightness);
            }
            else if (mode == VisionMode.Night)
            {
                _grainBase = DefaultGrain;
                SetTint(new Color(0.38f, 1.00f, 0.46f, 1f));
                SetHeat(0f);
                SetContrast(_nightContrast);
                SetBrightness(_nightBrightness);
            }
            else
            {
                _grainBase = DefaultGrain;
                SetContrast(1.02f);
                SetBrightness(_colorBrightness);
            }

            if (announce)
            {
                DebugConsole.Print("[Rake] Vision: " + mode.ToString().ToUpperInvariant() + " (F to switch)");
            }
        }

        public void CycleMode()
        {
            int next = ((int)_mode + 1) % 3;
            SetMode((VisionMode)next);
        }

        private void ApplyValues()
        {
            if (_hitFlash != null)
            {
                float flash = _damage * 0.55f;
                _hitFlash.color = new Color(0.85f, 0.06f, 0.03f, flash);
            }

            if (_grain != null)
            {
                float grain = _grainBase + _damage * 0.38f + _sight * 0.07f;
                _grain.Noise = grain;
                CameraFilterPack_Color_Noise.ChangeNoise = grain;
            }

        }

        private void SetTint(Color color)
        {
            if (_tint == null) return;
            _tint.ColorRGB = color;
            CameraFilterPack_Color_RGB.ChangeColorRGB = color;
        }

        private void SetHeat(float value)
        {
            if (_heat == null) return;
            _heat.Distortion = value;
            CameraFilterPack_Distortion_Heat.ChangeDistortion = value;
        }

        private void SetContrast(float value)
        {
            if (_contrast == null) return;
            _contrast.Contrast = value;
            CameraFilterPack_Color_Contrast.ChangeContrast = value;
        }

        private void SetBrightness(float value)
        {
            if (_brightness == null) return;
            _brightness._Brightness = value;
            CameraFilterPack_Colors_Brightness.ChangeBrightness = value;
        }

        // ---------------------------------------------------------------- dev bridge

        /// <summary>Live tuning from the local MCP bridge: /fx?mode=thermal&amp;lens=0.2...</summary>
        public static string BridgeApply(string mode, string grain, string fov, string damage,
            string bright, string heat, string contrast)
        {
            var fx = Active;
            if (fx == null) return "{\"ok\":false,\"error\":\"rake camera is not active\"}";

            float value;

            if (!string.IsNullOrEmpty(mode))
            {
                if (mode.Equals("color", StringComparison.OrdinalIgnoreCase)) fx.SetMode(VisionMode.Color, false);
                else if (mode.Equals("thermal", StringComparison.OrdinalIgnoreCase)) fx.SetMode(VisionMode.Thermal, false);
                else if (mode.Equals("night", StringComparison.OrdinalIgnoreCase)) fx.SetMode(VisionMode.Night, false);
            }

            if (TryParse(grain, out value)) fx._grainBase = Mathf.Clamp(value, 0f, 1f);
            if (TryParse(fov, out value)) FirstPersonFov = Mathf.Clamp(value, 60f, 130f);
            if (TryParse(damage, out value)) fx._damage = Mathf.Clamp01(value);
            if (TryParse(bright, out value))
            {
                value = Mathf.Clamp(value, 0f, 4f);
                if (fx._mode == VisionMode.Night) fx._nightBrightness = value;
                else if (fx._mode == VisionMode.Thermal) fx._thermalBrightness = value;
                else fx._colorBrightness = value;
            }
            if (TryParse(heat, out value)) fx._thermalHeat = Mathf.Clamp(value, 0f, 1f);
            if (TryParse(contrast, out value))
            {
                value = Mathf.Clamp(value, 0.5f, 4f);
                if (fx._mode == VisionMode.Night) fx._nightContrast = value;
                else fx._thermalContrast = value;
            }

            if (!string.IsNullOrEmpty(bright) || !string.IsNullOrEmpty(heat) || !string.IsNullOrEmpty(contrast))
            {
                fx.SetMode(fx._mode, false);
            }

            return "{\"ok\":true,\"mode\":\"" + fx._mode.ToString().ToLowerInvariant() + "\"," +
                   "\"grain\":" + fx._grainBase.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                   "\"fov\":" + FirstPersonFov.ToString("0.#", CultureInfo.InvariantCulture) + "," +
                   "\"effects\":" + (fx.HasAnyEffect ? "true" : "false") + "}";
        }

        private static bool TryParse(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        // ---------------------------------------------------------------- texture

        /// <summary>Dirty-glass vignette: soft corners plus a few dust specks and smudges.</summary>
        private static Sprite VignetteSprite()
        {
            if (_vignetteSprite != null) return _vignetteSprite;

            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            var px = new Color32[size * size];
            var rnd = new System.Random(8128);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny) * 0.72f;

                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 1.05f, r));
                    px[y * size + x] = Alpha(edge * 0.70f);
                }
            }

            // Dust and fingerprints: darker specks, mostly towards the edges.
            for (int i = 0; i < 90; i++)
            {
                float cx = (float)rnd.NextDouble() * size;
                float cy = (float)rnd.NextDouble() * size;
                float radius = 1.5f + (float)rnd.NextDouble() * 5.5f;
                float strength = 0.035f + (float)rnd.NextDouble() * 0.075f;

                int x0 = Mathf.FloorToInt(cx - radius - 1f);
                int x1 = Mathf.CeilToInt(cx + radius + 1f);
                int y0 = Mathf.FloorToInt(cy - radius - 1f);
                int y1 = Mathf.CeilToInt(cy + radius + 1f);

                for (int y = y0; y <= y1; y++)
                {
                    if (y < 0 || y >= size) continue;
                    for (int x = x0; x <= x1; x++)
                    {
                        if (x < 0 || x >= size) continue;
                        float dx = x - cx;
                        float dy = y - cy;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                        if (d > 1f) continue;

                        int index = y * size + x;
                        float alpha = px[index].a / 255f + strength * (1f - d) * (1f - d);
                        px[index] = Alpha(alpha);
                    }
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            _vignetteSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _vignetteSprite;
        }

        private static Color32 Alpha(float alpha)
        {
            return new Color32(0, 0, 0, (byte)(Mathf.Clamp01(alpha) * 255f));
        }
    }
}
