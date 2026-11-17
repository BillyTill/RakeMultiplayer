using System;
using System.IO;
using BepInEx;

namespace RakeMultiplayer
{
    /// <summary>
    /// Compares the files on disk with the manifest the installer wrote.
    ///
    /// Three outcomes:
    ///  - no manifest: the install was not made by this version of the installer (or it is a
    ///    development copy) - logged, multiplayer allowed;
    ///  - manifest matches: everything is the build it claims to be;
    ///  - manifest does not match: a file was replaced, truncated or mixed with another
    ///    version. Multiplayer is refused, because a half-matching install is exactly how a
    ///    "menu is missing" or a mono crash happens, and neither can be diagnosed remotely.
    ///
    /// Development copies (the build drops its dll into the game folder without a manifest,
    /// or with a stale one) are exempt while <see cref="Diagnostics.DevMode"/> is on.
    /// </summary>
    internal static class IntegrityCheck
    {
        public const string ManifestFileName = "RakeMultiplayer.manifest";

        public static bool HasManifest { get; private set; }
        public static bool VerifiedOk { get; private set; }
        public static string Problem { get; private set; }

        /// <summary>The manifest exists and disagrees with the installed files.</summary>
        public static bool Failed { get { return HasManifest && !VerifiedOk; } }

        /// <summary>Multiplayer must refuse to start.</summary>
        public static bool BlocksMultiplayer { get { return Failed && !Diagnostics.DevMode; } }

        public static void Run()
        {
            HasManifest = false;
            VerifiedOk = false;
            Problem = null;

            string root;
            try { root = Paths.GameRootPath; }
            catch (Exception ex)
            {
                Diagnostics.LogWarning("[Integrity] cannot resolve the game folder: " + ex.Message);
                return;
            }

            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;

            string plugins = Path.Combine(Path.Combine(root, "BepInEx"), "plugins");
            string path = Path.Combine(plugins, ManifestFileName);

            if (!File.Exists(path))
            {
                Problem = "install manifest missing (" + ManifestFileName + ")";
                Diagnostics.LogWarning("[Integrity] " + Problem +
                                       ": this install was not made by the installer, or it is a " +
                                       "development copy. Multiplayer is left enabled.");
                return;
            }

            HasManifest = true;

            InstallManifest manifest;
            try
            {
                manifest = InstallManifest.Parse(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Problem = "install manifest unreadable: " + ex.Message;
                Diagnostics.LogError("[Integrity] " + Problem);
                return;
            }

            if (manifest == null)
            {
                Problem = "install manifest is not a valid manifest";
                Diagnostics.LogError("[Integrity] " + Problem);
                return;
            }

            var result = manifest.Verify(root);
            if (!result.Ok)
            {
                Problem = result.Describe();
                Diagnostics.LogError("[Integrity] installed files do not match the manifest (" +
                                     manifest.Entries.Count + " entries): " + Problem +
                                     ". Reinstall the mod; a mixed install cannot be supported.");
                return;
            }

            VerifiedOk = true;
            Diagnostics.LogInfo("[Integrity] " + manifest.Entries.Count + " files match the manifest" +
                                (string.IsNullOrEmpty(manifest.ModVersion) ? "" : " (" + manifest.ModVersion + ")"));
        }
    }
}
