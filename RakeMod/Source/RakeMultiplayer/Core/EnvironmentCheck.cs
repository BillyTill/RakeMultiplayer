using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace RakeMultiplayer
{
    /// <summary>
    /// Startup check of the files the mod needs next to the game.
    ///
    /// The crash report from another PC (AV inside mono.dll, ~46 s after install) is exactly
    /// what a missing or replaced native dependency looks like: the managed layer is fine,
    /// the interop call is not. Instead of guessing, the mod states what it found, and refuses
    /// to touch Steam when the Steam files are not the ones we ship.
    ///
    /// It also reports foreign plugins in BepInEx\plugins: two mods that both patch the same
    /// methods produce behaviour nobody can reproduce, and it is the most common cause of
    /// "works on my PC".
    /// </summary>
    internal static class EnvironmentCheck
    {
        private const long MinimumNativeSize = 100 * 1024;

        private static readonly string[] SteamFiles =
        {
            "steam_api.dll",
            "CSteamworks.dll",
            "steam_appid.txt"
        };

        /// <summary>These plugins belong to this mod; anything else is somebody's else code.</summary>
        private static readonly HashSet<string> OwnPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "RakeMultiplayer.dll",
            "Telepathy.dll"
        };

        public static bool SteamFilesOk { get; private set; }
        public static string SteamProblem { get; private set; }
        public static string[] ForeignPlugins { get; private set; }

        public static void Run()
        {
            SteamFilesOk = true;
            SteamProblem = null;
            ForeignPlugins = new string[0];

            string root = null;
            try { root = Paths.GameRootPath; }
            catch (Exception ex) { Diagnostics.LogWarning("[Environment] cannot resolve the game folder: " + ex.Message); }

            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                SteamFilesOk = false;
                SteamProblem = "the game folder could not be resolved";
                Diagnostics.LogWarning("[Environment] " + SteamProblem + "; Steam will stay off");
                return;
            }

            for (int i = 0; i < SteamFiles.Length; i++)
            {
                string path = Path.Combine(root, SteamFiles[i]);

                if (!File.Exists(path))
                {
                    SteamFilesOk = false;
                    SteamProblem = SteamFiles[i] + " is missing in " + root;
                    Diagnostics.LogWarning("[Environment] " + SteamProblem);
                    continue;
                }

                // A quarantined or replaced native library is worse than a missing one: it
                // loads and then calls into the wrong ABI.
                var info = new FileInfo(path);
                if (info.Length < MinimumNativeSize && !SteamFiles[i].EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    SteamFilesOk = false;
                    SteamProblem = SteamFiles[i] + " looks truncated (" + info.Length + " bytes)";
                    Diagnostics.LogWarning("[Environment] " + SteamProblem);
                }
            }

            CheckPlugins(root);

            if (SteamFilesOk)
            {
                Diagnostics.LogInfo("[Environment] steam files present in " + root);
            }
            else
            {
                Diagnostics.LogWarning("[Environment] Steam disabled: " + SteamProblem +
                                       ". LAN is unaffected; reinstall the mod to restore Steam.");
            }
        }

        internal static bool IsOurFile(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            if (OwnPlugins.Contains(name)) return true;
            if (name.StartsWith("RakeMultiplayer.", StringComparison.OrdinalIgnoreCase)) return true;

            if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        private static void CheckPlugins(string gameRoot)
        {
            string plugins = Path.Combine(Path.Combine(gameRoot, "BepInEx"), "plugins");
            if (!Directory.Exists(plugins)) return;

            var foreign = new List<string>();

            try
            {
                foreach (var path in Directory.GetFiles(plugins))
                {
                    string name = Path.GetFileName(path);

                    // Everything this mod ships: the dll, its pdb and manifest, Telepathy,
                    // and the art/model payloads.
                    if (IsOurFile(name)) continue;

                    foreign.Add(name);
                }
            }
            catch (Exception ex)
            {
                Diagnostics.LogWarning("[Environment] cannot list " + plugins + ": " + ex.Message);
                return;
            }

            if (foreign.Count > 0)
            {
                ForeignPlugins = foreign.ToArray();
                Diagnostics.LogWarning("[Environment] foreign plugins in BepInEx\\plugins: " +
                                       string.Join(", ", ForeignPlugins) +
                                       ". Two mods patching the same methods behave unpredictably; " +
                                       "remove them if anything misbehaves.");
            }
        }
    }
}
