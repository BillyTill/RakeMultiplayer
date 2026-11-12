using System;
using System.Collections.Generic;
using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Hit reaction: spawns a physics ragdoll of the player body, throws it away from the
    /// hit, and keeps the camera attached to the ragdoll's head so the hit is felt in
    /// first person while the body tumbles. Restores first person after a couple of
    /// seconds. Purely visual - the real player never moves.
    ///
    /// The ragdoll is built at runtime from the baked body model: every major bone gets a
    /// Rigidbody, a CapsuleCollider and a CharacterJoint to its nearest physics parent.
    /// </summary>
    public class PlayerRagdoll : MonoBehaviour
    {
        /// <summary>Seconds the body stays on the ground before everything is restored.</summary>
        public static float Duration = 2.0f;

        /// <summary>Horizontal launch speed (m/s). ~6 gives a 2-3 m arc.</summary>
        public static float LaunchSpeed = 6.0f;

        /// <summary>Upward launch speed (m/s).</summary>
        public static float LaunchLift = 3.2f;

        /// <summary>Random spin applied to the pelvis.</summary>
        public static float LaunchSpin = 5.0f;

        private static PlayerRagdoll _instance;

        public static bool IsActive { get { return _instance != null; } }

        // Bones that get physics. Fingers/toes stay as kinematic children.
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

        // Camera attachment, computed once from the initial pose
        private Vector3 _headLocalEye;
        private Quaternion _headRotationOffset;

        private Camera _camera;
        private Camera _originalCamera;
        private readonly List<Camera> _disabledCameras = new List<Camera>();
        private CharacterController _controller;

        private float _timer;
        private bool _restored;

        /// <summary>
        /// Start the reaction. <paramref name="awayFromHit"/> is the horizontal direction the
        /// body should fly towards (usually away from the Rake).
        /// </summary>
        public static bool Trigger(Vector3 awayFromHit)
        {
            if (_instance != null) return false;
            if (!RemotePlayerModelData.EnsureLoaded()) return false;

            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;
            if (local == null) return false;

            var go = new GameObject("RakePlayerRagdoll");
            var ragdoll = go.AddComponent<PlayerRagdoll>();
            if (!ragdoll.Build(local.transform))
            {
                UnityEngine.Object.Destroy(go);
                return false;
            }

            ragdoll.Launch(awayFromHit);
            return true;
        }

        private bool Build(Transform player)
        {
            var data = RemotePlayerModelData.Nodes;
            if (data == null || data.Length == 0) return false;

            _controller = player.GetComponent<CharacterController>();

            // Place the body's feet where the player's feet are
            float footOffset = _controller != null ? _controller.center.y - _controller.height * 0.5f : 0f;
            transform.position = player.position + Vector3.up * footOffset;
            transform.rotation = Quaternion.Euler(0f, player.eulerAngles.y, 0f);

            // ---- hierarchy ----
            _nodes = new Transform[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                var node = data[i];
                Transform t;
                if (i == 0)
                {
                    t = transform;
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

            _nodes[0].localPosition = RemotePlayerModelData.RootPosition;
            _nodes[0].localRotation = RemotePlayerModelData.RootRotation;
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

            // Start from the idle pose instead of the stiff bind pose
            ApplyIdlePose();

            // Camera sits at the head; remember where relative to that bone
            CaptureHeadAttachment();

            // ---- physics ----
            if (!AddPhysics()) return false;

            // ---- cameras ----
            DisablePlayerCameras(player);
            CreateCamera();

            // Freeze the player while the body is flying
            PlayerInputLock.SetRagdoll(true);
            if (_controller != null) _controller.enabled = false;

            _instance = this;
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

        /// <summary>
        /// Compute the eye position in head-bone space and the rotation offset that maps the
        /// head bone's orientation to the body's forward, so the camera looks where the
        /// character looks no matter how the head bone is oriented in the rig.
        /// </summary>
        private void CaptureHeadAttachment()
        {
            Vector3 eyeWorld = _head.position + transform.forward * 0.09f + Vector3.up * 0.05f;
            _headLocalEye = _head.InverseTransformPoint(eyeWorld);
            _headRotationOffset = Quaternion.Inverse(_head.rotation) * transform.rotation;
        }

        private bool AddPhysics()
        {
            var rigidbodies = new Dictionary<Transform, Rigidbody>();

            for (int b = 0; b < PhysicsBones.Length; b++)
            {
                Transform bone = FindNode(PhysicsBones[b]);
                if (bone == null) continue;

                var rb = bone.gameObject.AddComponent<Rigidbody>();
                rb.mass = MassFor(bone.name);
                rb.drag = 0.05f;
                rb.angularDrag = 0.5f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rigidbodies[bone] = rb;

                var collider = bone.gameObject.AddComponent<CapsuleCollider>();
                SizeCollider(bone, collider);
            }

            if (rigidbodies.Count < 4) return false;

            // Joints: connect every physics bone to its nearest physics ancestor
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
                joint.axis = Vector3.right;
                joint.swingAxis = Vector3.up;

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

        /// <summary>Size a capsule along the dominant axis towards the bone's main child.</summary>
        private static void SizeCollider(Transform bone, CapsuleCollider collider)
        {
            Transform child = MainChild(bone);
            Vector3 childLocal = child != null ? child.localPosition : Vector3.zero;
            float length = childLocal.magnitude;

            int axis = 1; // Y
            float best = Mathf.Abs(childLocal.y);
            if (Mathf.Abs(childLocal.x) > best) { axis = 0; best = Mathf.Abs(childLocal.x); }
            if (Mathf.Abs(childLocal.z) > best) { axis = 2; best = Mathf.Abs(childLocal.z); }

            collider.direction = axis;
            collider.height = Mathf.Max(length, 0.08f);
            collider.radius = Mathf.Clamp(length * 0.28f, 0.035f, 0.13f);
            collider.center = childLocal * 0.5f;

            // Bones with no usable child (head, hands, feet) get a blob
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
            }

            var pelvisBody = _pelvis.GetComponent<Rigidbody>();
            if (pelvisBody != null)
            {
                pelvisBody.AddTorque(UnityEngine.Random.insideUnitSphere * LaunchSpin, ForceMode.VelocityChange);
            }

            RakeMultiplayerPlugin.Log?.LogInfo("[Ragdoll] Hit reaction started (dir=" + dir.ToString("F2") + ")");
        }

        private void LateUpdate()
        {
            // First-person view from the ragdoll's head: it tumbles with the body so the
            // hit is felt, and the world spins around the player.
            if (_camera != null && _head != null)
            {
                _camera.transform.position = _head.TransformPoint(_headLocalEye);
                _camera.transform.rotation = _head.rotation * _headRotationOffset;
            }

            _timer += Time.deltaTime;
            if (_timer >= Duration && !_restored) Restore();
        }

        private void Restore()
        {
            _restored = true;
            _instance = null;

            RestorePlayerCameras();

            if (_camera != null) UnityEngine.Object.Destroy(_camera.gameObject);
            _camera = null;

            if (_controller != null) _controller.enabled = true;
            PlayerInputLock.SetRagdoll(false);

            RakeMultiplayerPlugin.Log?.LogInfo("[Ragdoll] Hit reaction finished");
            UnityEngine.Object.Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ---------------------------------------------------------------- cameras

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

        private void CreateCamera()
        {
            var go = new GameObject("RagdollHeadCamera");
            go.hideFlags = HideFlags.HideAndDontSave;

            _camera = go.AddComponent<Camera>();
            if (_originalCamera != null)
            {
                _camera.CopyFrom(_originalCamera);
                _camera.depth = _originalCamera.depth + 1f;
            }

            // The camera sits inside the head, so pull the near plane in
            _camera.nearClipPlane = 0.03f;
            _camera.enabled = true;

            if (_head != null)
            {
                _camera.transform.position = _head.TransformPoint(_headLocalEye);
                _camera.transform.rotation = _head.rotation * _headRotationOffset;
            }
        }
    }
}
