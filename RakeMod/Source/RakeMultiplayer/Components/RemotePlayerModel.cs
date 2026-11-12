using System.Collections.Generic;
using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Real third-person body for a remote player: builds the baked skeleton/mesh hierarchy
    /// and plays the baked animation clips procedurally (no Animator, no AssetBundle).
    ///
    /// Clip selection is driven by the remote player's movement speed and the animation
    /// bitmask that <see cref="NetworkedPlayer"/> already synchronises.
    /// </summary>
    public class RemotePlayerModel : MonoBehaviour
    {
        private const string ClipIdle = "Idle";
        private const string ClipWalk = "Walk";
        private const string ClipRun = "Run";
        private const string ClipAim = "RifleHold";

        // Reference speeds come from the root motion that was baked into the clips
        // (Walk 1.56 m per 0.99 s cycle, Run 3.34 m per 0.59 s cycle); playing at
        // speed / reference keeps the feet from sliding.
        private const float WalkReferenceSpeed = 1.58f;
        private const float RunReferenceSpeed = 5.66f;

        /// <summary>
        /// Per-clip yaw correction (degrees). The humanoid clips carry a root rotation that
        /// the bake does not extract, so the body faces a different direction in each clip
        /// (Idle was 114 deg off, i.e. the body looked 90 deg to the right). Values are
        /// produced by RakeMod/ModelBake/canonical_placement.py.
        /// </summary>
        /// <summary>Yaw correction for a clip, so other systems (the ragdoll) can match the body.</summary>
        public static Quaternion GetClipYaw(string clipName)
        {
            float yaw;
            return ClipYaw.TryGetValue(clipName, out yaw) ? Quaternion.Euler(0f, yaw, 0f) : Quaternion.identity;
        }

        private static readonly Dictionary<string, float> ClipYaw = new Dictionary<string, float>
        {
            { "Idle", -113.87f },
            { "Walk", 2.64f },
            { "Run", 2.51f },
            { "Attack", -0.22f },
            { "WalkLeft", 27.76f },
            { "WalkRight", -27.78f },
            { "RifleHold", -0.01f },
        };

        private Transform[] _nodes;
        private Transform _tracked;
        private SkinnedMeshRenderer _renderer;
        private MeshRenderer _weapon;

        private RemotePlayerModelClip _clip;
        private string _clipName;
        private Quaternion _clipYawRot = Quaternion.identity;
        private float _time;
        private float _rate = 1f;

        private Vector3 _lastPosition;
        private bool _hasLastPosition;
        private float _speed;

        private NetworkedPlayer _networked;
        private bool _loggedBounds;
        private bool _manualClip;
        private bool _weaponVisible = true;

        /// <summary>
        /// Create the body under <paramref name="parent"/>, tracking <paramref name="tracked"/>
        /// (the player root that actually moves). Returns null when the baked model is
        /// unavailable so the caller can fall back to a proxy.
        /// </summary>
        public static RemotePlayerModel Attach(Transform parent, Transform tracked)
        {
            if (parent == null) return null;
            if (!RemotePlayerModelData.EnsureLoaded()) return null;

            var go = new GameObject("RakeBody");
            go.transform.SetParent(parent, false);

            var model = go.AddComponent<RemotePlayerModel>();
            if (!model.Build(tracked))
            {
                Destroy(go);
                return null;
            }
            return model;
        }

        private bool Build(Transform tracked)
        {
            var data = RemotePlayerModelData.Nodes;
            if (data == null || data.Length == 0) return false;

            _nodes = new Transform[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                var node = data[i];
                Transform t;
                if (i == 0)
                {
                    t = transform;
                    t.name = "RakeBodyRoot";
                }
                else
                {
                    var child = new GameObject(node.Name);
                    t = child.transform;
                    int parent = node.Parent;
                    if (parent < 0 || parent >= i) parent = 0;
                    t.SetParent(_nodes[parent], false);
                }

                _nodes[i] = t;
                t.localPosition = node.Position;
                t.localRotation = node.Rotation;
                t.localScale = node.Scale;
            }

            // ---- skinned renderer ----
            int smrNode = RemotePlayerModelData.SmrNode;
            if (smrNode < 0 || smrNode >= _nodes.Length) return false;

            var boneNodes = RemotePlayerModelData.BoneNodes;
            var bones = new Transform[boneNodes.Length];
            for (int i = 0; i < boneNodes.Length; i++)
            {
                int idx = boneNodes[i];
                if (idx < 0 || idx >= _nodes.Length) return false;
                bones[i] = _nodes[idx];
            }

            _renderer = _nodes[smrNode].gameObject.AddComponent<SkinnedMeshRenderer>();
            _renderer.sharedMesh = RemotePlayerModelData.Mesh;
            _renderer.bones = bones;
            int rootBone = RemotePlayerModelData.SmrRootBone;
            _renderer.rootBone = rootBone >= 0 && rootBone < _nodes.Length ? _nodes[rootBone] : bones[0];
            _renderer.sharedMaterial = RemotePlayerModelData.Material;
            _renderer.updateWhenOffscreen = true;
            _renderer.receiveShadows = true;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            // The body prefab carries the shotgun as a plain mesh parented to the right
            // hand bone; add it as static geometry on the same node.
            _weapon = RemotePlayerWeaponData.Attach(_nodes);

            // ---- tracking / state ----
            _tracked = tracked != null ? tracked : transform.root;
            _lastPosition = _tracked.position;
            _hasLastPosition = true;
            _networked = _tracked.GetComponent<NetworkedPlayer>();

            SetClip(ClipIdle);
            ApplyPose();
            UpdateWeaponVisibility();

            RakeMultiplayerPlugin.Log?.LogInfo("[Model] Built remote body for " + _tracked.name);
            return true;
        }

        private void LateUpdate()
        {
            if (_nodes == null || _clip == null) return;

            UpdateSpeed();
            UpdateClip();
            ApplyPose();
            UpdateWeaponVisibility();

            if (!_loggedBounds && _renderer != null)
            {
                _loggedBounds = true;
                Bounds b = _renderer.bounds;
                RakeMultiplayerPlugin.Log?.LogInfo(
                    "[Model] Bounds centre=" + b.center.ToString("F2") + " size=" + b.size.ToString("F2") +
                    " tracked=" + (_tracked != null ? _tracked.position.ToString("F2") : "null"));
            }
        }

        private void UpdateSpeed()
        {
            if (_tracked == null) return;

            Vector3 position = _tracked.position;
            float dt = Time.deltaTime;
            if (_hasLastPosition && dt > 0.0001f)
            {
                float instant = (position - _lastPosition).magnitude / dt;
                // smooth so a single bad frame does not flip the clip
                float k = 1f - Mathf.Exp(-dt * 10f);
                _speed = Mathf.Lerp(_speed, instant, k);
            }
            _lastPosition = position;
            _hasLastPosition = true;
        }

        private void UpdateClip()
        {
            if (_manualClip)
            {
                _time += Time.deltaTime * _rate;
                if (_clip != null && _clip.Length > 0.001f && _time > _clip.Length * 4f)
                {
                    _time -= _clip.Length * Mathf.Floor(_time / _clip.Length);
                }
                return;
            }

            bool moving = _speed > 0.25f;
            bool running = moving && IsRunning();
            bool aiming = IsAiming() && HasWeapon();

            string wanted;
            if (!moving && aiming && RemotePlayerModelData.GetClip(ClipAim) != null) wanted = ClipAim;
            else if (!moving) wanted = ClipIdle;
            else wanted = running ? ClipRun : ClipWalk;

            SetClip(wanted);

            if (_clipName == ClipWalk) _rate = Mathf.Clamp(_speed / WalkReferenceSpeed, 0.55f, 1.9f);
            else if (_clipName == ClipRun) _rate = Mathf.Clamp(_speed / RunReferenceSpeed, 0.55f, 1.9f);
            else _rate = 1f;

            _time += Time.deltaTime * _rate;
            if (_clip != null && _clip.Length > 0.001f && _time > _clip.Length * 4f)
            {
                _time -= _clip.Length * Mathf.Floor(_time / _clip.Length);
            }
        }

        private byte AnimState()
        {
            if (_networked == null && _tracked != null) _networked = _tracked.GetComponent<NetworkedPlayer>();
            return _networked != null ? _networked.CurrentAnimState : (byte)0;
        }

        private bool IsRunning()
        {
            if (_networked == null && _tracked != null) _networked = _tracked.GetComponent<NetworkedPlayer>();
            if (_networked == null) return _speed > 3.2f;

            // bit0 of the animation bitmask is "running"
            return (_networked.CurrentAnimState & 1) != 0;
        }

        private bool IsAiming()
        {
            // bit2 of the animation bitmask is "aiming"
            return (AnimState() & 4) != 0;
        }

        /// <summary>bit4: the player has a weapon out (the mod only bakes the shotgun).</summary>
        private bool HasWeapon()
        {
            return (AnimState() & 16) != 0;
        }

        private void UpdateWeaponVisibility()
        {
            if (_weapon == null) return;

            // The debug preview always shows the weapon so it can be inspected.
            bool visible = _manualClip || HasWeapon();
            if (visible == _weaponVisible) return;

            _weaponVisible = visible;
            _weapon.enabled = visible;
        }

        /// <summary>Force the weapon on/off (used by the F10 debug preview).</summary>
        public void SetWeaponVisible(bool visible)
        {
            _weaponVisible = visible;
            if (_weapon != null) _weapon.enabled = visible;
        }

        private static bool IsLocomotion(string clip)
        {
            return clip == ClipWalk || clip == ClipRun;
        }

        private void SetClip(string name)
        {
            if (_clipName == name) return;

            var next = RemotePlayerModelData.GetClip(name);
            if (next == null || next.FrameCount <= 0)
            {
                if (_clip == null)
                {
                    next = RemotePlayerModelData.GetClip(ClipIdle);
                    name = ClipIdle;
                }
                if (next == null || next.FrameCount <= 0) return;
            }

            // keep the walk/run cycle in phase when switching between them
            float phase = 0f;
            if (_clip != null && _clip.Length > 0.001f && IsLocomotion(_clipName) && IsLocomotion(name))
            {
                phase = Mathf.Repeat(_time, _clip.Length) / _clip.Length;
            }

            _clip = next;
            _clipName = name;

            float yaw;
            _clipYawRot = ClipYaw.TryGetValue(name, out yaw)
                ? Quaternion.Euler(0f, yaw, 0f)
                : Quaternion.identity;
            ApplyRootPlacement();

            _time = phase * next.Length;
        }

        /// <summary>
        /// Place the model root: canonical offset/scale from the bake plus the per-clip yaw
        /// correction. Rotating the offset as well keeps the feet on the owner's position.
        /// </summary>
        private void ApplyRootPlacement()
        {
            if (_nodes == null || _nodes.Length == 0) return;

            _nodes[0].localPosition = _clipYawRot * RemotePlayerModelData.RootPosition;
            _nodes[0].localRotation = _clipYawRot * RemotePlayerModelData.RootRotation;
            _nodes[0].localScale = RemotePlayerModelData.RootScale;
        }

        private void ApplyPose()
        {
            var clip = _clip;
            if (clip == null || clip.FrameCount <= 0) return;
            if (clip.Length <= 0.001f) return;

            var animNodes = RemotePlayerModelData.AnimNodes;
            int animCount = animNodes.Length;
            if (animCount <= 0) return;

            int frames = clip.FrameCount;
            float frame = Mathf.Repeat(_time, clip.Length) * clip.Fps;

            int f0 = Mathf.FloorToInt(frame) % frames;
            if (f0 < 0) f0 += frames;
            int f1 = (f0 + 1) % frames;
            float blend = frame - Mathf.Floor(frame);

            int base0 = f0 * animCount;
            int base1 = f1 * animCount;

            for (int k = 0; k < animCount; k++)
            {
                int nodeIndex = animNodes[k];
                if (nodeIndex <= 0 || nodeIndex >= _nodes.Length) continue; // root keeps the canonical placement

                var t = _nodes[nodeIndex];
                t.localPosition = Vector3.Lerp(clip.Positions[base0 + k], clip.Positions[base1 + k], blend);
                t.localRotation = Quaternion.Slerp(clip.Rotations[base0 + k], clip.Rotations[base1 + k], blend);
            }
        }

        /// <summary>Force a specific clip (used by the F10 debug preview).</summary>
        public void PlayClip(string name, float rate)
        {
            _manualClip = true;
            _rate = rate <= 0f ? 1f : rate;
            _clipName = null; // force SetClip to actually switch
            SetClip(name);
            _time = 0f;
            ApplyPose();
            SetWeaponVisible(true);
        }

        /// <summary>Name of the clip currently playing (diagnostics).</summary>
        public string CurrentClip { get { return _clipName; } }
    }
}
