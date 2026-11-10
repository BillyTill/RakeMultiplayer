using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer.Tests
{
    /// <summary>
    /// Unit tests for network packet serialization and deserialization
    /// Covers all packet types defined in Packets.cs
    /// </summary>
    [TestFixture]
    public class PacketSerializationTests
    {
        #region Enum Value Tests

        [Test]
        public void Phase_EnumValues_MatchExpected()
        {
            Assert.AreEqual(0, (byte)Phase.Day, "Phase.Day should be 0");
            Assert.AreEqual(1, (byte)Phase.Night, "Phase.Night should be 1");
        }

        [Test]
        public void Phase_CanRoundTripViaByte()
        {
            foreach (Phase phase in Enum.GetValues(typeof(Phase)))
            {
                byte phaseByte = (byte)phase;
                Phase restored = (Phase)phaseByte;
                Assert.AreEqual(phase, restored, $"Phase {phase} should round-trip via byte");
            }
        }

        [Test]
        public void SessionState_EnumValues_MatchExpected()
        {
            Assert.AreEqual(0, (byte)SessionState.Waiting, "SessionState.Waiting should be 0");
            Assert.AreEqual(1, (byte)SessionState.Active, "SessionState.Active should be 1");
            Assert.AreEqual(2, (byte)SessionState.GameOver, "SessionState.GameOver should be 2");
            Assert.AreEqual(3, (byte)SessionState.Victory, "SessionState.Victory should be 3");
        }

        [Test]
        public void SessionState_CanRoundTripViaByte()
        {
            foreach (SessionState state in Enum.GetValues(typeof(SessionState)))
            {
                byte stateByte = (byte)state;
                SessionState restored = (SessionState)stateByte;
                Assert.AreEqual(state, restored, $"SessionState {state} should round-trip via byte");
            }
        }

        [Test]
        public void VictoryType_EnumValues_MatchExpected()
        {
            Assert.AreEqual(0, (byte)VictoryType.None, "VictoryType.None should be 0");
            Assert.AreEqual(1, (byte)VictoryType.HuntersWin, "VictoryType.HuntersWin should be 1");
            Assert.AreEqual(2, (byte)VictoryType.RakeWin, "VictoryType.RakeWin should be 2");
            Assert.AreEqual(3, (byte)VictoryType.Timeout, "VictoryType.Timeout should be 3");
        }

        [Test]
        public void VictoryType_CanRoundTripViaByte()
        {
            foreach (VictoryType vt in Enum.GetValues(typeof(VictoryType)))
            {
                byte vtByte = (byte)vt;
                VictoryType restored = (VictoryType)vtByte;
                Assert.AreEqual(vt, restored, $"VictoryType {vt} should round-trip via byte");
            }
        }

        #endregion

        #region PhaseChangePacket Tests

        [Test]
        public void PhaseChangePacket_SerializeDeserialize_RoundTrip()
        {
            var original = new PhaseChangePacket
            {
                Phase = Phase.Night,
                RemainingTime = 120.5f,
                NightNumber = 2
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as PhaseChangePacket;

            Assert.IsNotNull(restored, "Deserialized packet should not be null");
            Assert.AreEqual(original.Phase, restored.Phase, "Phase should match");
            Assert.AreEqual(original.RemainingTime, restored.RemainingTime, 0.001f, "RemainingTime should match");
            Assert.AreEqual(original.NightNumber, restored.NightNumber, "NightNumber should match");
            Assert.AreEqual(PacketType.PhaseChange, restored.Type, "PacketType should be PhaseChange");
        }

        [Test]
        public void PhaseChangePacket_DayPhase_SerializesCorrectly()
        {
            var original = new PhaseChangePacket
            {
                Phase = Phase.Day,
                RemainingTime = 300f,
                NightNumber = 0
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as PhaseChangePacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(Phase.Day, restored.Phase);
            Assert.AreEqual(300f, restored.RemainingTime, 0.001f);
            Assert.AreEqual(0, restored.NightNumber);
        }

        [Test]
        public void PhaseChangePacket_ZeroValues_SerializeCorrectly()
        {
            var original = new PhaseChangePacket
            {
                Phase = Phase.Day,
                RemainingTime = 0f,
                NightNumber = 0
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as PhaseChangePacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(Phase.Day, restored.Phase);
            Assert.AreEqual(0f, restored.RemainingTime, 0.001f);
            Assert.AreEqual(0, restored.NightNumber);
        }

        [Test]
        public void PhaseChangePacket_MaxValues_SerializeCorrectly()
        {
            var original = new PhaseChangePacket
            {
                Phase = Phase.Night,
                RemainingTime = float.MaxValue,
                NightNumber = int.MaxValue
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as PhaseChangePacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(Phase.Night, restored.Phase);
            Assert.AreEqual(float.MaxValue, restored.RemainingTime);
            Assert.AreEqual(int.MaxValue, restored.NightNumber);
        }

        [Test]
        public void PhaseChangePacket_NegativeRemainingTime_SerializesCorrectly()
        {
            var original = new PhaseChangePacket
            {
                Phase = Phase.Night,
                RemainingTime = -10.5f,
                NightNumber = 1
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as PhaseChangePacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(-10.5f, restored.RemainingTime, 0.001f);
        }

        #endregion

        #region SessionStatePacket Tests

        [Test]
        public void SessionStatePacket_SerializeDeserialize_RoundTrip()
        {
            var original = new SessionStatePacket
            {
                CurrentNight = 2,
                State = SessionState.Active,
                RakeHealth = 75.5f,
                VictoryType = VictoryType.None
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as SessionStatePacket;

            Assert.IsNotNull(restored, "Deserialized packet should not be null");
            Assert.AreEqual(original.CurrentNight, restored.CurrentNight, "CurrentNight should match");
            Assert.AreEqual(original.State, restored.State, "State should match");
            Assert.AreEqual(original.RakeHealth, restored.RakeHealth, 0.001f, "RakeHealth should match");
            Assert.AreEqual(original.VictoryType, restored.VictoryType, "VictoryType should match");
            Assert.AreEqual(PacketType.SessionState, restored.Type, "PacketType should be SessionState");
        }

        [Test]
        public void SessionStatePacket_AllStates_SerializeCorrectly()
        {
            foreach (SessionState state in Enum.GetValues(typeof(SessionState)))
            {
                foreach (VictoryType vt in Enum.GetValues(typeof(VictoryType)))
                {
                    var original = new SessionStatePacket
                    {
                        CurrentNight = 1,
                        State = state,
                        RakeHealth = 100f,
                        VictoryType = vt
                    };

                    byte[] data = original.Serialize();
                    var restored = PacketSerializer.Deserialize(data) as SessionStatePacket;

                    Assert.IsNotNull(restored, $"State {state}, VictoryType {vt} should deserialize");
                    Assert.AreEqual(state, restored.State, $"State {state} should round-trip");
                    Assert.AreEqual(vt, restored.VictoryType, $"VictoryType {vt} should round-trip");
                }
            }
        }

        [Test]
        public void SessionStatePacket_ZeroValues_SerializeCorrectly()
        {
            var original = new SessionStatePacket
            {
                CurrentNight = 0,
                State = SessionState.Waiting,
                RakeHealth = 0f,
                VictoryType = VictoryType.None
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as SessionStatePacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(0, restored.CurrentNight);
            Assert.AreEqual(SessionState.Waiting, restored.State);
            Assert.AreEqual(0f, restored.RakeHealth, 0.001f);
            Assert.AreEqual(VictoryType.None, restored.VictoryType);
        }

        [Test]
        public void SessionStatePacket_MaxValues_SerializeCorrectly()
        {
            var original = new SessionStatePacket
            {
                CurrentNight = int.MaxValue,
                State = SessionState.Victory,
                RakeHealth = float.MaxValue,
                VictoryType = VictoryType.Timeout
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as SessionStatePacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(int.MaxValue, restored.CurrentNight);
            Assert.AreEqual(SessionState.Victory, restored.State);
            Assert.AreEqual(float.MaxValue, restored.RakeHealth);
            Assert.AreEqual(VictoryType.Timeout, restored.VictoryType);
        }

        [Test]
        public void SessionStatePacket_NegativeRakeHealth_SerializesCorrectly()
        {
            var original = new SessionStatePacket
            {
                CurrentNight = 1,
                State = SessionState.Active,
                RakeHealth = -50f,
                VictoryType = VictoryType.None
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as SessionStatePacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(-50f, restored.RakeHealth, 0.001f);
        }

        #endregion

        #region PlayerStatePacket Tests

        [Test]
        public void PlayerStatePacket_SerializeDeserialize_RoundTrip()
        {
            var original = new PlayerStatePacket
            {
                PlayerId = 42,
                Position = new Vector3(10.5f, 5.25f, -20.75f),
                Rotation = new Quaternion(0.1f, 0.2f, 0.3f, 0.95f),
                AnimState = 3
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as PlayerStatePacket;

            Assert.IsNotNull(restored, "Deserialized packet should not be null");
            Assert.AreEqual(original.PlayerId, restored.PlayerId, "PlayerId should match");
            Assert.AreEqual(original.Position, restored.Position, "Position should match");
            Assert.AreEqual(original.Rotation, restored.Rotation, "Rotation should match");
            Assert.AreEqual(original.AnimState, restored.AnimState, "AnimState should match");
            Assert.AreEqual(PacketType.PlayerState, restored.Type, "PacketType should be PlayerState");
        }

        [Test]
        public void PlayerStatePacket_ZeroValues_SerializeCorrectly()
        {
            var original = new PlayerStatePacket
            {
                PlayerId = 0,
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                AnimState = 0
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as PlayerStatePacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(0, restored.PlayerId);
            Assert.AreEqual(Vector3.zero, restored.Position);
            Assert.AreEqual(Quaternion.identity, restored.Rotation);
            Assert.AreEqual(0, restored.AnimState);
        }

        [Test]
        public void PlayerStatePacket_MaxValues_SerializeCorrectly()
        {
            var original = new PlayerStatePacket
            {
                PlayerId = int.MaxValue,
                Position = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue),
                Rotation = new Quaternion(1, 1, 1, 1),
                AnimState = byte.MaxValue
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as PlayerStatePacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(int.MaxValue, restored.PlayerId);
            Assert.AreEqual(original.Position, restored.Position);
            Assert.AreEqual(original.Rotation, restored.Rotation);
            Assert.AreEqual(byte.MaxValue, restored.AnimState);
        }

        [Test]
        public void PlayerStatePacket_NegativeValues_SerializeCorrectly()
        {
            var original = new PlayerStatePacket
            {
                PlayerId = -1,
                Position = new Vector3(-100f, -50f, -200f),
                Rotation = new Quaternion(-0.5f, -0.5f, -0.5f, -0.5f),
                AnimState = 255
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as PlayerStatePacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(-1, restored.PlayerId);
            Assert.AreEqual(original.Position, restored.Position);
            Assert.AreEqual(original.Rotation, restored.Rotation);
            Assert.AreEqual(255, restored.AnimState);
        }

        #endregion

        #region RakeStatePacket Tests

        [Test]
        public void RakeStatePacket_SerializeDeserialize_RoundTrip()
        {
            var original = new RakeStatePacket
            {
                Position = new Vector3(10.5f, 5.25f, -20.75f),
                Rotation = new Quaternion(0.1f, 0.2f, 0.3f, 0.95f),
                State = 5,
                Health = 75.5f
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as RakeStatePacket;

            Assert.IsNotNull(restored, "Deserialized packet should not be null");
            Assert.AreEqual(original.Position, restored.Position, "Position should match");
            Assert.AreEqual(original.Rotation, restored.Rotation, "Rotation should match");
            Assert.AreEqual(original.State, restored.State, "State should match");
            Assert.AreEqual(original.Health, restored.Health, 0.001f, "Health should match");
            Assert.AreEqual(PacketType.RakeState, restored.Type, "PacketType should be RakeState");
        }

        [Test]
        public void RakeStatePacket_AllByteStates_SerializeCorrectly()
        {
            for (int state = 0; state <= 255; state += 17) // Sample values
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
            var original = new RakeStatePacket
            {
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                State = 0,
                Health = 0f
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as RakeStatePacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(Vector3.zero, restored.Position);
            Assert.AreEqual(Quaternion.identity, restored.Rotation);
            Assert.AreEqual(0, restored.State);
            Assert.AreEqual(0f, restored.Health, 0.001f);
        }

        [Test]
        public void RakeStatePacket_MaxValues_SerializeCorrectly()
        {
            var original = new RakeStatePacket
            {
                Position = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue),
                Rotation = new Quaternion(1, 1, 1, 1),
                State = byte.MaxValue,
                Health = float.MaxValue
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as RakeStatePacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(original.Position, restored.Position);
            Assert.AreEqual(byte.MaxValue, restored.State);
            Assert.AreEqual(float.MaxValue, restored.Health);
        }

        #endregion

        #region PlayerConnectedPacket Tests

        [Test]
        public void PlayerConnectedPacket_SerializeDeserialize_RoundTrip()
        {
            var original = new PlayerConnectedPacket
            {
                PlayerId = 7,
                SpawnPosition = new Vector3(100f, 10f, 50f),
                IsYou = true
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as PlayerConnectedPacket;

            Assert.IsNotNull(restored, "Deserialized packet should not be null");
            Assert.AreEqual(original.PlayerId, restored.PlayerId, "PlayerId should match");
            Assert.AreEqual(original.SpawnPosition, restored.SpawnPosition, "SpawnPosition should match");
            Assert.AreEqual(original.IsYou, restored.IsYou, "IsYou should match");
            Assert.AreEqual(PacketType.PlayerConnected, restored.Type, "PacketType should be PlayerConnected");
        }

        [Test]
        public void PlayerConnectedPacket_IsYouFalse_SerializesCorrectly()
        {
            var original = new PlayerConnectedPacket
            {
                PlayerId = 3,
                SpawnPosition = Vector3.zero,
                IsYou = false
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as PlayerConnectedPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(false, restored.IsYou);
        }

        [Test]
        public void PlayerConnectedPacket_ZeroValues_SerializeCorrectly()
        {
            var original = new PlayerConnectedPacket
            {
                PlayerId = 0,
                SpawnPosition = Vector3.zero,
                IsYou = false
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as PlayerConnectedPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(0, restored.PlayerId);
            Assert.AreEqual(Vector3.zero, restored.SpawnPosition);
            Assert.AreEqual(false, restored.IsYou);
        }

        [Test]
        public void PlayerConnectedPacket_NegativePlayerId_SerializesCorrectly()
        {
            var original = new PlayerConnectedPacket
            {
                PlayerId = -5,
                SpawnPosition = new Vector3(-10f, 0f, -20f),
                IsYou = true
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as PlayerConnectedPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(-5, restored.PlayerId);
            Assert.AreEqual(original.SpawnPosition, restored.SpawnPosition);
            Assert.AreEqual(true, restored.IsYou);
        }

        #endregion

        #region PlayerDisconnectedPacket Tests

        [Test]
        public void PlayerDisconnectedPacket_SerializeDeserialize_RoundTrip()
        {
            var original = new PlayerDisconnectedPacket
            {
                PlayerId = 12
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as PlayerDisconnectedPacket;

            Assert.IsNotNull(restored, "Deserialized packet should not be null");
            Assert.AreEqual(original.PlayerId, restored.PlayerId, "PlayerId should match");
            Assert.AreEqual(PacketType.PlayerDisconnected, restored.Type, "PacketType should be PlayerDisconnected");
        }

        [Test]
        public void PlayerDisconnectedPacket_ZeroPlayerId_SerializesCorrectly()
        {
            var original = new PlayerDisconnectedPacket
            {
                PlayerId = 0
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as PlayerDisconnectedPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(0, restored.PlayerId);
        }

        [Test]
        public void PlayerDisconnectedPacket_NegativePlayerId_SerializesCorrectly()
        {
            var original = new PlayerDisconnectedPacket
            {
                PlayerId = -1
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as PlayerDisconnectedPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(-1, restored.PlayerId);
        }

        #endregion

        #region DamageRequestPacket Tests

        [Test]
        public void DamageRequestPacket_SerializeDeserialize_RoundTrip()
        {
            var original = new DamageRequestPacket
            {
                ShooterId = 1,
                TargetId = 2,
                Amount = 25,
                DamageType = "Bullet",
                ShooterPosition = new Vector3(10f, 5f, 20f)
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as DamageRequestPacket;

            Assert.IsNotNull(restored, "Deserialized packet should not be null");
            Assert.AreEqual(original.ShooterId, restored.ShooterId, "ShooterId should match");
            Assert.AreEqual(original.TargetId, restored.TargetId, "TargetId should match");
            Assert.AreEqual(original.Amount, restored.Amount, "Amount should match");
            Assert.AreEqual(original.DamageType, restored.DamageType, "DamageType should match");
            Assert.AreEqual(original.ShooterPosition, restored.ShooterPosition, "ShooterPosition should match");
            Assert.AreEqual(PacketType.DamageRequest, restored.Type, "PacketType should be DamageRequest");
        }

        [Test]
        public void DamageRequestPacket_NullDamageType_SerializesAsEmptyString()
        {
            var original = new DamageRequestPacket
            {
                ShooterId = 1,
                TargetId = 2,
                Amount = 10,
                DamageType = null,
                ShooterPosition = Vector3.zero
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as DamageRequestPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(string.Empty, restored.DamageType, "Null DamageType should serialize as empty string");
        }

        [Test]
        public void DamageRequestPacket_EmptyDamageType_SerializesCorrectly()
        {
            var original = new DamageRequestPacket
            {
                ShooterId = 1,
                TargetId = 2,
                Amount = 10,
                DamageType = "",
                ShooterPosition = Vector3.zero
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as DamageRequestPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(string.Empty, restored.DamageType);
        }

        [Test]
        public void DamageRequestPacket_UnicodeDamageType_SerializesCorrectly()
        {
            var original = new DamageRequestPacket
            {
                ShooterId = 1,
                TargetId = 2,
                Amount = 10,
                DamageType = "🔥Fire💥",
                ShooterPosition = Vector3.zero
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as DamageRequestPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual("🔥Fire💥", restored.DamageType);
        }

        [Test]
        public void DamageRequestPacket_LongDamageType_SerializesCorrectly()
        {
            var longType = new string('A', 1000);
            var original = new DamageRequestPacket
            {
                ShooterId = 1,
                TargetId = 2,
                Amount = 10,
                DamageType = longType,
                ShooterPosition = Vector3.zero
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as DamageRequestPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(longType, restored.DamageType);
        }

        [Test]
        public void DamageRequestPacket_ZeroValues_SerializeCorrectly()
        {
            var original = new DamageRequestPacket
            {
                ShooterId = 0,
                TargetId = 0,
                Amount = 0,
                DamageType = "",
                ShooterPosition = Vector3.zero
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as DamageRequestPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(0, restored.ShooterId);
            Assert.AreEqual(0, restored.TargetId);
            Assert.AreEqual(0, restored.Amount);
            Assert.AreEqual(string.Empty, restored.DamageType);
            Assert.AreEqual(Vector3.zero, restored.ShooterPosition);
        }

        [Test]
        public void DamageRequestPacket_NegativeAmount_SerializesCorrectly()
        {
            var original = new DamageRequestPacket
            {
                ShooterId = 1,
                TargetId = 2,
                Amount = -50,
                DamageType = "Heal",
                ShooterPosition = Vector3.zero
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as DamageRequestPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(-50, restored.Amount);
        }

        #endregion

        #region DamageAppliedPacket Tests

        [Test]
        public void DamageAppliedPacket_SerializeDeserialize_RoundTrip()
        {
            var original = new DamageAppliedPacket
            {
                TargetId = 5,
                NewHealth = 75.5f
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as DamageAppliedPacket;

            Assert.IsNotNull(restored, "Deserialized packet should not be null");
            Assert.AreEqual(original.TargetId, restored.TargetId, "TargetId should match");
            Assert.AreEqual(original.NewHealth, restored.NewHealth, 0.001f, "NewHealth should match");
            Assert.AreEqual(PacketType.DamageApplied, restored.Type, "PacketType should be DamageApplied");
        }

        [Test]
        public void DamageAppliedPacket_ZeroValues_SerializeCorrectly()
        {
            var original = new DamageAppliedPacket
            {
                TargetId = 0,
                NewHealth = 0f
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as DamageAppliedPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(0, restored.TargetId);
            Assert.AreEqual(0f, restored.NewHealth, 0.001f);
        }

        [Test]
        public void DamageAppliedPacket_NegativeHealth_SerializesCorrectly()
        {
            var original = new DamageAppliedPacket
            {
                TargetId = 1,
                NewHealth = -100f
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as DamageAppliedPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(-100f, restored.NewHealth, 0.001f);
        }

        [Test]
        public void DamageAppliedPacket_MaxValues_SerializeCorrectly()
        {
            var original = new DamageAppliedPacket
            {
                TargetId = int.MaxValue,
                NewHealth = float.MaxValue
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as DamageAppliedPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(int.MaxValue, restored.TargetId);
            Assert.AreEqual(float.MaxValue, restored.NewHealth);
        }

        #endregion

        #region FireVisualsPacket Tests

        [Test]
        public void FireVisualsPacket_SerializeDeserialize_RoundTrip()
        {
            var original = new FireVisualsPacket
            {
                ShooterId = 3,
                WeaponType = 2,
                Origin = new Vector3(10f, 2f, 5f),
                Direction = new Vector3(0f, 0f, 1f)
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as FireVisualsPacket;

            Assert.IsNotNull(restored, "Deserialized packet should not be null");
            Assert.AreEqual(original.ShooterId, restored.ShooterId, "ShooterId should match");
            Assert.AreEqual(original.WeaponType, restored.WeaponType, "WeaponType should match");
            Assert.AreEqual(original.Origin, restored.Origin, "Origin should match");
            Assert.AreEqual(original.Direction, restored.Direction, "Direction should match");
            Assert.AreEqual(PacketType.FireVisuals, restored.Type, "PacketType should be FireVisuals");
        }

        [Test]
        public void FireVisualsPacket_AllWeaponTypes_SerializeCorrectly()
        {
            for (int weaponType = 0; weaponType <= 255; weaponType += 17)
            {
                var original = new FireVisualsPacket
                {
                    ShooterId = 1,
                    WeaponType = (byte)weaponType,
                    Origin = Vector3.zero,
                    Direction = Vector3.forward
                };

                byte[] data = original.Serialize();
                var restored = PacketSerializer.Deserialize(data) as FireVisualsPacket;

                Assert.IsNotNull(restored, $"WeaponType {weaponType} should deserialize");
                Assert.AreEqual((byte)weaponType, restored.WeaponType, $"WeaponType {weaponType} should round-trip");
            }
        }

        [Test]
        public void FireVisualsPacket_ZeroValues_SerializeCorrectly()
        {
            var original = new FireVisualsPacket
            {
                ShooterId = 0,
                WeaponType = 0,
                Origin = Vector3.zero,
                Direction = Vector3.zero
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as FireVisualsPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(0, restored.ShooterId);
            Assert.AreEqual(0, restored.WeaponType);
            Assert.AreEqual(Vector3.zero, restored.Origin);
            Assert.AreEqual(Vector3.zero, restored.Direction);
        }

        [Test]
        public void FireVisualsPacket_NormalizedDirection_SerializesCorrectly()
        {
            var original = new FireVisualsPacket
            {
                ShooterId = 1,
                WeaponType = 1,
                Origin = Vector3.zero,
                Direction = new Vector3(1f, 2f, 3f).normalized
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as FireVisualsPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(original.Direction, restored.Direction);
        }

        #endregion

        #region ItemPickupPacket Tests

        [Test]
        public void ItemPickupPacket_SerializeDeserialize_RoundTrip()
        {
            var original = new ItemPickupPacket
            {
                PlayerId = 2,
                ItemNetworkId = 100,
                ItemType = "Medkit"
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as ItemPickupPacket;

            Assert.IsNotNull(restored, "Deserialized packet should not be null");
            Assert.AreEqual(original.PlayerId, restored.PlayerId, "PlayerId should match");
            Assert.AreEqual(original.ItemNetworkId, restored.ItemNetworkId, "ItemNetworkId should match");
            Assert.AreEqual(original.ItemType, restored.ItemType, "ItemType should match");
            Assert.AreEqual(PacketType.ItemPickup, restored.Type, "PacketType should be ItemPickup");
        }

        [Test]
        public void ItemPickupPacket_NullItemType_SerializesAsEmptyString()
        {
            var original = new ItemPickupPacket
            {
                PlayerId = 1,
                ItemNetworkId = 10,
                ItemType = null
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as ItemPickupPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(string.Empty, restored.ItemType, "Null ItemType should serialize as empty string");
        }

        [Test]
        public void ItemPickupPacket_VariousItemTypes_SerializeCorrectly()
        {
            string[] itemTypes = { "Medkit", "Ammo", "Key", "Flashlight", "Battery", "Trap", "Bait", "Camera" };

            foreach (var itemType in itemTypes)
            {
                var original = new ItemPickupPacket
                {
                    PlayerId = 1,
                    ItemNetworkId = 5,
                    ItemType = itemType
                };

                byte[] data = original.Serialize();
                var restored = PacketSerializer.Deserialize(data) as ItemPickupPacket;

                Assert.IsNotNull(restored, $"ItemType '{itemType}' should deserialize");
                Assert.AreEqual(itemType, restored.ItemType, $"ItemType '{itemType}' should round-trip");
            }
        }

        [Test]
        public void ItemPickupPacket_ZeroValues_SerializeCorrectly()
        {
            var original = new ItemPickupPacket
            {
                PlayerId = 0,
                ItemNetworkId = 0,
                ItemType = ""
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as ItemPickupPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(0, restored.PlayerId);
            Assert.AreEqual(0, restored.ItemNetworkId);
            Assert.AreEqual(string.Empty, restored.ItemType);
        }

        #endregion

        #region ItemDestroyedPacket Tests

        [Test]
        public void ItemDestroyedPacket_SerializeDeserialize_RoundTrip()
        {
            var original = new ItemDestroyedPacket
            {
                ItemNetworkId = 50,
                PickerId = 3
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as ItemDestroyedPacket;

            Assert.IsNotNull(restored, "Deserialized packet should not be null");
            Assert.AreEqual(original.ItemNetworkId, restored.ItemNetworkId, "ItemNetworkId should match");
            Assert.AreEqual(original.PickerId, restored.PickerId, "PickerId should match");
            Assert.AreEqual(PacketType.ItemDestroyed, restored.Type, "PacketType should be ItemDestroyed");
        }

        [Test]
        public void ItemDestroyedPacket_ZeroValues_SerializeCorrectly()
        {
            var original = new ItemDestroyedPacket
            {
                ItemNetworkId = 0,
                PickerId = 0
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as ItemDestroyedPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(0, restored.ItemNetworkId);
            Assert.AreEqual(0, restored.PickerId);
        }

        [Test]
        public void ItemDestroyedPacket_NegativeValues_SerializeCorrectly()
        {
            var original = new ItemDestroyedPacket
            {
                ItemNetworkId = -1,
                PickerId = -1
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as ItemDestroyedPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(-1, restored.ItemNetworkId);
            Assert.AreEqual(-1, restored.PickerId);
        }

        #endregion

        #region CameraSyncPacket Tests

        [Test]
        public void CameraSyncPacket_SerializeDeserialize_RoundTrip()
        {
            var original = new CameraSyncPacket
            {
                CameraId = 42u,
                Position = new Vector3(100f, 10f, 50f),
                Rotation = new Quaternion(0f, 0.707f, 0f, 0.707f),
                IsActive = true,
                IsBroken = false,
                BatteryLevel = 75.5f,
                PlacedByPlayerId = 2
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as CameraSyncPacket;

            Assert.IsNotNull(restored, "Deserialized packet should not be null");
            Assert.AreEqual(original.CameraId, restored.CameraId, "CameraId should match");
            Assert.AreEqual(original.Position, restored.Position, "Position should match");
            Assert.AreEqual(original.Rotation, restored.Rotation, "Rotation should match");
            Assert.AreEqual(original.IsActive, restored.IsActive, "IsActive should match");
            Assert.AreEqual(original.IsBroken, restored.IsBroken, "IsBroken should match");
            Assert.AreEqual(original.BatteryLevel, restored.BatteryLevel, 0.001f, "BatteryLevel should match");
            Assert.AreEqual(original.PlacedByPlayerId, restored.PlacedByPlayerId, "PlacedByPlayerId should match");
            Assert.AreEqual(PacketType.CameraSync, restored.Type, "PacketType should be CameraSync");
        }

        [Test]
        public void CameraSyncPacket_InactiveAndBroken_SerializesCorrectly()
        {
            var original = new CameraSyncPacket
            {
                CameraId = 1u,
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                IsActive = false,
                IsBroken = true,
                BatteryLevel = 0f,
                PlacedByPlayerId = 0
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as CameraSyncPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(false, restored.IsActive);
            Assert.AreEqual(true, restored.IsBroken);
            Assert.AreEqual(0f, restored.BatteryLevel, 0.001f);
        }

        [Test]
        public void CameraSyncPacket_ZeroValues_SerializeCorrectly()
        {
            var original = new CameraSyncPacket
            {
                CameraId = 0u,
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                IsActive = false,
                IsBroken = false,
                BatteryLevel = 0f,
                PlacedByPlayerId = 0
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as CameraSyncPacket;

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
            var original = new CameraSyncPacket
            {
                CameraId = uint.MaxValue,
                Position = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue),
                Rotation = new Quaternion(1, 1, 1, 1),
                IsActive = true,
                IsBroken = true,
                BatteryLevel = float.MaxValue,
                PlacedByPlayerId = int.MaxValue
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as CameraSyncPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(uint.MaxValue, restored.CameraId);
            Assert.AreEqual(original.Position, restored.Position);
            Assert.AreEqual(true, restored.IsActive);
            Assert.AreEqual(true, restored.IsBroken);
            Assert.AreEqual(float.MaxValue, restored.BatteryLevel);
            Assert.AreEqual(int.MaxValue, restored.PlacedByPlayerId);
        }

        #endregion

        #region CameraSpawnPacket Tests

        [Test]
        public void CameraSpawnPacket_SerializeDeserialize_RoundTrip()
        {
            var original = new CameraSpawnPacket
            {
                CameraId = 10u,
                Position = new Vector3(50f, 5f, 25f),
                Rotation = new Quaternion(0f, 0.382683f, 0f, 0.92388f), // 45 degrees around Y axis
                PlacedByPlayerId = 1
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as CameraSpawnPacket;

            Assert.IsNotNull(restored, "Deserialized packet should not be null");
            Assert.AreEqual(original.CameraId, restored.CameraId, "CameraId should match");
            Assert.AreEqual(original.Position, restored.Position, "Position should match");
            Assert.AreEqual(original.Rotation, restored.Rotation, "Rotation should match");
            Assert.AreEqual(original.PlacedByPlayerId, restored.PlacedByPlayerId, "PlacedByPlayerId should match");
            Assert.AreEqual(PacketType.CameraSpawn, restored.Type, "PacketType should be CameraSpawn");
        }

        [Test]
        public void CameraSpawnPacket_ZeroValues_SerializeCorrectly()
        {
            var original = new CameraSpawnPacket
            {
                CameraId = 0u,
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                PlacedByPlayerId = 0
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as CameraSpawnPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(0u, restored.CameraId);
            Assert.AreEqual(Vector3.zero, restored.Position);
            Assert.AreEqual(Quaternion.identity, restored.Rotation);
            Assert.AreEqual(0, restored.PlacedByPlayerId);
        }

        #endregion

        #region CameraDestroyPacket Tests

        [Test]
        public void CameraDestroyPacket_SerializeDeserialize_RoundTrip()
        {
            var original = new CameraDestroyPacket
            {
                CameraId = 7u
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as CameraDestroyPacket;

            Assert.IsNotNull(restored, "Deserialized packet should not be null");
            Assert.AreEqual(original.CameraId, restored.CameraId, "CameraId should match");
            Assert.AreEqual(PacketType.CameraDestroy, restored.Type, "PacketType should be CameraDestroy");
        }

        [Test]
        public void CameraDestroyPacket_ZeroCameraId_SerializesCorrectly()
        {
            var original = new CameraDestroyPacket
            {
                CameraId = 0u
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as CameraDestroyPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(0u, restored.CameraId);
        }

        [Test]
        public void CameraDestroyPacket_MaxCameraId_SerializesCorrectly()
        {
            var original = new CameraDestroyPacket
            {
                CameraId = uint.MaxValue
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as CameraDestroyPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(uint.MaxValue, restored.CameraId);
        }

        #endregion

        #region QuestTriggerPacket Tests

        [Test]
        public void QuestTriggerPacket_SerializeDeserialize_RoundTrip()
        {
            var original = new QuestTriggerPacket
            {
                TriggerType = 1, // BaitTaken
                Position = new Vector3(100f, 2f, 50f),
                CameraId = 5u
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as QuestTriggerPacket;

            Assert.IsNotNull(restored, "Deserialized packet should not be null");
            Assert.AreEqual(original.TriggerType, restored.TriggerType, "TriggerType should match");
            Assert.AreEqual(original.Position, restored.Position, "Position should match");
            Assert.AreEqual(original.CameraId, restored.CameraId, "CameraId should match");
            Assert.AreEqual(PacketType.QuestTrigger, restored.Type, "PacketType should be QuestTrigger");
        }

        [Test]
        public void QuestTriggerPacket_AllTriggerTypes_SerializeCorrectly()
        {
            for (byte triggerType = 0; triggerType <= 2; triggerType++)
            {
                var original = new QuestTriggerPacket
                {
                    TriggerType = triggerType,
                    Position = Vector3.zero,
                    CameraId = 0u
                };

                byte[] data = original.Serialize();
                var restored = PacketSerializer.Deserialize(data) as QuestTriggerPacket;

                Assert.IsNotNull(restored, $"TriggerType {triggerType} should deserialize");
                Assert.AreEqual(triggerType, restored.TriggerType, $"TriggerType {triggerType} should round-trip");
            }
        }

        [Test]
        public void QuestTriggerPacket_ZeroValues_SerializeCorrectly()
        {
            var original = new QuestTriggerPacket
            {
                TriggerType = 0,
                Position = Vector3.zero,
                CameraId = 0u
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as QuestTriggerPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(0, restored.TriggerType);
            Assert.AreEqual(Vector3.zero, restored.Position);
            Assert.AreEqual(0u, restored.CameraId);
        }

        #endregion

        #region PigStatePacket Tests

        [Test]
        public void PigStatePacket_SerializeDeserialize_RoundTrip()
        {
            var original = new PigStatePacket
            {
                PigId = 3u,
                Position = new Vector3(20f, 1f, 30f),
                Rotation = new Quaternion(0f, 0.707107f, 0f, 0.707107f), // 90 degrees around Y axis
                State = 2, // Escape
                Health = 50f,
                CameraId = 7u
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as PigStatePacket;

            Assert.IsNotNull(restored, "Deserialized packet should not be null");
            Assert.AreEqual(original.PigId, restored.PigId, "PigId should match");
            Assert.AreEqual(original.Position, restored.Position, "Position should match");
            Assert.AreEqual(original.Rotation, restored.Rotation, "Rotation should match");
            Assert.AreEqual(original.State, restored.State, "State should match");
            Assert.AreEqual(original.Health, restored.Health, 0.001f, "Health should match");
            Assert.AreEqual(original.CameraId, restored.CameraId, "CameraId should match");
            Assert.AreEqual(PacketType.PigState, restored.Type, "PacketType should be PigState");
        }

        [Test]
        public void PigStatePacket_AllStates_SerializeCorrectly()
        {
            for (byte state = 0; state <= 3; state++)
            {
                var original = new PigStatePacket
                {
                    PigId = 1u,
                    Position = Vector3.zero,
                    Rotation = Quaternion.identity,
                    State = state,
                    Health = 100f,
                    CameraId = 0u
                };

                byte[] data = original.Serialize();
                var restored = PacketSerializer.Deserialize(data) as PigStatePacket;

                Assert.IsNotNull(restored, $"State {state} should deserialize");
                Assert.AreEqual(state, restored.State, $"State {state} should round-trip");
            }
        }

        [Test]
        public void PigStatePacket_ZeroValues_SerializeCorrectly()
        {
            var original = new PigStatePacket
            {
                PigId = 0u,
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                State = 0,
                Health = 0f,
                CameraId = 0u
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as PigStatePacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(0u, restored.PigId);
            Assert.AreEqual(Vector3.zero, restored.Position);
            Assert.AreEqual(Quaternion.identity, restored.Rotation);
            Assert.AreEqual(0, restored.State);
            Assert.AreEqual(0f, restored.Health, 0.001f);
            Assert.AreEqual(0u, restored.CameraId);
        }

        [Test]
        public void PigStatePacket_MaxValues_SerializeCorrectly()
        {
            var original = new PigStatePacket
            {
                PigId = uint.MaxValue,
                Position = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue),
                Rotation = new Quaternion(1, 1, 1, 1),
                State = byte.MaxValue,
                Health = float.MaxValue,
                CameraId = uint.MaxValue
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as PigStatePacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(uint.MaxValue, restored.PigId);
            Assert.AreEqual(original.Position, restored.Position);
            Assert.AreEqual(byte.MaxValue, restored.State);
            Assert.AreEqual(float.MaxValue, restored.Health);
            Assert.AreEqual(uint.MaxValue, restored.CameraId);
        }

        #endregion

        #region MapMarkerPacket Tests

        [Test]
        public void MapMarkerPacket_SerializeDeserialize_RoundTrip()
        {
            var original = new MapMarkerPacket
            {
                MarkerType = 2, // Bait
                Position = new Vector3(75f, 5f, 25f),
                MarkerId = 15u,
                IsActive = true
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as MapMarkerPacket;

            Assert.IsNotNull(restored, "Deserialized packet should not be null");
            Assert.AreEqual(original.MarkerType, restored.MarkerType, "MarkerType should match");
            Assert.AreEqual(original.Position, restored.Position, "Position should match");
            Assert.AreEqual(original.MarkerId, restored.MarkerId, "MarkerId should match");
            Assert.AreEqual(original.IsActive, restored.IsActive, "IsActive should match");
            Assert.AreEqual(PacketType.MapMarker, restored.Type, "PacketType should be MapMarker");
        }

        [Test]
        public void MapMarkerPacket_AllMarkerTypes_SerializeCorrectly()
        {
            for (byte markerType = 0; markerType <= 4; markerType++)
            {
                var original = new MapMarkerPacket
                {
                    MarkerType = markerType,
                    Position = Vector3.zero,
                    MarkerId = 0u,
                    IsActive = true
                };

                byte[] data = original.Serialize();
                var restored = PacketSerializer.Deserialize(data) as MapMarkerPacket;

                Assert.IsNotNull(restored, $"MarkerType {markerType} should deserialize");
                Assert.AreEqual(markerType, restored.MarkerType, $"MarkerType {markerType} should round-trip");
            }
        }

        [Test]
        public void MapMarkerPacket_Inactive_SerializesCorrectly()
        {
            var original = new MapMarkerPacket
            {
                MarkerType = 0,
                Position = Vector3.zero,
                MarkerId = 0u,
                IsActive = false
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as MapMarkerPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(false, restored.IsActive);
        }

        [Test]
        public void MapMarkerPacket_ZeroValues_SerializeCorrectly()
        {
            var original = new MapMarkerPacket
            {
                MarkerType = 0,
                Position = Vector3.zero,
                MarkerId = 0u,
                IsActive = false
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as MapMarkerPacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(0, restored.MarkerType);
            Assert.AreEqual(Vector3.zero, restored.Position);
            Assert.AreEqual(0u, restored.MarkerId);
            Assert.AreEqual(false, restored.IsActive);
        }

        #endregion

        #region TrapStatePacket Tests

        [Test]
        public void TrapStatePacket_SerializeDeserialize_RoundTrip()
        {
            var original = new TrapStatePacket
            {
                TrapId = 8u,
                Position = new Vector3(30f, 1f, 40f),
                IsArmed = true,
                TriggerRadius = 3.5f,
                IsTriggered = false
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as TrapStatePacket;

            Assert.IsNotNull(restored, "Deserialized packet should not be null");
            Assert.AreEqual(original.TrapId, restored.TrapId, "TrapId should match");
            Assert.AreEqual(original.Position, restored.Position, "Position should match");
            Assert.AreEqual(original.IsArmed, restored.IsArmed, "IsArmed should match");
            Assert.AreEqual(original.TriggerRadius, restored.TriggerRadius, 0.001f, "TriggerRadius should match");
            Assert.AreEqual(original.IsTriggered, restored.IsTriggered, "IsTriggered should match");
            Assert.AreEqual(PacketType.TrapState, restored.Type, "PacketType should be TrapState");
        }

        [Test]
        public void TrapStatePacket_Triggered_SerializesCorrectly()
        {
            var original = new TrapStatePacket
            {
                TrapId = 1u,
                Position = Vector3.zero,
                IsArmed = false,
                TriggerRadius = 0f,
                IsTriggered = true
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as TrapStatePacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(false, restored.IsArmed);
            Assert.AreEqual(true, restored.IsTriggered);
        }

        [Test]
        public void TrapStatePacket_ZeroValues_SerializeCorrectly()
        {
            var original = new TrapStatePacket
            {
                TrapId = 0u,
                Position = Vector3.zero,
                IsArmed = false,
                TriggerRadius = 0f,
                IsTriggered = false
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as TrapStatePacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(0u, restored.TrapId);
            Assert.AreEqual(Vector3.zero, restored.Position);
            Assert.AreEqual(false, restored.IsArmed);
            Assert.AreEqual(0f, restored.TriggerRadius, 0.001f);
            Assert.AreEqual(false, restored.IsTriggered);
        }

        [Test]
        public void TrapStatePacket_MaxValues_SerializeCorrectly()
        {
            var original = new TrapStatePacket
            {
                TrapId = uint.MaxValue,
                Position = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue),
                IsArmed = true,
                TriggerRadius = float.MaxValue,
                IsTriggered = true
            };

            byte[] data = original.Serialize();
            var restored = PacketSerializer.Deserialize(data) as TrapStatePacket;

            Assert.IsNotNull(restored);
            Assert.AreEqual(uint.MaxValue, restored.TrapId);
            Assert.AreEqual(original.Position, restored.Position);
            Assert.AreEqual(true, restored.IsArmed);
            Assert.AreEqual(float.MaxValue, restored.TriggerRadius);
            Assert.AreEqual(true, restored.IsTriggered);
        }

        #endregion

        #region Edge Cases and Adversarial Tests

        [Test]
        public void PacketSerializer_DeserializeNull_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => PacketSerializer.Deserialize(null),
                "Deserialize should throw ArgumentException for null data");
        }

        [Test]
        public void PacketSerializer_DeserializeEmptyArray_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => PacketSerializer.Deserialize(new byte[0]),
                "Deserialize should throw ArgumentException for empty array");
        }

        [Test]
        public void PacketSerializer_DeserializeTooLarge_ThrowsArgumentException()
        {
            byte[] largeData = new byte[(typeof(PacketSerializer).GetField("MAX_PACKET_SIZE", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.GetValue(null) as int? ?? 65536) + 1];
            Assert.Throws<ArgumentException>(() => PacketSerializer.Deserialize(largeData),
                "Deserialize should throw ArgumentException for oversized packet");
        }

        [Test]
        public void PacketSerializer_DeserializeUnknownPacketType_ThrowsInvalidOperationException()
        {
            // Create a packet with an unknown type byte (e.g., 255)
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)255); // Unknown packet type
                writer.Write(0); // Some dummy data
                byte[] data = ms.ToArray();

                Assert.Throws<InvalidOperationException>(() => PacketSerializer.Deserialize(data),
                    "Deserialize should throw InvalidOperationException for unknown packet type");
            }
        }

        [Test]
        public void PacketSerializer_SerializeNullPacket_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => PacketSerializer.Serialize(null),
                "Serialize should throw ArgumentNullException for null packet");
        }

        [Test]
        public void AllPacketTypes_HaveUniquePacketTypeValues()
        {
            var packetTypes = Enum.GetValues(typeof(PacketType));
            var byteValues = new System.Collections.Generic.HashSet<byte>();
            var duplicates = new System.Collections.Generic.List<PacketType>();

            foreach (PacketType pt in packetTypes)
            {
                byte b = (byte)pt;
                if (!byteValues.Add(b))
                {
                    duplicates.Add(pt);
                }
            }

            Assert.AreEqual(0, duplicates.Count, $"Duplicate PacketType byte values found: {string.Join(", ", duplicates)}");
        }

        [Test]
        public void PhaseChangePacket_Serialize_ProducesCorrectFirstByte()
        {
            var packet = new PhaseChangePacket
            {
                Phase = Phase.Night,
                RemainingTime = 100f,
                NightNumber = 1
            };

            byte[] data = packet.Serialize();

            Assert.AreEqual((byte)PacketType.PhaseChange, data[0], "First byte should be PacketType.PhaseChange (24)");
        }

        [Test]
        public void SessionStatePacket_Serialize_ProducesCorrectFirstByte()
        {
            var packet = new SessionStatePacket
            {
                CurrentNight = 1,
                State = SessionState.Active,
                RakeHealth = 100f,
                VictoryType = VictoryType.None
            };

            byte[] data = packet.Serialize();

            Assert.AreEqual((byte)PacketType.SessionState, data[0], "First byte should be PacketType.SessionState (25)");
        }

        #endregion

        #region Serialization Round-Trip Stress Tests

        [Test]
        public void AllPacketTypes_MultipleRoundTrips_ProduceConsistentResults()
        {
            // Test multiple round-trips for each packet type to ensure consistency
            var packets = new NetworkPacket[]
            {
                new PhaseChangePacket { Phase = Phase.Day, RemainingTime = 123.45f, NightNumber = 2 },
                new SessionStatePacket { CurrentNight = 3, State = SessionState.Victory, RakeHealth = 50f, VictoryType = VictoryType.HuntersWin },
                new PlayerStatePacket { PlayerId = 10, Position = new Vector3(1,2,3), Rotation = new Quaternion(0.1f,0.2f,0.3f,0.4f), AnimState = 5 },
                new RakeStatePacket { Position = new Vector3(4,5,6), Rotation = new Quaternion(0.5f,0.5f,0.5f,0.5f), State = 7, Health = 75f },
                new PlayerConnectedPacket { PlayerId = 20, SpawnPosition = new Vector3(7,8,9), IsYou = true },
                new PlayerDisconnectedPacket { PlayerId = 30 },
                new DamageRequestPacket { ShooterId = 1, TargetId = 2, Amount = 50, DamageType = "Arrow", ShooterPosition = new Vector3(10,10,10) },
                new DamageAppliedPacket { TargetId = 2, NewHealth = 25f },
                new FireVisualsPacket { ShooterId = 1, WeaponType = 3, Origin = new Vector3(0,1,0), Direction = Vector3.up },
                new ItemPickupPacket { PlayerId = 1, ItemNetworkId = 100, ItemType = "Key" },
                new ItemDestroyedPacket { ItemNetworkId = 100, PickerId = 1 },
                new CameraSyncPacket { CameraId = 5u, Position = new Vector3(1,2,3), Rotation = Quaternion.identity, IsActive = true, IsBroken = false, BatteryLevel = 100f, PlacedByPlayerId = 1 },
                new CameraSpawnPacket { CameraId = 6u, Position = new Vector3(4,5,6), Rotation = Quaternion.identity, PlacedByPlayerId = 2 },
                new CameraDestroyPacket { CameraId = 7u },
                new QuestTriggerPacket { TriggerType = 0, Position = new Vector3(7,8,9), CameraId = 5u },
                new PigStatePacket { PigId = 1u, Position = new Vector3(10,10,10), Rotation = Quaternion.identity, State = 1, Health = 80f, CameraId = 0u },
                new MapMarkerPacket { MarkerType = 3, Position = new Vector3(20,20,20), MarkerId = 10u, IsActive = true },
                new TrapStatePacket { TrapId = 1u, Position = new Vector3(30,30,30), IsArmed = true, TriggerRadius = 5f, IsTriggered = false }
            };

            foreach (var original in packets)
            {
                for (int i = 0; i < 3; i++) // 3 round-trips
                {
                    byte[] data = original.Serialize();
                    var restored = PacketSerializer.Deserialize(data);

                    Assert.IsNotNull(restored, $"Packet {original.Type} should deserialize on iteration {i}");
                    Assert.AreEqual(original.Type, restored.Type, $"Packet {original.Type} Type should match on iteration {i}");

                    // Verify the restored packet can be serialized again to identical bytes
                    byte[] data2 = restored.Serialize();
                    Assert.AreEqual(data, data2, $"Packet {original.Type} should produce identical bytes on re-serialization (iteration {i})");
                }
            }
        }

        #endregion
    }
}