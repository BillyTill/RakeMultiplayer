using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    /// <summary>
    /// Consistency checks for the rules that must always hold between the session state
    /// machine, the loaded level and the UI.
    ///
    /// Every finding of the 1.1.5 full-cycle audit was a violation of one of these rules,
    /// but the rules lived in the author's head: nothing failed at the moment the state went
    /// wrong, so the symptom appeared minutes later somewhere else (a frozen level, a loop on
    /// the results screen, "9999" damage on the first trap).
    ///
    /// Off by default. Enabled by the dev bridge flag (-rakemcp), because the checks are
    /// diagnostics: they log loudly once and then count, and never stop the game.
    /// </summary>
    internal static class Invariants
    {
        private static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();

        /// <summary>
        /// Checks run only when the dev bridge is enabled. A shipping install must not
        /// pay for them, and a player must never see a diagnostic they cannot act on.
        /// </summary>
        public static bool Enabled
        {
            get { return Diagnostics.DevMode; }
        }

        /// <summary>
        /// Records a broken rule. The first occurrence and every 60th after it are logged,
        /// so a per-frame violation cannot flood the log (the 32-bit process has little
        /// memory and the log is the only evidence we get from a player's PC).
        /// </summary>
        public static void Check(string rule, bool ok, string detail = null)
        {
            if (ok || !Enabled) return;

            int count;
            Counts.TryGetValue(rule, out count);
            Counts[rule] = ++count;

            if (count == 1 || count % 60 == 0)
            {
                Diagnostics.LogWarning("[Invariant] " + rule + " (x" + count + ")" +
                                       (string.IsNullOrEmpty(detail) ? "" : " :: " + detail));
            }
        }

        /// <summary>The session says a match is running, so a game level must be loaded.</summary>
        public static void CheckSessionHasLevel(SessionState state, string where)
        {
            if (state != SessionState.Active) return;

            // loadedLevel is the reliable signal on this build: the level names are
            // "Level_1(Day)" / "Level_1(Night)" and the menu is level 0.
            int level = Application.loadedLevel;
            Check("session Active without a loaded level",
                  level >= 1,
                  where + ": state=" + state + " level=" + level +
                  " scene='" + Application.loadedLevelName + "'");
        }

        /// <summary>
        /// The results overlay owns the viewport, so it may only exist for a finished match.
        /// Arming it on a Waiting session is what made RETURN TO LOBBY loop forever.
        /// </summary>
        public static void CheckMatchOverIsReal(bool matchOver, string where)
        {
            if (!matchOver) return;

            var session = SessionManager.Instance;
            if (session == null) return;

            Check("results overlay on a session that is not over",
                  session.CurrentState == SessionState.GameOver,
                  where + ": session=" + session.CurrentState);
        }

        /// <summary>
        /// The session and the match must agree: an Active session owns a match, and a
        /// finished or aborted session owns nothing. This is the rule whose absence let the
        /// results overlay sit on a Waiting session and loop.
        /// </summary>
        public static void CheckMatchAgreesWithSession(SessionState state, string where)
        {
            if (SessionManager.Instance == null) return;

            if (state == SessionState.Active)
            {
                Check("session Active without a match", MatchState.IsActive,
                      where + ": level=" + Application.loadedLevel);
            }
            else
            {
                Check("match running after the session ended", !MatchState.IsActive,
                      where + ": state=" + state + " match=" +
                      (MatchState.IsActive ? MatchState.Current.Describe() : "-"));
            }
        }

        /// <summary>Total recorded violations (sum of all rule counters).</summary>
        public static int Violations
        {
            get
            {
                int total = 0;
                foreach (var pair in Counts) total += pair.Value;
                return total;
            }
        }

        /// <summary>Compact rule=count dump for the dev bridge /state endpoint.</summary>
        public static string Dump()
        {
            if (Counts.Count == 0) return "";

            var sb = new StringBuilder(160);
            foreach (var pair in Counts)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(pair.Key).Append('=').Append(pair.Value);
            }
            return sb.ToString();
        }

        public static void Reset()
        {
            Counts.Clear();
        }
    }
}
