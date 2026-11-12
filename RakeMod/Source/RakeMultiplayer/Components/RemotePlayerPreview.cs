using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Debug helper for testing the baked player body without running a second game
    /// instance: spawns the body in front of the camera and cycles its clips.
    ///
    /// F10 = spawn / next clip, F11 = remove.
    /// </summary>
    public static class RemotePlayerPreview
    {
        private static readonly string[] ClipNames = { "Idle", "Walk", "Run", "Attack", "RifleHold" };

        private static GameObject _root;
        private static RemotePlayerModel _model;
        private static int _index = -1;

        public static void Next()
        {
            if (!RemotePlayerModelData.EnsureLoaded())
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Preview] model not available: " + RemotePlayerModelData.Error);
                return;
            }

            if (_root == null)
            {
                if (!Spawn()) return;
                _index = -1;
            }

            Place();

            _index = (_index + 1) % ClipNames.Length;
            string clip = ClipNames[_index];
            if (_model != null) _model.PlayClip(clip, 1f);

            RakeMultiplayerPlugin.Log?.LogInfo("[Preview] clip=" + clip + " (F10 = next, F11 = remove)");
        }

        /// <summary>Force a clip on the existing preview. Returns false when there is none.</summary>
        public static bool PlayClip(string clipName)
        {
            if (_model == null || string.IsNullOrEmpty(clipName)) return false;
            if (RemotePlayerModelData.GetClip(clipName) == null) return false;
            _model.PlayClip(clipName, 1f);
            _index = -1;
            return true;
        }

        public static void Remove()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = null;
            _model = null;
            _index = -1;
            RakeMultiplayerPlugin.Log?.LogInfo("[Preview] removed");
        }

        private static bool Spawn()
        {
            _root = new GameObject("RemotePlayerPreview");
            _model = RemotePlayerModel.Attach(_root.transform, _root.transform);
            if (_model == null)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Preview] could not build the body");
                UnityEngine.Object.Destroy(_root);
                _root = null;
                return false;
            }
            return true;
        }

        private static void Place()
        {
            if (_root == null) return;

            Camera cam = FindCamera();
            if (cam == null)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Preview] no camera found");
                return;
            }

            Transform ct = cam.transform;
            Vector3 pos = ct.position + ct.forward * 3.5f;

            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out hit, 8f))
            {
                pos = hit.point;
            }
            else
            {
                pos.y -= 1.6f;
            }

            Vector3 dir = ct.position - pos;
            dir.y = 0f;
            _root.transform.rotation = dir.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(dir)
                : Quaternion.identity;

            _root.transform.position = pos;
        }

        private static Camera FindCamera()
        {
            Camera cam = Camera.main;
            if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy) return cam;

            var cams = UnityEngine.Object.FindObjectsOfType<Camera>();
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i] != null && cams[i].enabled && cams[i].gameObject.activeInHierarchy) return cams[i];
            }
            return null;
        }
    }
}
