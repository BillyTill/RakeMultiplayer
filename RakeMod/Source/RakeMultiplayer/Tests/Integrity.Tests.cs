using System;
using System.IO;
using NUnit.Framework;

namespace RakeMultiplayer.Tests
{
    /// <summary>
    /// The install manifest is what turns "the menu is missing on the other PC" into a precise
    /// statement about which file is not the one the installer wrote.
    /// </summary>
    [TestFixture]
    public class IntegrityTests
    {
        private string _root;

        [SetUp]
        public void CreateTree()
        {
            _root = Path.Combine(Path.GetTempPath(), "rake-manifest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Path.Combine(_root, "BepInEx"), "plugins"));
            File.WriteAllText(Path.Combine(_root, "Rake.exe"), "game");
        }

        [TearDown]
        public void RemoveTree()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
        }

        private string Write(string relative, string content)
        {
            string path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content);
            return path;
        }

        [Test]
        public void Sha256_KnownVector()
        {
            // sha256("abc") - a fixed value, so a broken hex conversion cannot pass.
            Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
                            FileHash.OfBytes(System.Text.Encoding.ASCII.GetBytes("abc")));
        }

        [Test]
        public void Manifest_RoundTripsThroughText()
        {
            var manifest = new InstallManifest { ModVersion = "1.1.6.0" };
            manifest.Add("BepInEx/plugins/RakeMultiplayer.dll", new string('a', 64));
            manifest.Add("steam_api.dll", new string('b', 64));

            var parsed = InstallManifest.Parse(manifest.Serialize());

            Assert.IsNotNull(parsed);
            Assert.AreEqual("1.1.6.0", parsed.ModVersion);
            Assert.AreEqual(2, parsed.Entries.Count);
            Assert.AreEqual("bepinex/plugins/rakemultiplayer.dll", parsed.Entries[0].Path);
            Assert.AreEqual(new string('a', 64), parsed.Entries[0].Hash);
        }

        [Test]
        public void Manifest_RejectsForeignText()
        {
            Assert.IsNull(InstallManifest.Parse(""));
            Assert.IsNull(InstallManifest.Parse("not a manifest"));
            Assert.IsNull(InstallManifest.Parse("RakeMultiplayer manifest 1\nno entries here\n"));
            Assert.IsNull(InstallManifest.Parse(
                "RakeMultiplayer manifest 1\n" + new string('z', 64) + " BepInEx/plugins/x.dll\n"));
        }

        [Test]
        public void Verify_AcceptsUnchangedFiles()
        {
            Write("steam_api.dll", "steam");
            Write("BepInEx/plugins/RakeMultiplayer.dll", "mod");

            var manifest = new InstallManifest();
            manifest.Add("steam_api.dll", FileHash.OfFile(Path.Combine(_root, "steam_api.dll")));
            manifest.Add("BepInEx/plugins/RakeMultiplayer.dll",
                         FileHash.OfFile(Path.Combine(Path.Combine(Path.Combine(_root, "BepInEx"), "plugins"),
                                                     "RakeMultiplayer.dll")));

            Assert.IsTrue(manifest.Verify(_root).Ok, "an untouched install must verify");
        }

        [Test]
        public void Verify_ReportsAModifiedFile()
        {
            string path = Write("BepInEx/plugins/RakeMultiplayer.dll", "mod");
            var manifest = new InstallManifest();
            manifest.Add("BepInEx/plugins/RakeMultiplayer.dll", FileHash.OfFile(path));

            // Another build of the same file: this is the mixed-install case.
            File.WriteAllText(path, "other build");

            var result = manifest.Verify(_root);

            Assert.IsFalse(result.Ok);
            Assert.AreEqual(1, result.Modified.Count);
            Assert.AreEqual(0, result.Missing.Count);
            StringAssert.Contains("modified", result.Describe());
        }

        [Test]
        public void Verify_ReportsAMissingFile()
        {
            string path = Write("steam_api.dll", "steam");
            var manifest = new InstallManifest();
            manifest.Add("steam_api.dll", FileHash.OfFile(path));

            File.Delete(path);

            var result = manifest.Verify(_root);

            Assert.IsFalse(result.Ok);
            Assert.AreEqual(1, result.Missing.Count);
            StringAssert.Contains("missing", result.Describe());
        }

        [Test]
        public void Verify_TreatsATruncatedFileAsModified()
        {
            string path = Write("BepInEx/plugins/RakeMultiplayer.dll", "a full plugin");
            var manifest = new InstallManifest();
            manifest.Add("BepInEx/plugins/RakeMultiplayer.dll", FileHash.OfFile(path));

            // What an interrupted copy or an antivirus quarantine leaves behind.
            File.WriteAllText(path, "a ful");

            var result = manifest.Verify(_root);

            Assert.IsFalse(result.Ok);
            Assert.AreEqual(1, result.Modified.Count);
        }
    }
}
