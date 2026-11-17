using System;
using NUnit.Framework;

namespace RakeMultiplayer.Tests
{
    /// <summary>
    /// The match lifecycle is pure managed code on purpose: this is the layer that used to
    /// leak between sessions, and it must be verifiable without starting the game.
    /// </summary>
    [TestFixture]
    public class MatchStateTests
    {
        [TearDown]
        public void Cleanup()
        {
            // Every test leaves the process in the between-matches state, otherwise the
            // next one inherits a match. This is the same discipline the game needs.
            MatchState.End("test cleanup");
        }

        [Test]
        public void Begin_StartsAnActiveMatchOnTheGivenLevel()
        {
            var match = MatchState.Begin(3, "test");

            Assert.IsTrue(MatchState.IsActive);
            Assert.AreSame(match, MatchState.Current);
            Assert.AreEqual(3, match.LevelIndex);
            Assert.IsFalse(match.IsOver);
            Assert.AreEqual(0, match.DeadCount);
            Assert.AreEqual(0, match.PlayerHealth.Count);
            Assert.AreEqual(0, match.RespawnDue.Count);
        }

        [Test]
        public void End_ClosesTheMatchAndClearsCurrent()
        {
            var match = MatchState.Begin(1, "test");
            MatchState.End("game over");

            Assert.IsFalse(MatchState.IsActive);
            Assert.IsNull(MatchState.Current);
            Assert.IsTrue(match.IsOver);
            Assert.AreEqual("game over", match.EndReason);
        }

        [Test]
        public void End_WithoutAMatch_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => MatchState.End("nothing to end"));
            Assert.IsFalse(MatchState.IsActive);
        }

        [Test]
        public void Begin_WhileAMatchIsActive_EndsTheOldOne()
        {
            var first = MatchState.Begin(1, "first");
            var second = MatchState.Begin(2, "second");

            Assert.IsTrue(first.IsOver);
            Assert.IsTrue(first.EndReason.StartsWith("restarted by", StringComparison.Ordinal));
            Assert.AreSame(second, MatchState.Current);
            Assert.AreEqual(2, second.LevelIndex);
        }

        [Test]
        public void DeadPlayers_AreScopedToTheMatch()
        {
            var match = MatchState.Begin(1, "test");
            match.MarkDead(7);
            match.MarkDead(9);

            Assert.IsTrue(match.IsDead(7));
            Assert.AreEqual(2, match.DeadCount);

            match.MarkAlive(7);
            Assert.IsFalse(match.IsDead(7));
            Assert.AreEqual(1, match.DeadCount);
        }

        [Test]
        public void End_ClearsDeadPlayers()
        {
            var match = MatchState.Begin(1, "test");
            match.MarkDead(1);
            match.MarkDead(2);

            MatchState.End("test");

            Assert.AreEqual(0, match.DeadCount, "a finished match must not keep its dead");
        }

        [Test]
        public void End_ClearsHealthAndRespawnTimers()
        {
            var match = MatchState.Begin(1, "test");
            match.PlayerHealth[2] = 0f;
            match.RespawnDue[2] = 12.5f;
            match.RespawnScheduled.Add(2);

            MatchState.End("test");

            Assert.AreEqual(0, match.PlayerHealth.Count, "health must not survive the match");
            Assert.AreEqual(0, match.RespawnDue.Count, "respawn timers must not survive the match");
            Assert.AreEqual(0, match.RespawnScheduled.Count);
        }

        [Test]
        public void ResetCombat_KeepsTheMatchRunning()
        {
            var match = MatchState.Begin(1, "test");
            match.MarkDead(3);
            match.PlayerHealth[3] = 0f;
            match.RespawnDue[3] = 8f;

            match.ResetCombat();

            // A day/night change inside a match drops the combat state but not the match.
            Assert.IsTrue(MatchState.IsActive);
            Assert.AreSame(match, MatchState.Current);
            Assert.AreEqual(0, match.DeadCount);
            Assert.AreEqual(0, match.PlayerHealth.Count);
            Assert.AreEqual(0, match.RespawnDue.Count);
        }

        [Test]
        public void Describe_ReportsTheLevelAndTheAge()
        {
            var match = MatchState.Begin(2, "test");

            var text = match.Describe();

            StringAssert.Contains("level=2", text);
            StringAssert.Contains("over=false", text);
            StringAssert.Contains("dead=0", text);
            StringAssert.Contains("hp=0", text);
        }
    }
}
