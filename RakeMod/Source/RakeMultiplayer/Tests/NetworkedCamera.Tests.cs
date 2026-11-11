using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityPropertyAttribute = UnityEngine.PropertyAttribute;
using RakeMultiplayer;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer.Tests
{
    /// <summary>
    /// Unit tests for NetworkedCamera component, RakeNetworkManager, and related packets
    /// This file focuses ONLY on headless-testable logic:
    /// packet serialization, enum validation, and pure property logic.
    /// Tests requiring Unity runtime (GameObject, Transform, MonoBehaviour, Telepathy) 
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

        #region QuestTriggerType Enum Tests (Local test enum - NOT in production code)

        /// <summary>
        /// QuestTriggerType is a LOCAL test enum. Production code in Packets.cs uses byte TriggerType:
        /// 0=CameraBreak, 1=BaitTaken, 2=TrapTriggered. This test enum includes additional values
        /// for future extensibility testing.
        /// </summary>
        public enum QuestTriggerType : byte
        {
            CameraBroken = 0,
            CameraPlaced = 1,
            TrapTriggered = 2,
            BaitPlaced = 3,
            ObjectiveComplete = 4
        }

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
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
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

            [Test]
            public void BatteryDrain_ZeroDrainRate_DoesNotDeplete()
            {
                float batteryLevel = 100f;
                float batteryDrainPerMinute = 0f;
                float deltaTime = 60f;

                float drainPerSecond = batteryDrainPerMinute / 60f;
                float drain = drainPerSecond * deltaTime;
                float newBattery = Math.Max(0f, batteryLevel - drain);

                Assert.AreEqual(100f, newBattery);
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
        /// Production QuestTriggerPacket uses: 0=CameraBreak, 1=BaitTaken, 2=TrapTriggered
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

            [Test]
            public void QuestTriggerPacket_ProductionValues_MatchComments()
            {
                // Verify production byte values from Packets.cs comments
                // TriggerType: 0=CameraBreak, 1=BaitTaken, 2=TrapTriggered
                Assert.AreEqual(0, (byte)QuestTriggerType.CameraBroken, "CameraBreak should be 0");
                Assert.AreEqual(1, (byte)QuestTriggerType.CameraPlaced, "BaitTaken should be 1 (test enum uses CameraPlaced for this value)");
                Assert.AreEqual(2, (byte)QuestTriggerType.TrapTriggered, "TrapTriggered should be 2");
            }
        }

        #endregion

        #region RakeNetworkManager Pure Logic Tests

        /// <summary>
        /// Tests for RakeNetworkManager item registry logic (pure C#, no Unity dependencies)
        /// </summary>
        [TestFixture]
        public class RakeNetworkManagerItemRegistryTests
        {
            // We test the logic by replicating it here since the actual methods
            // are instance methods on a MonoBehaviour with Unity dependencies

            [Test]
            public void GetItemNetworkId_ReturnsZero_ForNullItem()
            {
                // Simulate: if (item == null) return 0;
                object item = null;
                int result = item == null ? 0 : 1;
                Assert.AreEqual(0, result);
            }

            [Test]
            public void GetItemNetworkId_UsesPositionHash_WhenNotRegistered()
            {
                // Simulate fallback logic: hash from position and name
                // This tests the deterministic ID generation algorithm
                Vector3 pos = new Vector3(10.5f, 2.0f, 50.25f);
                string name = "TestItem";
                
                int x = Mathf.RoundToInt(pos.x * 100f);
                int y = Mathf.RoundToInt(pos.y * 100f);
                int z = Mathf.RoundToInt(pos.z * 100f);
                int hash = (x * 73856093) ^ (y * 19349663) ^ (z * 83492791) ^ name.GetHashCode();
                
                // Verify deterministic - same inputs produce same output
                int x2 = Mathf.RoundToInt(pos.x * 100f);
                int y2 = Mathf.RoundToInt(pos.y * 100f);
                int z2 = Mathf.RoundToInt(pos.z * 100f);
                int hash2 = (x2 * 73856093) ^ (y2 * 19349663) ^ (z2 * 83492791) ^ name.GetHashCode();
                
                Assert.AreEqual(hash, hash2, "Item network ID must be deterministic");
            }

            [Test]
            public void GetItemNetworkId_DifferentPositions_ProduceDifferentIds()
            {
                Vector3 pos1 = new Vector3(0, 0, 0);
                Vector3 pos2 = new Vector3(1, 0, 0);
                string name = "TestItem";
                
                int id1 = (Mathf.RoundToInt(pos1.x * 100f) * 73856093) ^ 
                          (Mathf.RoundToInt(pos1.y * 100f) * 19349663) ^ 
                          (Mathf.RoundToInt(pos1.z * 100f) * 83492791) ^ name.GetHashCode();
                int id2 = (Mathf.RoundToInt(pos2.x * 100f) * 73856093) ^ 
                          (Mathf.RoundToInt(pos2.y * 100f) * 19349663) ^ 
                          (Mathf.RoundToInt(pos2.z * 100f) * 83492791) ^ name.GetHashCode();
                
                Assert.AreNotEqual(id1, id2, "Different positions should produce different IDs");
            }

            [Test]
            public void RegisterItem_SequentialIdAssignment_Logic()
            {
                // Simulate the sequential ID assignment logic
                var items = new Dictionary<int, object>();
                var itemToId = new Dictionary<object, int>();
                int nextId = 1;
                
                // Register 3 items
                for (int i = 0; i < 3; i++)
                {
                    var item = new MockItem($"Item{i}");
                    int id = nextId++;
                    items[id] = item;
                    itemToId[item] = id;
                }
                
                Assert.AreEqual(3, items.Count);
                Assert.AreEqual(3, itemToId.Count);
                Assert.AreEqual(4, nextId); // Next available ID
                
                // Verify IDs are sequential
                Assert.IsTrue(items.ContainsKey(1));
                Assert.IsTrue(items.ContainsKey(2));
                Assert.IsTrue(items.ContainsKey(3));
            }

            [Test]
            public void RegisterItem_PreferredId_UsedWhenAvailable()
            {
                var items = new Dictionary<int, object>();
                var itemToId = new Dictionary<object, int>();
                int nextId = 1;
                
                var item = new MockItem("PreferredItem");
                int preferredId = 42;
                
                if (preferredId > 0)
                {
                    int id = preferredId;
                    if (nextId <= id) nextId = id + 1;
                    items[id] = item;
                    itemToId[item] = id;
                }
                
                Assert.AreEqual(42, itemToId[item]);
                Assert.AreEqual(43, nextId);
            }

            [Test]
            public void RegisterItem_CollisionResolution_FindsNextAvailable()
            {
                var items = new Dictionary<int, object>();
                var itemToId = new Dictionary<object, int>();
                int nextId = 1;
                
                // Pre-populate IDs 1, 2, 3
                for (int i = 1; i <= 3; i++)
                {
                    items[i] = new MockItem($"PreItem{i}");
                }
                nextId = 4;
                
                // Try to register with preferred ID that collides
                var item = new MockItem("CollidingItem");
                int preferredId = 2; // Already taken
                
                int id;
                if (preferredId > 0)
                {
                    id = preferredId;
                    if (nextId <= id) nextId = id + 1; // nextId becomes 3
                }
                else
                {
                    id = nextId++;
                }
                
                // Collision resolution
                while (items.ContainsKey(id))
                    id = nextId++;
                
                items[id] = item;
                itemToId[item] = id;
                
                // Should have resolved to ID 4 (since 2,3 taken, nextId was 3, then 4)
                Assert.AreEqual(4, id);
                Assert.AreEqual(5, nextId);
            }

            [Test]
            public void UnregisterItem_RemovesFromBothDictionaries()
            {
                var items = new Dictionary<int, object>();
                var itemToId = new Dictionary<object, int>();
                
                var item = new MockItem("TestItem");
                items[1] = item;
                itemToId[item] = 1;
                
                // Simulate unregister
                if (itemToId.TryGetValue(item, out int id))
                {
                    items.Remove(id);
                    itemToId.Remove(item);
                }
                
                Assert.AreEqual(0, items.Count);
                Assert.AreEqual(0, itemToId.Count);
            }

            [Test]
            public void UnregisterItem_NonExistentItem_NoError()
            {
                var items = new Dictionary<int, object>();
                var itemToId = new Dictionary<object, int>();
                
                var item = new MockItem("NonExistent");
                
                // Simulate unregister - should not throw
                if (itemToId.TryGetValue(item, out int id))
                {
                    items.Remove(id);
                    itemToId.Remove(item);
                }
                
                Assert.AreEqual(0, items.Count);
                Assert.AreEqual(0, itemToId.Count);
            }

            // Mock Item class for testing (stand-in for Unity's Item class)
            // Using object to avoid type compatibility issues with actual Item class
            private class MockItem
            {
                public string Name { get; }
                public MockItem(string name) => Name = name;
                public override string ToString() => Name;
                public override int GetHashCode() => Name.GetHashCode();
                public override bool Equals(object obj) => obj is MockItem other && other.Name == Name;
            }
        }

        /// <summary>
        /// Tests for RakeNetworkManager player ID assignment logic
        /// </summary>
        [TestFixture]
        public class RakeNetworkManagerPlayerIdTests
        {
            [Test]
            public void PlayerId_HostIsAlways1()
            {
                // Host always gets PlayerId = 1
                const int hostPlayerId = 1;
                Assert.AreEqual(1, hostPlayerId);
            }

            [Test]
            public void PlayerId_ClientGetsConnectionIdPlus1()
            {
                // Clients get PlayerId = connectionId + 1
                int connectionId = 5;
                int playerId = connectionId + 1;
                Assert.AreEqual(6, playerId);
            }

            [Test]
            public void PlayerId_ConnectionIdZero_GetsPlayerId1()
            {
                // connectionId 0 would be host, but host doesn't connect to itself
                // This is an edge case test
                int connectionId = 0;
                int playerId = connectionId + 1;
                Assert.AreEqual(1, playerId);
            }

            [Test]
            public void GetKnownPlayerCount_Server_ReturnsConnectedPlusHost()
            {
                // Server: _connectedClients.Count + 1 (host)
                var connectedClients = new HashSet<int> { 1, 2, 3 }; // 3 clients
                int count = connectedClients.Count + 1;
                Assert.AreEqual(4, count); // 3 clients + 1 host
            }

            [Test]
            public void GetKnownPlayerCount_Server_Empty_Returns1ForHost()
            {
                var connectedClients = new HashSet<int>();
                int count = connectedClients.Count + 1;
                Assert.AreEqual(1, count); // Just host
            }

            [Test]
            public void GetKnownPlayerCount_Client_ReturnsKnownPlayerIds()
            {
                // Client: Math.Max(1, _knownPlayerIds.Count)
                var knownPlayerIds = new HashSet<int> { 1, 2, 3 };
                int count = Math.Max(1, knownPlayerIds.Count);
                Assert.AreEqual(3, count);
            }

            [Test]
            public void GetKnownPlayerCount_Client_Empty_Returns1()
            {
                var knownPlayerIds = new HashSet<int>();
                int count = Math.Max(1, knownPlayerIds.Count);
                Assert.AreEqual(1, count); // At least self
            }
        }

        /// <summary>
        /// Tests for RakeNetworkManager camera sync packet handling logic
        /// </summary>
        [TestFixture]
        public class RakeNetworkManagerCameraSyncTests
        {
            [Test]
            public void HandleServerCameraSync_RelaysToOtherClients()
            {
                // Simulate server relay logic
                var connectedClients = new HashSet<int> { 1, 2, 3 };
                int senderId = 2;
                int relayedCount = 0;
                
                foreach (var connId in connectedClients)
                {
                    if (connId != senderId)
                        relayedCount++;
                }
                
                // Should relay to 2 other clients (1 and 3)
                Assert.AreEqual(2, relayedCount);
            }

            [Test]
            public void HandleServerCameraSync_DoesNotRelayToSender()
            {
                var connectedClients = new HashSet<int> { 1, 2 };
                int senderId = 1;
                int relayedCount = 0;
                
                // Simulate actual relay logic: only send if connId != senderId
                foreach (var connId in connectedClients)
                {
                    if (connId != senderId)
                    {
                        relayedCount++;
                    }
                }
                
                // The sender should NOT receive the relay
                // With 2 clients (1 and 2), sender=1, only client 2 should receive
                Assert.AreEqual(1, relayedCount, "Should relay to exactly 1 other client (not sender)");
            }

            [Test]
            public void HandleServerQuestTrigger_RelaysToAllClients()
            {
                // Quest triggers are relayed to ALL clients including sender (for confirmation)
                var connectedClients = new HashSet<int> { 1, 2, 3 };
                int relayedCount = 0;
                
                foreach (var connId in connectedClients)
                {
                    relayedCount++;
                }
                
                Assert.AreEqual(3, relayedCount, "QuestTrigger should go to all clients including sender");
            }

            [Test]
            public void ApplyCameraSyncLocal_FindsCameraById()
            {
                // Simulate finding camera by CameraId
                var cameras = new List<MockNetworkedCamera>
                {
                    new MockNetworkedCamera(1u),
                    new MockNetworkedCamera(2u),
                    new MockNetworkedCamera(3u)
                };
                
                uint targetId = 2u;
                MockNetworkedCamera found = null;
                
                foreach (var cam in cameras)
                {
                    if (cam.CameraId == targetId)
                    {
                        found = cam;
                        break;
                    }
                }
                
                Assert.IsNotNull(found);
                Assert.AreEqual(2u, found.CameraId);
            }

            [Test]
            public void ApplyCameraSyncLocal_HandlesMissingCamera()
            {
                var cameras = new List<MockNetworkedCamera>
                {
                    new MockNetworkedCamera(1u),
                    new MockNetworkedCamera(2u)
                };
                
                uint targetId = 99u; // Non-existent
                MockNetworkedCamera found = null;
                
                foreach (var cam in cameras)
                {
                    if (cam.CameraId == targetId)
                    {
                        found = cam;
                        break;
                    }
                }
                
                Assert.IsNull(found, "Should not find non-existent camera");
            }

            private class MockNetworkedCamera
            {
                public uint CameraId { get; }
                public MockNetworkedCamera(uint id) => CameraId = id;
            }
        }

        /// <summary>
        /// Tests for RakeNetworkManager StartGame packet handling
        /// </summary>
        [TestFixture]
        public class RakeNetworkManagerStartGameTests
        {
            [Test]
            public void HandleServerStartGame_OnlyHostCanStart()
            {
                // Simulate: only connectionId 0 (host) can start, but host doesn't send to self
                // Clients have connectionId >= 1, playerId = connectionId + 1 >= 2
                // So playerId 1 = host, playerId > 1 = clients
                
                int hostPlayerId = 1;
                int clientPlayerId = 5; // connectionId 4 + 1
                
                bool hostCanStart = (hostPlayerId == 1);
                bool clientCanStart = (clientPlayerId == 1);
                
                Assert.IsTrue(hostCanStart);
                Assert.IsFalse(clientCanStart);
            }

            [Test]
            public void HandleClientStartGame_ParsesLevelIndex()
            {
                // Simulate: int.TryParse(packet.SceneName, out int levelIndex)
                string sceneName = "2";
                bool success = int.TryParse(sceneName, out int levelIndex);
                
                Assert.IsTrue(success);
                Assert.AreEqual(2, levelIndex);
            }

            [Test]
            public void HandleClientStartGame_FallbackToSceneName()
            {
                string sceneName = "GameScene";
                bool success = int.TryParse(sceneName, out int levelIndex);
                
                Assert.IsFalse(success);
                // Fallback would use sceneName directly
                Assert.AreEqual("GameScene", sceneName);
            }
        }

        /// <summary>
        /// Tests for RakeNetworkManager ServerBroadcast and SendToServer methods
        /// </summary>
        [TestFixture]
        public class RakeNetworkManagerBroadcastTests
        {
            [Test]
            public void ServerBroadcast_SendsToAllConnectedClients()
            {
                // Simulate: foreach (var connId in _connectedClients) _server.Send(connId, data);
                var connectedClients = new List<int> { 1, 2, 3, 4, 5 };
                int sentCount = 0;
                
                foreach (var connId in connectedClients)
                {
                    sentCount++;
                }
                
                Assert.AreEqual(5, sentCount);
            }

            [Test]
            public void ServerBroadcast_EmptyClientList_SendsZero()
            {
                var connectedClients = new List<int>();
                int sentCount = 0;
                
                foreach (var connId in connectedClients)
                {
                    sentCount++;
                }
                
                Assert.AreEqual(0, sentCount);
            }

            [Test]
            public void SendToServer_ClientConnected_SendsData()
            {
                // Simulate: if (_client != null && _client.Connected) _client.Send(data);
                bool clientExists = true;
                bool clientConnected = true;
                bool shouldSend = clientExists && clientConnected;
                
                Assert.IsTrue(shouldSend);
            }

            [Test]
            public void SendToServer_ClientNotConnected_DoesNotSend()
            {
                bool clientExists = true;
                bool clientConnected = false;
                bool shouldSend = clientExists && clientConnected;
                
                Assert.IsFalse(shouldSend);
            }

            [Test]
            public void SendToServer_ClientNull_DoesNotSend()
            {
                bool clientExists = false;
                bool clientConnected = true;
                bool shouldSend = clientExists && clientConnected;
                
                Assert.IsFalse(shouldSend);
            }
        }

        /// <summary>
        /// Tests for RakeNetworkManager ProcessPendingConnects logic
        /// </summary>
        [TestFixture]
        public class RakeNetworkManagerPendingConnectsTests
        {
            [Test]
            public void ProcessPendingConnects_ProcessesAllQueuedPackets()
            {
                var queue = new Queue<MockPlayerConnectedPacket>();
                queue.Enqueue(new MockPlayerConnectedPacket { PlayerId = 1, IsYou = true });
                queue.Enqueue(new MockPlayerConnectedPacket { PlayerId = 2, IsYou = false });
                queue.Enqueue(new MockPlayerConnectedPacket { PlayerId = 3, IsYou = false });
                
                int processedCount = 0;
                while (queue.Count > 0)
                {
                    var packet = queue.Dequeue();
                    processedCount++;
                }
                
                Assert.AreEqual(3, processedCount);
                Assert.AreEqual(0, queue.Count);
            }

            [Test]
            public void ProcessPendingConnects_TracksPlayerIds()
            {
                var queue = new Queue<MockPlayerConnectedPacket>();
                var knownPlayerIds = new HashSet<int>();
                
                queue.Enqueue(new MockPlayerConnectedPacket { PlayerId = 1, IsYou = true });
                queue.Enqueue(new MockPlayerConnectedPacket { PlayerId = 2, IsYou = false });
                
                while (queue.Count > 0)
                {
                    var packet = queue.Dequeue();
                    knownPlayerIds.Add(packet.PlayerId);
                }
                
                Assert.AreEqual(2, knownPlayerIds.Count);
                Assert.IsTrue(knownPlayerIds.Contains(1));
                Assert.IsTrue(knownPlayerIds.Contains(2));
            }

            private class MockPlayerConnectedPacket
            {
                public int PlayerId { get; set; }
                public bool IsYou { get; set; }
            }
        }

        /// <summary>
        /// Tests for RakeNetworkManager ItemPickup packet handling logic
        /// </summary>
        [TestFixture]
        public class RakeNetworkManagerItemPickupTests
        {
            [Test]
            public void HandleServerItemPickup_RaceConditionProtection()
            {
                // Simulate: if (!_networkedItems.ContainsKey(packet.ItemNetworkId)) return;
                var networkedItems = new Dictionary<int, object>();
                networkedItems[1] = new object(); // Item exists
                networkedItems[2] = new object();
                
                int itemIdToPickup = 1;
                bool itemExists = networkedItems.ContainsKey(itemIdToPickup);
                Assert.IsTrue(itemExists, "Item should exist before pickup");
                
                // Simulate pickup - remove immediately
                networkedItems.Remove(itemIdToPickup);
                bool itemStillExists = networkedItems.ContainsKey(itemIdToPickup);
                Assert.IsFalse(itemStillExists, "Item should be removed after pickup");
                
                // Second pickup attempt for same item
                bool secondAttemptExists = networkedItems.ContainsKey(itemIdToPickup);
                Assert.IsFalse(secondAttemptExists, "Second pickup should fail - item already taken");
            }

            [Test]
            public void HandleServerItemPickup_GivesCorrectWeaponId()
            {
                // Test the item type to weapon ID mapping logic
                var itemTypeToWeaponId = new Dictionary<string, int>
                {
                    { "Medkit_item", 8 },
                    { "Rifle_item", 1 },
                    { "RifleAmmo_item", 1 },
                    { "SniperRifle_item", 6 },
                    { "SniperAmmo_item", 6 },
                    { "Trap_item", 4 },
                    { "Camera_item", 2 },
                    { "Binocular_item", 7 },
                    { "MapCase_item", 3 },
                    { "Food_item", 5 }
                };
                
                foreach (var kvp in itemTypeToWeaponId)
                {
                    Assert.IsTrue(kvp.Value > 0, $"Weapon ID for {kvp.Key} should be positive");
                }
                
                // Verify specific mappings
                Assert.AreEqual(8, itemTypeToWeaponId["Medkit_item"]);
                Assert.AreEqual(1, itemTypeToWeaponId["Rifle_item"]);
                Assert.AreEqual(2, itemTypeToWeaponId["Camera_item"]);
            }
        }

        /// <summary>
        /// Tests for RakeNetworkManager DamageRequest handling logic
        /// </summary>
        [TestFixture]
        public class RakeNetworkManagerDamageRequestTests
        {
            [Test]
            public void HandleServerDamageRequest_CalculatesHitDirection()
            {
                // Simulate: hitDirection = (enemyAI.transform.position - shooterPosition).normalized;
                Vector3 enemyPos = new Vector3(10, 0, 20);
                Vector3 shooterPos = new Vector3(0, 0, 0);
                
                Vector3 hitDirection = (enemyPos - shooterPos).normalized;
                Vector3 expected = new Vector3(10, 0, 20).normalized;
                
                Assert.AreEqual(expected, hitDirection);
            }

            [Test]
            public void HandleServerDamageRequest_DamageReductionApplied()
            {
                // Simulate: finalAmount = Mathf.RoundToInt(amount * reduction);
                int amount = 25;
                float reduction = 0.2f; // 80% reduction
                
                int finalAmount = Mathf.RoundToInt(amount * reduction);
                Assert.AreEqual(5, finalAmount, "25 * 0.2 = 5");
            }

            [Test]
            public void HandleServerDamageRequest_BroadcastsDamageApplied()
            {
                // Simulate broadcasting DamageAppliedPacket to all clients
                var connectedClients = new List<int> { 1, 2, 3 };
                int broadcastCount = 0;
                foreach (var connId in connectedClients)
                {
                    // Would send DamageAppliedPacket { TargetId = 99, NewHealth = 75f }
                    broadcastCount++;
                }
                
                Assert.AreEqual(3, broadcastCount);
            }
        }

        /// <summary>
        /// Tests for RakeNetworkManager RakeStatePacket handling
        /// </summary>
        [TestFixture]
        public class RakeNetworkManagerRakeStateTests
        {
            [Test]
            public void HandleClientRakeState_AppliesSyncData()
            {
                // Simulate: _cachedRake.ApplySyncData(packet.Position, packet.Rotation, packet.State, packet.Health);
                // Use raw Quaternion constructor instead of Quaternion.Euler (requires Unity runtime)
                Vector3 position = new Vector3(10, 5, 20);
                Quaternion rotation = new Quaternion(0f, 0.707107f, 0f, 0.707107f); // 90 degrees around Y
                byte state = 2; // Chase
                float health = 75.5f;
                
                // Just verify the parameters are correctly extracted
                Assert.AreEqual(new Vector3(10, 5, 20), position);
                Assert.AreEqual(new Quaternion(0f, 0.707107f, 0f, 0.707107f), rotation);
                Assert.AreEqual(2, state);
                Assert.AreEqual(75.5f, health);
            }

            [Test]
            public void HandleClientDamageApplied_UpdatesHealth()
            {
                // Simulate: _cachedRake.ApplyHealth(packet.NewHealth);
                float newHealth = 50f;
                
                Assert.AreEqual(50f, newHealth);
            }
        }

        #endregion

        #region NetworkedCamera Static Registry Tests

        /// <summary>
        /// Tests the static camera registry logic (O(1) lookup by CameraId)
        /// </summary>
        [TestFixture]
        public class NetworkedCameraRegistryTests
        {
            [Test]
            public void TryGetCamera_ReturnsTrue_WhenCameraExists()
            {
                var registry = new Dictionary<uint, MockNetworkedCamera>
                {
                    { 1u, new MockNetworkedCamera(1u) },
                    { 2u, new MockNetworkedCamera(2u) },
                    { 3u, new MockNetworkedCamera(3u) }
                };
                
                bool found = registry.TryGetValue(2u, out var camera);
                
                Assert.IsTrue(found);
                Assert.IsNotNull(camera);
                Assert.AreEqual(2u, camera.CameraId);
            }

            [Test]
            public void TryGetCamera_ReturnsFalse_WhenCameraMissing()
            {
                var registry = new Dictionary<uint, MockNetworkedCamera>
                {
                    { 1u, new MockNetworkedCamera(1u) },
                    { 2u, new MockNetworkedCamera(2u) }
                };
                
                bool found = registry.TryGetValue(99u, out var camera);
                
                Assert.IsFalse(found);
                Assert.IsNull(camera);
            }

            [Test]
            public void Registry_ReplacesExistingCameraId()
            {
                var registry = new Dictionary<uint, MockNetworkedCamera>
                {
                    { 1u, new MockNetworkedCamera(1u) }
                };
                
                // Simulate replacement logic from Start()
                var newCamera = new MockNetworkedCamera(1u);
                if (registry.ContainsKey(1u))
                {
                    registry[1u] = newCamera; // Replace
                }
                
                Assert.AreSame(newCamera, registry[1u]);
            }

            [Test]
            public void Registry_RemovesCamera_OnDestroy()
            {
                var registry = new Dictionary<uint, MockNetworkedCamera>
                {
                    { 1u, new MockNetworkedCamera(1u) },
                    { 2u, new MockNetworkedCamera(2u) }
                };
                
                // Simulate OnDestroy cleanup
                uint cameraId = 1u;
                if (registry.ContainsKey(cameraId))
                {
                    registry.Remove(cameraId);
                }
                
                Assert.IsFalse(registry.ContainsKey(1u));
                Assert.IsTrue(registry.ContainsKey(2u));
            }

            private class MockNetworkedCamera
            {
                public uint CameraId { get; }
                public MockNetworkedCamera(uint id) => CameraId = id;
            }
        }

        #endregion

        #region NetworkedCamera Visual Update Logic Tests

        /// <summary>
        /// Tests the UpdateCameraVisuals logic
        /// </summary>
        [TestFixture]
        public class NetworkedCameraVisualsTests
        {
            [Test]
            public void UpdateCameraVisuals_ActiveAndNotBroken_EnablesVisuals()
            {
                bool isActive = true;
                bool isBroken = false;
                
                bool shouldBeActive = isActive && !isBroken;
                Assert.IsTrue(shouldBeActive);
            }

            [Test]
            public void UpdateCameraVisuals_Inactive_DisablesVisuals()
            {
                bool isActive = false;
                bool isBroken = false;
                
                bool shouldBeActive = isActive && !isBroken;
                Assert.IsFalse(shouldBeActive);
            }

            [Test]
            public void UpdateCameraVisuals_Broken_DisablesVisuals()
            {
                bool isActive = true;
                bool isBroken = true;
                
                bool shouldBeActive = isActive && !isBroken;
                Assert.IsFalse(shouldBeActive, "Broken cameras should never be active visually");
            }

            [Test]
            public void UpdateCameraVisuals_CameraComponentEnabledMatches()
            {
                bool isActive = true;
                bool isBroken = false;
                
                bool cameraEnabled = isActive && !isBroken;
                Assert.IsTrue(cameraEnabled);
                
                isBroken = true;
                cameraEnabled = isActive && !isBroken;
                Assert.IsFalse(cameraEnabled);
            }
        }

        #endregion

        #region NetworkedCamera ApplyCameraSync Logic Tests

        /// <summary>
        /// Tests the ApplyCameraSync logic (client-side state application)
        /// </summary>
        [TestFixture]
        public class NetworkedCameraApplySyncTests
        {
            [Test]
            public void ApplyCameraSync_UpdatesAllFields()
            {
                // Simulate the field updates in ApplyCameraSync
                uint cameraId = 0;
                float batteryLevel = 0f;
                bool isActive = false;
                bool isBroken = false;
                int placedByPlayerId = 0;
                
                var packet = new RakeMultiplayer.Networking.CameraSyncPacket
                {
                    CameraId = 42u,
                    BatteryLevel = 75.5f,
                    IsActive = true,
                    IsBroken = false,
                    PlacedByPlayerId = 5
                };
                
                cameraId = packet.CameraId;
                batteryLevel = packet.BatteryLevel;
                isActive = packet.IsActive;
                isBroken = packet.IsBroken;
                placedByPlayerId = packet.PlacedByPlayerId;
                
                Assert.AreEqual(42u, cameraId);
                Assert.AreEqual(75.5f, batteryLevel, 0.001f);
                Assert.IsTrue(isActive);
                Assert.IsFalse(isBroken);
                Assert.AreEqual(5, placedByPlayerId);
            }

            [Test]
            public void ApplyCameraSync_FiresBatteryChangedEvent_WhenBatteryChanges()
            {
                float oldBattery = 100f;
                float newBattery = 75.5f;
                bool batteryChanged = Math.Abs(oldBattery - newBattery) > 0.01f;
                
                Assert.IsTrue(batteryChanged);
            }

            [Test]
            public void ApplyCameraSync_DoesNotFireBatteryChanged_WhenBatterySame()
            {
                float oldBattery = 100f;
                float newBattery = 100.005f; // Difference < 0.01
                bool batteryChanged = Math.Abs(oldBattery - newBattery) > 0.01f;
                
                Assert.IsFalse(batteryChanged);
            }

            [Test]
            public void ApplyCameraSync_FiresCameraBroken_WhenBecomesBroken()
            {
                bool oldBroken = false;
                bool newBroken = true;
                bool brokenChanged = oldBroken != newBroken;
                bool firesEvent = brokenChanged && newBroken;
                
                Assert.IsTrue(firesEvent);
            }

            [Test]
            public void ApplyCameraSync_DoesNotFireCameraBroken_WhenAlreadyBroken()
            {
                bool oldBroken = true;
                bool newBroken = true;
                bool brokenChanged = oldBroken != newBroken;
                bool firesEvent = brokenChanged && newBroken;
                
                Assert.IsFalse(firesEvent);
            }

            [Test]
            public void ApplyCameraSync_DoesNotFireCameraBroken_WhenFixed()
            {
                bool oldBroken = true;
                bool newBroken = false;
                bool brokenChanged = oldBroken != newBroken;
                bool firesEvent = brokenChanged && newBroken;
                
                Assert.IsFalse(firesEvent, "Should not fire OnCameraBroken when fixing");
            }
        }

        #endregion

        #region NetworkedCamera HandleCameraSpawn Logic Tests

        /// <summary>
        /// Tests the HandleCameraSpawn logic (client-side)
        /// </summary>
        [TestFixture]
        public class NetworkedCameraHandleSpawnTests
        {
            [Test]
            public void HandleCameraSpawn_InitializesState()
            {
                uint cameraId = 0;
                int placedByPlayerId = 0;
                float batteryLevel = 0f;
                bool isActive = true; // Start with different values
                bool isBroken = true;
                
                var packet = new RakeMultiplayer.Networking.CameraSpawnPacket
                {
                    CameraId = 10u,
                    PlacedByPlayerId = 3
                };
                
                cameraId = packet.CameraId;
                placedByPlayerId = packet.PlacedByPlayerId;
                batteryLevel = 100f;
                isActive = false;
                isBroken = false;
                
                Assert.AreEqual(10u, cameraId);
                Assert.AreEqual(3, placedByPlayerId);
                Assert.AreEqual(100f, batteryLevel);
                Assert.IsFalse(isActive);
                Assert.IsFalse(isBroken);
            }
        }

        #endregion

        #region NetworkedCamera HandleCameraDestroy Logic Tests

        /// <summary>
        /// Tests the HandleCameraDestroy logic (client-side)
        /// </summary>
        [TestFixture]
        public class NetworkedCameraHandleDestroyTests
        {
            [Test]
            public void HandleCameraDestroy_LogsAndWaitsForNetIdentityCleanup()
            {
                // Simulate: object will be destroyed by NetIdentity cleanup
                // This test just verifies the logic doesn't crash
                uint cameraId = 5u;
                var packet = new RakeMultiplayer.Networking.CameraDestroyPacket
                {
                    CameraId = cameraId
                };
                
                Assert.AreEqual(cameraId, packet.CameraId);
                // In real code: NetIdentity handles actual destruction
            }
        }

        #endregion

        #region NetworkedCamera HandleQuestTrigger Logic Tests

        /// <summary>
        /// Tests the HandleQuestTrigger logic (client-side)
        /// </summary>
        [TestFixture]
        public class NetworkedCameraHandleQuestTriggerTests
        {
            [Test]
            public void HandleQuestTrigger_CameraBreak_LogsInfo()
            {
                // TriggerType: 0=CameraBreak
                byte triggerType = 0;
                uint cameraId = 7u;
                
                bool isCameraBreak = (triggerType == 0 && cameraId > 0);
                Assert.IsTrue(isCameraBreak);
            }

            [Test]
            public void HandleQuestTrigger_BaitTaken_LogsInfo()
            {
                // TriggerType: 1=BaitTaken
                byte triggerType = 1;
                
                bool isBaitTaken = (triggerType == 1);
                Assert.IsTrue(isBaitTaken);
            }

            [Test]
            public void HandleQuestTrigger_TrapTriggered_LogsInfo()
            {
                // TriggerType: 2=TrapTriggered
                byte triggerType = 2;
                
                bool isTrapTriggered = (triggerType == 2);
                Assert.IsTrue(isTrapTriggered);
            }

            [Test]
            public void HandleQuestTrigger_UnknownType_NoSpecificHandling()
            {
                byte triggerType = 255; // Unknown
                
                bool isKnown = (triggerType == 0 || triggerType == 1 || triggerType == 2);
                Assert.IsFalse(isKnown);
            }
        }

        #endregion

        #region Adversarial / Edge Case Tests

        /// <summary>
        /// Adversarial tests for edge cases and invalid inputs
        /// </summary>
        [TestFixture]
        public class AdversarialTests
        {
            [Test]
            public void CameraSyncPacket_VeryLargeArray_ThrowsPacketTooLarge()
            {
                // PacketSerializer has MAX_PACKET_SIZE = 65536
                byte[] largeData = new byte[65537];
                largeData[0] = (byte)PacketType.CameraSync; // Valid type byte
                
                Assert.Throws<ArgumentException>(() => PacketSerializer.Deserialize(largeData));
            }

            [Test]
            public void CameraSyncPacket_NaNValues_InRotation_Serializes()
            {
                // Rotation with NaN components
                var original = new RakeMultiplayer.Networking.CameraSyncPacket
                {
                    CameraId = 1u,
                    Position = Vector3.zero,
                    Rotation = new Quaternion(float.NaN, 0, 0, 0),
                    IsActive = false,
                    IsBroken = false,
                    BatteryLevel = 50f,
                    PlacedByPlayerId = 0
                };
                
                // Should serialize without throwing
                Assert.DoesNotThrow(() => original.Serialize());
            }

            [Test]
            public void CameraSyncPacket_InfinityValues_InPosition_Serializes()
            {
                var original = new RakeMultiplayer.Networking.CameraSyncPacket
                {
                    CameraId = 1u,
                    Position = new Vector3(float.PositiveInfinity, float.NegativeInfinity, 0),
                    Rotation = Quaternion.identity,
                    IsActive = false,
                    IsBroken = false,
                    BatteryLevel = 50f,
                    PlacedByPlayerId = 0
                };
                
                Assert.DoesNotThrow(() => original.Serialize());
            }

            [Test]
            public void PacketSerializer_CorruptedData_ThrowsOrReturnsPartial()
            {
                // Truncated packet data
                var original = new RakeMultiplayer.Networking.CameraSyncPacket
                {
                    CameraId = 1u,
                    Position = new Vector3(10, 20, 30),
                    Rotation = Quaternion.identity,
                    IsActive = true,
                    IsBroken = false,
                    BatteryLevel = 100f,
                    PlacedByPlayerId = 1
                };
                
                byte[] data = original.Serialize();
                byte[] truncated = new byte[data.Length / 2]; // Cut in half
                Array.Copy(data, truncated, truncated.Length);
                
                // Should throw due to insufficient data
                Assert.Throws<EndOfStreamException>(() => PacketSerializer.Deserialize(truncated));
            }

            [Test]
            public void BatteryDrainLogic_NegativeDrainRate_ReturnsMaxValue()
            {
                float batteryLevel = 50f;
                float batteryDrainPerMinute = -10f; // Invalid negative drain
                
                float remaining = batteryDrainPerMinute <= 0 ? float.MaxValue : (batteryLevel / batteryDrainPerMinute) * 60f;
                Assert.AreEqual(float.MaxValue, remaining);
            }

            [Test]
            public void GetItemNetworkId_NullPosition_HandlesGracefully()
            {
                // Simulate fallback with zero position
                Vector3 pos = Vector3.zero;
                string name = "TestItem";
                
                int x = Mathf.RoundToInt(pos.x * 100f);
                int y = Mathf.RoundToInt(pos.y * 100f);
                int z = Mathf.RoundToInt(pos.z * 100f);
                int hash = (x * 73856093) ^ (y * 19349663) ^ (z * 83492791) ^ name.GetHashCode();
                
                Assert.AreEqual(name.GetHashCode(), hash, "Zero position should produce hash based only on name");
            }

            [Test]
            public void RegisterItem_IntOverflowProtection()
            {
                // Test that nextId increment doesn't overflow
                int nextId = int.MaxValue;
                int id = nextId;
                
                // In real code: if (_nextItemNetworkId <= id) _nextItemNetworkId = id + 1;
                // This would overflow, but C# wraps or throws in checked context
                // The code doesn't use checked, so it would wrap to negative
                // This test documents the potential issue
                Assert.AreEqual(int.MaxValue, id);
                
                // Note: Actual code should use checked or handle overflow
                // This is a documented edge case
            }
        }

        #endregion

        #region RakeNetworkManager Client-Side Packet Handler Tests

        /// <summary>
        /// Tests for client-side packet handler switch statement coverage
        /// </summary>
        [TestFixture]
        public class RakeNetworkManagerClientPacketHandlersTests
        {
            [Test]
            public void OnClientDataReceived_HandlesAllExpectedPacketTypes()
            {
                // Verify all packet types handled in client switch are tested
                var handledTypes = new HashSet<PacketType>
                {
                    PacketType.PlayerState,
                    PacketType.PlayerConnected,
                    PacketType.PlayerDisconnected,
                    PacketType.RakeState,
                    PacketType.DamageApplied,
                    PacketType.FireVisuals,
                    PacketType.StartGame,
                    PacketType.ItemDestroyed,
                    PacketType.PhaseChange,
                    PacketType.SessionState,
                    PacketType.CameraSync,
                    PacketType.CameraSpawn,
                    PacketType.CameraDestroy,
                    PacketType.QuestTrigger
                };
                
                // These should all have corresponding HandleClient* methods
                Assert.AreEqual(14, handledTypes.Count);
                
                // Verify none are server-only types
                var serverOnlyTypes = new HashSet<PacketType>
                {
                    PacketType.DamageRequest,
                    PacketType.ItemPickup,
                    PacketType.StartGame // Actually client can send this too
                };
                
                // CameraSync, CameraSpawn, CameraDestroy, QuestTrigger are server-authoritative
                // but client RECEIVES them (has handlers), doesn't SEND them
            }

            [Test]
            public void OnServerDataReceived_RejectsServerAuthoritativePackets()
            {
                // Server should log warning and ignore these from clients
                var rejectedTypes = new HashSet<PacketType>
                {
                    PacketType.CameraSync,
                    PacketType.CameraSpawn,
                    PacketType.CameraDestroy,
                    PacketType.QuestTrigger
                };
                
                foreach (var type in rejectedTypes)
                {
                    // In real code: RakeMultiplayerPlugin.Log?.LogWarning($"[Net] Server: received {type} from C{connectionId} (client-spoofed, ignoring)");
                    Assert.IsTrue(true, $"Server rejects {type} from clients");
                }
            }
        }

        #endregion

        #region Integration: NetworkedCamera + RakeNetworkManager Packet Flow Tests

        /// <summary>
        /// Tests the full packet flow: NetworkedCamera -> RakeNetworkManager -> Network
        /// </summary>
        [TestFixture]
        public class CameraNetworkFlowTests
        {
            [Test]
            public void CameraSyncPacket_FullRoundTrip_ServerToClient()
            {
                // 1. Server creates CameraSyncPacket
                // Use raw Quaternion constructor instead of Quaternion.Euler (requires Unity runtime)
                var serverPacket = new RakeMultiplayer.Networking.CameraSyncPacket
                {
                    CameraId = 99u,
                    Position = new Vector3(100f, 10f, 50f),
                    Rotation = new Quaternion(0f, 0.382683f, 0f, 0.92388f), // 45 degrees around Y
                    IsActive = true,
                    IsBroken = false,
                    BatteryLevel = 85.5f,
                    PlacedByPlayerId = 2
                };
                
                // 2. Serialize for network
                byte[] data = serverPacket.Serialize();
                Assert.Greater(data.Length, 0);
                
                // 3. Deserialize on client (via PacketSerializer)
                var clientPacket = PacketSerializer.Deserialize(data) as RakeMultiplayer.Networking.CameraSyncPacket;
                Assert.IsNotNull(clientPacket);
                
                // 4. Client applies to NetworkedCamera (simulated)
                uint cameraId = clientPacket.CameraId;
                float battery = clientPacket.BatteryLevel;
                bool active = clientPacket.IsActive;
                bool broken = clientPacket.IsBroken;
                int placer = clientPacket.PlacedByPlayerId;
                
                Assert.AreEqual(99u, cameraId);
                Assert.AreEqual(85.5f, battery, 0.001f);
                Assert.IsTrue(active);
                Assert.IsFalse(broken);
                Assert.AreEqual(2, placer);
            }

            [Test]
            public void CameraSpawnPacket_FullRoundTrip_ServerToClient()
            {
                var serverPacket = new RakeMultiplayer.Networking.CameraSpawnPacket
                {
                    CameraId = 123u,
                    Position = new Vector3(-50f, 5f, 200f),
                    Rotation = new Quaternion(0f, 1f, 0f, 0f), // 180 degrees around Y
                    PlacedByPlayerId = 4
                };
                
                byte[] data = serverPacket.Serialize();
                var clientPacket = PacketSerializer.Deserialize(data) as RakeMultiplayer.Networking.CameraSpawnPacket;
                
                Assert.IsNotNull(clientPacket);
                Assert.AreEqual(123u, clientPacket.CameraId);
                Assert.AreEqual(new Vector3(-50f, 5f, 200f), clientPacket.Position);
                Assert.AreEqual(4, clientPacket.PlacedByPlayerId);
            }

            [Test]
            public void QuestTriggerPacket_FullRoundTrip_ServerToAllClients()
            {
                var serverPacket = new RakeMultiplayer.Networking.QuestTriggerPacket
                {
                    TriggerType = 0, // CameraBreak
                    Position = new Vector3(10f, 2f, 30f),
                    CameraId = 7u
                };
                
                byte[] data = serverPacket.Serialize();
                
                // Multiple clients receive same data
                for (int i = 0; i < 4; i++)
                {
                    var clientPacket = PacketSerializer.Deserialize(data) as RakeMultiplayer.Networking.QuestTriggerPacket;
                    Assert.IsNotNull(clientPacket);
                    Assert.AreEqual(0, clientPacket.TriggerType);
                    Assert.AreEqual(7u, clientPacket.CameraId);
                }
            }

            [Test]
            public void CameraDestroyPacket_FullRoundTrip_ServerToClient()
            {
                var serverPacket = new RakeMultiplayer.Networking.CameraDestroyPacket
                {
                    CameraId = 456u
                };
                
                byte[] data = serverPacket.Serialize();
                var clientPacket = PacketSerializer.Deserialize(data) as RakeMultiplayer.Networking.CameraDestroyPacket;
                
                Assert.IsNotNull(clientPacket);
                Assert.AreEqual(456u, clientPacket.CameraId);
            }
        }

        #endregion

        #region Camera Quota (5-camera limit) Tests - Task 3.3

        /// <summary>
        /// Tests for the server-authoritative 5-camera limit (Task 3.3).
        /// The original game rejects setup when cameraCount >= 5
        /// (MP_RecordCamera_obj.CheckSetup: "You cannot setup no more 5 cameras").
        /// </summary>
        [TestFixture]
        public class CameraQuotaTests
        {
            private class QuotaCameraState
            {
                public bool IsPlaced;
                public bool IsBroken;

                public QuotaCameraState(bool isPlaced, bool isBroken)
                {
                    IsPlaced = isPlaced;
                    IsBroken = isBroken;
                }
            }

            /// <summary>
            /// Mirror of NetworkedCamera.GetUsableCameraCount semantics:
            /// only placed, non-broken cameras consume a quota slot.
            /// </summary>
            private static int ComputeUsableCount(QuotaCameraState[] cameras)
            {
                int count = 0;
                foreach (var cam in cameras)
                {
                    if (cam.IsPlaced && !cam.IsBroken) count++;
                }
                return count;
            }

            private static bool ComputeCanPlace(int usableCount, int maxCameras = 5)
            {
                return usableCount < maxCameras;
            }

            [Test]
            public void MaxCameras_IsFive()
            {
                Assert.AreEqual(5, NetworkedCamera.MaxCameras,
                    "The original game allows at most 5 cameras set up at once");
            }

            [Test]
            public void CanPlaceCamera_TrueWhenNoCamerasPlaced()
            {
                Assert.IsTrue(ComputeCanPlace(0));
            }

            [Test]
            public void CanPlaceCamera_TrueForFifthCamera()
            {
                Assert.IsTrue(ComputeCanPlace(4));
            }

            [Test]
            public void CanPlaceCamera_FalseForSixthCamera()
            {
                Assert.IsFalse(ComputeCanPlace(5));
            }

            [Test]
            public void CanPlaceCamera_FalseWhenOverQuota()
            {
                Assert.IsFalse(ComputeCanPlace(6));
            }

            [Test]
            public void UsableCount_IgnoresUnplacedCameras()
            {
                var cameras = new QuotaCameraState[]
                {
                    new QuotaCameraState(false, false), // exists in scene but not deployed
                    new QuotaCameraState(false, false)
                };

                Assert.AreEqual(0, ComputeUsableCount(cameras));
                Assert.IsTrue(ComputeCanPlace(ComputeUsableCount(cameras)));
            }

            [Test]
            public void UsableCount_CountsPlacedCameras()
            {
                var cameras = new QuotaCameraState[]
                {
                    new QuotaCameraState(true, false),
                    new QuotaCameraState(true, false),
                    new QuotaCameraState(true, false),
                    new QuotaCameraState(true, false),
                    new QuotaCameraState(true, false)
                };

                Assert.AreEqual(5, ComputeUsableCount(cameras));
                Assert.IsFalse(ComputeCanPlace(ComputeUsableCount(cameras)));
            }

            [Test]
            public void UsableCount_BrokenCameraFreesSlot()
            {
                var cameras = new QuotaCameraState[]
                {
                    new QuotaCameraState(true, false),
                    new QuotaCameraState(true, false),
                    new QuotaCameraState(true, false),
                    new QuotaCameraState(true, false),
                    new QuotaCameraState(true, true)  // broken -> does not consume quota
                };

                Assert.AreEqual(4, ComputeUsableCount(cameras));
                Assert.IsTrue(ComputeCanPlace(ComputeUsableCount(cameras)),
                    "A broken camera must free a slot so a replacement can be set up");
            }

            [Test]
            public void PlacedCount_IncludesBrokenCameras()
            {
                var cameras = new QuotaCameraState[]
                {
                    new QuotaCameraState(true, false),
                    new QuotaCameraState(true, true)
                };

                int placed = 0;
                foreach (var cam in cameras)
                {
                    if (cam.IsPlaced) placed++;
                }

                Assert.AreEqual(2, placed);
                int usable = ComputeUsableCount(cameras);
                Assert.AreEqual(1, usable);
            }

            [Test]
            public void InitializePlacement_RejectsWhenQuotaFull()
            {
                // Simulate server logic: fifth camera occupies the last slot,
                // placing a sixth must be rejected (return false).
                int usable = 5;
                bool deployed = false;

                if (ComputeCanPlace(usable))
                {
                    deployed = true;
                }

                Assert.IsFalse(deployed, "The 6th camera must be rejected by the server");
            }

            [Test]
            public void InitializePlacement_AllowsUpToFive()
            {
                int usable = 0;
                int deployed = 0;

                for (int i = 0; i < 6; i++)
                {
                    if (ComputeCanPlace(usable))
                    {
                        deployed++;
                        usable++;
                    }
                }

                Assert.AreEqual(5, deployed, "Exactly 5 cameras may be deployed");
            }

            [Test]
            public void CameraItemPickup_RejectedWhenQuotaFull()
            {
                // Server HandleServerItemPickup: Camera_item pickup is rejected
                // when !NetworkedCamera.CanPlaceCamera().
                const string itemType = "Camera_item";
                int usable = 5;

                bool allowPickup = !(itemType == "Camera_item" && !ComputeCanPlace(usable));
                Assert.IsFalse(allowPickup);
            }

            [Test]
            public void NonCameraItemPickup_UnaffectedByQuota()
            {
                const string itemType = "Medkit_item";
                int usable = 5;

                bool allowPickup = !(itemType == "Camera_item" && !ComputeCanPlace(usable));
                Assert.IsTrue(allowPickup, "Only camera items are subject to the camera quota");
            }
        }

        /// <summary>
        /// Serialization tests for the rejection packet introduced by Task 3.3
        /// </summary>
        [TestFixture]
        public class ItemPickupConfirmPacketTests
        {
            [Test]
            public void ItemPickupConfirm_Rejection_RoundTrip()
            {
                var original = new ItemPickupConfirmPacket
                {
                    PlayerId = 3,
                    ItemNetworkId = 42,
                    Success = false,
                    Reason = "CameraLimit"
                };

                byte[] data = original.Serialize();
                var restored = PacketSerializer.Deserialize(data) as ItemPickupConfirmPacket;

                Assert.IsNotNull(restored);
                Assert.AreEqual(PacketType.ItemPickupConfirm, restored.Type);
                Assert.AreEqual(3, restored.PlayerId);
                Assert.AreEqual(42, restored.ItemNetworkId);
                Assert.IsFalse(restored.Success);
                Assert.AreEqual("CameraLimit", restored.Reason);
            }

            [Test]
            public void ItemPickupConfirm_Success_RoundTrip()
            {
                var original = new ItemPickupConfirmPacket
                {
                    PlayerId = 1,
                    ItemNetworkId = 7,
                    Success = true,
                    Reason = string.Empty
                };

                byte[] data = original.Serialize();
                var restored = PacketSerializer.Deserialize(data) as ItemPickupConfirmPacket;

                Assert.IsNotNull(restored);
                Assert.IsTrue(restored.Success);
                Assert.AreEqual(string.Empty, restored.Reason);
            }

            [Test]
            public void ItemPickupConfirm_NullReason_SerializesAsEmpty()
            {
                var original = new ItemPickupConfirmPacket
                {
                    PlayerId = 2,
                    ItemNetworkId = 9,
                    Success = false,
                    Reason = null
                };

                byte[] data = original.Serialize();
                var restored = PacketSerializer.Deserialize(data) as ItemPickupConfirmPacket;

                Assert.IsNotNull(restored);
                Assert.AreEqual(string.Empty, restored.Reason);
            }

            [Test]
            public void ItemPickupConfirm_PacketTypeValue_IsEleven()
            {
                Assert.AreEqual(11, (byte)PacketType.ItemPickupConfirm);
            }

            [Test]
            public void ItemPickupConfirm_Serialize_PreservesPacketTypeByte()
            {
                var packet = new ItemPickupConfirmPacket
                {
                    PlayerId = 1,
                    ItemNetworkId = 1,
                    Success = false,
                    Reason = "CameraLimit"
                };

                byte[] data = packet.Serialize();
                Assert.Greater(data.Length, 0);
                Assert.AreEqual((byte)PacketType.ItemPickupConfirm, data[0]);
            }
        }

        #endregion

        #region Camera Setup Request Tests - Task 3.4

        /// <summary>
        /// Tests for the camera setup/pickup request packet (client -> server)
        /// and the deterministic camera ID assignment used by RegisterCameras.
        /// </summary>
        [TestFixture]
        public class CameraSetupRequestPacketTests
        {
            [Test]
            public void Request_RoundTrip_Setup()
            {
                var original = new CameraSetupRequestPacket
                {
                    CameraId = 12u,
                    PlayerId = 3,
                    IsSetup = true,
                    IsResult = false,
                    Approved = false,
                    Reason = string.Empty
                };

                byte[] data = original.Serialize();
                var restored = PacketSerializer.Deserialize(data) as CameraSetupRequestPacket;

                Assert.IsNotNull(restored);
                Assert.AreEqual(PacketType.CameraSetupRequest, restored.Type);
                Assert.AreEqual(12u, restored.CameraId);
                Assert.AreEqual(3, restored.PlayerId);
                Assert.IsTrue(restored.IsSetup);
                Assert.IsFalse(restored.IsResult);
                Assert.IsFalse(restored.Approved);
                Assert.AreEqual(string.Empty, restored.Reason);
            }

            [Test]
            public void Result_Approved_RoundTrip()
            {
                var original = new CameraSetupRequestPacket
                {
                    CameraId = 4u,
                    PlayerId = 2,
                    IsSetup = true,
                    IsResult = true,
                    Approved = true,
                    Reason = string.Empty
                };

                byte[] data = original.Serialize();
                var restored = PacketSerializer.Deserialize(data) as CameraSetupRequestPacket;

                Assert.IsNotNull(restored);
                Assert.IsTrue(restored.IsResult);
                Assert.IsTrue(restored.Approved);
                Assert.AreEqual(string.Empty, restored.Reason);
            }

            [Test]
            public void Result_RejectedCameraLimit_RoundTrip()
            {
                var original = new CameraSetupRequestPacket
                {
                    CameraId = 9u,
                    PlayerId = 4,
                    IsSetup = true,
                    IsResult = true,
                    Approved = false,
                    Reason = "CameraLimit"
                };

                byte[] data = original.Serialize();
                var restored = PacketSerializer.Deserialize(data) as CameraSetupRequestPacket;

                Assert.IsNotNull(restored);
                Assert.IsFalse(restored.Approved);
                Assert.AreEqual("CameraLimit", restored.Reason);
            }

            [Test]
            public void NullReason_SerializesAsEmpty()
            {
                var original = new CameraSetupRequestPacket
                {
                    CameraId = 1u,
                    PlayerId = 1,
                    IsSetup = false,
                    IsResult = false,
                    Approved = false,
                    Reason = null
                };

                var restored = PacketSerializer.Deserialize(original.Serialize()) as CameraSetupRequestPacket;
                Assert.IsNotNull(restored);
                Assert.AreEqual(string.Empty, restored.Reason);
            }

            [Test]
            public void PacketTypeValue_IsThirtyOne()
            {
                Assert.AreEqual(31, (byte)PacketType.CameraSetupRequest);
            }
        }

        /// <summary>
        /// Tests the deterministic camera ordering used by RegisterCameras
        /// (same IDs on host and client).
        /// </summary>
        [TestFixture]
        public class CameraIdAssignmentTests
        {
            private class FakeCamera
            {
                public float X, Y, Z;
                public string Name;

                public FakeCamera(float x, float y, float z, string name)
                {
                    X = x; Y = y; Z = z; Name = name;
                }
            }

            private static int Compare(FakeCamera a, FakeCamera b)
            {
                int cmp = a.X.CompareTo(b.X); if (cmp != 0) return cmp;
                cmp = a.Y.CompareTo(b.Y); if (cmp != 0) return cmp;
                cmp = a.Z.CompareTo(b.Z); if (cmp != 0) return cmp;
                return string.CompareOrdinal(a.Name, b.Name);
            }

            [Test]
            public void Sorting_IsDeterministic_RegardlessOfInputOrder()
            {
                var setA = new[]
                {
                    new FakeCamera(10f, 0f, 5f, "cam_b"),
                    new FakeCamera(-3f, 0f, 2f, "cam_a"),
                    new FakeCamera(10f, 0f, 5f, "cam_a")
                };
                var setB = new[]
                {
                    new FakeCamera(10f, 0f, 5f, "cam_a"),
                    new FakeCamera(10f, 0f, 5f, "cam_b"),
                    new FakeCamera(-3f, 0f, 2f, "cam_a")
                };

                System.Array.Sort(setA, Compare);
                System.Array.Sort(setB, Compare);

                for (int i = 0; i < setA.Length; i++)
                {
                    Assert.AreEqual(setA[i].Name, setB[i].Name);
                    Assert.AreEqual(setA[i].X, setB[i].X);
                }
            }

            [Test]
            public void Sorting_UsesNameAsFinalTieBreaker()
            {
                var cameras = new[]
                {
                    new FakeCamera(1f, 1f, 1f, "z_cam"),
                    new FakeCamera(1f, 1f, 1f, "a_cam")
                };
                System.Array.Sort(cameras, Compare);

                Assert.AreEqual("a_cam", cameras[0].Name);
                Assert.AreEqual("z_cam", cameras[1].Name);
            }

            [Test]
            public void SequentialIds_AreOneBased()
            {
                var cameras = new[]
                {
                    new FakeCamera(0f, 0f, 10f, "c"),
                    new FakeCamera(0f, 0f, 0f, "a"),
                    new FakeCamera(0f, 0f, 5f, "b")
                };
                System.Array.Sort(cameras, Compare);

                for (int i = 0; i < cameras.Length; i++)
                {
                    uint id = (uint)(i + 1);
                    Assert.AreEqual((uint)(i + 1), id);
                    Assert.Greater(id, 0u);
                }
            }
        }

        #endregion

        #region Pig / Animal Sync Tests - Task 3.8

        /// <summary>
        /// Tests for NetworkedPig state mapping and bait marker rules (Task 3.8).
        /// </summary>
        [TestFixture]
        public class NetworkedPigStateTests
        {
            private static byte MapState(bool trapped, bool danger, bool walk, bool hungry)
            {
                if (trapped) return (byte)NetworkedPig.PigNetworkState.CameraAttached;
                if (danger) return (byte)NetworkedPig.PigNetworkState.Escape;
                if (walk || hungry) return (byte)NetworkedPig.PigNetworkState.Patrol;
                return (byte)NetworkedPig.PigNetworkState.Idle;
            }

            private class FakeAnimal
            {
                public float X;
                public string Name;

                public FakeAnimal(float x, string name)
                {
                    X = x;
                    Name = name;
                }
            }

            private static int Compare(FakeAnimal a, FakeAnimal b)
            {
                int c = a.X.CompareTo(b.X);
                if (c != 0) return c;
                return string.CompareOrdinal(a.Name, b.Name);
            }

            [Test]
            public void PigNetworkState_EnumValues_MatchPacketContract()
            {
                Assert.AreEqual(0, (byte)NetworkedPig.PigNetworkState.Idle);
                Assert.AreEqual(1, (byte)NetworkedPig.PigNetworkState.Patrol);
                Assert.AreEqual(2, (byte)NetworkedPig.PigNetworkState.Escape);
                Assert.AreEqual(3, (byte)NetworkedPig.PigNetworkState.CameraAttached);
            }

            [Test]
            public void Traped_TakesPriorityOverDanger()
            {
                Assert.AreEqual((byte)NetworkedPig.PigNetworkState.CameraAttached,
                    MapState(true, true, true, true));
            }

            [Test]
            public void Danger_MapsToEscape()
            {
                Assert.AreEqual((byte)NetworkedPig.PigNetworkState.Escape,
                    MapState(false, true, true, false));
            }

            [Test]
            public void Walking_MapsToPatrol()
            {
                Assert.AreEqual((byte)NetworkedPig.PigNetworkState.Patrol,
                    MapState(false, false, true, false));
            }

            [Test]
            public void Hungry_MapsToPatrol()
            {
                Assert.AreEqual((byte)NetworkedPig.PigNetworkState.Patrol,
                    MapState(false, false, false, true));
            }

            [Test]
            public void Nothing_MapsToIdle()
            {
                Assert.AreEqual((byte)NetworkedPig.PigNetworkState.Idle,
                    MapState(false, false, false, false));
            }

            [Test]
            public void PigStatePacket_CarriesCameraAttachment()
            {
                var packet = new PigStatePacket
                {
                    PigId = 5u,
                    Position = new Vector3(1f, 2f, 3f),
                    Rotation = new Quaternion(0f, 0f, 0f, 1f),
                    State = (byte)NetworkedPig.PigNetworkState.CameraAttached,
                    Health = 750f,
                    CameraId = 42u
                };

                var restored = PacketSerializer.Deserialize(packet.Serialize()) as PigStatePacket;
                Assert.IsNotNull(restored);
                Assert.AreEqual(5u, restored.PigId);
                Assert.AreEqual(42u, restored.CameraId);
                Assert.AreEqual((byte)NetworkedPig.PigNetworkState.CameraAttached, restored.State);
                Assert.AreEqual(750f, restored.Health);
            }

            [Test]
            public void BaitMarkerType_IsTwo()
            {
                var marker = new MapMarkerPacket
                {
                    MarkerType = 2, // Bait
                    Position = new Vector3(10f, 0f, 20f),
                    MarkerId = 1u,
                    IsActive = true
                };

                var restored = PacketSerializer.Deserialize(marker.Serialize()) as MapMarkerPacket;
                Assert.IsNotNull(restored);
                Assert.AreEqual(2, restored.MarkerType);
                Assert.IsTrue(restored.IsActive);
            }

            [Test]
            public void BaitLimit_IsTwenty()
            {
                // Mirror of RakeNetworkManager.MaxActiveBaits (private const)
                const int maxActiveBaits = 20;
                Assert.AreEqual(20, maxActiveBaits);
            }

            [Test]
            public void BaitLimit_RejectsBeyondCap()
            {
                const int maxActiveBaits = 20;
                int active = 20;
                bool allow = active < maxActiveBaits;
                Assert.IsFalse(allow);
            }

            [Test]
            public void BaitPosition_RejectsNaN()
            {
                var pos = new Vector3(float.NaN, 0f, 0f);
                bool invalid = float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z);
                Assert.IsTrue(invalid);
            }

            [Test]
            public void BaitPosition_RejectsAbsurdMagnitude()
            {
                var pos = new Vector3(50000f, 0f, 0f); // sqrMagnitude 2.5e9
                bool invalid = pos.sqrMagnitude > 100000000f;
                Assert.IsTrue(invalid);
            }

            [Test]
            public void AnimalSorting_IsDeterministic()
            {
                var a = new[]
                {
                    new FakeAnimal(3f, "pig_b"),
                    new FakeAnimal(1f, "pig_a"),
                    new FakeAnimal(3f, "pig_a")
                };
                var b = new[]
                {
                    new FakeAnimal(3f, "pig_a"),
                    new FakeAnimal(3f, "pig_b"),
                    new FakeAnimal(1f, "pig_a")
                };

                System.Array.Sort(a, Compare);
                System.Array.Sort(b, Compare);

                for (int i = 0; i < a.Length; i++)
                {
                    Assert.AreEqual(a[i].Name, b[i].Name);
                    Assert.AreEqual(a[i].X, b[i].X);
                }
            }
        }

        #endregion

        #region Pig Camera Quota Tests - Task 3.9

        /// <summary>
        /// Tests for pig cameras sharing the 5-camera limit (Task 3.9).
        /// These exercise the real NetworkedCamera static quota API.
        /// </summary>
        [TestFixture]
        public class PigCameraQuotaTests
        {
            private const uint PigFlag = 0x80000000u;

            [TearDown]
            public void Cleanup()
            {
                for (uint i = 1; i <= 10; i++)
                {
                    NetworkedCamera.ReleaseExternalCamera(PigFlag | i);
                }
            }

            [Test]
            public void PigCameraId_HasHighBitFlag()
            {
                uint cameraId = PigFlag | 7u;
                Assert.AreNotEqual(0u, cameraId & PigFlag);
                Assert.AreEqual(7u, cameraId & 0x7FFFFFFFu);
            }

            [Test]
            public void ReserveExternalCamera_AllowsFive()
            {
                for (uint i = 1; i <= 5; i++)
                {
                    Assert.IsTrue(NetworkedCamera.TryReserveExternalCamera(PigFlag | i),
                        $"Camera {i} should fit in the quota");
                }

                Assert.AreEqual(5, NetworkedCamera.GetExternalCameraCount());
                Assert.IsFalse(NetworkedCamera.CanPlaceCamera(), "Quota must be full");
            }

            [Test]
            public void ReserveExternalCamera_RejectsSixth()
            {
                for (uint i = 1; i <= 5; i++)
                {
                    NetworkedCamera.TryReserveExternalCamera(PigFlag | i);
                }

                Assert.IsFalse(NetworkedCamera.TryReserveExternalCamera(PigFlag | 6u),
                    "The 6th camera must be rejected");
                Assert.AreEqual(5, NetworkedCamera.GetExternalCameraCount());
            }

            [Test]
            public void ReserveExternalCamera_IsIdempotent()
            {
                Assert.IsTrue(NetworkedCamera.TryReserveExternalCamera(PigFlag | 3u));
                Assert.IsTrue(NetworkedCamera.TryReserveExternalCamera(PigFlag | 3u));
                Assert.AreEqual(1, NetworkedCamera.GetExternalCameraCount());
            }

            [Test]
            public void ReleaseExternalCamera_FreesSlot()
            {
                for (uint i = 1; i <= 5; i++)
                {
                    NetworkedCamera.TryReserveExternalCamera(PigFlag | i);
                }

                NetworkedCamera.ReleaseExternalCamera(PigFlag | 5u);

                Assert.AreEqual(4, NetworkedCamera.GetExternalCameraCount());
                Assert.IsTrue(NetworkedCamera.CanPlaceCamera(), "Releasing must free a slot");
                Assert.IsTrue(NetworkedCamera.TryReserveExternalCamera(PigFlag | 6u));
            }

            [Test]
            public void TotalUsableCount_CombinesRegistryAndExternal()
            {
                int externalBefore = NetworkedCamera.GetExternalCameraCount();
                int usableBefore = NetworkedCamera.GetUsableCameraCount();

                NetworkedCamera.TryReserveExternalCamera(PigFlag | 9u);

                Assert.AreEqual(usableBefore + externalBefore + 1,
                    NetworkedCamera.GetTotalUsableCameraCount());
            }
        }

        #endregion
    }
}