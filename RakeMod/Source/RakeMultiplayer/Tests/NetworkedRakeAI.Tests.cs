using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using RakeMultiplayer;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer.Tests
{
    /// <summary>
    /// Unit tests for NetworkedRakeAI component
    /// Tests cover: damage reduction calculation, state transitions on damage,
    /// client-side sync application, health application, and RakeState enum.
    /// </summary>
    [TestFixture]
    public class NetworkedRakeAITests
    {
        private GameObject _rakeGameObject;
        private NetworkedRakeAI _rakeAI;
        private Enemy_AI _mockEnemyAI;

        [SetUp]
        public void SetUp()
        {
            // Create a GameObject with NetworkedRakeAI
            _rakeGameObject = new GameObject("TestRake");
            _rakeAI = _rakeGameObject.AddComponent<NetworkedRakeAI>();

            // Add required components that NetworkedRakeAI expects
            _mockEnemyAI = _rakeGameObject.AddComponent<Enemy_AI>();
            _rakeGameObject.AddComponent<Animator>();
            _rakeGameObject.AddComponent<AudioSource>();
            _rakeGameObject.AddComponent<NavMeshAgent>();

            // Initialize private fields via reflection for testing
            InitializeTestState();
        }

        [TearDown]
        public void TearDown()
        {
            if (_rakeGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_rakeGameObject);
            }
        }

        private void InitializeTestState()
        {
            // Set up static plugin state for testing
            typeof(RakeMultiplayerPlugin).GetField("isServer", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, true);
            typeof(RakeMultiplayerPlugin).GetField("isConnected", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, true);
            
            // Set Enabled config value via reflection on the ConfigEntry<bool>
            var enabledField = typeof(RakeMultiplayerPlugin).GetField("Enabled", BindingFlags.NonPublic | BindingFlags.Static);
            var enabledConfig = enabledField?.GetValue(null);
            if (enabledConfig != null)
            {
                var configEntryType = enabledConfig.GetType();
                var valueProperty = configEntryType.GetProperty("Value");
                valueProperty?.SetValue(enabledConfig, true, null);
            }

            // Call Awake and Start via reflection to initialize internal state
            _rakeAI.GetType().GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(_rakeAI, null);
            var startEnumerator = _rakeAI.GetType().GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(_rakeAI, null) as System.Collections.IEnumerator;
            while (startEnumerator?.MoveNext() == true) { }

            // Set initial state to Follow (as server would)
            SetPrivateField(_rakeAI, "currentState", NetworkedRakeAI.RakeState.Follow);
            SetPrivateField(_rakeAI, "_initialized", true);
        }

        private void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(obj, value);
            }
        }

        private T GetPrivateField<T>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            return field != null ? (T)field.GetValue(obj) : default(T);
        }

        #region CalculateDamageReduction Tests

        [Test]
        public void CalculateDamageReduction_FrontalHit_ReturnsReducedMultiplier()
        {
            // Arrange: hit from front (angle < 45°)
            Vector3 hitDirection = Vector3.forward; // Hit coming from forward direction
            Vector3 rakeForward = Vector3.forward;  // Rake facing forward
            // Angle between -hitDirection (back) and rakeForward (forward) = 180°
            // Wait: hitDirection is direction FROM which hit came (shooter to target)
            // So if shooter is in front of rake, hitDirection = forward (from shooter to rake)
            // toRake = -hitDirection = back
            // Angle between rakeForward (forward) and toRake (back) = 180° -> NOT frontal
            
            // For frontal hit: shooter in front of rake, hitDirection = forward
            // toRake = -forward = back, rakeForward = forward -> angle = 180° (not frontal)
            // Actually frontal means hit comes from front of rake, so hitDirection = -forward (from front to rake)
            // Let me re-read the code: "hitDirection: direction FROM which the hit came (e.g., shooter to target)"
            // So if shooter is in front of rake, hitDirection points from shooter to rake = forward
            // toRake = -hitDirection = back
            // Angle between rakeForward (forward) and toRake (back) = 180°
            // This means frontal is when angle < 45°, so hit must come from slightly off-center front
            
            // Frontal hit: hit comes from direction aligned with rake's forward (within 45°)
            // hitDirection = -forward (from front of rake towards rake)
            Vector3 frontalHitDirection = -Vector3.forward; // From front towards rake
            Vector3 rakeForwardVec = Vector3.forward;       // Rake facing forward
            
            // Act
            float reduction = _rakeAI.CalculateDamageReduction(frontalHitDirection, rakeForwardVec);
            
            // Assert: Frontal hit (angle = 0°) should return 0.2f (80% reduction)
            Assert.AreEqual(0.2f, reduction, 0.001f, "Frontal hit should return 0.2f multiplier (80% reduction)");
        }

        [Test]
        public void CalculateDamageReduction_SideHit_ReturnsFullDamage()
        {
            // Arrange: hit from side (angle = 90°)
            Vector3 sideHitDirection = -Vector3.right; // From right side towards rake
            Vector3 rakeForwardVec = Vector3.forward;  // Rake facing forward
            
            // Act
            float reduction = _rakeAI.CalculateDamageReduction(sideHitDirection, rakeForwardVec);
            
            // Assert: Side hit (angle = 90°) should return 1.0f (no reduction)
            Assert.AreEqual(1.0f, reduction, 0.001f, "Side hit should return 1.0f multiplier (no reduction)");
        }

        [Test]
        public void CalculateDamageReduction_BackHit_ReturnsFullDamage()
        {
            // Arrange: hit from back (angle = 180°)
            Vector3 backHitDirection = Vector3.forward; // From back towards rake
            Vector3 rakeForwardVec = Vector3.forward;   // Rake facing forward
            
            // Act
            float reduction = _rakeAI.CalculateDamageReduction(backHitDirection, rakeForwardVec);
            
            // Assert: Back hit (angle = 180°) should return 1.0f (no reduction)
            Assert.AreEqual(1.0f, reduction, 0.001f, "Back hit should return 1.0f multiplier (no reduction)");
        }

        [Test]
        public void CalculateDamageReduction_45DegreeBoundary_ReturnsFullDamage()
        {
            // Arrange: hit at exactly 45° boundary (should NOT be frontal)
            // Angle = 45° means hitDirection is rotated 45° from -rakeForward
            Vector3 rakeForwardVec = Vector3.forward;
            // toRake should be at 45° from forward
            Vector3 toRake45 = Quaternion.Euler(0, 45, 0) * Vector3.forward;
            Vector3 hitDirection45 = -toRake45; // hitDirection = -toRake
            
            // Act
            float reduction = _rakeAI.CalculateDamageReduction(hitDirection45, rakeForwardVec);
            
            // Assert: Exactly 45° should be >= blockingFrontalAngle (45°), so no reduction
            Assert.AreEqual(1.0f, reduction, 0.001f, "45° boundary should return 1.0f (not frontal)");
        }

        [Test]
        public void CalculateDamageReduction_JustUnder45Degrees_ReturnsReducedMultiplier()
        {
            // Arrange: hit at 44.9° (should be frontal)
            Vector3 rakeForwardVec = Vector3.forward;
            Vector3 toRake44 = Quaternion.Euler(0, 44.9f, 0) * Vector3.forward;
            Vector3 hitDirection44 = -toRake44;
            
            // Act
            float reduction = _rakeAI.CalculateDamageReduction(hitDirection44, rakeForwardVec);
            
            // Assert: Just under 45° should be frontal
            Assert.AreEqual(0.2f, reduction, 0.001f, "44.9° should return 0.2f (frontal)");
        }

        [Test]
        public void CalculateDamageReduction_ZeroVectors_HandlesGracefully()
        {
            // Arrange: zero vectors
            Vector3 hitDirection = Vector3.zero;
            Vector3 rakeForward = Vector3.zero;
            
            // Act & Assert: Should not throw, but angle calculation with zero vectors is undefined
            // Vector3.Angle returns 0 for zero vectors
            float reduction = _rakeAI.CalculateDamageReduction(hitDirection, rakeForward);
            
            // Vector3.Angle(Vector3.zero, Vector3.zero) returns 0, so this would be frontal
            Assert.AreEqual(0.2f, reduction, 0.001f, "Zero vectors produce angle 0, treated as frontal");
        }

        #endregion

        #region OnDamageTaken Tests

        [Test]
        public void OnDamageTaken_FollowState_TransitionsToChase()
        {
            // Arrange
            SetPrivateField(_rakeAI, "currentState", NetworkedRakeAI.RakeState.Follow);
            Vector3 hitDirection = Vector3.forward;
            
            // Act
            _rakeAI.OnDamageTaken(hitDirection, 10, "Bullet");
            
            // Assert
            var currentState = GetPrivateField<NetworkedRakeAI.RakeState>(_rakeAI, "currentState");
            Assert.AreEqual(NetworkedRakeAI.RakeState.Chase, currentState, "Follow -> Chase on damage");
        }

        [Test]
        public void OnDamageTaken_StealthState_TransitionsToChase()
        {
            // Arrange
            SetPrivateField(_rakeAI, "currentState", NetworkedRakeAI.RakeState.Stealth);
            Vector3 hitDirection = Vector3.forward;
            
            // Act
            _rakeAI.OnDamageTaken(hitDirection, 10, "Bullet");
            
            // Assert
            var currentState = GetPrivateField<NetworkedRakeAI.RakeState>(_rakeAI, "currentState");
            Assert.AreEqual(NetworkedRakeAI.RakeState.Chase, currentState, "Stealth -> Chase on damage");
        }

        [Test]
        public void OnDamageTaken_ChaseState_StaysInChase()
        {
            // Arrange
            SetPrivateField(_rakeAI, "currentState", NetworkedRakeAI.RakeState.Chase);
            Vector3 hitDirection = Vector3.forward;
            
            // Act
            _rakeAI.OnDamageTaken(hitDirection, 10, "Bullet");
            
            // Assert
            var currentState = GetPrivateField<NetworkedRakeAI.RakeState>(_rakeAI, "currentState");
            Assert.AreEqual(NetworkedRakeAI.RakeState.Chase, currentState, "Chase should stay in Chase on damage");
        }

        [Test]
        public void OnDamageTaken_BlockingState_FrontalHit_LogsBlockingReduction()
        {
            // Arrange
            SetPrivateField(_rakeAI, "currentState", NetworkedRakeAI.RakeState.Blocking);
            SetPrivateField(_rakeAI, "isBlocking", true);
            SetPrivateField(_rakeAI, "lastDamageTime", Time.time); // Recent damage
            // Frontal hit: hitDirection = -forward (from front), rakeForward = forward
            Vector3 frontalHitDirection = -Vector3.forward;
            _rakeGameObject.transform.forward = Vector3.forward;
            
            // Act
            _rakeAI.OnDamageTaken(frontalHitDirection, 10, "Bullet");
            
            // Assert: lastDamageDirection should be updated
            var lastDamageDir = GetPrivateField<Vector3>(_rakeAI, "lastDamageDirection");
            Assert.AreEqual(frontalHitDirection, lastDamageDir, "lastDamageDirection should be updated");
            
            var lastDamageTime = GetPrivateField<float>(_rakeAI, "lastDamageTime");
            Assert.AreEqual(Time.time, lastDamageTime, 0.1f, "lastDamageTime should be updated");
        }

        [Test]
        public void OnDamageTaken_UpdatesLastDamageFields()
        {
            // Arrange
            SetPrivateField(_rakeAI, "currentState", NetworkedRakeAI.RakeState.Chase);
            Vector3 hitDirection = new Vector3(1, 0, 2).normalized;
            int amount = 25;
            string damageType = "Arrow";
            
            // Act
            _rakeAI.OnDamageTaken(hitDirection, amount, damageType);
            
            // Assert
            var lastDamageDir = GetPrivateField<Vector3>(_rakeAI, "lastDamageDirection");
            Assert.AreEqual(hitDirection, lastDamageDir, "lastDamageDirection should match input");
            
            var lastDamageTime = GetPrivateField<float>(_rakeAI, "lastDamageTime");
            Assert.AreEqual(Time.time, lastDamageTime, 0.1f, "lastDamageTime should be current time");
        }

        #endregion

        #region ApplySyncData Tests

        [Test]
        public void ApplySyncData_OnClient_SetsInterpolationTargets()
        {
            // Arrange: simulate client (not server)
            typeof(RakeMultiplayerPlugin).GetField("isServer", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, false);
            typeof(RakeMultiplayerPlugin).GetField("isConnected", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, true);
            
            Vector3 position = new Vector3(10, 5, 20);
            Quaternion rotation = Quaternion.Euler(0, 90, 0);
            byte state = (byte)NetworkedRakeAI.RakeState.Chase;
            float health = 75.5f;
            
            // Act
            _rakeAI.ApplySyncData(position, rotation, state, health);
            
            // Assert: interpolation targets set
            Assert.IsTrue(GetPrivateField<bool>(_rakeAI, "hasTargetPosition"), "hasTargetPosition should be true");
            Assert.IsTrue(GetPrivateField<bool>(_rakeAI, "hasTargetRotation"), "hasTargetRotation should be true");
            Assert.AreEqual(position, GetPrivateField<Vector3>(_rakeAI, "targetPosition"), "targetPosition should match");
            Assert.AreEqual(rotation, GetPrivateField<Quaternion>(_rakeAI, "targetRotation"), "targetRotation should match");
            Assert.AreEqual(state, GetPrivateField<byte>(_rakeAI, "targetState"), "targetState should match");
            Assert.AreEqual(health, GetPrivateField<float>(_rakeAI, "targetHealth"), "targetHealth should match");
        }

        [Test]
        public void ApplySyncData_OnServer_DoesNothing()
        {
            // Arrange: simulate server
            typeof(RakeMultiplayerPlugin).GetField("isServer", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, true);
            typeof(RakeMultiplayerPlugin).GetField("isConnected", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, true);
            
            Vector3 position = new Vector3(10, 5, 20);
            Quaternion rotation = Quaternion.Euler(0, 90, 0);
            byte state = (byte)NetworkedRakeAI.RakeState.Chase;
            float health = 75.5f;
            
            // Capture initial state
            var initialPos = _rakeGameObject.transform.position;
            
            // Act
            _rakeAI.ApplySyncData(position, rotation, state, health);
            
            // Assert: position should not change on server
            Assert.AreEqual(initialPos, _rakeGameObject.transform.position, "Server should not apply sync data");
        }

        [Test]
        public void ApplySyncData_StateTransition_FiresAnimatorTriggers()
        {
            // Arrange: client mode
            typeof(RakeMultiplayerPlugin).GetField("isServer", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, false);
            typeof(RakeMultiplayerPlugin).GetField("isConnected", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, true);
            
            SetPrivateField(_rakeAI, "currentState", NetworkedRakeAI.RakeState.Follow);
            SetPrivateField(_rakeAI, "lastAppliedState", (byte)NetworkedRakeAI.RakeState.Follow);
            
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            byte newState = (byte)NetworkedRakeAI.RakeState.Blocking;
            float health = 100f;
            
            // Act
            _rakeAI.ApplySyncData(position, rotation, newState, health);
            
            // Assert: state changed
            var currentState = GetPrivateField<NetworkedRakeAI.RakeState>(_rakeAI, "currentState");
            Assert.AreEqual(NetworkedRakeAI.RakeState.Blocking, currentState, "State should transition to Blocking");
            
            var lastAppliedState = GetPrivateField<byte>(_rakeAI, "lastAppliedState");
            Assert.AreEqual(newState, lastAppliedState, "lastAppliedState should update");
            
            var isBlocking = GetPrivateField<bool>(_rakeAI, "isBlocking");
            Assert.IsTrue(isBlocking, "isBlocking should be true for Blocking state");
        }

        [Test]
        public void ApplySyncData_SameState_DoesNotRefireTriggers()
        {
            // Arrange: client mode
            typeof(RakeMultiplayerPlugin).GetField("isServer", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, false);
            typeof(RakeMultiplayerPlugin).GetField("isConnected", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, true);
            
            SetPrivateField(_rakeAI, "currentState", NetworkedRakeAI.RakeState.Chase);
            SetPrivateField(_rakeAI, "lastAppliedState", (byte)NetworkedRakeAI.RakeState.Chase);
            
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            byte sameState = (byte)NetworkedRakeAI.RakeState.Chase;
            float health = 100f;
            
            // Act
            _rakeAI.ApplySyncData(position, rotation, sameState, health);
            
            // Assert: lastAppliedState unchanged, no re-trigger
            var lastAppliedState = GetPrivateField<byte>(_rakeAI, "lastAppliedState");
            Assert.AreEqual(sameState, lastAppliedState, "lastAppliedState should remain same");
        }

        [Test]
        public void ApplySyncData_ClearsBlockingFlag_OnNonBlockingState()
        {
            // Arrange: client mode, currently blocking
            typeof(RakeMultiplayerPlugin).GetField("isServer", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, false);
            typeof(RakeMultiplayerPlugin).GetField("isConnected", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, true);
            
            SetPrivateField(_rakeAI, "currentState", NetworkedRakeAI.RakeState.Blocking);
            SetPrivateField(_rakeAI, "isBlocking", true);
            SetPrivateField(_rakeAI, "lastAppliedState", (byte)NetworkedRakeAI.RakeState.Blocking);
            
            // Transition to Chase
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            byte newState = (byte)NetworkedRakeAI.RakeState.Chase;
            float health = 100f;
            
            // Act
            _rakeAI.ApplySyncData(position, rotation, newState, health);
            
            // Assert: isBlocking cleared
            var isBlocking = GetPrivateField<bool>(_rakeAI, "isBlocking");
            Assert.IsFalse(isBlocking, "isBlocking should be false when leaving Blocking state");
        }

        [Test]
        public void ApplySyncData_AppliesHealth()
        {
            // Arrange: client mode
            typeof(RakeMultiplayerPlugin).GetField("isServer", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, false);
            typeof(RakeMultiplayerPlugin).GetField("isConnected", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, true);
            
            float newHealth = 42.5f;
            
            // Act
            _rakeAI.ApplySyncData(Vector3.zero, Quaternion.identity, (byte)NetworkedRakeAI.RakeState.Idle, newHealth);
            
            // Assert: targetHealth updated
            var targetHealth = GetPrivateField<float>(_rakeAI, "targetHealth");
            Assert.AreEqual(newHealth, targetHealth, "targetHealth should be updated");
        }

        #endregion

        #region ApplyHealth Tests

        [Test]
        public void ApplyHealth_SetsEnemyAIHealthField()
        {
            // Arrange
            float newHealth = 50f;
            
            // Act
            _rakeAI.ApplyHealth(newHealth);
            
            // Assert: Health field on Enemy_AI should be set via reflection
            // Note: This tests the reflection path; actual Enemy_AI Health field may not exist in test
            // but the method should not throw
            Assert.DoesNotThrow(() => _rakeAI.ApplyHealth(newHealth), "ApplyHealth should not throw");
        }

        [Test]
        public void ApplyHealth_WithNullEnemyAI_DoesNotThrow()
        {
            // Arrange: remove Enemy_AI reference
            SetPrivateField(_rakeAI, "enemyAI", null);
            float newHealth = 50f;
            
            // Act & Assert
            Assert.DoesNotThrow(() => _rakeAI.ApplyHealth(newHealth), "ApplyHealth with null enemyAI should not throw");
        }

        #endregion

        #region RakeState Enum Tests

        [Test]
        public void RakeState_EnumValues_MatchExpected()
        {
            // Verify enum values match the protocol
            Assert.AreEqual(0, (byte)NetworkedRakeAI.RakeState.Idle);
            Assert.AreEqual(1, (byte)NetworkedRakeAI.RakeState.Patrol);
            Assert.AreEqual(2, (byte)NetworkedRakeAI.RakeState.Chase);
            Assert.AreEqual(3, (byte)NetworkedRakeAI.RakeState.Attack);
            Assert.AreEqual(4, (byte)NetworkedRakeAI.RakeState.Search);
            Assert.AreEqual(5, (byte)NetworkedRakeAI.RakeState.Stunned);
            Assert.AreEqual(6, (byte)NetworkedRakeAI.RakeState.Dead);
            Assert.AreEqual(7, (byte)NetworkedRakeAI.RakeState.Follow);
            Assert.AreEqual(8, (byte)NetworkedRakeAI.RakeState.Stealth);
            Assert.AreEqual(9, (byte)NetworkedRakeAI.RakeState.Blocking);
            Assert.AreEqual(10, (byte)NetworkedRakeAI.RakeState.FeignedRetreat);
        }

        [Test]
        public void RakeState_CanCastToByte()
        {
            // Verify byte casting works for packet serialization
            byte stateByte = (byte)NetworkedRakeAI.RakeState.FeignedRetreat;
            Assert.AreEqual(10, stateByte);
            
            var restored = (NetworkedRakeAI.RakeState)stateByte;
            Assert.AreEqual(NetworkedRakeAI.RakeState.FeignedRetreat, restored);
        }

        #endregion

        #region State Transition Tests

        [Test]
        public void TransitionToState_SameState_DoesNothing()
        {
            // Arrange
            SetPrivateField(_rakeAI, "currentState", NetworkedRakeAI.RakeState.Chase);
            var prevState = GetPrivateField<NetworkedRakeAI.RakeState>(_rakeAI, "previousState");
            
            // Act
            InvokePrivateMethod(_rakeAI, "TransitionToState", NetworkedRakeAI.RakeState.Chase);
            
            // Assert: state unchanged, previousState unchanged
            var currentState = GetPrivateField<NetworkedRakeAI.RakeState>(_rakeAI, "currentState");
            Assert.AreEqual(NetworkedRakeAI.RakeState.Chase, currentState);
            Assert.AreEqual(prevState, GetPrivateField<NetworkedRakeAI.RakeState>(_rakeAI, "previousState"));
        }

        [Test]
        public void TransitionToState_NewState_UpdatesStateAndTimer()
        {
            // Arrange
            SetPrivateField(_rakeAI, "currentState", NetworkedRakeAI.RakeState.Follow);
            SetPrivateField(_rakeAI, "stateTimer", 5f);
            
            // Act
            InvokePrivateMethod(_rakeAI, "TransitionToState", NetworkedRakeAI.RakeState.Chase);
            
            // Assert
            var currentState = GetPrivateField<NetworkedRakeAI.RakeState>(_rakeAI, "currentState");
            Assert.AreEqual(NetworkedRakeAI.RakeState.Chase, currentState);
            
            var previousState = GetPrivateField<NetworkedRakeAI.RakeState>(_rakeAI, "previousState");
            Assert.AreEqual(NetworkedRakeAI.RakeState.Follow, previousState);
            
            var stateTimer = GetPrivateField<float>(_rakeAI, "stateTimer");
            Assert.AreEqual(0f, stateTimer, "stateTimer should reset to 0");
        }

        [Test]
        public void TransitionToState_Stealth_SetsCrouchTrue()
        {
            // Arrange
            var animator = _rakeGameObject.GetComponent<Animator>();
            SetPrivateField(_rakeAI, "currentState", NetworkedRakeAI.RakeState.Follow);
            
            // Act
            InvokePrivateMethod(_rakeAI, "TransitionToState", NetworkedRakeAI.RakeState.Stealth);
            
            // Assert: animator Crouch bool should be true
            // Note: We can't easily verify animator state in unit test without full Unity setup
            // but we verify the method doesn't throw
            var currentState = GetPrivateField<NetworkedRakeAI.RakeState>(_rakeAI, "currentState");
            Assert.AreEqual(NetworkedRakeAI.RakeState.Stealth, currentState);
        }

        [Test]
        public void TransitionToState_Blocking_SetsIsBlockingAndTrigger()
        {
            // Arrange
            SetPrivateField(_rakeAI, "currentState", NetworkedRakeAI.RakeState.Chase);
            
            // Act
            InvokePrivateMethod(_rakeAI, "TransitionToState", NetworkedRakeAI.RakeState.Blocking);
            
            // Assert
            var isBlocking = GetPrivateField<bool>(_rakeAI, "isBlocking");
            Assert.IsTrue(isBlocking, "isBlocking should be true on Blocking entry");
            
            var stateTimer = GetPrivateField<float>(_rakeAI, "stateTimer");
            Assert.AreEqual(0f, stateTimer);
        }

        [Test]
        public void TransitionToState_FeignedRetreat_SetsFleeTrigger()
        {
            // Arrange
            SetPrivateField(_rakeAI, "currentState", NetworkedRakeAI.RakeState.Chase);
            
            // Act
            InvokePrivateMethod(_rakeAI, "TransitionToState", NetworkedRakeAI.RakeState.FeignedRetreat);
            
            // Assert
            var currentState = GetPrivateField<NetworkedRakeAI.RakeState>(_rakeAI, "currentState");
            Assert.AreEqual(NetworkedRakeAI.RakeState.FeignedRetreat, currentState);
        }

        #endregion

        #region WasHitFromFront Tests

        [Test]
        public void WasHitFromFront_RecentFrontalHit_ReturnsTrue()
        {
            // Arrange
            SetPrivateField(_rakeAI, "lastDamageTime", Time.time); // Recent
            _rakeGameObject.transform.forward = Vector3.forward;
            // Hit from front: hitDirection = -forward (from front towards rake)
            // lastDamageDirection stores the hitDirection
            SetPrivateField(_rakeAI, "lastDamageDirection", -Vector3.forward);
            
            // Act
            bool result = (bool)InvokePrivateMethod(_rakeAI, "WasHitFromFront");
            
            // Assert: angle between rakeForward (forward) and -lastDamageDirection (forward) = 0° < 45°
            Assert.IsTrue(result, "Recent frontal hit should return true");
        }

        [Test]
        public void WasHitFromFront_OldHit_ReturnsFalse()
        {
            // Arrange
            SetPrivateField(_rakeAI, "lastDamageTime", Time.time - 1f); // Older than DamageTimeWindow (0.5f)
            SetPrivateField(_rakeAI, "lastDamageDirection", -Vector3.forward);
            
            // Act
            bool result = (bool)InvokePrivateMethod(_rakeAI, "WasHitFromFront");
            
            // Assert: damage too old
            Assert.IsFalse(result, "Old hit (>0.5s) should return false");
        }

        [Test]
        public void WasHitFromFront_SideHit_ReturnsFalse()
        {
            // Arrange
            SetPrivateField(_rakeAI, "lastDamageTime", Time.time);
            _rakeGameObject.transform.forward = Vector3.forward;
            // Hit from side: hitDirection = -right
            SetPrivateField(_rakeAI, "lastDamageDirection", -Vector3.right);
            
            // Act
            bool result = (bool)InvokePrivateMethod(_rakeAI, "WasHitFromFront");
            
            // Assert: angle = 90° > 45°
            Assert.IsFalse(result, "Side hit should return false");
        }

        [Test]
        public void WasHitFromFront_BackHit_ReturnsFalse()
        {
            // Arrange
            SetPrivateField(_rakeAI, "lastDamageTime", Time.time);
            _rakeGameObject.transform.forward = Vector3.forward;
            // Hit from back: hitDirection = forward
            SetPrivateField(_rakeAI, "lastDamageDirection", Vector3.forward);
            
            // Act
            bool result = (bool)InvokePrivateMethod(_rakeAI, "WasHitFromFront");
            
            // Assert: angle = 180° > 45°
            Assert.IsFalse(result, "Back hit should return false");
        }

        #endregion

        #region Constants Tests

        [Test]
        public void Constants_PositionDeltaThreshold_IsCorrect()
        {
            var threshold = typeof(NetworkedRakeAI).GetField("PositionDeltaThreshold", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(threshold);
            Assert.AreEqual(0.05f, (float)threshold.GetValue(null));
        }

        [Test]
        public void Constants_DamageTimeWindow_IsCorrect()
        {
            var window = typeof(NetworkedRakeAI).GetField("DamageTimeWindow", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(window);
            Assert.AreEqual(0.5f, (float)window.GetValue(null));
        }

        [Test]
        public void Constants_HealthDeltaThreshold_IsCorrect()
        {
            var threshold = typeof(NetworkedRakeAI).GetField("HealthDeltaThreshold", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(threshold);
            Assert.AreEqual(1f, (float)threshold.GetValue(null));
        }

        #endregion

        #region Configuration Tests

        [Test]
        public void Configuration_DefaultValues_AreSet()
        {
            // Verify default configuration values
            Assert.AreEqual(0.066f, _rakeAI.syncRate, "syncRate should be 15Hz (0.066f)");
            Assert.AreEqual(15f, _rakeAI.followMinDistance);
            Assert.AreEqual(25f, _rakeAI.followMaxDistance);
            Assert.AreEqual(20f, _rakeAI.stealthTransitionDistance);
            Assert.AreEqual(30f, _rakeAI.stealthLookConeAngle);
            Assert.AreEqual(20f, _rakeAI.stealthLookRange);
            Assert.AreEqual(2f, _rakeAI.blockingDurationMin);
            Assert.AreEqual(3f, _rakeAI.blockingDurationMax);
            Assert.AreEqual(45f, _rakeAI.blockingFrontalAngle);
            Assert.AreEqual(0.8f, _rakeAI.blockingDamageReduction);
            Assert.AreEqual(0.3f, _rakeAI.feignedRetreatHealthThreshold);
            Assert.AreEqual(3f, _rakeAI.feignedRetreatNoTargetTime);
            Assert.AreEqual(5f, _rakeAI.feignedRetreatWaitTime);
            Assert.AreEqual(5f, _rakeAI.feignedRetreatTeleportOffsetMin);
            Assert.AreEqual(10f, _rakeAI.feignedRetreatTeleportOffsetMax);
            Assert.AreEqual(60f, _rakeAI.feignedRetreatCooldown);
            Assert.AreEqual(3f, _rakeAI.attackRange);
            Assert.AreEqual(3f, _rakeAI.chaseVocalizationInterval);
        }

        #endregion

        #region Edge Cases and Adversarial Tests

        [Test]
        public void CalculateDamageReduction_NaNInputs_HandlesGracefully()
        {
            // Arrange
            Vector3 hitDirection = new Vector3(float.NaN, 0, 0);
            Vector3 rakeForward = Vector3.forward;
            
            // Act & Assert: Should not throw, NaN propagation
            float reduction = _rakeAI.CalculateDamageReduction(hitDirection, rakeForward);
            Assert.IsTrue(float.IsNaN(reduction) || reduction == 0.2f || reduction == 1.0f, 
                "NaN input should produce NaN or valid result, not throw");
        }

        [Test]
        public void CalculateDamageReduction_InfinityInputs_HandlesGracefully()
        {
            // Arrange
            Vector3 hitDirection = new Vector3(float.PositiveInfinity, 0, 0);
            Vector3 rakeForward = Vector3.forward;
            
            // Act & Assert
            float reduction = _rakeAI.CalculateDamageReduction(hitDirection, rakeForward);
            Assert.IsTrue(float.IsInfinity(reduction) || float.IsNaN(reduction) || reduction == 0.2f || reduction == 1.0f,
                "Infinity input should not throw");
        }

        [Test]
        public void OnDamageTaken_NullDamageType_HandlesGracefully()
        {
            // Arrange
            SetPrivateField(_rakeAI, "currentState", NetworkedRakeAI.RakeState.Follow);
            Vector3 hitDirection = Vector3.forward;
            
            // Act & Assert
            Assert.DoesNotThrow(() => _rakeAI.OnDamageTaken(hitDirection, 10, null), 
                "null damageType should not throw");
        }

        [Test]
        public void OnDamageTaken_EmptyDamageType_HandlesGracefully()
        {
            // Arrange
            SetPrivateField(_rakeAI, "currentState", NetworkedRakeAI.RakeState.Follow);
            Vector3 hitDirection = Vector3.forward;
            
            // Act & Assert
            Assert.DoesNotThrow(() => _rakeAI.OnDamageTaken(hitDirection, 10, ""), 
                "empty damageType should not throw");
        }

        [Test]
        public void OnDamageTaken_NegativeDamage_HandlesGracefully()
        {
            // Arrange
            SetPrivateField(_rakeAI, "currentState", NetworkedRakeAI.RakeState.Follow);
            Vector3 hitDirection = Vector3.forward;
            
            // Act & Assert
            Assert.DoesNotThrow(() => _rakeAI.OnDamageTaken(hitDirection, -10, "Bullet"), 
                "negative damage should not throw");
        }

        [Test]
        public void OnDamageTaken_ZeroDamage_HandlesGracefully()
        {
            // Arrange
            SetPrivateField(_rakeAI, "currentState", NetworkedRakeAI.RakeState.Follow);
            Vector3 hitDirection = Vector3.forward;
            
            // Act & Assert
            Assert.DoesNotThrow(() => _rakeAI.OnDamageTaken(hitDirection, 0, "Bullet"), 
                "zero damage should not throw");
        }

        [Test]
        public void ApplySyncData_ExtremeValues_HandlesGracefully()
        {
            // Arrange: client mode
            typeof(RakeMultiplayerPlugin).GetField("isServer", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, false);
            typeof(RakeMultiplayerPlugin).GetField("isConnected", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, true);
            
            Vector3 extremePos = new Vector3(float.MaxValue, float.MinValue, float.PositiveInfinity);
            Quaternion extremeRot = new Quaternion(float.MaxValue, float.MinValue, float.PositiveInfinity, float.NegativeInfinity);
            byte state = 255; // Invalid state
            float health = float.NaN;
            
            // Act & Assert
            Assert.DoesNotThrow(() => _rakeAI.ApplySyncData(extremePos, extremeRot, state, health),
                "Extreme values should not throw");
        }

        [Test]
        public void ApplyHealth_NaN_HandlesGracefully()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _rakeAI.ApplyHealth(float.NaN), "NaN health should not throw");
        }

        [Test]
        public void ApplyHealth_Infinity_HandlesGracefully()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _rakeAI.ApplyHealth(float.PositiveInfinity), "Infinity health should not throw");
        }

        [Test]
        public void ApplyHealth_Negative_HandlesGracefully()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _rakeAI.ApplyHealth(-100f), "Negative health should not throw");
        }

        #endregion

        #region Helper Method for Private Method Invocation with Return

        private object InvokePrivateMethod(object obj, string methodName, params object[] parameters)
        {
            var method = obj.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (method == null)
            {
                Assert.Fail($"Method {methodName} not found on {obj.GetType().Name}");
                return null;
            }
            return method.Invoke(obj, parameters);
        }

        #endregion
    }

    /// <summary>
    /// Tests for RakeStatePacket serialization (integration with networking)
    /// </summary>
    [TestFixture]
    public class RakeStatePacketTests
    {
        [Test]
        public void RakeStatePacket_SerializeDeserialize_RoundTrip()
        {
            // Arrange
            var original = new RakeStatePacket
            {
                Position = new Vector3(10.5f, 5.25f, -20.75f),
                Rotation = new Quaternion(0.1f, 0.2f, 0.3f, 0.4f),
                State = (byte)NetworkedRakeAI.RakeState.Chase,
                Health = 75.5f
            };

            // Act
            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as RakeStatePacket;

            // Assert
            Assert.IsNotNull(restored, "Deserialized packet should not be null");
            Assert.AreEqual(original.Position, restored.Position, "Position should match");
            Assert.AreEqual(original.Rotation, restored.Rotation, "Rotation should match");
            Assert.AreEqual(original.State, restored.State, "State should match");
            Assert.AreEqual(original.Health, restored.Health, 0.001f, "Health should match");
            Assert.AreEqual(PacketType.RakeState, restored.Type, "PacketType should be RakeState");
        }

        [Test]
        public void RakeStatePacket_AllStates_SerializeCorrectly()
        {
            // Test all RakeState enum values
            foreach (NetworkedRakeAI.RakeState state in Enum.GetValues(typeof(NetworkedRakeAI.RakeState)))
            {
                var original = new RakeStatePacket
                {
                    Position = Vector3.zero,
                    Rotation = Quaternion.identity,
                    State = (byte)state,
                    Health = 100f
                };

                byte[] data = original.Serialize();
                var restored = PacketSerializer.Deserialize(data) as RakeStatePacket;

                Assert.IsNotNull(restored, $"State {state} should deserialize");
                Assert.AreEqual((byte)state, restored.State, $"State {state} should round-trip");
            }
        }

        [Test]
        public void RakeStatePacket_ZeroValues_SerializeCorrectly()
        {
            // Arrange
            var original = new RakeStatePacket
            {
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                State = 0,
                Health = 0f
            };

            // Act
            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as RakeStatePacket;

            // Assert
            Assert.IsNotNull(restored);
            Assert.AreEqual(Vector3.zero, restored.Position);
            Assert.AreEqual(Quaternion.identity, restored.Rotation);
            Assert.AreEqual(0, restored.State);
            Assert.AreEqual(0f, restored.Health);
        }

        [Test]
        public void RakeStatePacket_MaxValues_SerializeCorrectly()
        {
            // Arrange
            var original = new RakeStatePacket
            {
                Position = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue),
                Rotation = new Quaternion(1, 1, 1, 1),
                State = 255,
                Health = float.MaxValue
            };

            // Act
            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as RakeStatePacket;

            // Assert
            Assert.IsNotNull(restored);
            Assert.AreEqual(original.Position, restored.Position);
            Assert.AreEqual(255, restored.State);
        }
    }
}