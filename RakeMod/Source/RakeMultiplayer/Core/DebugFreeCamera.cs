using System.Collections.Generic;
using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Free-fly spectator camera used by the debug console ("debug camera").
    /// It creates its own camera, disables the player cameras (including the weapon
    /// viewmodel camera) and freezes player input so the view can be inspected without
    /// the character moving.
    ///
    /// Controls: WASD to move, Q/E down/up, Shift to fly faster, mouse to look.
    /// </summary>
    public class DebugFreeCamera : MonoBehaviour
    {
        public static bool IsActive { get; private set; }

        private static DebugFreeCamera _instance;
        private static float _speed = 8f;
        private static readonly List<Camera> _disabledCameras = new List<Camera>();

        private float _yaw;
        private float _pitch;

        public static void SetEnabled(bool enable)
        {
            if (enable)
            {
                Enable();
            }
            else
            {
                Disable();
            }
        }

        public static void SetSpeed(float speed)
        {
            _speed = Mathf.Clamp(speed, 0.1f, 200f);
        }

        private static void Enable()
        {
            if (IsActive) return;

            Camera source = FindCamera();
            if (source == null)
            {
                DebugConsole.Print("debug camera: no camera found");
                return;
            }

            var go = new GameObject("RakeDebugCamera");
            go.hideFlags = HideFlags.HideAndDontSave;

            var camera = go.AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.depth = source.depth + 1f;
            camera.enabled = true;

            go.transform.position = source.transform.position;

            Vector3 euler = source.transform.eulerAngles;
            float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            go.transform.rotation = Quaternion.Euler(pitch, euler.y, 0f);

            var instance = go.AddComponent<DebugFreeCamera>();
            instance._yaw = euler.y;
            instance._pitch = pitch;

            DisablePlayerCameras(source);

            _instance = instance;
            IsActive = true;

            PlayerInputLock.SetCamera(true);
        }

        private static void Disable()
        {
            if (!IsActive) return;

            RestorePlayerCameras();

            if (_instance != null) UnityEngine.Object.Destroy(_instance.gameObject);
            _instance = null;
            IsActive = false;

            PlayerInputLock.SetCamera(false);
        }

        /// <summary>Hide every camera of the local player (main view + weapon viewmodel).</summary>
        private static void DisablePlayerCameras(Camera source)
        {
            _disabledCameras.Clear();

            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;
            if (local != null)
            {
                var cameras = local.GetComponentsInChildren<Camera>();
                for (int i = 0; i < cameras.Length; i++)
                {
                    var camera = cameras[i];
                    if (camera == null || !camera.enabled) continue;
                    camera.enabled = false;
                    _disabledCameras.Add(camera);
                }
            }

            if (source != null && source.enabled && !_disabledCameras.Contains(source))
            {
                source.enabled = false;
                _disabledCameras.Add(source);
            }
        }

        private static void RestorePlayerCameras()
        {
            for (int i = 0; i < _disabledCameras.Count; i++)
            {
                if (_disabledCameras[i] != null) _disabledCameras[i].enabled = true;
            }
            _disabledCameras.Clear();
        }

        private static Camera FindCamera()
        {
            Camera main = Camera.main;
            if (main != null && main.enabled) return main;

            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;
            if (local != null)
            {
                var playerCamera = local.GetComponentInChildren<Camera>();
                if (playerCamera != null && playerCamera.enabled) return playerCamera;
            }

            var all = UnityEngine.Object.FindObjectsOfType<Camera>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].enabled && all[i].gameObject.activeInHierarchy) return all[i];
            }
            return null;
        }

        private void Update()
        {
            // Do not fly while the console has the keyboard.
            if (DebugConsole.ConsoleOpen) return;

            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) dt = Time.deltaTime;

            const float sensitivity = 2.5f;
            _yaw += Input.GetAxis("Mouse X") * sensitivity;
            _pitch -= Input.GetAxis("Mouse Y") * sensitivity;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            float speed = _speed;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) speed *= 3f;

            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += transform.forward;
            if (Input.GetKey(KeyCode.S)) move -= transform.forward;
            if (Input.GetKey(KeyCode.D)) move += transform.right;
            if (Input.GetKey(KeyCode.A)) move -= transform.right;
            if (Input.GetKey(KeyCode.E)) move += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;

            if (move.sqrMagnitude > 0.0001f)
            {
                transform.position += move.normalized * speed * dt;
            }
        }
    }
}
