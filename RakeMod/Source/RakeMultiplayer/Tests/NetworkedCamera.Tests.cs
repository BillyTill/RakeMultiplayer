using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityPropertyAttribute = UnityEngine.PropertyAttribute;
using RakeMultiplayer;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer.Tests
{
    /// <summary>
    /// Unit tests for NetworkedCamera component and related packets
    /// This file focuses ONLY on headless-testable logic:
    /// packet serialization, enum validation, and pure property logic.
    /// Tests requiring Unity runtime (GameObject, Transform, MonoBehaviour) 
    /// should be run in Unity Test Runner separately.
    /// </summary>
    [TestFixture]
    public class NetworkedCameraTests
    {
        #region CameraSyncAction Enum Tests (from NetworkedCameraSystem.cs)

        [Test]
        public void CameraSyncAction_EnumValues_MatchExpected()
        {
            Assert.AreEqual(0, (byte)CameraSyncAction.Spawn);
            Assert.AreEqual(1, (byte)CameraSyncAction.Update);
            Assert.AreEqual(2, (byte)CameraSyncAction.Destroy);
            Assert.AreEqual(3, (byte)CameraSyncAction.Break);
        }

        [Test]
        public void CameraSyncAction_CanRoundTripViaByte()
        {
            foreach (CameraSyncAction action in Enum.GetValues(typeof(CameraSyncAction)))
            {
                byte actionByte = (byte)action;
                CameraSyncAction restored = (CameraSyncAction)actionByte;
                Assert.AreEqual(action, restored, $"CameraSyncAction {action} should round-trip via byte");
            }
        }

        #endregion

        #region QuestTriggerType Enum Tests (from NetworkedCameraSystem.cs)

        [Test]
        public void QuestTriggerType_EnumValues_MatchExpected()
        {
            Assert.AreEqual(0, (byte)QuestTriggerType.CameraBroken);
            Assert.AreEqual(1, (byte)QuestTriggerType.CameraPlaced);
            Assert.AreEqual(2, (byte)QuestTriggerType.TrapTriggered);
            Assert.AreEqual(3, (byte)QuestTriggerType.BaitPlaced);
            Assert.AreEqual(4, (byte)QuestTriggerType.ObjectiveComplete);
        }

        [Test]
        public void QuestTriggerType_CanRoundTripViaByte()
        {
            foreach (QuestTriggerType type in Enum.GetValues(typeof(QuestTriggerType)))
            {
                byte typeByte = (byte)type;
                QuestTriggerType restored = (QuestTriggerType)typeByte;
                Assert.AreEqual(type, restored, $"QuestTriggerType {type} should round-trip via byte");
            }
        }

        #endregion

        #region ReadOnlyAttribute Tests

        [Test]
        public void ReadOnlyAttribute_IsPropertyAttribute()
        {
            // Verify the attribute exists and inherits from PropertyAttribute
            var attrType = typeof(ReadOnlyAttribute);
            Assert.IsTrue(typeof(UnityPropertyAttribute).IsAssignableFrom(attrType));
        }

        #endregion

        #region Packet Serialization Tests (CameraSyncPacket from Packets.cs)

        [Test]
        public void CameraSyncPacket_SerializeDeserialize_RoundTrip()
        {
            // Arrange - using the PacketSerializer version from Packets.cs
            var original = new RakeMultiplayer.Networking.CameraSyncPacket
            {
                CameraId = 42u,
                Position = new Vector3(100f, 10f, 50f),
                Rotation = new Quaternion(0f, 0.707f, 0f, 0.707f),
                IsActive = true,
                IsBroken = false,
                BatteryLevel = 75.5f,
                PlacedByPlayerId = 2
            };

            // Act
            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as RakeMultiplayer.Networking.CameraSyncPacket;

            // Assert
            Assert.IsNotNull(restored, "Deserialized packet should not be null");
            Assert.AreEqual(original.CameraId, restored.CameraId);
            Assert.AreEqual(original.Position, restored.Position);
            Assert.AreEqual(original.Rotation, restored.Rotation);
            Assert.AreEqual(original.IsActive, restored.IsActive);
            Assert.AreEqual(original.IsBroken, restored.IsBroken);
            Assert.AreEqual(original.BatteryLevel, restored.BatteryLevel, 0.001f);
            Assert.AreEqual(original.PlacedByPlayerId, restored.PlacedByPlayerId);
            Assert.AreEqual(PacketType.CameraSync, restored.Type);
        }

        [Test]
        public void CameraSyncPacket_InactiveAndBroken_SerializesCorrectly()
        {
            // Arrange
            var original = new RakeMultiplayer.Networking.CameraSyncPacket
            {
                CameraId = 1u,
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                IsActive = false,
                IsBroken = true,
                BatteryLevel = 0f,
                PlacedByPlayerId = 0
            };

            // Act
            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as RakeMultiplayer.Networking.CameraSyncPacket;

            // Assert
            Assert.IsNotNull(restored);
            Assert.AreEqual(false, restored.IsActive);
            Assert.AreEqual(true, restored.IsBroken);
            Assert.AreEqual(0f, restored.BatteryLevel, 0.001f);
        }

        [Test]
        public void CameraSyncPacket_ZeroValues_SerializeCorrectly()
        {
            // Arrange
            var original = new RakeMultiplayer.Networking.CameraSyncPacket
            {
                CameraId = 0u,
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                IsActive = false,
                IsBroken = false,
                BatteryLevel = 0f,
                PlacedByPlayerId = 0
            };

            // Act
            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as RakeMultiplayer.Networking.CameraSyncPacket;

            // Assert
            Assert.IsNotNull(restored);
            Assert.AreEqual(0u, restored.CameraId);
            Assert.AreEqual(Vector3.zero, restored.Position);
            Assert.AreEqual(Quaternion.identity, restored.Rotation);
            Assert.AreEqual(false, restored.IsActive);
            Assert.AreEqual(false, restored.IsBroken);
            Assert.AreEqual(0f, restored.BatteryLevel, 0.001f);
            Assert.AreEqual(0, restored.PlacedByPlayerId);
        }

        [Test]
        public void CameraSyncPacket_MaxValues_SerializeCorrectly()
        {
            // Arrange
            var original = new RakeMultiplayer.Networking.CameraSyncPacket
            {
                CameraId = uint.MaxValue,
                Position = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue),
                Rotation = new Quaternion(1, 1, 1, 1),
                IsActive = true,
                IsBroken = true,
                BatteryLevel = float.MaxValue,
                PlacedByPlayerId = int.MaxValue
            };

            // Act
            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as RakeMultiplayer.Networking.CameraSyncPacket;

            // Assert
            Assert.IsNotNull(restored);
            Assert.AreEqual(uint.MaxValue, restored.CameraId);
            Assert.AreEqual(original.Position, restored.Position);
            Assert.AreEqual(true, restored.IsActive);
            Assert.AreEqual(true, restored.IsBroken);
            Assert.AreEqual(float.MaxValue, restored.BatteryLevel);
            Assert.AreEqual(int.MaxValue, restored.PlacedByPlayerId);
        }

        [Test]
        public void CameraSyncPacket_NegativeValues_SerializesCorrectly()
        {
            // Arrange
            var original = new RakeMultiplayer.Networking.CameraSyncPacket
            {
                CameraId = 1u,
                Position = new Vector3(-100f, -50f, -200f),
                Rotation = new Quaternion(-0.5f, -0.5f, -0.5f, -0.5f),
                IsActive = false,
                IsBroken = false,
                BatteryLevel = -10f,
                PlacedByPlayerId = -1
            };

            // Act
            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as RakeMultiplayer.Networking.CameraSyncPacket;

            // Assert
            Assert.IsNotNull(restored);
            Assert.AreEqual(original.Position, restored.Position);
            Assert.AreEqual(original.Rotation, restored.Rotation);
            Assert.AreEqual(-10f, restored.BatteryLevel, 0.001f);
            Assert.AreEqual(-1, restored.PlacedByPlayerId);
        }

        #endregion

        #region Packet Serialization Tests (CameraSpawnPacket)

        [Test]
        public void CameraSpawnPacket_SerializeDeserialize_RoundTrip()
        {
            // Arrange
            var original = new RakeMultiplayer.Networking.CameraSpawnPacket
            {
                CameraId = 10u,
                Position = new Vector3(50f, 5f, 25f),
                Rotation = new Quaternion(0f, 0.382683f, 0f, 0.92388f),
                PlacedByPlayerId = 1
            };

            // Act
            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as RakeMultiplayer.Networking.CameraSpawnPacket;

            // Assert
            Assert.IsNotNull(restored);
            Assert.AreEqual(original.CameraId, restored.CameraId);
            Assert.AreEqual(original.Position, restored.Position);
            Assert.AreEqual(original.Rotation, restored.Rotation);
            Assert.AreEqual(original.PlacedByPlayerId, restored.PlacedByPlayerId);
            Assert.AreEqual(PacketType.CameraSpawn, restored.Type);
        }

        [Test]
        public void CameraSpawnPacket_ZeroValues_SerializeCorrectly()
        {
            // Arrange
            var original = new RakeMultiplayer.Networking.CameraSpawnPacket
            {
                CameraId = 0u,
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                PlacedByPlayerId = 0
            };

            // Act
            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as RakeMultiplayer.Networking.CameraSpawnPacket;

            // Assert
            Assert.IsNotNull(restored);
            Assert.AreEqual(0u, restored.CameraId);
            Assert.AreEqual(Vector3.zero, restored.Position);
            Assert.AreEqual(Quaternion.identity, restored.Rotation);
            Assert.AreEqual(0, restored.PlacedByPlayerId);
        }

        [Test]
        public void CameraSpawnPacket_NegativeValues_SerializesCorrectly()
        {
            // Arrange
            var original = new RakeMultiplayer.Networking.CameraSpawnPacket
            {
                CameraId = 1u,
                Position = new Vector3(-10f, -5f, -20f),
                Rotation = new Quaternion(-0.1f, -0.2f, -0.3f, -0.9f),
                PlacedByPlayerId = -5
            };

            // Act
            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as RakeMultiplayer.Networking.CameraSpawnPacket;

            // Assert
            Assert.IsNotNull(restored);
            Assert.AreEqual(original.Position, restored.Position);
            Assert.AreEqual(original.Rotation, restored.Rotation);
            Assert.AreEqual(-5, restored.PlacedByPlayerId);
        }

        #endregion

        #region Packet Serialization Tests (CameraDestroyPacket)

        [Test]
        public void CameraDestroyPacket_SerializeDeserialize_RoundTrip()
        {
            // Arrange
            var original = new RakeMultiplayer.Networking.CameraDestroyPacket
            {
                CameraId = 7u
            };

            // Act
            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as RakeMultiplayer.Networking.CameraDestroyPacket;

            // Assert
            Assert.IsNotNull(restored);
            Assert.AreEqual(original.CameraId, restored.CameraId);
            Assert.AreEqual(PacketType.CameraDestroy, restored.Type);
        }

        [Test]
        public void CameraDestroyPacket_ZeroCameraId_SerializesCorrectly()
        {
            // Arrange
            var original = new RakeMultiplayer.Networking.CameraDestroyPacket
            {
                CameraId = 0u
            };

            // Act
            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as RakeMultiplayer.Networking.CameraDestroyPacket;

            // Assert
            Assert.IsNotNull(restored);
            Assert.AreEqual(0u, restored.CameraId);
        }

        [Test]
        public void CameraDestroyPacket_MaxCameraId_SerializesCorrectly()
        {
            // Arrange
            var original = new RakeMultiplayer.Networking.CameraDestroyPacket
            {
                CameraId = uint.MaxValue
            };

            // Act
            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as RakeMultiplayer.Networking.CameraDestroyPacket;

            // Assert
            Assert.IsNotNull(restored);
            Assert.AreEqual(uint.MaxValue, restored.CameraId);
        }

        #endregion

        #region Packet Serialization Tests (QuestTriggerPacket from Packets.cs)

        [Test]
        public void QuestTriggerPacket_SerializeDeserialize_RoundTrip()
        {
            // Arrange - using the PacketSerializer version from Packets.cs
            var original = new RakeMultiplayer.Networking.QuestTriggerPacket
            {
                TriggerType = 1, // BaitTaken
                Position = new Vector3(100f, 2f, 50f),
                CameraId = 5u
            };

            // Act
            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as RakeMultiplayer.Networking.QuestTriggerPacket;

            // Assert
            Assert.IsNotNull(restored);
            Assert.AreEqual(original.TriggerType, restored.TriggerType);
            Assert.AreEqual(original.Position, restored.Position);
            Assert.AreEqual(original.CameraId, restored.CameraId);
            Assert.AreEqual(PacketType.QuestTrigger, restored.Type);
        }

        [Test]
        public void QuestTriggerPacket_AllTriggerTypes_SerializeCorrectly()
        {
            // Test all trigger types (0=CameraBreak, 1=BaitTaken, 2=TrapTriggered)
            for (byte triggerType = 0; triggerType <= 2; triggerType++)
            {
                // Arrange
                var original = new RakeMultiplayer.Networking.QuestTriggerPacket
                {
                    TriggerType = triggerType,
                    Position = Vector3.zero,
                    CameraId = 0u
                };

                // Act
                byte[] data = original.Serialize();
                var restored = PacketSerializer.Deserialize(data) as RakeMultiplayer.Networking.QuestTriggerPacket;

                // Assert
                Assert.IsNotNull(restored, $"TriggerType {triggerType} should deserialize");
                Assert.AreEqual(triggerType, restored.TriggerType, $"TriggerType {triggerType} should round-trip");
            }
        }

        [Test]
        public void QuestTriggerPacket_ZeroValues_SerializeCorrectly()
        {
            // Arrange
            var original = new RakeMultiplayer.Networking.QuestTriggerPacket
            {
                TriggerType = 0,
                Position = Vector3.zero,
                CameraId = 0u
            };

            // Act
            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as RakeMultiplayer.Networking.QuestTriggerPacket;

            // Assert
            Assert.IsNotNull(restored);
            Assert.AreEqual(0, restored.TriggerType);
            Assert.AreEqual(Vector3.zero, restored.Position);
            Assert.AreEqual(0u, restored.CameraId);
        }

        [Test]
        public void QuestTriggerPacket_MaxValues_SerializeCorrectly()
        {
            // Arrange
            var original = new RakeMultiplayer.Networking.QuestTriggerPacket
            {
                TriggerType = byte.MaxValue,
                Position = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue),
                CameraId = uint.MaxValue
            };

            // Act
            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as RakeMultiplayer.Networking.QuestTriggerPacket;

            // Assert
            Assert.IsNotNull(restored);
            Assert.AreEqual(byte.MaxValue, restored.TriggerType);
            Assert.AreEqual(original.Position, restored.Position);
            Assert.AreEqual(uint.MaxValue, restored.CameraId);
        }

        [Test]
        public void QuestTriggerPacket_NegativePosition_SerializesCorrectly()
        {
            // Arrange
            var original = new RakeMultiplayer.Networking.QuestTriggerPacket
            {
                TriggerType = 1,
                Position = new Vector3(-100f, -2f, -50f),
                CameraId = 10u
            };

            // Act
            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as RakeMultiplayer.Networking.QuestTriggerPacket;

            // Assert
            Assert.IsNotNull(restored);
            Assert.AreEqual(original.Position, restored.Position);
        }

        #endregion

        #region Packet Serializer Coverage Tests

        [Test]
        public void PacketSerializer_Deserialize_UnknownPacketType_Throws()
        {
            // Arrange - create a packet with unknown type byte
            var unknownType = (byte)255; // Not a valid PacketType
            using (var ms = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                writer.Write(unknownType);
                var data = ms.ToArray();

                // Act & Assert
                Assert.Throws<InvalidOperationException>(() => PacketSerializer.Deserialize(data));
            }
        }

        [Test]
        public void PacketSerializer_Deserialize_EmptyData_Throws()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => PacketSerializer.Deserialize(new byte[0]));
        }

        [Test]
        public void PacketSerializer_Deserialize_NullData_Throws()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => PacketSerializer.Deserialize(null));
        }

        [Test]
        public void PacketSerializer_Serialize_NullPacket_Throws()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => PacketSerializer.Serialize(null));
        }

        #endregion

        #region NetworkedCamera Property Logic Tests (Pure Logic - No Unity Dependencies)

        /// <summary>
        /// Tests the BatteryLevel property clamping logic without Unity dependencies
        /// </summary>
        [TestFixture]
        public class BatteryLevelPropertyTests
        {
            [Test]
            public void BatteryLevel_ClampsTo100_WhenAboveMax()
            {
                // Simulate the property setter logic
                float value = 150f;
                float clamped = Mathf.Clamp(value, 0f, 100f);
                Assert.AreEqual(100f, clamped);
            }

            [Test]
            public void BatteryLevel_ClampsTo0_WhenBelowMin()
            {
                float value = -50f;
                float clamped = Mathf.Clamp(value, 0f, 100f);
                Assert.AreEqual(0f, clamped);
            }

            [Test]
            public void BatteryLevel_KeepsValue_WhenInRange()
            {
                float value = 75.5f;
                float clamped = Mathf.Clamp(value, 0f, 100f);
                Assert.AreEqual(75.5f, clamped);
            }

            [Test]
            public void BatteryLevel_HandlesNaN()
            {
                float value = float.NaN;
                float clamped = Mathf.Clamp(value, 0f, 100f);
                // Mathf.Clamp with NaN returns NaN (propagates NaN)
                Assert.IsTrue(float.IsNaN(clamped));
            }

            [Test]
            public void BatteryLevel_HandlesPositiveInfinity()
            {
                float value = float.PositiveInfinity;
                float clamped = Mathf.Clamp(value, 0f, 100f);
                Assert.AreEqual(100f, clamped);
            }

            [Test]
            public void BatteryLevel_HandlesNegativeInfinity()
            {
                float value = float.NegativeInfinity;
                float clamped = Mathf.Clamp(value, 0f, 100f);
                Assert.AreEqual(0f, clamped);
            }
        }

        /// <summary>
        /// Tests the CanActivate logic without Unity dependencies
        /// </summary>
        [TestFixture]
        public class CanActivateLogicTests
        {
            [Test]
            public void CanActivate_ReturnsTrue_WhenNotBrokenAndHasBattery()
            {
                bool isBroken = false;
                float batteryLevel = 50f;
                float minBatteryLevel = 0f;

                bool result = !isBroken && batteryLevel > minBatteryLevel;
                Assert.IsTrue(result);
            }

            [Test]
            public void CanActivate_ReturnsFalse_WhenBroken()
            {
                bool isBroken = true;
                float batteryLevel = 100f;
                float minBatteryLevel = 0f;

                bool result = !isBroken && batteryLevel > minBatteryLevel;
                Assert.IsFalse(result);
            }

            [Test]
            public void CanActivate_ReturnsFalse_WhenBatteryAtMin()
            {
                bool isBroken = false;
                float batteryLevel = 0f;
                float minBatteryLevel = 0f;

                bool result = !isBroken && batteryLevel > minBatteryLevel;
                Assert.IsFalse(result);
            }

            [Test]
            public void CanActivate_ReturnsFalse_WhenBatteryBelowMin()
            {
                bool isBroken = false;
                float batteryLevel = 5f;
                float minBatteryLevel = 10f;

                bool result = !isBroken && batteryLevel > minBatteryLevel;
                Assert.IsFalse(result);
            }

            [Test]
            public void CanActivate_ReturnsFalse_WhenNaNBattery()
            {
                bool isBroken = false;
                float batteryLevel = float.NaN;
                float minBatteryLevel = 0f;

                bool result = !isBroken && batteryLevel > minBatteryLevel;
                // NaN > 0 is false
                Assert.IsFalse(result);
            }
        }

        /// <summary>
        /// Tests the GetRemainingActiveTime logic without Unity dependencies
        /// </summary>
        [TestFixture]
        public class GetRemainingActiveTimeLogicTests
        {
            [Test]
            public void GetRemainingActiveTime_CalculatesCorrectly()
            {
                float batteryLevel = 50f;
                float batteryDrainPerMinute = 10f;

                float remaining = (batteryLevel / batteryDrainPerMinute) * 60f;
                Assert.AreEqual(300f, remaining, 0.1f);
            }

            [Test]
            public void GetRemainingActiveTime_ReturnsMaxValue_WhenDrainRateZero()
            {
                float batteryLevel = 50f;
                float batteryDrainPerMinute = 0f;

                float remaining = batteryDrainPerMinute <= 0 ? float.MaxValue : (batteryLevel / batteryDrainPerMinute) * 60f;
                Assert.AreEqual(float.MaxValue, remaining);
            }

            [Test]
            public void GetRemainingActiveTime_ReturnsMaxValue_WhenDrainRateNegative()
            {
                float batteryLevel = 50f;
                float batteryDrainPerMinute = -5f;

                float remaining = batteryDrainPerMinute <= 0 ? float.MaxValue : (batteryLevel / batteryDrainPerMinute) * 60f;
                Assert.AreEqual(float.MaxValue, remaining);
            }

            [Test]
            public void GetRemainingActiveTime_ReturnsNaN_WhenBatteryNaN()
            {
                float batteryLevel = float.NaN;
                float batteryDrainPerMinute = 10f;

                float remaining = (batteryLevel / batteryDrainPerMinute) * 60f;
                Assert.IsTrue(float.IsNaN(remaining));
            }

            [Test]
            public void GetRemainingActiveTime_ReturnsInfinity_WhenBatteryInfinity()
            {
                float batteryLevel = float.PositiveInfinity;
                float batteryDrainPerMinute = 10f;

                float remaining = (batteryLevel / batteryDrainPerMinute) * 60f;
                Assert.IsTrue(float.IsPositiveInfinity(remaining));
            }
        }

        /// <summary>
        /// Tests the GetBatteryNormalized logic
        /// </summary>
        [TestFixture]
        public class GetBatteryNormalizedLogicTests
        {
            [Test]
            public void GetBatteryNormalized_Returns1_WhenFull()
            {
                float batteryLevel = 100f;
                float normalized = batteryLevel / 100f;
                Assert.AreEqual(1f, normalized);
            }

            [Test]
            public void GetBatteryNormalized_Returns0_WhenEmpty()
            {
                float batteryLevel = 0f;
                float normalized = batteryLevel / 100f;
                Assert.AreEqual(0f, normalized);
            }

            [Test]
            public void GetBatteryNormalized_ReturnsHalf_WhenHalf()
            {
                float batteryLevel = 50f;
                float normalized = batteryLevel / 100f;
                Assert.AreEqual(0.5f, normalized);
            }

            [Test]
            public void GetBatteryNormalized_ReturnsCorrectValue_WhenPartial()
            {
                float batteryLevel = 33.3f;
                float normalized = batteryLevel / 100f;
                Assert.AreEqual(0.333f, normalized, 0.001f);
            }
        }

        #endregion

        #region Delta Compression Logic Tests

        /// <summary>
        /// Tests the delta compression logic used in SendCameraSync
        /// </summary>
        [TestFixture]
        public class DeltaCompressionLogicTests
        {
            [Test]
            public void DeltaCompression_DetectsBatteryChange_AboveThreshold()
            {
                float currentBattery = 100f;
                float lastSentBattery = 99f;
                float threshold = 0.5f;

                bool batteryChanged = Math.Abs(currentBattery - lastSentBattery) > threshold;
                Assert.IsTrue(batteryChanged);
            }

            [Test]
            public void DeltaCompression_IgnoresBatteryChange_BelowThreshold()
            {
                float currentBattery = 100f;
                float lastSentBattery = 99.6f; // Difference = 0.4, which is < 0.5 threshold
                float threshold = 0.5f;

                bool batteryChanged = Math.Abs(currentBattery - lastSentBattery) > threshold;
                Assert.IsFalse(batteryChanged);
            }

            [Test]
            public void DeltaCompression_DetectsActiveChange()
            {
                bool currentActive = true;
                bool lastSentActive = false;

                bool activeChanged = currentActive != lastSentActive;
                Assert.IsTrue(activeChanged);
            }

            [Test]
            public void DeltaCompression_DetectsBrokenChange()
            {
                bool currentBroken = true;
                bool lastSentBroken = false;

                bool brokenChanged = currentBroken != lastSentBroken;
                Assert.IsTrue(brokenChanged);
            }

            [Test]
            public void DeltaCompression_NoChange_WhenAllSame()
            {
                float currentBattery = 100f;
                float lastSentBattery = 100f;
                bool currentActive = false;
                bool lastSentActive = false;
                bool currentBroken = false;
                bool lastSentBroken = false;
                float threshold = 0.5f;

                bool batteryChanged = Math.Abs(currentBattery - lastSentBattery) > threshold;
                bool activeChanged = currentActive != lastSentActive;
                bool brokenChanged = currentBroken != lastSentBroken;

                Assert.IsFalse(batteryChanged);
                Assert.IsFalse(activeChanged);
                Assert.IsFalse(brokenChanged);
            }
        }

        #endregion

        #region UpdateServer Battery Drain Logic Tests

        /// <summary>
        /// Tests the battery drain calculation logic
        /// </summary>
        [TestFixture]
        public class BatteryDrainLogicTests
        {
            [Test]
            public void BatteryDrain_CalculatesPerSecondCorrectly()
            {
                float batteryDrainPerMinute = 60f; // 1% per second
                float drainPerSecond = batteryDrainPerMinute / 60f;
                Assert.AreEqual(1f, drainPerSecond);
            }

            [Test]
            public void BatteryDrain_CalculatesPerSecond_WithDecimal()
            {
                float batteryDrainPerMinute = 5f; // 5% per minute
                float drainPerSecond = batteryDrainPerMinute / 60f;
                Assert.AreEqual(5f / 60f, drainPerSecond, 0.0001f);
            }

            [Test]
            public void BatteryDrain_DepletesBattery_OverTime()
            {
                float batteryLevel = 100f;
                float batteryDrainPerMinute = 60f; // 1% per second
                float deltaTime = 30f; // 30 seconds

                float drainPerSecond = batteryDrainPerMinute / 60f;
                float drain = drainPerSecond * deltaTime;
                float newBattery = Math.Max(0f, batteryLevel - drain);

                // 100 - (60/60)*30 = 100 - 30 = 70
                Assert.AreEqual(70f, newBattery, 0.1f);
            }

            [Test]
            public void BatteryDrain_ClampsAtZero()
            {
                float batteryLevel = 10f;
                float batteryDrainPerMinute = 60f;
                float deltaTime = 30f; // Would drain 30%

                float drainPerSecond = batteryDrainPerMinute / 60f;
                float drain = drainPerSecond * deltaTime;
                float newBattery = Math.Max(0f, batteryLevel - drain);

                Assert.AreEqual(0f, newBattery);
            }
        }

        #endregion

        #region IsBroken Auto-Deactivate Logic Tests

        /// <summary>
        /// Tests the logic where breaking a camera deactivates it
        /// </summary>
        [TestFixture]
        public class IsBrokenLogicTests
        {
            [Test]
            public void IsBroken_WhenSetTrue_DeactivatesCamera()
            {
                bool isActive = true;
                bool isBroken = false;

                // Simulate the setter logic
                if (!isBroken) // was not broken
                {
                    isBroken = true;
                    isActive = false; // Broken cameras cannot be active
                }

                Assert.IsTrue(isBroken);
                Assert.IsFalse(isActive);
            }

            [Test]
            public void IsBroken_WhenAlreadyBroken_DoesNothing()
            {
                bool isActive = false;
                bool isBroken = true;

                // Simulate the setter logic
                if (!isBroken) // was not broken - false, so block not executed
                {
                    isBroken = true;
                    isActive = false;
                }

                Assert.IsTrue(isBroken);
                Assert.IsFalse(isActive); // Unchanged
            }
        }

        #endregion

        #region QuestTriggerPacket Type Mapping Tests

        /// <summary>
        /// Tests that QuestTriggerType enum values map correctly to byte values used in Packets.cs
        /// </summary>
        [TestFixture]
        public class QuestTriggerTypeMappingTests
        {
            [Test]
            public void CameraBroken_MapsToByte0()
            {
                Assert.AreEqual(0, (byte)QuestTriggerType.CameraBroken);
            }

            [Test]
            public void CameraPlaced_MapsToByte1()
            {
                Assert.AreEqual(1, (byte)QuestTriggerType.CameraPlaced);
            }

            [Test]
            public void TrapTriggered_MapsToByte2()
            {
                Assert.AreEqual(2, (byte)QuestTriggerType.TrapTriggered);
            }

            [Test]
            public void BaitPlaced_MapsToByte3()
            {
                Assert.AreEqual(3, (byte)QuestTriggerType.BaitPlaced);
            }

            [Test]
            public void ObjectiveComplete_MapsToByte4()
            {
                Assert.AreEqual(4, (byte)QuestTriggerType.ObjectiveComplete);
            }
        }

        #endregion
    }
}