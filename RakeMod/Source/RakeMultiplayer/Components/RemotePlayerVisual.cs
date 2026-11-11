using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Visible third-person representation for a remote player.
    ///
    /// The game ships only a first-person player prefab (camera + hands + weapons),
    /// so a remote clone renders nothing useful (or worse, duplicated first-person hands).
    /// This component hides every renderer on the remote clone and builds a simple
    /// coloured humanoid proxy so players can actually see each other.
    /// </summary>
    public class RemotePlayerVisual : MonoBehaviour
    {
        private static readonly Color[] Palette =
        {
            new Color(0.95f, 0.35f, 0.25f), // P1 red-orange
            new Color(0.25f, 0.60f, 0.95f), // P2 blue
            new Color(0.35f, 0.85f, 0.40f), // P3 green
            new Color(0.95f, 0.85f, 0.25f), // P4 yellow
        };

        private Transform _bodyRoot;
        private int _playerId;

        /// <summary>
        /// Build (or rebuild) the proxy for a remote player clone.
        /// </summary>
        public static RemotePlayerVisual Attach(GameObject playerObject, int playerId)
        {
            if (playerObject == null) return null;

            // Remove a previous proxy (e.g. re-spawn after scene reload)
            var existing = playerObject.GetComponentInChildren<RemotePlayerVisual>();
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            HideOriginalVisuals(playerObject);

            var go = new GameObject("RemotePlayerVisual");
            go.transform.SetParent(playerObject.transform, false);

            var visual = go.AddComponent<RemotePlayerVisual>();
            visual._playerId = playerId;

            // The player root is usually at eye height, so the proxy must be lowered
            // until its feet touch the ground.
            visual.Build(playerId, ComputeFootOffset(playerObject));
            return visual;
        }

        /// <summary>
        /// Vertical offset (relative to the player root) of the feet.
        /// Derived from the CharacterController; falls back to a ground raycast.
        /// </summary>
        private static float ComputeFootOffset(GameObject playerObject)
        {
            if (playerObject == null) return 0f;

            var controller = playerObject.GetComponent<CharacterController>();
            if (controller == null) controller = playerObject.GetComponentInChildren<CharacterController>();
            if (controller != null)
            {
                // bottom of the capsule relative to the transform
                return controller.center.y - controller.height * 0.5f;
            }

            var origin = playerObject.transform.position + Vector3.up * 0.2f;
            RaycastHit hit;
            if (Physics.Raycast(origin, Vector3.down, out hit, 6f))
            {
                return hit.point.y - playerObject.transform.position.y;
            }

            // Last resort: assume the root sits at eye height
            return -1.6f;
        }

        /// <summary>
        /// Hide first-person hands, weapons and any other renderer of the original prefab.
        /// Cameras and lights are disabled too so a remote clone cannot affect the view.
        /// </summary>
        private static void HideOriginalVisuals(GameObject playerObject)
        {
            foreach (var renderer in playerObject.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                renderer.enabled = false;
            }

            foreach (var camera in playerObject.GetComponentsInChildren<Camera>(true))
            {
                if (camera == null) continue;
                camera.enabled = false;
            }

            foreach (var listener in playerObject.GetComponentsInChildren<AudioListener>(true))
            {
                if (listener == null) continue;
                listener.enabled = false;
            }

            // Remote flashlights should not illuminate the local world
            foreach (var light in playerObject.GetComponentsInChildren<Light>(true))
            {
                if (light == null) continue;
                light.enabled = false;
            }
        }

        private void Build(int playerId, float footOffset)
        {
            Color color = Palette[Mathf.Abs(playerId - 1) % Palette.Length];
            Color dark = Color.Lerp(color, Color.black, 0.35f);

            _bodyRoot = transform;
            // Feet are modeled at local y = 0, so shift the whole proxy to the ground
            _bodyRoot.localPosition = new Vector3(0f, footOffset, 0f);
            _bodyRoot.localRotation = Quaternion.identity;

            // Torso
            AddPrimitive(PrimitiveType.Capsule, "Torso", new Vector3(0f, 0.90f, 0f),
                new Vector3(0.45f, 0.55f, 0.40f), color, Quaternion.identity);

            // Head
            AddPrimitive(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.65f, 0f),
                new Vector3(0.32f, 0.32f, 0.32f), color, Quaternion.identity);

            // Visor (shows which way the player is facing)
            AddPrimitive(PrimitiveType.Cube, "Visor", new Vector3(0f, 1.66f, 0.17f),
                new Vector3(0.22f, 0.08f, 0.06f), dark, Quaternion.identity);

            // Legs
            AddPrimitive(PrimitiveType.Capsule, "LegL", new Vector3(-0.14f, 0.38f, 0f),
                new Vector3(0.16f, 0.38f, 0.16f), dark, Quaternion.identity);
            AddPrimitive(PrimitiveType.Capsule, "LegR", new Vector3(0.14f, 0.38f, 0f),
                new Vector3(0.16f, 0.38f, 0.16f), dark, Quaternion.identity);

            // Arms
            AddPrimitive(PrimitiveType.Capsule, "ArmL", new Vector3(-0.38f, 1.05f, 0f),
                new Vector3(0.13f, 0.30f, 0.13f), color, Quaternion.Euler(0f, 0f, 12f));
            AddPrimitive(PrimitiveType.Capsule, "ArmR", new Vector3(0.38f, 1.05f, 0f),
                new Vector3(0.13f, 0.30f, 0.13f), color, Quaternion.Euler(0f, 0f, -12f));

            // Small "team" bar above the head for long-distance spotting
            AddPrimitive(PrimitiveType.Cube, "Tag", new Vector3(0f, 1.98f, 0f),
                new Vector3(0.42f, 0.06f, 0.06f), color, Quaternion.identity);

            RakeMultiplayerPlugin.Log?.LogInfo($"[Visual] Built remote player proxy for P{playerId}");
        }

        private void AddPrimitive(PrimitiveType type, string name, Vector3 localPos,
            Vector3 localScale, Color color, Quaternion localRot)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(_bodyRoot, false);
            part.transform.localPosition = localPos;
            part.transform.localRotation = localRot;
            part.transform.localScale = localScale;

            // Proxy must never affect physics or AI raycasts
            var collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Diffuse");
                if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");

                if (shader != null)
                {
                    var material = new Material(shader);
                    material.color = color;
                    renderer.material = material;
                }
                else
                {
                    renderer.material.color = color; // fall back to the primitive's default material
                }

                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        private void LateUpdate()
        {
            // Keep the proxy's feet on the ground even if the player root sits at eye
            // height, and on stairs/slopes. The proxy has no colliders of its own.
            var root = transform.parent;
            if (root == null) return;

            Vector3 origin = root.position + Vector3.up * 0.3f;
            RaycastHit hit;
            if (Physics.Raycast(origin, Vector3.down, out hit, 10f))
            {
                float targetY = hit.point.y - root.position.y;
                Vector3 local = transform.localPosition;
                local.y = Mathf.Lerp(local.y, targetY, Time.deltaTime * 12f);
                transform.localPosition = local;
            }
        }

        /// <summary>Update the proxy colour (kept for future team colours).</summary>
        public void SetPlayerId(int playerId)
        {
            if (_playerId == playerId) return;
            _playerId = playerId;
            Destroy(gameObject);
            Attach(transform.parent != null ? transform.parent.gameObject : null, playerId);
        }
    }
}
