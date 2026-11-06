using System;
using System.Collections;
using System.IO;
using UnityEngine;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    /// <summary>
    /// Networked Rake AI - server-authoritative Rake synchronization
    /// Uses new RakeStatePacket protocol
    /// </summary>
    public class NetworkedRakeAI : MonoBehaviour
    {
        [Header("Sync Settings")]
        public float syncRate = 0.066f; // 15Hz unified rate

        private Enemy_AI enemyAI;
        private Animator animator;
        private AudioSource audioSource;

        // Sync timer
        private float sendTimer;

        // Last sent values
        private Vector3 lastSentPosition;
        private byte lastSentState;
        private float lastSentHealth;

        // Interpolation targets for clients
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private byte targetState;
        private float targetHealth;
        private bool hasTargetPosition = false;
        private bool hasTargetRotation = false;

        private bool _initialized = false;

        public enum RakeState : byte
        {
            Idle = 0,
            Patrol = 1,
            Chase = 2,
            Attack = 3,
            Search = 4,
            Stunned = 5,
            Dead = 6
        }

        private void Awake()
        {
            enemyAI = GetComponent<Enemy_AI>();
            animator = GetComponent<Animator>();
            audioSource = GetComponent<AudioSource>();

            lastSentPosition = transform.position;
            targetPosition = transform.position;
            targetRotation = transform.rotation;
        }

        private IEnumerator Start()
        {
            // Wait one frame for all Awake/Start to complete (race condition fix)
            yield return null;

            // Disable local AI on clients (server-authoritative)
            if (!RakeMultiplayerPlugin.isServer && enemyAI != null)
            {
                enemyAI.enabled = false;
            }

            _initialized = true;
        }

        private void Update()
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (!_initialized) return;

            // Only server syncs Rake
            if (RakeMultiplayerPlugin.isServer)
            {
                if (!RakeMultiplayerPlugin.isConnected) return;

                sendTimer += Time.deltaTime;
                if (sendTimer >= syncRate)
                {
                    SendRakeState();
                    sendTimer = 0f;
                }
            }
            else
            {
                // Client: interpolate to target values
                if (!RakeMultiplayerPlugin.isConnected) return;

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

        private void SendRakeState()
        {
            Vector3 currentPos = transform.position;
            byte currentState = GetCurrentState();
            float currentHealth = GetCurrentHealth();

            // Delta compression check
            bool posChanged = Vector3.Distance(currentPos, lastSentPosition) > 0.05f;
            bool stateChanged = currentState != lastSentState;
            bool healthChanged = Math.Abs(currentHealth - lastSentHealth) > 1f;

            if (!posChanged && !stateChanged && !healthChanged) return;

            var packet = new RakeStatePacket
            {
                Position = currentPos,
                Rotation = transform.rotation,
                State = currentState,
                Health = currentHealth
            };

            var data = packet.Serialize();

            // Broadcast to all clients
            var manager = RakeNetworkManager.Instance;
            if (manager != null)
            {
                manager.ServerBroadcast(data);
            }

            lastSentPosition = currentPos;
            lastSentState = currentState;
            lastSentHealth = currentHealth;
        }

        private byte GetCurrentState()
        {
            if (enemyAI == null) return 0;

            // Enemy_AI uses boolean flags (no single currentState field)
            // Encode as bitmask: bit0=bDanger, bit1=bHaunt, bit2=bVisible, bit3=bPlayerHount,
            // bit4=bKillingProccess, bit5=bPlayerKilling, bit6=bCameraBreak, bit7=bHungry
            byte state = 0;
            string[] flagNames = new string[] { "bDanger", "bHaunt", "bVisible", "bPlayerHount", "bKillingProccess", "bPlayerKilling", "bCameraBreak", "bHungry" };

            for (int i = 0; i < flagNames.Length; i++)
            {
                var field = typeof(Enemy_AI).GetField(flagNames[i], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null && (bool)field.GetValue(enemyAI))
                {
                    state |= (byte)(1 << i);
                }
            }
            return state;
        }

        private float GetCurrentHealth()
        {
            if (enemyAI == null) return 100f;
            var healthField = typeof(Enemy_AI).GetField("Health", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (healthField != null)
            {
                return Convert.ToSingle(healthField.GetValue(enemyAI));
            }
            return 100f;
        }

        // Client-side apply (for 3.4)
        public void ApplySyncData(Vector3 position, Quaternion rotation, byte state, float health)
        {
            if (RakeMultiplayerPlugin.isServer) return; // Don't apply on server

            // Set interpolation targets instead of direct assignment
            targetPosition = position;
            targetRotation = rotation;
            hasTargetPosition = true;
            hasTargetRotation = true;
            targetState = state;
            targetHealth = health;

            // Apply state/health immediately (they don't interpolate)
            ApplyState(state);
            ApplyHealth(health);
        }

        private void ApplyState(byte state)
        {
            // Decode bitmask and set boolean flags on Enemy_AI
            if (enemyAI != null)
            {
                string[] flagNames = new string[] { "bDanger", "bHaunt", "bVisible", "bPlayerHount", "bKillingProccess", "bPlayerKilling", "bCameraBreak", "bHungry" };

                for (int i = 0; i < flagNames.Length; i++)
                {
                    var field = typeof(Enemy_AI).GetField(flagNames[i], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        field.SetValue(enemyAI, (state & (1 << i)) != 0);
                    }
                }
            }

            if (animator != null)
            {
                animator.SetInteger("State", state);
            }
        }

        public void ApplyHealth(float health)
        {
            if (enemyAI != null)
            {
                var healthField = typeof(Enemy_AI).GetField("Health", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (healthField != null)
                {
                    healthField.SetValue(enemyAI, health);
                }
            }
        }
    }
}