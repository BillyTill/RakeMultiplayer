using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RakeMultiplayer
{
    /// <summary>
    /// Black-and-white thermal silhouettes.
    ///
    /// A dedicated overlay camera (depth = main + 1, "don't clear", culling mask 0) draws only
    /// the player / animal renderers with an unlit material, so the silhouettes are written
    /// after the main camera's post stack and cannot be dimmed by scene lighting.
    /// </summary>
    public class RakeThermalTargets : MonoBehaviour
    {
        public static RakeThermalTargets Active;

        private Camera _main;
        private Camera _overlay;
        private CommandBuffer _buffer;
        private Material _playerMaterial;
        private Material _animalMaterial;
        private Texture2D _white;

        private readonly List<Renderer> _players = new List<Renderer>(16);
        private readonly List<Renderer> _animals = new List<Renderer>(32);

        private float _refresh;
        private bool _visible;
        private Transform _self;
        private bool _loggedBuffer;

        public static RakeThermalTargets Attach(Camera camera)
        {
            if (camera == null) return null;

            var rig = camera.GetComponent<RakeThermalTargets>();
            if (rig == null) rig = camera.gameObject.AddComponent<RakeThermalTargets>();
            rig.Build(camera);
            return rig;
        }

        private void Build(Camera main)
        {
            _main = main;
            Active = this;

            string[] candidates =
            {
                "Unlit/Color", "Unlit/Texture", "Mobile/Unlit", "Sprites/Default",
                "UI/Unlit/Transparent", "Particles/Additive", "Legacy Shaders/Diffuse",
                "Standard", "Legacy Shaders/Self-Illumin/Diffuse"
            };

            Shader unlit = null;
            string used = null;
            string available = string.Empty;

            for (int i = 0; i < candidates.Length; i++)
            {
                Shader found = Shader.Find(candidates[i]);
                if (found != null) available += candidates[i] + " ";
                if (unlit == null && found != null)
                {
                    unlit = found;
                    used = candidates[i];
                }
            }

            RakeMultiplayerPlugin.Log?.LogInfo("[Thermal] shaders available: " + available);

            if (unlit == null)
            {
                RakeMultiplayerPlugin.Log?.LogError("[Thermal] no unlit shader: target pass disabled");
                enabled = false;
                return;
            }

            _white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();

            // Sprites/Default multiplies by vertex colour, which character meshes often store
            // as black/transparent. Standard with emission is lighting-independent and ignores
            // vertex colours, so the silhouette is a solid hot shape.
            _playerMaterial = MakeHotMaterial(4.0f);
            _animalMaterial = MakeHotMaterial(4.0f);

            var go = new GameObject("ThermalOverlayCamera");
            go.transform.SetParent(main.transform, false);

            _overlay = go.AddComponent<Camera>();
            _overlay.CopyFrom(main);
            _overlay.clearFlags = CameraClearFlags.Depth;
            _overlay.cullingMask = 0;
            _overlay.depth = main.depth + 1f;
            _overlay.renderingPath = RenderingPath.Forward;
            _overlay.enabled = false;

            _buffer = new CommandBuffer { name = "RakeThermalTargets" };
            _overlay.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, _buffer);

            Rebuild();
            RakeMultiplayerPlugin.Log?.LogInfo("[Thermal] target pass ready (" + used + "): players=" +
                                              _players.Count + " animals=" + _animals.Count);
        }

        private Material MakeHotMaterial(float emission)
        {
            Shader standard = Shader.Find("Standard");
            Shader selfIllum = Shader.Find("Legacy Shaders/Self-Illumin/Diffuse");
            Material material;

            if (standard != null)
            {
                material = new Material(standard);
                material.SetColor("_Color", Color.white);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Glossiness", 0f);
                material.SetColor("_EmissionColor", new Color(emission, emission, emission, 1f));
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else if (selfIllum != null)
            {
                material = new Material(selfIllum);
                material.SetColor("_Color", Color.white);
                material.SetTexture("_Illum", _white);
            }
            else
            {
                material = new Material(Shader.Find("Sprites/Default")) { color = new Color(emission, emission, emission, 1f) };
            }

            material.mainTexture = _white;
            material.renderQueue = 3900;
            return material;
        }

        private void OnDestroy()
        {
            if (_overlay != null)
            {
                if (_buffer != null) _overlay.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, _buffer);
                Destroy(_overlay.gameObject);
            }

            if (_playerMaterial != null) Destroy(_playerMaterial);
            if (_animalMaterial != null) Destroy(_animalMaterial);
            if (_white != null) Destroy(_white);
            if (Active == this) Active = null;
        }

        public void SetSelf(Transform rake)
        {
            _self = rake;
        }

        public void SetVisible(bool visible)
        {
            if (_visible == visible) return;
            _visible = visible;

            if (_overlay != null) _overlay.enabled = visible;
            if (_buffer != null && !visible) _buffer.Clear();
        }

        private void Update()
        {
            _refresh -= Time.unscaledDeltaTime;
            if (_refresh <= 0f)
            {
                _refresh = 0.5f;
                Rebuild();
            }

            if (!_visible || _buffer == null) return;

            _buffer.Clear();
            Draw(_players, _playerMaterial);
            Draw(_animals, _animalMaterial);


            if (!_loggedBuffer)
            {
                _loggedBuffer = true;
                RakeMultiplayerPlugin.Log?.LogInfo("[Thermal] drawing " + _players.Count + " player / " +
                                                  _animals.Count + " animal renderers");
            }
        }

        private void Draw(List<Renderer> renderers, Material material)
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled) continue;
                _buffer.DrawRenderer(renderer, material);
            }
        }

        private void Rebuild()
        {
            _players.Clear();
            _animals.Clear();

            if (_self != null) Collect(_self, _players);

            var spawner = PlayerSpawner.Instance;
            if (spawner != null)
            {
                var local = spawner.LocalPlayer;
                if (local != null) Collect(local.transform, _players);

                foreach (Transform remote in spawner.GetAllRemotePlayerTransforms())
                {
                    if (remote != null) Collect(remote, _players);
                }
            }

            var animals = Object.FindObjectsOfType<Animal_AI>();
            for (int i = 0; i < animals.Length; i++)
            {
                if (animals[i] != null) Collect(animals[i].transform, _animals);
            }
        }

        private static void Collect(Transform root, List<Renderer> into)
        {
            var net = root.GetComponentInParent<NetworkedPlayer>();
            if (net != null && net.IsDead) return;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) continue;
                if (!into.Contains(renderer)) into.Add(renderer);
            }
        }
    }
}
