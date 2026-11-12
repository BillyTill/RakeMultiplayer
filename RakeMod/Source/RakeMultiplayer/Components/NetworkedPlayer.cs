using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    /// <summary>
    /// Networked player component - handles synchronization of player state
    /// Uses new PacketType/PlayerStatePacket protocol
    /// </summary>
    [RequireComponent(typeof(FPSController))]
    public class NetworkedPlayer : MonoBehaviour
    {
        [Header("Sync Settings")]
        public float syncRate = 0.05f; // 20Hz unified rate

        private FPSController fpsController;
        private PlayerMovement playerMovement;
        private CameraMouseLook cameraMouseLook;
        private Animator _cachedAnimator;
        private WeaponManager _weaponManager;

        // Cached reflection lookup of the weapon classes' public bool bSight flag
        private static readonly Dictionary<Type, System.Reflection.FieldInfo> _sightFields =
            new Dictionary<Type, System.Reflection.FieldInfo>();

        // Sync timer
        private float sendTimer;

        // Last sent values for delta compression
        private Vector3 lastSentPosition;
        private Quaternion lastSentRotation;
        private byte lastSentAnimState;

        // Player ID (assigned by server)
        public int PlayerId { get; private set; }

        // Interpolation targets for remote players
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private byte targetAnimState;
        private bool hasTargetPosition = false;
        private bool hasTargetRotation = false;

        private void Awake()
        {
            fpsController = GetComponent<FPSController>();
            playerMovement = GetComponent<PlayerMovement>();
            cameraMouseLook = GetComponentInChildren<CameraMouseLook>();
            _cachedAnimator = GetComponentInChildren<Animator>();
            _weaponManager = GetComponentInChildren<WeaponManager>();

            lastSentPosition = transform.position;
            lastSentRotation = transform.rotation;
            targetPosition = transform.position;
            targetRotation = transform.rotation;
        }

        private void Start()
        {
            // Register with player spawner (only for local player)
            if (isLocalPlayer && PlayerSpawner.Instance != null)
            {
                PlayerSpawner.Instance.RegisterLocalPlayer(this);
            }
        }

        private void Update()
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (!RakeMultiplayerPlugin.isConnected) return;

            if (isLocalPlayer)
            {
                // Local player: send updates
                sendTimer += Time.deltaTime;
                if (sendTimer >= syncRate)
                {
                    SendPlayerState();
                    sendTimer = 0f;
                }
            }
            else
            {
                // Remote player: interpolate
                if (hasTargetPosition)
                {
                    transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 15f);
                }
                if (hasTargetRotation)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 15f);
                }
            }
        }

        public bool isLocalPlayer { get; private set; }

        public void SetAsLocalPlayer()
        {
            isLocalPlayer = true;
            enabled = true;
            
            // Assign local player ID
            if (RakeMultiplayerPlugin.isServer)
            {
                // Host gets fixed ID 1
                PlayerId = 1;
            }
            // Client gets ID from PlayerConnected packet (handled in HandleClientPlayerConnected)
        }

        public void SetPlayerId(int id)
        {
            PlayerId = id;

            if (isLocalPlayer && PlayerSpawner.Instance != null)
            {
                PlayerSpawner.Instance.NotifyLocalPlayerId(id);
            }
        }

        public void SetAsRemotePlayer()
        {
            isLocalPlayer = false;
            enabled = true; // NetworkedPlayer stays enabled to receive sync data

            // FPSController, WeaponManager, PlayerMovement уже отключены в SpawnRemotePlayer ДО Start()
            // Здесь добираем остальное и гарантируем отключение рендера рук/оружия

            var fps = GetComponent<FPSController>();
            if (fps) fps.enabled = false; // На случай, если вызвано не из SpawnRemotePlayer

            var pm = GetComponent<PlayerMovement>();
            if (pm) pm.enabled = false;

            var cml = GetComponentInChildren<CameraMouseLook>();
            if (cml) cml.enabled = false;

            // FPSCamera runs the "Press E to pickup" raycast using the LOCAL keyboard;
            // on a clone it would pick up items for the local player.
            var fpsCamera = GetComponentInChildren<FPSCamera>();
            if (fpsCamera) fpsCamera.enabled = false;

            var wm = GetComponentInChildren<WeaponManager>();
            if (wm) wm.enabled = false;

            foreach (var weapon in GetComponentsInChildren<Weapon>(true))
            {
                if (weapon != null) weapon.enabled = false;
            }

            var rifle = GetComponentInChildren<Rifle_weapon>();
            if (rifle) rifle.enabled = false;

            var sniper = GetComponentInChildren<SniperRifle_weapon>();
            if (sniper) sniper.enabled = false;

            var pauseMgr = GetComponent<PauseManager>();
            if (pauseMgr) pauseMgr.enabled = false;

            var gamePauseMenu = GetComponent<GamePauseMenu>();
            if (gamePauseMenu) gamePauseMenu.enabled = false;

            var inventory = GetComponent<Inventory>();
            if (inventory) inventory.enabled = false;

            // Disable CharacterController (collision blocking host movement)
            var cc = GetComponent<CharacterController>();
            if (cc) cc.enabled = false;

            // Disable Rigidbody (prevent falling/jitter)
            var rb = GetComponent<Rigidbody>();
            if (rb)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }

            // Disable ALL cameras and AudioListener on remote player
            var cameras = GetComponentsInChildren<Camera>(true);
            foreach (var cam in cameras)
            {
                cam.enabled = false;
                RakeMultiplayerPlugin.Log?.LogInfo($"[3.5] Disabled camera: {cam.name}");
            }

            var listener = GetComponentInChildren<AudioListener>();
            if (listener) listener.enabled = false;

            // CRITICAL: Disable weapon camera (weapCam) which renders the view model (hands/weapon)
            // Используем прямое поле (public в FPSController), а не рефлексию
            if (fps != null && fps.weapCam != null)
            {
                fps.weapCam.enabled = false;
                RakeMultiplayerPlugin.Log?.LogInfo("[3.5] Disabled weapCam (weapon view model camera) via direct field");
            }

            // CRITICAL: Disable all weapon objects (view model) in WeaponManager
            // WeaponManager.playerWeapons — public List<Transform>
            if (wm != null && wm.playerWeapons != null)
            {
                foreach (var weapon in wm.playerWeapons)
                {
                    if (weapon != null && weapon.gameObject != null)
                    {
                        weapon.gameObject.SetActive(false);
                        RakeMultiplayerPlugin.Log?.LogInfo($"[3.5] Disabled weapon view model: {weapon.name}");
                    }
                }
            }

            // Ensure Animator is enabled for animation sync (if body model exists)
            var anim = GetComponentInChildren<Animator>();
            if (anim) anim.enabled = true;
        }

        /// <summary>
        /// Immediately broadcast the local player's state (used right after a scene load /
        /// remote-player spawn so others do not have to wait for movement to see us).
        /// </summary>
        public void ForceSendState()
        {
            if (!isLocalPlayer) return;
            SendPlayerState(force: true);
        }

        private void SendPlayerState(bool force = false)
        {
            Vector3 currentPos = transform.position;
            Quaternion currentRot = transform.rotation;
            byte currentAnimState = GetCurrentAnimState();

            if (currentAnimState != lastSentAnimState)
            {
                var wmLog = GetWeaponManager();
                RakeMultiplayerPlugin.Log?.LogInfo("[Anim] state=" + currentAnimState +
                    " weaponID=" + (wmLog != null ? wmLog.currentWeaponID : -1) +
                    " sight=" + (wmLog != null && ReadSight(wmLog.weaponSelect)));
            }

            // Delta compression check
            bool posChanged = Vector3.Distance(currentPos, lastSentPosition) > 0.01f;
            bool rotChanged = Quaternion.Angle(currentRot, lastSentRotation) > 0.5f;
            bool animChanged = currentAnimState != lastSentAnimState;

            if (!force && !posChanged && !rotChanged && !animChanged) return;

            var packet = new PlayerStatePacket
            {
                PlayerId = PlayerId,
                Position = currentPos,
                Rotation = currentRot,
                AnimState = currentAnimState
            };

            var data = packet.Serialize();

            if (RakeMultiplayerPlugin.isServer)
            {
                // Host: broadcast to all clients
                var manager = RakeNetworkManager.Instance;
                if (manager != null)
                {
                    manager.ServerBroadcast(data);
                }
            }
            else
            {
                // Client: send to server
                RakeMultiplayerPlugin.SendToServer(data);
            }

            lastSentPosition = currentPos;
            lastSentRotation = currentRot;
            lastSentAnimState = currentAnimState;
        }

        private byte GetCurrentAnimState()
        {
            // Bitmask: bit0=running, bit1=crouching, bit2=aiming, bit3=grounded,
            //          bit4=weapon out, bit5=flashlight on
            byte state = 0;
            if (_cachedAnimator != null)
            {
                if (_cachedAnimator.GetBool("Run")) state |= 1 << 0;
                if (_cachedAnimator.GetBool("Crouch")) state |= 1 << 1;
                if (_cachedAnimator.GetBool("Grounded")) state |= 1 << 3;
            }

            var wm = GetWeaponManager();
            if (wm != null)
            {
                if (HasWeaponOut(wm)) state |= 1 << 4;

                // aiming is the sight key (Fire2 / right mouse) handled by the weapon script
                if (ReadSight(wm.weaponSelect)) state |= 1 << 2;
            }

            var fps = GetComponent<FPSController>();
            if (fps != null && fps.flashlight != null && fps.flashlight.enabled) state |= 1 << 5;

            // The hunter is hidden while its owner plays as the Rake.
            if (isLocalPlayer && RakePlayerController.IsActive) state |= 1 << 6;

            return state;
        }

        /// <summary>
        /// True while a real weapon is out. currentWeaponID alone was unreliable: the game
        /// leaves it at 0 in some pickup paths while the weapon model is still held, so the
        /// remote body never got its rifle.
        /// </summary>
        private static bool HasWeaponOut(WeaponManager wm)
        {
            if (wm.currentWeaponID > 0) return true;

            var selected = wm.weaponSelect;
            if (selected == null || !selected.gameObject.activeInHierarchy) return false;

            var weapon = selected.GetComponent<Weapon>();
            return weapon != null && weapon.weaponID > 0;
        }

        private WeaponManager GetWeaponManager()
        {
            if (_weaponManager != null) return _weaponManager;

            // The weapon hierarchy can still be inactive during Awake, so resolve lazily
            // and include inactive children.
            _weaponManager = WeaponManager.instance;
            if (_weaponManager == null) _weaponManager = GetComponentInChildren<WeaponManager>();
            return _weaponManager;
        }

        /// <summary>
        /// Read the sight/aim flag from the currently selected weapon. Every weapon class
        /// (Rifle_weapon, ...) declares its own public bool bSight, so it is looked up by
        /// name once per type instead of depending on a concrete class.
        /// </summary>
        private static bool ReadSight(Transform weaponSelect)
        {
            if (weaponSelect == null) return false;

            var components = weaponSelect.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null) continue;

                Type type = component.GetType();
                System.Reflection.FieldInfo field;
                if (!_sightFields.TryGetValue(type, out field))
                {
                    field = type.GetField("bSight",
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);
                    _sightFields[type] = field;
                }

                if (field != null && field.FieldType == typeof(bool))
                {
                    return (bool)field.GetValue(component);
                }
            }
            return false;
        }

        public void ApplyPosition(Vector3 position)
        {
            if (!isLocalPlayer)
            {
                targetPosition = position;
                hasTargetPosition = true;
            }
        }

        public void ApplyRotation(Quaternion rotation)
        {
            if (!isLocalPlayer)
            {
                targetRotation = rotation;
                hasTargetRotation = true;
            }
        }

        /// <summary>
        /// Last animation bitmask for this player: bit0 running, bit1 crouching,
        /// bit2 aiming, bit3 grounded. Used by the third-person body to pick a clip.
        /// </summary>
        public byte CurrentAnimState
        {
            get { return isLocalPlayer ? lastSentAnimState : targetAnimState; }
        }
        public void ApplyAnimation(byte animState)
        {
            if (!isLocalPlayer)
            {
                targetAnimState = animState;
                var anim = GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    anim.SetBool("Run", (animState & (1 << 0)) != 0);
                    anim.SetBool("Crouch", (animState & (1 << 1)) != 0);
                    anim.SetBool("Aim", (animState & (1 << 2)) != 0);
                    anim.SetBool("Grounded", (animState & (1 << 3)) != 0);
                    ApplyFlashlight((animState & (1 << 5)) != 0);
                }
            }
        }

        public void ApplyHealth(float health)
        {
            // Apply health to local player components
        }

        public void ApplyStamina(float stamina)
        {
            // Apply stamina to local player components
        }

        public void ApplyFlashlight(bool enabled)
        {
            if (isLocalPlayer) return;

            var fps = GetComponent<FPSController>();
            if (fps == null || fps.flashlight == null) return;
            if (fps.flashlight.enabled == enabled) return;

            fps.flashlight.enabled = enabled;
        }
    }
}