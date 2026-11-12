using System;
using System.Collections.Generic;
using UnityEngine;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    /// <summary>
    /// Universal hit-reaction ragdoll. Works for any damage source, not just the Rake:
    /// call <see cref="TriggerNetworked"/> on the server (or <see cref="ApplyFor"/> locally)
    /// with a player id and the direction the body should fly.
    ///
    /// Local mode (the victim): builds the ragdoll, throws it, and keeps the first-person
    /// camera attached to the ragdoll's head so the hit is felt. Restores after a moment.
    ///
    /// Remote mode (everyone else): same ragdoll, no camera, and the victim's normal remote
    /// body is hidden while the ragdoll is visible.
    /// </summary>
    public class PlayerRagdoll : MonoBehaviour
    {
        /// <summary>Seconds the body stays down before everything is restored.</summary>
        public static float Duration = 2.0f;

        /// <summary>Horizontal launch speed (m/s). ~3.5 gives a 2-3 m fall.</summary>
        public static float LaunchSpeed = 3.0f;

        /// <summary>Upward launch speed (m/s).</summary>
        public static float LaunchLift = 0f;

        /// <summary>Random spin applied to the pelvis.</summary>
        public static float LaunchSpin = 2.5f;

        // Only the local (victim) ragdoll is tracked here; remote ones are independent.
        private static PlayerRagdoll _localInstance;

        // Remote ragdolls in flight, keyed by player id, so duplicate packets from the
        // Rake AI and the damage patch do not stack several bodies on top of each other.
        private static readonly Dictionary<int, PlayerRagdoll> _remoteInstances =
            new Dictionary<int, PlayerRagdoll>();
        private static PhysicMaterial _friction;

        public static bool IsActive { get { return _localInstance != null; } }

        private static readonly string[] PhysicsBones =
        {
            "Bip01 Pelvis", "Bip01 Spine", "Bip01 Spine1", "Bip01 Spine2",
            "Bip01 Neck", "Bip01 Head",
            "Bip01 L UpperArm", "Bip01 L Forearm", "Bip01 L Hand",
            "Bip01 R UpperArm", "Bip01 R Forearm", "Bip01 R Hand",
            "Bip01 L Thigh", "Bip01 L Calf", "Bip01 L Foot",
            "Bip01 R Thigh", "Bip01 R Calf", "Bip01 R Foot",
        };

        private Transform[] _nodes;
        private Transform _pelvis;
        private Transform _head;
        private Rigidbody[] _bodies;
        private int _playerId;

        private Camera _camera;
        private Camera _originalCamera;
        private readonly List<Camera> _disabledCameras = new List<Camera>();
        private readonly List<Renderer> _hiddenRenderers = new List<Renderer>();
        private CharacterController _controller;

        private bool _isLocal;
        private Vector3 _spawnPosition;
        private Vector3 _playerPosition;
        private float _timer;
        private bool _restored;

        // ---------------------------------------------------------------- public API

        /// <summary>
        /// Show the hit reaction for a player on this machine (local player or a remote clone).
        /// </summary>
        public static bool ApplyFor(int playerId, Vector3 direction)
        {
            var spawner = PlayerSpawner.Instance;
            if (spawner == null) return false;

            var local = spawner.LocalPlayer;
            if (local != null && local.PlayerId == playerId)
            {
                return Trigger(direction);
            }

            NetworkedPlayer remote;
            if (!spawner.TryGetRemotePlayer(playerId, out remote) || remote == null) return false;
            return TriggerRemote(playerId, remote.transform, direction);
        }

        /// <summary>
        /// Server: show it here and tell every client to show it too.
        /// On a client this only plays locally (debug use).
        /// </summary>
        public static bool TriggerNetworked(int playerId, Vector3 direction)
        {
            if (playerId <= 0) return false;

            bool applied = ApplyFor(playerId, direction);

            if (RakeMultiplayerPlugin.isServer)
            {
                var packet = new PlayerRagdollPacket { PlayerId = playerId, Direction = direction };
                RakeNetworkManager.Instance?.ServerBroadcast(packet.Serialize());
            }

            return applied;
        }

        /// <summary>Server: tell clients to show the reaction (no local spawn).</summary>
        public static void BroadcastOnly(int playerId, Vector3 direction)
        {
            if (!RakeMultiplayerPlugin.isServer || playerId <= 0) return;

            var packet = new PlayerRagdollPacket { PlayerId = playerId, Direction = direction };
            RakeNetworkManager.Instance?.ServerBroadcast(packet.Serialize());
        }

        /// <summary>Local player hit: ragdoll + head camera.</summary>
        public static bool Trigger(Vector3 awayFromHit)
        {
            if (_localInstance != null) return false;
            if (!RemotePlayerModelData.EnsureLoaded()) return false;

            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;
            if (local == null) return false;

            var go = new GameObject("RakePlayerRagdoll");
            var ragdoll = go.AddComponent<PlayerRagdoll>();
            if (!ragdoll.BuildInternal(local.transform, true))
            {
                UnityEngine.Object.Destroy(go);
                return false;
            }

            ragdoll.Launch(awayFromHit);
            _localInstance = ragdoll;
            return true;
        }

        /// <summary>Another player got hit: show their body flying (no camera).</summary>
        public static bool TriggerRemote(int playerId, Transform remotePlayer, Vector3 awayFromHit)
        {
            if (remotePlayer == null) return false;
            if (playerId > 0 && _remoteInstances.ContainsKey(playerId)) return false;
            if (!RemotePlayerModelData.EnsureLoaded()) return false;

            var go = new GameObject("RakeRemoteRagdoll");
            var ragdoll = go.AddComponent<PlayerRagdoll>();
            if (!ragdoll.BuildInternal(remotePlayer, false))
            {
                UnityEngine.Object.Destroy(go);
                return false;
            }

            ragdoll._playerId = playerId;
            if (playerId > 0) _remoteInstances[playerId] = ragdoll;

            ragdoll.Launch(awayFromHit);
            return true;
        }

        /// <summary>
        /// Tell every other peer that the local player just got hit. Called right after the
        /// local ragdoll starts, so the reaction is visible to everyone no matter which
        /// damage path triggered it (Rake AI, vanilla AI or the debug console).
        /// </summary>
        public static void ShareLocal(Vector3 direction)
        {
            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;
            if (local == null || local.PlayerId <= 0) return;

            if (RakeMultiplayerPlugin.isServer)
            {
                BroadcastOnly(local.PlayerId, direction);
                return;
            }

            if (!RakeMultiplayerPlugin.isConnected) return;

            var manager = RakeNetworkManager.Instance;
            if (manager == null) return;

            var packet = new PlayerRagdollRequestPacket
            {
                PlayerId = local.PlayerId,
                Direction = direction
            };
            manager.SendToServer(packet.Serialize());
        }

        // ---------------------------------------------------------------- build

        private bool BuildInternal(Transform player, bool local)
        {
            var data = RemotePlayerModelData.Nodes;
            if (data == null || data.Length == 0) return false;

            _isLocal = local;
            _controller = player.GetComponent<CharacterController>();

            float footOffset = _controller != null ? _controller.center.y - _controller.height * 0.5f : 0f;
            transform.position = player.position + Vector3.up * (footOffset + 0.05f);
            transform.rotation = Quaternion.Euler(0f, player.eulerAngles.y, 0f);
            _spawnPosition = transform.position;
            _playerPosition = player.position;

            // ---- hierarchy ----
            _nodes = new Transform[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                var node = data[i];
                Transform t;
                if (i == 0)
                {
                    // Node 0 must be a CHILD of the ragdoll root: the canonical placement
                    // is a local offset, and writing it onto the root itself would move
                    // the whole ragdoll to the world origin.
                    var bodyRoot = new GameObject(node.Name);
                    t = bodyRoot.transform;
                    t.SetParent(transform, false);
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

            // The baked model needs the same per-clip yaw correction as the animated body,
            // otherwise the ragdoll faces ~114 degrees off the player (Idle yaw).
            Quaternion clipYaw = RemotePlayerModel.GetClipYaw("Idle");
            _nodes[0].localPosition = clipYaw * RemotePlayerModelData.RootPosition;
            _nodes[0].localRotation = clipYaw * RemotePlayerModelData.RootRotation;
            _nodes[0].localScale = RemotePlayerModelData.RootScale;

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

            var smr = _nodes[smrNode].gameObject.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = RemotePlayerModelData.Mesh;
            smr.bones = bones;
            int rootBone = RemotePlayerModelData.SmrRootBone;
            smr.rootBone = rootBone >= 0 && rootBone < _nodes.Length ? _nodes[rootBone] : bones[0];
            smr.sharedMaterial = RemotePlayerModelData.Material;
            smr.updateWhenOffscreen = true;

            _pelvis = FindNode("Bip01 Pelvis");
            _head = FindNode("Bip01 Head");
            if (_pelvis == null || _head == null) return false;

            ApplyIdlePose();

            if (!AddPhysics()) return false;

            if (local)
            {
                AttachHeadCamera();
                DisablePlayerCameras(player);
                PlayerInputLock.SetRagdoll(true);
                if (_controller != null) _controller.enabled = false;
            }
            else
            {
                HideRenderers(player);
            }

            return true;
        }

        private void ApplyIdlePose()
        {
            var clip = RemotePlayerModelData.GetClip("Idle");
            if (clip == null || clip.FrameCount <= 0) return;

            var animNodes = RemotePlayerModelData.AnimNodes;
            int animCount = animNodes.Length;

            for (int k = 0; k < animCount; k++)
            {
                int nodeIndex = animNodes[k];
                if (nodeIndex <= 0 || nodeIndex >= _nodes.Length) continue;

                var t = _nodes[nodeIndex];
                t.localPosition = clip.Positions[k];
                t.localRotation = clip.Rotations[k];
            }
        }

        // ---------------------------------------------------------------- camera

        /// <summary>
        /// Parent the camera to the head bone through an anchor at eye height, so the
        /// position is exact by construction and follows every tumble of the body.
        /// </summary>
        private void AttachHeadCamera()
        {
            var headNub = FindNode("Bip01 HeadNub");

            Vector3 headPos = _head.position;
            Vector3 crown = headNub != null ? headNub.position : headPos + Vector3.up * 0.15f;
            Vector3 up = crown - headPos;
            float skull = up.magnitude;
            if (skull < 0.01f) { up = Vector3.up; skull = 0.15f; }
            else up /= skull;

            Vector3 eyeWorld = headPos + up * (skull * 0.55f) + transform.forward * (skull * 0.35f);

            var anchor = new GameObject("RagdollHeadAnchor").transform;
            anchor.SetParent(_head, false);
            anchor.position = eyeWorld;
            anchor.rotation = transform.rotation;

            var go = new GameObject("RagdollHeadCamera");
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            _camera = go.AddComponent<Camera>();
            if (_originalCamera != null)
            {
                _camera.CopyFrom(_originalCamera);
                _camera.depth = _originalCamera.depth + 1f;
            }
            _camera.nearClipPlane = 0.03f;
            _camera.enabled = true;

            RakeMultiplayerPlugin.Log?.LogInfo("[Ragdoll] camera eye=" + eyeWorld.ToString("F2") +
                                               " head=" + _head.position.ToString("F2") +
                                               " root=" + transform.position.ToString("F2"));
        }

        private void DisablePlayerCameras(Transform player)
        {
            _disabledCameras.Clear();

            var cameras = player.GetComponentsInChildren<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (cam == null || !cam.enabled) continue;
                if (_originalCamera == null) _originalCamera = cam;
                cam.enabled = false;
                _disabledCameras.Add(cam);
            }

            if (_originalCamera == null) _originalCamera = Camera.main;
        }

        private void RestorePlayerCameras()
        {
            for (int i = 0; i < _disabledCameras.Count; i++)
            {
                if (_disabledCameras[i] != null) _disabledCameras[i].enabled = true;
            }
            _disabledCameras.Clear();
        }

        private void HideRenderers(Transform player)
        {
            _hiddenRenderers.Clear();

            var renderers = player.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                r.enabled = false;
                _hiddenRenderers.Add(r);
            }
        }

        private void RestoreRenderers()
        {
            for (int i = 0; i < _hiddenRenderers.Count; i++)
            {
                if (_hiddenRenderers[i] != null) _hiddenRenderers[i].enabled = true;
            }
            _hiddenRenderers.Clear();
        }

        // ---------------------------------------------------------------- physics

        private static PhysicMaterial Friction()
        {
            if (_friction == null)
            {
                _friction = new PhysicMaterial("RagdollFriction");
                _friction.dynamicFriction = 0.9f;
                _friction.staticFriction = 0.9f;
                _friction.bounciness = 0f;
                _friction.frictionCombine = PhysicMaterialCombine.Average;
            }
            return _friction;
        }

        private bool AddPhysics()
        {
            var rigidbodies = new Dictionary<Transform, Rigidbody>();
            var material = Friction();

            for (int b = 0; b < PhysicsBones.Length; b++)
            {
                Transform bone = FindNode(PhysicsBones[b]);
                if (bone == null) continue;

                var rb = bone.gameObject.AddComponent<Rigidbody>();
                rb.mass = MassFor(bone.name);
                rb.drag = 0.1f;
                rb.angularDrag = 1.5f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rigidbodies[bone] = rb;

                var collider = bone.gameObject.AddComponent<CapsuleCollider>();
                collider.sharedMaterial = material;
                SizeCollider(bone, collider);
            }

            if (rigidbodies.Count < 4) return false;

            _bodies = new Rigidbody[rigidbodies.Count];
            rigidbodies.Values.CopyTo(_bodies, 0);

            foreach (var kv in rigidbodies)
            {
                Transform bone = kv.Key;

                Transform parentBone = bone.parent;
                Rigidbody parentBody = null;
                while (parentBone != null)
                {
                    if (rigidbodies.TryGetValue(parentBone, out parentBody)) break;
                    parentBone = parentBone.parent;
                }

                if (parentBody == null) continue; // pelvis / root

                var joint = bone.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = parentBody;
                joint.anchor = Vector3.zero;
                joint.connectedAnchor = parentBody.transform.InverseTransformPoint(bone.position);
                // The twist axis must run along the bone (towards its main child),
                // otherwise every limb twists around an arbitrary direction and the
                // ragdoll tears itself apart on the first frame.
                Vector3 axis = BoneAxis(bone);
                Vector3 swingDir = Vector3.Cross(axis, Vector3.forward);
                if (swingDir.sqrMagnitude < 0.01f) swingDir = Vector3.Cross(axis, Vector3.up);
                swingDir.Normalize();

                joint.axis = axis;
                joint.swingAxis = swingDir;

                var low = new SoftJointLimit(); low.limit = -45f;
                var high = new SoftJointLimit(); high.limit = 45f;
                var swing = new SoftJointLimit(); swing.limit = 55f;

                joint.lowTwistLimit = low;
                joint.highTwistLimit = high;
                joint.swing1Limit = swing;
                joint.swing2Limit = swing;
            }

            return true;
        }

        private Transform FindNode(string name)
        {
            for (int i = 0; i < _nodes.Length; i++)
            {
                if (_nodes[i] != null && _nodes[i].name == name) return _nodes[i];
            }
            return null;
        }

        private static float MassFor(string boneName)
        {
            if (boneName.Contains("Pelvis")) return 9f;
            if (boneName.Contains("Spine2")) return 6f;
            if (boneName.Contains("Spine1")) return 6f;
            if (boneName.Contains("Spine")) return 7f;
            if (boneName.Contains("Neck")) return 3f;
            if (boneName.Contains("Head")) return 5f;
            if (boneName.Contains("UpperArm")) return 3f;
            if (boneName.Contains("Forearm")) return 2f;
            if (boneName.Contains("Hand")) return 1f;
            if (boneName.Contains("Thigh")) return 5f;
            if (boneName.Contains("Calf")) return 3f;
            if (boneName.Contains("Foot")) return 1.5f;
            return 2f;
        }

        private static void SizeCollider(Transform bone, CapsuleCollider collider)
        {
            Transform child = MainChild(bone);
            Vector3 childLocal = child != null ? child.localPosition : Vector3.zero;
            float length = childLocal.magnitude;

            int axis = 1;
            float best = Mathf.Abs(childLocal.y);
            if (Mathf.Abs(childLocal.x) > best) { axis = 0; best = Mathf.Abs(childLocal.x); }
            if (Mathf.Abs(childLocal.z) > best) { axis = 2; best = Mathf.Abs(childLocal.z); }

            collider.direction = axis;
            collider.height = Mathf.Max(length, 0.08f);
            collider.radius = Mathf.Clamp(length * 0.28f, 0.035f, 0.13f);
            collider.center = childLocal * 0.5f;

            if (length < 0.02f)
            {
                collider.height = 0.14f;
                collider.radius = 0.075f;
                collider.center = Vector3.zero;
            }
        }

        private static Transform MainChild(Transform bone)
        {
            Transform best = null;
            float bestDist = 0f;
            for (int i = 0; i < bone.childCount; i++)
            {
                var child = bone.GetChild(i);
                if (child.name.EndsWith("Nub")) continue;
                float d = child.localPosition.magnitude;
                if (d > bestDist) { bestDist = d; best = child; }
            }
            return best;
        }

        /// <summary>Direction of the bone in its own local space (towards its main child).</summary>
        private static Vector3 BoneAxis(Transform bone)
        {
            Transform child = MainChild(bone);
            if (child == null) return Vector3.up;

            Vector3 local = child.localPosition;
            if (local.sqrMagnitude < 0.0001f) return Vector3.up;
            return local.normalized;
        }

        private void Launch(Vector3 awayFromHit)
        {
            Vector3 dir = awayFromHit;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = -transform.forward;
                dir.y = 0f;
            }
            dir.Normalize();

            Vector3 velocity = dir * LaunchSpeed + Vector3.up * LaunchLift;

            var bodies = GetComponentsInChildren<Rigidbody>();
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].AddForce(velocity, ForceMode.VelocityChange);

                // A small independent spin per bone: without it a ragdoll in free fall
                // keeps its idle pose and looks like a mannequin sliding through the air.
                bodies[i].angularVelocity += UnityEngine.Random.insideUnitSphere * 2.5f;
            }

            var pelvisBody = _pelvis.GetComponent<Rigidbody>();
            if (pelvisBody != null)
            {
                pelvisBody.AddTorque(UnityEngine.Random.insideUnitSphere * LaunchSpin, ForceMode.VelocityChange);
            }

            var rake = RakeHighlight.FindRake();
            RakeMultiplayerPlugin.Log?.LogInfo("[Ragdoll] " + (_isLocal ? "local" : "remote") +
                                               " spawn=" + _spawnPosition.ToString("F2") +
                                               " player=" + _playerPosition.ToString("F2") +
                                               " rake=" + (rake != null ? rake.position.ToString("F2") : "none") +
                                               " dir=" + dir.ToString("F2"));
        }

        private void LateUpdate()
        {
            _timer += Time.deltaTime;
            if (_timer >= Duration && !_restored) Restore();
        }

        /// <summary>
        /// Keeps the simulation sane: a joint that starts fighting can otherwise
        /// accelerate a limb to hundreds of m/s and launch the whole body across
        /// the level.
        /// </summary>
        private void FixedUpdate()
        {
            if (_bodies == null) return;

            const float maxSpeed = 8f;
            const float maxSpin = 14f;

            for (int i = 0; i < _bodies.Length; i++)
            {
                var rb = _bodies[i];
                if (rb == null) continue;

                Vector3 v = rb.velocity;
                if (v.sqrMagnitude > maxSpeed * maxSpeed) rb.velocity = v.normalized * maxSpeed;

                Vector3 w = rb.angularVelocity;
                if (w.sqrMagnitude > maxSpin * maxSpin) rb.angularVelocity = w.normalized * maxSpin;

                if (rb.position.y < _spawnPosition.y - 30f)
                {
                    if (!_restored) Restore();
                    return;
                }
            }
        }

        private void Restore()
        {
            _restored = true;

            if (_isLocal)
            {
                RestorePlayerCameras();
                if (_controller != null) _controller.enabled = true;
                PlayerInputLock.SetRagdoll(false);
            }
            else
            {
                RestoreRenderers();
            }

            if (_camera != null) UnityEngine.Object.Destroy(_camera.gameObject);
            _camera = null;

            Vector3 moved = _pelvis != null ? _pelvis.position - _spawnPosition : Vector3.zero;
            RakeMultiplayerPlugin.Log?.LogInfo("[Ragdoll] " + (_isLocal ? "local" : "remote") +
                                               " finished, body moved " + moved.magnitude.ToString("F2") + " m");

            UnityEngine.Object.Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_localInstance == this) _localInstance = null;

            if (_playerId > 0)
            {
                PlayerRagdoll active;
                if (_remoteInstances.TryGetValue(_playerId, out active) && active == this)
                {
                    _remoteInstances.Remove(_playerId);
                }
            }
        }
    }
}
