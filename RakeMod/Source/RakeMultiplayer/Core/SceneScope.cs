using System;
using System.Collections.Generic;
using System.Text;

namespace RakeMultiplayer
{
    /// <summary>
    /// The single place that resets every process-wide subsystem when a level loads.
    ///
    /// Before this existed, each new subsystem added one more line to one more patch:
    /// `PlayerInputLock.ResetForScene()` here, `NetIdentity.Registry.Clear()` there,
    /// `PlayerDeathSystem.ResetForScene()` after the load. Forgetting one of them is exactly
    /// how the health map, the player states and the camera locks survived into the next
    /// level. Now the list is in one file, next to the subsystems it resets, and a failed
    /// reset is reported instead of being skipped.
    ///
    /// Nothing in here may delete data that is needed for persistence: the world snapshot is
    /// saved before ResetAll runs.
    /// </summary>
    internal static class SceneScope
    {
        private sealed class Entry
        {
            public string Name;
            public Action Reset;
        }

        private static readonly List<Entry> Resetters = new List<Entry>();
        private static bool _initialized;

        /// <summary>Number of registered resetters (0 if Initialize has not run yet).</summary>
        public static int Count { get { return Resetters.Count; } }

        public static void Register(string name, Action reset)
        {
            if (reset == null) return;

            for (int i = 0; i < Resetters.Count; i++)
            {
                if (Resetters[i].Name == name)
                {
                    Resetters[i].Reset = reset;
                    return;
                }
            }

            Resetters.Add(new Entry { Name = name, Reset = reset });
        }

        /// <summary>
        /// Registers the built-in resetters. Called from ResetAll, so a subsystem cannot be
        /// "forgotten to be initialised" the way it used to be "forgotten to be called".
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // Every process-wide subsystem belongs in this list. If you add static state
            // that belongs to a level, add it here in the same commit.
            Register("NetIdentity", delegate
            {
                NetIdentity.Registry.Clear();
                NetIdentity.NextNetId = 1;
            });
            Register("NetworkedTrap", NetworkedTrap.ResetSceneState);
            Register("NetworkedCamera", NetworkedCamera.ClearRegistry);
            Register("PlayerDeath", PlayerDeathSystem.ResetForScene);
            Register("RakeCombatState", delegate
            {
                // Health and respawn timers belong to the level that is being unloaded.
                if (RakeNetworkManager.Instance != null) RakeNetworkManager.Instance.ResetDeathState();
            });
            Register("PlayerInputLock", PlayerInputLock.ResetForScene);
        }

        /// <summary>
        /// Resets everything before the new level is built. One failing reset must not stop
        /// the others: a partially reset scene is worse than a reported failure.
        /// </summary>
        public static void ResetAll(string reason)
        {
            Initialize();

            int failed = 0;
            for (int i = 0; i < Resetters.Count; i++)
            {
                var entry = Resetters[i];
                try
                {
                    entry.Reset();
                }
                catch (Exception ex)
                {
                    failed++;
                    Diagnostics.LogWarning("[SceneScope] reset '" + entry.Name +
                                           "' failed: " + ex.Message);
                }
            }

            Invariants.Check("scene reset failed", failed == 0,
                             reason + ": " + failed + " of " + Resetters.Count);

            Diagnostics.LogInfo("[SceneScope] reset " + Resetters.Count +
                                " subsystems (" + reason + ")" +
                                (failed > 0 ? ", failed=" + failed : ""));
        }

        /// <summary>Post-reset sanity check used by the invariant harness.</summary>
        public static void CheckCleanState(string where)
        {
            Invariants.Check("NetIdentity registry survived a level load",
                             NetIdentity.Registry.Count == 0,
                             where + ": count=" + NetIdentity.Registry.Count);
            Invariants.Check("dead players survived a level load",
                             !MatchState.IsActive || MatchState.Current.DeadCount == 0,
                             where + ": dead=" + (MatchState.IsActive ? MatchState.Current.DeadCount : 0));
        }

        public static string Dump()
        {
            var sb = new StringBuilder(96);
            for (int i = 0; i < Resetters.Count; i++)
            {
                if (sb.Length > 0) sb.Append(',');
                sb.Append(Resetters[i].Name);
            }
            return sb.ToString();
        }
    }
}
