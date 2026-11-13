using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Free-fly noclip for the local hunter. The old console command only disabled the
    /// CharacterController, which also stopped PlayerMovement from moving at all. This
    /// component takes over locomotion: collision and gravity are off, the camera look
    /// keeps working, and WASD/Space/Ctrl/Shift fly the body through the level.
    /// </summary>
    public class NoclipController : MonoBehaviour
    {
        public static bool Toggle(Transform player)
        {
            if (player == null) return false;

            var noclip = player.GetComponent<NoclipController>();
            if (noclip == null) noclip = player.gameObject.AddComponent<NoclipController>();

            noclip.SetEnabled(!noclip.IsEnabled);
            return noclip.IsEnabled;
        }

        public static bool IsActiveOn(Transform player)
        {
            if (player == null) return false;
            var noclip = player.GetComponent<NoclipController>();
            return noclip != null && noclip.IsEnabled;
        }

        public bool IsEnabled { get { return _enabled; } }

        private CharacterController _controller;
        private PlayerMovement _movement;
        private FPSController _fps;
        private bool _enabled;
        private bool _controllerWasEnabled;
        private bool _movementWasEnabled;
        private Vector3 _lastGroundedPosition;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _movement = GetComponent<PlayerMovement>();
            _fps = GetComponent<FPSController>();
        }

        public void SetEnabled(bool value)
        {
            if (_enabled == value) return;

            if (value)
            {
                if (PlayerRagdoll.IsActive) return;
                if (_fps != null && _fps.Health < 0) return;

                _lastGroundedPosition = transform.position;

                if (_controller == null) _controller = GetComponent<CharacterController>();
                if (_movement == null) _movement = GetComponent<PlayerMovement>();
                if (_fps == null) _fps = GetComponent<FPSController>();

                if (_controller != null)
                {
                    _controllerWasEnabled = _controller.enabled;
                    _controller.enabled = false;
                }

                if (_movement != null)
                {
                    _movementWasEnabled = _movement.enabled;
                    _movement.enabled = false;
                }

                _enabled = true;
                RakeMultiplayerPlugin.Log?.LogInfo("[Noclip] ON");
            }
            else
            {
                _enabled = false;

                // If the player flew into a wall, put the body on the nearest floor
                // before the CharacterController starts colliding again.
                PlaceOnGround();

                if (_controller != null)
                {
                    _controller.enabled = _controllerWasEnabled;
                }

                if (_movement != null)
                {
                    _movement.enabled = _movementWasEnabled;
                    if (_movementWasEnabled) _movement.UnFreezePlayer();
                }

                RakeMultiplayerPlugin.Log?.LogInfo("[Noclip] OFF");
            }
        }

        private void Update()
        {
            if (!_enabled) return;

            // Do not fly while the user is typing in the console or using the debug camera.
            if (DebugConsole.ConsoleOpen || DebugFreeCamera.IsActive) return;
            if (PlayerRagdoll.IsActive) return;
            if (_fps != null && _fps.Health < 0) return;

            float speed = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                ? 22f : 9f;

            Transform view = _fps != null && _fps.cam != null
                ? _fps.cam.transform
                : (Camera.main != null ? Camera.main.transform : transform);

            Vector3 move = view.right * Input.GetAxisRaw("Horizontal") +
                           view.forward * Input.GetAxisRaw("Vertical");

            if (Input.GetKey(KeyCode.Space)) move += Vector3.up;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                Input.GetKey(KeyCode.C))
            {
                move += Vector3.down;
            }

            if (move.sqrMagnitude > 1f) move.Normalize();
            if (move.sqrMagnitude < 0.0001f) return;

            transform.position += move * speed * Time.deltaTime;
        }

        private void PlaceOnGround()
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Vector3 point;

            // Down first; if the player flew under the map the floor is above, so look up
            // too. The old 40 m down-only ray left the body falling forever.
            if (FindFloor(origin, Vector3.down, out point) ||
                FindFloor(origin, Vector3.up, out point))
            {
                transform.position = point + Vector3.up * 0.1f;
                return;
            }

            // No floor at all: return to where the flight started.
            transform.position = _lastGroundedPosition + Vector3.up * 0.1f;
        }

        private bool FindFloor(Vector3 origin, Vector3 direction, out Vector3 point)
        {
            point = origin;
            float best = float.MaxValue;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, 250f, ~0);

            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    point = hit.point;
                }
            }

            return best < float.MaxValue;
        }

        private void OnDisable()
        {
            if (_enabled) SetEnabled(false);
        }
    }
}
