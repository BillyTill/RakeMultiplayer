using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RakeMultiplayerSetup
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // Silent install: /silent <game folder>
            if (args.Length >= 2 && string.Equals(args[0], "/silent", StringComparison.OrdinalIgnoreCase))
            {
                Environment.ExitCode = Installer.Install(args[1], null) ? 0 : 1;
                return;
            }

            // Silent uninstall: /uninstall [game folder]
            if (args.Length >= 1 && string.Equals(args[0], "/uninstall", StringComparison.OrdinalIgnoreCase))
            {
                string dir = args.Length >= 2 ? args[1] : Path.GetDirectoryName(Application.ExecutablePath);
                bool removed = Installer.Uninstall(dir, null);
                if (removed) Installer.ScheduleSelfDelete(dir);
                Environment.ExitCode = removed ? 0 : 1;
                return;
            }

            string exeName = Path.GetFileNameWithoutExtension(Application.ExecutablePath);
            bool uninstallMode = exeName.IndexOf("Uninstall", StringComparison.OrdinalIgnoreCase) >= 0;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm(uninstallMode));
        }
    }

    internal static class Installer
    {
        public const string PayloadResource = "payload.pak";
        public const string UninstallerName = "Uninstall_RakeMultiplayer.exe";

        public static bool IsGameDir(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return false;
            try
            {
                return File.Exists(Path.Combine(dir, "Rake.exe")) &&
                       Directory.Exists(Path.Combine(dir, "Rake_Data"));
            }
            catch
            {
                return false;
            }
        }

        // ------------------------------------------------------------------ install

        public static bool Install(string gameDir, Action<string> log)
        {
            if (!IsGameDir(gameDir))
            {
                if (log != null) log("В этой папке нет Rake.exe / Rake_Data.");
                return false;
            }

            string temp = Path.Combine(Path.GetTempPath(), "RakeMultiplayerSetup_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);

            try
            {
                string pak = Path.Combine(temp, "payload.pak");
                using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResource))
                {
                    if (input == null)
                    {
                        if (log != null) log("Ошибка: в установщике нет данных.");
                        return false;
                    }
                    using (FileStream output = File.Create(pak))
                    {
                        input.CopyTo(output);
                    }
                }

                int files = Extract(pak, gameDir, log);
                if (log != null) log("Установлено файлов: " + files + ".");

                WriteUninstaller(gameDir, log);
                return true;
            }
            catch (Exception ex)
            {
                if (log != null) log("Ошибка: " + ex.Message);
                return false;
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }

        private static void WriteUninstaller(string gameDir, Action<string> log)
        {
            try
            {
                string self = Application.ExecutablePath;
                string target = Path.Combine(gameDir, UninstallerName);

                if (string.Equals(self, target, StringComparison.OrdinalIgnoreCase)) return;

                File.Copy(self, target, true);
                if (log != null) log("Создан деинсталлятор: " + UninstallerName);
            }
            catch (Exception ex)
            {
                if (log != null) log("Не удалось создать деинсталлятор: " + ex.Message);
            }
        }

        private static int Extract(string pakPath, string gameDir, Action<string> log)
        {
            int count = 0;

            using (FileStream fs = File.OpenRead(pakPath))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                string magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
                if (magic != "RMPK") throw new InvalidDataException("bad payload");

                int entries = reader.ReadInt32();
                for (int i = 0; i < entries; i++)
                {
                    int nameLen = reader.ReadInt32();
                    string name = Encoding.UTF8.GetString(reader.ReadBytes(nameLen));
                    int dataLen = reader.ReadInt32();
                    byte[] data = reader.ReadBytes(dataLen);

                    string target = Path.Combine(gameDir, name.Replace('/', Path.DirectorySeparatorChar));
                    string dir = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                    File.WriteAllBytes(target, data);
                    count++;

                    if (log != null) log("  " + name);
                }
            }

            return count;
        }

        // ---------------------------------------------------------------- uninstall

        public static bool Uninstall(string gameDir, Action<string> log)
        {
            if (!IsGameDir(gameDir))
            {
                if (log != null) log("В этой папке нет Rake.exe / Rake_Data.");
                return false;
            }

            List<string> installed = ReadPayloadNames();
            int removed = 0;

            foreach (string name in installed)
            {
                string path = Path.Combine(gameDir, name.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) continue;

                try
                {
                    File.Delete(path);
                    removed++;
                    if (log != null) log("  удалён " + name);
                }
                catch (Exception ex)
                {
                    if (log != null) log("  занят: " + name + " (" + ex.Message + ")");
                }
            }

            // Leftovers from builds that shipped other file names.
            string plugins = Path.Combine(gameDir, "BepInEx", "plugins");
            if (Directory.Exists(plugins))
            {
                string[] patterns =
                {
                    "RakeMultiplayer.*",
                    "RakePlayerModel.bin",
                    "RakePlayerRifle.bin",
                    "RakePlayerSkin.png",
                    "rifle.png"
                };

                for (int i = 0; i < patterns.Length; i++)
                {
                    string[] matches;
                    try { matches = Directory.GetFiles(plugins, patterns[i]); }
                    catch { continue; }

                    for (int j = 0; j < matches.Length; j++)
                    {
                        try { File.Delete(matches[j]); removed++; } catch { }
                    }
                }
            }

            string bepInEx = Path.Combine(gameDir, "BepInEx");
            bool otherPlugins = false;

            try
            {
                if (Directory.Exists(plugins))
                {
                    otherPlugins = Directory.GetFiles(plugins).Length > 0 ||
                                   Directory.GetDirectories(plugins).Length > 0;
                }
            }
            catch { }

            if (!otherPlugins && Directory.Exists(bepInEx))
            {
                try
                {
                    Directory.Delete(bepInEx, true);
                    if (log != null) log("  папка BepInEx удалена (других плагинов нет)");
                }
                catch (Exception ex)
                {
                    if (log != null) log("  BepInEx удалить не удалось: " + ex.Message);
                }
            }
            else if (Directory.Exists(bepInEx))
            {
                RemoveEmptyDirs(bepInEx);
            }

            if (log != null) log("Удалено файлов: " + removed + ".");
            return true;
        }

        private static List<string> ReadPayloadNames()
        {
            List<string> names = new List<string>();

            using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResource))
            {
                if (input == null) return names;

                using (BinaryReader reader = new BinaryReader(input))
                {
                    string magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    if (magic != "RMPK") return names;

                    int entries = reader.ReadInt32();
                    for (int i = 0; i < entries; i++)
                    {
                        int nameLen = reader.ReadInt32();
                        names.Add(Encoding.UTF8.GetString(reader.ReadBytes(nameLen)));
                        int dataLen = reader.ReadInt32();
                        if (dataLen > 0) reader.BaseStream.Seek(dataLen, SeekOrigin.Current);
                    }
                }
            }

            return names;
        }

        private static void RemoveEmptyDirs(string dir)
        {
            try
            {
                foreach (string sub in Directory.GetDirectories(dir)) RemoveEmptyDirs(sub);

                if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                {
                    Directory.Delete(dir);
                }
            }
            catch
            {
                // best effort
            }
        }

        /// <summary>Delete the uninstaller copy after it finishes (a running exe cannot delete itself).</summary>
        public static void ScheduleSelfDelete(string gameDir)
        {
            try
            {
                string self = Application.ExecutablePath;
                if (!self.StartsWith(gameDir, StringComparison.OrdinalIgnoreCase)) return;

                ProcessStartInfo info = new ProcessStartInfo("cmd.exe",
                    "/c ping 127.0.0.1 -n 3 >nul & del /f /q \"" + self + "\"");
                info.CreateNoWindow = true;
                info.UseShellExecute = false;
                info.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(info);
            }
            catch
            {
                // not critical
            }
        }

        // ------------------------------------------------------------------ helpers

        public static string DetectGame()
        {
            string nextToInstaller = Path.GetDirectoryName(Application.ExecutablePath);
            if (IsGameDir(nextToInstaller)) return nextToInstaller;

            try
            {
                RegistryHive[] hives = { RegistryHive.LocalMachine, RegistryHive.CurrentUser };
                RegistryView[] views = { RegistryView.Registry32, RegistryView.Registry64 };

                foreach (RegistryHive hive in hives)
                {
                    foreach (RegistryView view in views)
                    {
                        using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                        using (RegistryKey uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                        {
                            if (uninstall == null) continue;

                            foreach (string subName in uninstall.GetSubKeyNames())
                            {
                                using (RegistryKey sub = uninstall.OpenSubKey(subName))
                                {
                                    if (sub == null) continue;

                                    string display = sub.GetValue("DisplayName") as string;
                                    string location = sub.GetValue("InstallLocation") as string;

                                    if (display == null || location == null) continue;
                                    if (display.IndexOf("Rake", StringComparison.OrdinalIgnoreCase) < 0) continue;
                                    if (IsGameDir(location)) return location;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // registry is best-effort only
            }

            string[] common =
            {
                @"C:\Games\Rake",
                @"D:\Games\Rake",
                @"C:\Rake",
                @"D:\Rake",
                @"C:\Program Files (x86)\Rake",
                @"C:\Program Files\Rake"
            };

            for (int i = 0; i < common.Length; i++)
            {
                if (IsGameDir(common[i])) return common[i];
            }

            return string.Empty;
        }

        public static bool IsGameRunning()
        {
            return Process.GetProcessesByName("Rake").Length > 0;
        }
    }

    internal class InstallerForm : Form
    {
        private readonly bool _uninstallMode;
        private readonly TextBox _path = new TextBox();
        private readonly TextBox _log = new TextBox();
        private readonly Button _install = new Button();
        private readonly Button _launch = new Button();
        private readonly Label _status = new Label();

        public InstallerForm(bool uninstallMode)
        {
            _uninstallMode = uninstallMode;

            Text = uninstallMode ? "Rake Multiplayer - удаление" : "Rake Multiplayer - установка";
            ClientSize = new Size(640, 440);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);

            Label title = new Label();
            title.Text = uninstallMode ? "Удаление Rake Multiplayer" : "Rake Multiplayer";
            title.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
            title.SetBounds(16, 12, 500, 28);
            Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = uninstallMode
                ? "Уберёт BepInEx и файлы мода. Файлы игры не трогаются."
                : "BepInEx 5.4.23.2 (x86) + мод и все ассеты. Ставится на чистую игру.";
            subtitle.SetBounds(16, 44, 600, 20);
            subtitle.ForeColor = Color.DimGray;
            Controls.Add(subtitle);

            Label pathLabel = new Label();
            pathLabel.Text = "Папка игры (где лежит Rake.exe):";
            pathLabel.SetBounds(16, 80, 320, 20);
            Controls.Add(pathLabel);

            _path.SetBounds(16, 102, 480, 24);
            _path.Text = Installer.DetectGame();
            Controls.Add(_path);

            Button browse = new Button();
            browse.Text = "Обзор...";
            browse.SetBounds(506, 101, 110, 26);
            browse.Click += BrowseClick;
            Controls.Add(browse);

            _install.Text = uninstallMode ? "Удалить мод" : "Установить";
            _install.SetBounds(16, 142, 160, 34);
            _install.Click += ActionClick;
            Controls.Add(_install);

            _launch.Text = "Запустить игру";
            _launch.SetBounds(186, 142, 160, 34);
            _launch.Enabled = false;
            _launch.Visible = !uninstallMode;
            _launch.Click += LaunchClick;
            Controls.Add(_launch);

            _log.SetBounds(16, 190, 600, 190);
            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = ScrollBars.Vertical;
            _log.BackColor = Color.White;
            _log.Font = new Font("Consolas", 8.5f);
            Controls.Add(_log);

            _status.SetBounds(16, 392, 600, 24);
            _status.Text = uninstallMode ? "Готов к удалению." : "Готов к установке.";
            Controls.Add(_status);
        }

        private void BrowseClick(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Выберите папку с Rake.exe";
                if (Installer.IsGameDir(_path.Text)) dialog.SelectedPath = _path.Text;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _path.Text = dialog.SelectedPath;
                }
            }
        }

        private void ActionClick(object sender, EventArgs e)
        {
            string gameDir = _path.Text.Trim().TrimEnd('\\', '/');

            if (!Installer.IsGameDir(gameDir))
            {
                MessageBox.Show(this,
                    "В выбранной папке нет Rake.exe и Rake_Data.\nУкажите папку с установленной игрой.",
                    "Rake Multiplayer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (Installer.IsGameRunning())
            {
                MessageBox.Show(this,
                    "Игра уже запущена. Закройте Rake.exe и попробуйте снова -\nиначе файлы будут заняты.",
                    "Rake Multiplayer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_uninstallMode)
            {
                DialogResult confirm = MessageBox.Show(this,
                    "Удалить BepInEx и файлы мода из этой папки?\n\n" + gameDir,
                    "Rake Multiplayer", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes) return;
            }

            _install.Enabled = false;
            _launch.Enabled = false;
            _log.Clear();
            _status.ForeColor = Color.Black;
            _status.Text = _uninstallMode ? "Удаление..." : "Установка...";
            Application.DoEvents();

            Append("Папка игры: " + gameDir);

            bool ok = _uninstallMode
                ? Installer.Uninstall(gameDir, Append)
                : Installer.Install(gameDir, Append);

            if (ok)
            {
                if (_uninstallMode)
                {
                    Append(string.Empty);
                    Append("Мод удалён. Деинсталлятор закроется и удалит себя.");
                    _status.Text = "Удалено.";
                    _status.ForeColor = Color.Green;

                    Installer.ScheduleSelfDelete(gameDir);
                    Close();
                    return;
                }

                Append(string.Empty);
                Append("Готово. Запустите игру и нажмите F1 - откроется меню мультиплеера.");
                Append("Хост: Host Game. Клиент: IP хоста и порт 7777.");
                Append("Radmin VPN / Hamachi тоже работают: введите VPN-адрес хоста.");
                Append("Удалить мод потом: " + Installer.UninstallerName + " в папке игры.");
                _status.Text = "Установлено.";
                _status.ForeColor = Color.Green;
                _launch.Enabled = true;
            }
            else
            {
                _status.Text = _uninstallMode ? "Удаление не удалось." : "Установка не удалась.";
                _status.ForeColor = Color.Firebrick;
            }

            _install.Enabled = true;
        }

        private void LaunchClick(object sender, EventArgs e)
        {
            string exe = Path.Combine(_path.Text.Trim().TrimEnd('\\', '/'), "Rake.exe");
            try
            {
                Process.Start(exe);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось запустить игру: " + ex.Message,
                    "Rake Multiplayer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Append(string text)
        {
            _log.AppendText(text + Environment.NewLine);
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
            Application.DoEvents();
        }
    }
}
