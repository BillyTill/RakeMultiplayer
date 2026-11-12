using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Debug highlight for the Rake: a bright point light plus a glowing marker above its
    /// head, so it can be found quickly. Everything is parented to the Rake and destroyed
    /// when the highlight is turned off, so nothing about the enemy itself is modified.
    /// </summary>
    internal static class RakeHighlight
    {
        private static GameObject _marker;

        /// <summary>True while a marker object exists (it dies with the Rake on scene reload).</summary>
        public static bool IsActive { get { return _marker != null; } }

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
                DebugConsole.Print("rake highlight: Rake not found in this scene");
                return;
            }

            _marker = new GameObject("RakeHighlightMarker");
            _marker.transform.SetParent(rake, false);
            _marker.transform.localPosition = new Vector3(0f, 2.1f, 0f);
            _marker.transform.localRotation = Quaternion.identity;

            // Light so the Rake itself glows in the environment
            var light = _marker.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.42f, 0.12f);
            light.range = 16f;
            light.intensity = 5f;
            light.shadows = LightShadows.None;

            // Glowing marker above the head so it can be spotted from a distance
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "RakeHighlightBeacon";
            var collider = sphere.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);

            sphere.transform.SetParent(_marker.transform, false);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localScale = Vector3.one * 0.4f;

            var renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = RemotePlayerModelData.FindSkinShader();
                if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");

                var material = new Material(shader);
                material.name = "RakeHighlightBeacon";
                material.color = new Color(1f, 0.35f, 0.05f);
                try
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.1f) * 2f);
                }
                catch
                {
                    // shader without emission - the flat colour is still visible
                }
                renderer.material = material;
            }

            DebugConsole.Print("rake highlight ON at " + rake.position.ToString("F1") +
                               " (marker + light). 'rake highlight off' to stop.");
        }

        public static void Clear()
        {
            if (_marker != null) UnityEngine.Object.Destroy(_marker);
            _marker = null;
        }
    }
}
