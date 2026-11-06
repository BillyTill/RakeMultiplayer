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
            if (!RakeMultiplayerPlugin.isServer) return;
            if (!RakeMultiplayerPlugin.isConnected) return;

            sendTimer += Time.deltaTime;
            if (sendTimer >= syncRate)
            {
                SendRakeState();
                sendTimer = 0f;
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
            if (enemyAI == null) return (byte)RakeState.Idle;

            // Map Enemy_AI states to our enum
            var stateField = typeof(Enemy_AI).GetField("currentState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (stateField != null)
            {
                var stateValue = stateField.GetValue(enemyAI);
                if (stateValue is int intState)
                {
                    return (byte)Mathf.Clamp(intState, 0, 6);
                }
            }
            return (byte)RakeState.Idle;
        }

        private float GetCurrentHealth()
        {
            if (enemyAI == null) return 100f;
            var healthField = typeof(Enemy_AI).GetField("health", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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

            transform.position = position;
            transform.rotation = rotation;
            ApplyState(state);
            ApplyHealth(health);
        }

        private void ApplyState(byte state)
        {
            // Apply state to Enemy_AI
            if (enemyAI != null)
            {
                var stateField = typeof(Enemy_AI).GetField("currentState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (stateField != null)
                {
                    stateField.SetValue(enemyAI, (int)state);
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
                var healthField = typeof(Enemy_AI).GetField("health", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (healthField != null)
                {
                    healthField.SetValue(enemyAI, health);
                }
            }
        }
    }
}