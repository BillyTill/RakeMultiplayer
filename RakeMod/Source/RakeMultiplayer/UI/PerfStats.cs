using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RakeMultiplayer.UI
{
    /// <summary>
    /// Frame statistics overlay (F8): frame, CPU, GPU and render time in milliseconds,
    /// plus triangles, draw calls and visible objects.
    ///
    /// Unity 5 has no runtime GPU timer, so the numbers are measured like this:
    ///   FRAME  wall clock between two frames;
    ///   CPU    from the start of the frame to the end of the camera rendering;
    ///   REND   CPU time spent submitting the cameras;
    ///   GPU~   FRAME - CPU, i.e. the part of the frame spent waiting for the device.
    /// With V-Sync on the frame is capped, so GPU~ is an upper bound rather than a
    /// measurement. Triangles and draw calls are summed from the renderers the cameras
    /// kept this frame; terrain is not included (Unity 5 exposes no terrain count).
    /// </summary>
    public class PerfStats : MonoBehaviour
    {
        public static PerfStats Instance;

        private const float SampleInterval = 0.5f;
        private const float RescanInterval = 8f;
        private const float Smoothing = 0.18f;

        private static readonly Color Bone = new Color(0.847f, 0.816f, 0.745f, 0.95f);
        private static readonly System.Diagnostics.Stopwatch Clock =
            System.Diagnostics.Stopwatch.StartNew();

        private bool _visible;
        private Canvas _canvas;
        private Text _text;
        private Font _font;

        private float _fpsWindow;
        private int _fpsFrames;
        private float _fps;

        private double _frameStartMs;
        private double _renderStartMs = -1.0;
        private double _lastRenderEndMs = -1.0;
        private double _renderMs;

        private Camera _renderingCamera;
        private int _cameraCount;
        private readonly System.Collections.Generic.Dictionary<Camera, float> _cameraMs =
            new System.Collections.Generic.Dictionary<Camera, float>();

        private float _frameMs;
        private float _cpuMs;
        private float _gpuMs;
        private float _renderFrameMs;
        private bool _smoothed;

        private float _sampleTimer;
        private float _rescanTimer;

        private double _lastCameraFrameMs;
        private int _lastCameraCount;
        private readonly System.Collections.Generic.Dictionary<Camera, float> _lastCameraTimes =
            new System.Collections.Generic.Dictionary<Camera, float>();

        private Renderer[] _renderers = new Renderer[0];
        private MeshFilter[] _filters = new MeshFilter[0];
        private Graphic[] _graphics = new Graphic[0];
        private Terrain[] _terrains = new Terrain[0];

        private readonly Dictionary<Mesh, int> _triangleCache = new Dictionary<Mesh, int>();

        private int _triangles;
        private int _draws;
        private int _meshDraws;
        private int _objects;
        private int _uiDraws;
        private float _terrainTriangles;

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Camera.onPreRender += OnCameraPreRender;
            Camera.onPostRender += OnCameraPostRender;
        }

        private void OnDestroy()
        {
            Camera.onPreRender -= OnCameraPreRender;
            Camera.onPostRender -= OnCameraPostRender;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            MeasureFrame();

            float dt = Time.unscaledDeltaTime;

            _fpsFrames++;
            _fpsWindow += dt;
            if (_fpsWindow >= 0.5f)
            {
                _fps = _fpsFrames / _fpsWindow;
                _fpsFrames = 0;
                _fpsWindow = 0f;
            }

            if (Input.GetKeyDown(KeyCode.F8) && !DebugConsole.ConsoleOpen)
            {
                SetVisible(!_visible);
            }

            if (!_visible) return;

            _sampleTimer -= dt;
            if (_sampleTimer > 0f) return;
            _sampleTimer = SampleInterval;

            _rescanTimer -= SampleInterval;
            if (_rescanTimer <= 0f)
            {
                _rescanTimer = RescanInterval;
                Rescan();
            }

            Count();
            RefreshText();
        }

        // ------------------------------------------------------------------ timing

        private void OnCameraPreRender(Camera camera)
        {
            if (camera == null) return;

            _renderStartMs = Clock.Elapsed.TotalMilliseconds;
            _renderingCamera = camera;
        }

        private void OnCameraPostRender(Camera camera)
        {
            if (camera == null) return;

            double now = Clock.Elapsed.TotalMilliseconds;
            if (_renderStartMs >= 0.0)
            {
                double took = now - _renderStartMs;
                _renderMs += took;

                Camera rendered = _renderingCamera != null ? _renderingCamera : camera;
                float known;
                _cameraMs.TryGetValue(rendered, out known);
                _cameraMs[rendered] = known + (float)took;
                _cameraCount++;
            }

            _renderStartMs = -1.0;
            _renderingCamera = null;
            _lastRenderEndMs = now;
        }

        private void MeasureFrame()
        {
            double now = Clock.Elapsed.TotalMilliseconds;

            if (_frameStartMs > 0.0 && _lastRenderEndMs > _frameStartMs)
            {
                double frame = now - _frameStartMs;
                double cpu = _lastRenderEndMs - _frameStartMs;
                if (frame < 0.01) frame = 0.01;
                if (cpu < 0.0) cpu = 0.0;
                if (cpu > frame) cpu = frame;

                float frameMs = (float)frame;
                float cpuMs = (float)cpu;
                float gpuMs = frameMs - cpuMs;
                float renderMs = (float)_renderMs;

                if (!_smoothed)
                {
                    _frameMs = frameMs;
                    _cpuMs = cpuMs;
                    _gpuMs = gpuMs;
                    _renderFrameMs = renderMs;
                    _smoothed = true;
                }
                else
                {
                    _frameMs += (frameMs - _frameMs) * Smoothing;
                    _cpuMs += (cpuMs - _cpuMs) * Smoothing;
                    _gpuMs += (gpuMs - _gpuMs) * Smoothing;
                    _renderFrameMs += (renderMs - _renderFrameMs) * Smoothing;
                }
            }

            _lastCameraFrameMs = _renderMs;
            _lastCameraCount = _cameraCount;
            _lastCameraTimes.Clear();
            foreach (var pair in _cameraMs) _lastCameraTimes.Add(pair.Key, pair.Value);
            _cameraMs.Clear();
            _cameraCount = 0;

            _frameStartMs = now;
            _renderMs = 0.0;
            _lastRenderEndMs = -1.0;
            _renderStartMs = -1.0;
            _renderingCamera = null;
        }

        // ------------------------------------------------------------------ toggling

        public void SetVisible(bool visible)
        {
            _visible = visible;

            if (visible && _canvas == null) Build();
            if (_canvas != null) _canvas.gameObject.SetActive(visible);

            if (!visible) return;

            // Sample at once so the panel never shows a stale frame.
            _sampleTimer = 0f;
            _rescanTimer = 0f;
        }

        public bool IsVisible { get { return _visible; } }

        // ------------------------------------------------------------------ counting

        private void Rescan()
        {
            Renderer[] renderers = FindObjectsOfType<Renderer>();
            _renderers = renderers;
            _filters = new MeshFilter[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                _filters[i] = renderers[i] == null ? null : renderers[i].GetComponent<MeshFilter>();
            }

            _graphics = FindObjectsOfType<Graphic>();
            _terrains = FindObjectsOfType<Terrain>();
        }

        private void Count()
        {
            int triangles = 0;
            int draws = 0;
            int objects = 0;

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.isVisible) continue;

                GameObject go = renderer.gameObject;
                if (go == null || !go.activeInHierarchy) continue;

                objects++;

                Mesh mesh = null;
                SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
                if (skinned != null) mesh = skinned.sharedMesh;
                else if (_filters[i] != null) mesh = _filters[i].sharedMesh;

                if (mesh == null)
                {
                    // Particles, trails, lines: they still cost a draw, but no mesh count.
                    draws++;
                    continue;
                }

                triangles += Triangles(mesh);
                int subMeshes = mesh.subMeshCount;
                draws += subMeshes > 0 ? subMeshes : 1;
            }

            // uGUI has no Renderer, but every visible graphic is one more draw call.
            int ui = 0;
            for (int i = 0; i < _graphics.Length; i++)
            {
                Graphic graphic = _graphics[i];
                if (graphic == null || !graphic.isActiveAndEnabled) continue;
                if (graphic.canvas == null || !graphic.canvas.enabled) continue;
                ui++;
            }

            float terrain = 0f;
            for (int i = 0; i < _terrains.Length; i++)
            {
                Terrain t = _terrains[i];
                if (t == null || t.terrainData == null || !t.enabled) continue;
                float side = t.terrainData.heightmapResolution - 1f;
                terrain += side * side * 2f; // full detail grid, for reference only
            }

            _triangles = triangles;
            _meshDraws = draws;
            _draws = draws + ui;
            _objects = objects;
            _uiDraws = ui;
            _terrainTriangles = terrain;
        }

        private int Triangles(Mesh mesh)
        {
            int cached;
            if (_triangleCache.TryGetValue(mesh, out cached)) return cached;

            cached = mesh.triangles.Length / 3;
            if (_triangleCache.Count > 4096) _triangleCache.Clear(); // scene churn
            _triangleCache[mesh] = cached;
            return cached;
        }

        // ------------------------------------------------------------------ panel

        private void RefreshText()
        {
            if (_text == null) return;

            _text.text = string.Format(
                "{0,-6}{1,7}\n{2,-6}{3,7:0.0}\n{4,-6}{5,7:0.0}\n{6,-6}{7,7:0.0}\n" +
                "{8,-6}{9,7:0.0}\n{10,-6}{11,7}\n{12,-6}{13,7}\n{14,-6}{15,7}\n{16,-6}{17,7}\n" +
                "{18,-6}{19,7}",
                "FPS", Mathf.RoundToInt(_fps),
                "FRAME", _frameMs,
                "CPU", _cpuMs,
                "GPU~", _gpuMs,
                "REND", _renderFrameMs,
                "TRIS", Short(_triangles),
                "DRAW~", Short(_meshDraws),
                "UI", Short(_uiDraws),
                "OBJS", Short(_objects),
                "CAMS", _lastCameraCount);
        }

        private static string Short(int value)
        {
            if (value >= 1000000) return (value / 1000000f).ToString("0.00") + "M";
            if (value >= 10000) return (value / 1000f).ToString("0.0") + "k";
            return value.ToString();
        }

        private void Build()
        {
            _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Consolas", "Lucida Console", "Courier New" }, 15);
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var canvasObj = new GameObject("RakePerfCanvas", typeof(Canvas), typeof(CanvasScaler));
            DontDestroyOnLoad(canvasObj);

            _canvas = canvasObj.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32000;

            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var plate = new GameObject("Plate", typeof(RectTransform), typeof(Image));
            plate.transform.SetParent(canvasObj.transform, false);

            var rt = (RectTransform)plate.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(26f, -26f);
            rt.sizeDelta = new Vector2(198f, 206f);

            var img = plate.GetComponent<Image>();
            img.sprite = PauseMenuSkin.Skin.Plate(198, 206, 63);
            img.color = new Color(0.027f, 0.035f, 0.031f, 0.9f);

            var textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(plate.transform, false);

            var textRt = (RectTransform)textObj.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(16f, 12f);
            textRt.offsetMax = new Vector2(-12f, -12f);

            _text = textObj.AddComponent<Text>();
            _text.font = _font;
            _text.fontSize = 15;
            _text.color = Bone;
            _text.alignment = TextAnchor.UpperLeft;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.text = "";
        }

        // ------------------------------------------------------------------ dev bridge

        /// <summary>Dev bridge: the same numbers as the panel, as JSON.</summary>
        public string BridgeStats()
        {
            return "{\"ok\":true," +
                   "\"visible\":" + (_visible ? "true" : "false") + "," +
                   "\"fps\":" + Mathf.RoundToInt(_fps) + "," +
                   "\"frame\":" + _frameMs.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "," +
                   "\"cpu\":" + _cpuMs.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "," +
                   "\"gpu\":" + _gpuMs.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "," +
                   "\"render\":" + _renderFrameMs.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "," +
                   "\"tris\":" + _triangles + "," +
                   "\"draws\":" + _draws + "," +
                   "\"ui\":" + _uiDraws + "," +
                   "\"objects\":" + _objects + "," +
                   "\"terrain\":" + Mathf.RoundToInt(_terrainTriangles) + "," +
                   "\"cameras\":" + _lastCameraCount + "," +
                   "\"cams\":[" + CameraList() + "]}";
        }

        private string CameraList()
        {
            var sb = new System.Text.StringBuilder();
            bool first = true;

            foreach (var pair in _lastCameraTimes)
            {
                Camera cam = pair.Key;
                if (cam == null) continue;
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"name\":\"").Append(cam.name);
                sb.Append("\",\"depth\":").Append(cam.depth);
                sb.Append(",\"ms\":").Append(pair.Value.ToString("0.00",
                    System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(",\"enabled\":").Append(cam.enabled ? "true" : "false").Append('}');
            }

            return sb.ToString();
        }
    }
}
