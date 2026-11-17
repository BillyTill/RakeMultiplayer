using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RakeMultiplayer
{
    /// <summary>
    /// The list of files an install is supposed to contain, with their SHA-256.
    ///
    /// Written by the installer, verified by the mod at startup. A mixed install (a plugin
    /// from one build, a loader from another) or a truncated/quarantined file shows up as a
    /// hash mismatch instead of as "the menu is missing" three steps later.
    ///
    /// The manifest is anti-corruption, not anti-malware: someone who edits both the file and
    /// its hash wins. That is fine - it is meant to catch bad installs, not attackers.
    /// </summary>
    public sealed class InstallManifest
    {
        public const string Header = "RakeMultiplayer manifest 1";

        public sealed class Entry
        {
            public string Path;   // relative to the game folder, '/' separated
            public string Hash;   // lowercase hex sha256

            public Entry(string path, string hash)
            {
                Path = path;
                Hash = hash;
            }
        }

        public readonly List<Entry> Entries = new List<Entry>();
        public string ModVersion;   // informational: the build that wrote this manifest

        public void Add(string path, string hash)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(hash)) return;
            Entries.Add(new Entry(Normalize(path), hash.ToLowerInvariant()));
        }

        public string Serialize()
        {
            var sb = new StringBuilder(Entries.Count * 80 + 64);
            sb.Append(Header).Append('\n');
            if (!string.IsNullOrEmpty(ModVersion)) sb.Append("version=").Append(ModVersion).Append('\n');

            for (int i = 0; i < Entries.Count; i++)
            {
                sb.Append(Entries[i].Hash).Append(' ').Append(Entries[i].Path).Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>Parses a manifest; returns null when the text is not a manifest at all.</summary>
        public static InstallManifest Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            var lines = text.Replace("\r\n", "\n").Split('\n');
            if (lines.Length == 0 || lines[0].Trim() != Header) return null;

            var manifest = new InstallManifest();

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;

                if (line.StartsWith("version=", StringComparison.Ordinal))
                {
                    manifest.ModVersion = line.Substring(8).Trim();
                    continue;
                }

                int space = line.IndexOf(' ');
                if (space != 64) continue;   // a sha256 hex line is exactly 64 chars + space

                string hash = line.Substring(0, 64);
                string path = line.Substring(65).Trim();
                if (path.Length == 0) continue;
                if (!IsHex(hash)) continue;

                manifest.Add(path, hash);
            }

            return manifest.Entries.Count > 0 ? manifest : null;
        }

        public sealed class Result
        {
            public readonly List<string> Missing = new List<string>();
            public readonly List<string> Modified = new List<string>();

            public bool Ok { get { return Missing.Count == 0 && Modified.Count == 0; } }

            public string Describe()
            {
                if (Ok) return "ok";

                var sb = new StringBuilder();
                if (Missing.Count > 0) sb.Append("missing: ").Append(string.Join(", ", Missing.ToArray()));
                if (Modified.Count > 0)
                {
                    if (sb.Length > 0) sb.Append("; ");
                    sb.Append("modified: ").Append(string.Join(", ", Modified.ToArray()));
                }
                return sb.ToString();
            }
        }

        /// <summary>Recomputes every hash under <paramref name="root"/> and compares.</summary>
        public Result Verify(string root)
        {
            var result = new Result();
            if (string.IsNullOrEmpty(root)) return result;

            for (int i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                string full = System.IO.Path.Combine(root, entry.Path.Replace('/', System.IO.Path.DirectorySeparatorChar));

                if (!File.Exists(full))
                {
                    result.Missing.Add(entry.Path);
                    continue;
                }

                string actual = FileHash.OfFile(full);
                if (!string.Equals(actual, entry.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    result.Modified.Add(entry.Path);
                }
            }

            return result;
        }

        private static string Normalize(string path)
        {
            return path.Replace('\\', '/').TrimStart('/').ToLowerInvariant();
        }

        private static bool IsHex(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return false;
            }
            return value.Length > 0;
        }
    }
}
