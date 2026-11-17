using System;
using System.Collections.Generic;

namespace RakeMultiplayer
{
    /// <summary>
    /// State that lives exactly as long as one match.
    ///
    /// The 1.1.5 audit found one root cause behind most of its 21 findings: per-match state
    /// lived in process-wide statics, so it survived the level change, the results screen and
    /// the return to the menu. "Clear it in one more place" does not scale — the next
    /// subsystem forgets, and the bug comes back in a new form.
    ///
    /// Instead, the match has an owner. It is created when the session becomes Active and is
    /// destroyed when the session ends, and everything match-scoped belongs to it.
    ///
    /// Deliberately free of UnityEngine types so the lifecycle can be unit tested outside the
    /// engine (see Tests/MatchState.Tests.cs).
    /// </summary>
    public sealed class MatchState
    {
        private static MatchState _current;

        /// <summary>The running match, or null between matches.</summary>
        public static MatchState Current { get { return _current; } }

        public static bool IsActive { get { return _current != null; } }

        public int LevelIndex { get; private set; }
        public DateTime StartedUtc { get; private set; }
        public bool IsOver { get; private set; }
        public string EndReason { get; private set; }

        /// <summary>Players currently dead in this match. Cleared with the match.</summary>
        internal readonly HashSet<int> DeadPlayers = new HashSet<int>();

        /// <summary>Server-side health per player id. Cleared with the match.</summary>
        internal readonly Dictionary<int, float> PlayerHealth = new Dictionary<int, float>();

        /// <summary>Pending respawn timers. Cleared with the match (and on a level change).</summary>
        internal readonly Dictionary<int, float> RespawnDue = new Dictionary<int, float>();

        internal readonly HashSet<int> RespawnScheduled = new HashSet<int>();

        private MatchState(int levelIndex)
        {
            LevelIndex = levelIndex;
            StartedUtc = DateTime.UtcNow;
        }

        public static MatchState Begin(int levelIndex, string where)
        {
            if (_current != null)
            {
                // Starting a match while one is still running means the previous one leaked.
                // Do not silently overwrite it: that is exactly how stale state travelled
                // between sessions.
                Diagnostics.LogWarning("[MatchState] begin while a match is active (" +
                                       where + "); the previous match is ended");
                _current.IsOver = true;
                _current.EndReason = "restarted by " + where;
                _current.ResetCombat();
            }

            var state = new MatchState(levelIndex);
            _current = state;

            Invariants.Check("match started without a loaded level", levelIndex >= 1,
                             where + ": level=" + levelIndex);

            Diagnostics.LogInfo("[MatchState] begin: level=" + levelIndex + " (" + where + ")");
            return state;
        }

        public static void End(string reason)
        {
            var state = _current;
            if (state == null) return;

            state.IsOver = true;
            state.EndReason = reason;
            state.ResetCombat();
            _current = null;

            Diagnostics.LogInfo("[MatchState] end: level=" + state.LevelIndex +
                                " reason=" + reason);
        }

        /// <summary>
        /// Drops everything the combat layer accumulated: deaths, health and respawn timers.
        /// Called between matches and, inside one, when the level changes - the new level
        /// brings everyone back alive, and a stale 0 health made the next trap deal 9999.
        /// </summary>
        public void ResetCombat()
        {
            DeadPlayers.Clear();
            PlayerHealth.Clear();
            RespawnDue.Clear();
            RespawnScheduled.Clear();
        }

        /// <summary>
        /// A match spans day/night levels, so the level index has to follow the loaded one.
        /// A stale value here silently disabled level-dependent logic before (the 1.1.5
        /// audit), and it made /state describe a level the player was not standing in.
        /// </summary>
        public void SetLevel(int levelIndex)
        {
            if (levelIndex > 0) LevelIndex = levelIndex;
        }

        public bool IsDead(int playerId)
        {
            return playerId > 0 && DeadPlayers.Contains(playerId);
        }

        public void MarkDead(int playerId)
        {
            if (playerId > 0) DeadPlayers.Add(playerId);
        }

        public void MarkAlive(int playerId)
        {
            if (playerId > 0) DeadPlayers.Remove(playerId);
        }

        public int DeadCount { get { return DeadPlayers.Count; } }

        /// <summary>Compact description for the dev bridge and the invariant log.</summary>
        public string Describe()
        {
            return "level=" + LevelIndex +
                   " over=" + (IsOver ? "true" : "false") +
                   " dead=" + DeadPlayers.Count +
                   " hp=" + PlayerHealth.Count +
                   " respawns=" + RespawnDue.Count +
                   " age=" + (int)(DateTime.UtcNow - StartedUtc).TotalSeconds + "s";
        }
    }
}
