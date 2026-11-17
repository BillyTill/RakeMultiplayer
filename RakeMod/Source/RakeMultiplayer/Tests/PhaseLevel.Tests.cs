using NUnit.Framework;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer.Tests
{
    /// <summary>
    /// Phase/level agreement and the Rake-owner rule. Both caused visible bugs: a client that
    /// darkened while still standing in the day level, and a hunter proxy spawned for the
    /// player who owns the Rake.
    /// </summary>
    [TestFixture]
    public class PhaseLevelTests
    {
        [Test]
        public void DayLevel_MatchesDay_AndNotNight()
        {
            Assert.IsTrue(DayNightManager.LevelMatchesPhase(Phase.Day, "Level_1(Day)"));
            Assert.IsFalse(DayNightManager.LevelMatchesPhase(Phase.Night, "Level_1(Day)"));
        }

        [Test]
        public void NightLevel_MatchesNight_AndNotDay()
        {
            Assert.IsTrue(DayNightManager.LevelMatchesPhase(Phase.Night, "Level_1(Night)"));
            Assert.IsFalse(DayNightManager.LevelMatchesPhase(Phase.Day, "Level_1(Night)"));
        }

        [Test]
        public void LevelWithoutSuffix_MatchesAnyPhase()
        {
            // A mode that ships a single level must keep the old behaviour.
            Assert.IsTrue(DayNightManager.LevelMatchesPhase(Phase.Night, "level1"));
            Assert.IsTrue(DayNightManager.LevelMatchesPhase(Phase.Day, "level1"));
        }

        [Test]
        public void EmptySceneName_MatchesAnyPhase()
        {
            Assert.IsTrue(DayNightManager.LevelMatchesPhase(Phase.Night, null));
            Assert.IsTrue(DayNightManager.LevelMatchesPhase(Phase.Day, ""));
        }

        [TearDown]
        public void Cleanup()
        {
            LobbyState.RakeOwnerId = 0;
        }

        [Test]
        public void RakeOwner_HasNoHunterBody()
        {
            LobbyState.RakeOwnerId = 2;

            Assert.IsTrue(LobbyState.IsRakeOwner(2), "the owner must not get a hunter proxy");
            Assert.IsFalse(LobbyState.IsRakeOwner(1));
            Assert.IsFalse(LobbyState.IsRakeOwner(0));
        }

        [Test]
        public void NobodyOwnsTheRake_MeansEverybodyIsAHunter()
        {
            LobbyState.RakeOwnerId = 0;

            Assert.IsFalse(LobbyState.IsRakeOwner(1));
            Assert.IsFalse(LobbyState.IsRakeOwner(2));
        }
    }
}
