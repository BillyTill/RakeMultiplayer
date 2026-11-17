using System;

namespace RakeMultiplayer
{
    /// <summary>
    /// Logging indirection for the parts of the mod that must stay engine-independent.
    ///
    /// `RakeMultiplayerPlugin` derives from BepInEx's `BaseUnityPlugin`, so touching any of
    /// its members drags BepInEx into the process. Classes like <see cref="MatchState"/> are
    /// pure state machines and are unit tested outside the game, so they log through this
    /// facade instead: the plugin installs the real handlers on startup, tests see no-ops.
    /// </summary>
    internal static class Diagnostics
    {
        public static bool DevMode;
        public static Action<string> Info;
        public static Action<string> Warning;
        public static Action<string> Error;

        public static void LogInfo(string message)
        {
            var handler = Info;
            if (handler != null) handler(message);
        }

        public static void LogWarning(string message)
        {
            var handler = Warning;
            if (handler != null) handler(message);
        }

        public static void LogError(string message)
        {
            var handler = Error;
            if (handler != null) handler(message);
        }
    }
}
