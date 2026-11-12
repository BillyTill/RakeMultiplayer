using System;
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
            // Silent mode for testing / scripted installs: /silent <game folder>
            if (args.Length >= 2 && string.Equals(args[0], "/silent", StringComparison.OrdinalIgnoreCase))
            {
                Environment.ExitCode = Installer.Install(args[1], null) ? 0 : 1;
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
        }
    }

    internal static class Installer
    {
        public const string PayloadResource = "payload.pak";

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
        private readonly TextBox _path = new TextBox();
        private readonly TextBox _log = new TextBox();
        private readonly Button _install = new Button();
        private readonly Button _launch = new Button();
        private readonly Label _status = new Label();

        public InstallerForm()
        {
            Text = "Rake Multiplayer - установка";
            ClientSize = new Size(640, 440);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);

            Label title = new Label();
            title.Text = "Rake Multiplayer";
            title.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
            title.SetBounds(16, 12, 400, 28);
            Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "BepInEx 5.4.23.2 (x86) + мод и все ассеты. Ставится на чистую игру.";
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

            _install.Text = "Установить";
            _install.SetBounds(16, 142, 150, 34);
            _install.Click += InstallClick;
            Controls.Add(_install);

            _launch.Text = "Запустить игру";
            _launch.SetBounds(176, 142, 160, 34);
            _launch.Enabled = false;
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
            _status.Text = "Готов к установке.";
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

        private void InstallClick(object sender, EventArgs e)
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

            _install.Enabled = false;
            _launch.Enabled = false;
            _log.Clear();
            _status.ForeColor = Color.Black;
            _status.Text = "Установка...";
            Application.DoEvents();

            Append("Папка игры: " + gameDir);
            bool ok = Installer.Install(gameDir, Append);

            if (ok)
            {
                Append(string.Empty);
                Append("Готово. Запустите игру и нажмите F1 - откроется меню мультиплеера.");
                Append("Хост: Host Game. Клиент: IP хоста и порт 7777.");
                Append("Radmin VPN / Hamachi тоже работают: введите VPN-адрес хоста.");
                _status.Text = "Установлено.";
                _status.ForeColor = Color.Green;
                _launch.Enabled = true;
            }
            else
            {
                _status.Text = "Установка не удалась.";
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
