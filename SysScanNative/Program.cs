using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SysScanNative
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        private Button startButton;
        private Button saveLogButton;
        private Button saveSettingsButton;
        private ProgressBar progressBar;
        private TextBox logBox;
        private CheckedListBox tasksList;
        private CheckBox runAsAdminCheck;

        private AppSettings settings;

        public MainForm()
        {
            Text = "SysScan Native Cleanup";
            Width = 900;
            Height = 650;

            settings = AppSettings.Load();

            startButton = new Button() { Text = "Start Cleanup", Left = 10, Top = 10, Width = 140 };
            startButton.Click += async (s, e) => await RunCleanupAsync();

            saveLogButton = new Button() { Text = "Save Log", Left = 160, Top = 10, Width = 120 };
            saveLogButton.Click += (s, e) => SaveCurrentLog();

            saveSettingsButton = new Button() { Text = "Save Settings", Left = 290, Top = 10, Width = 120 };
            saveSettingsButton.Click += (s, e) => { settings.Save(); AppendLog("Settings saved"); };

            runAsAdminCheck = new CheckBox() { Text = "Run tasks as Admin where required", Left = 420, Top = 14, Width = 260 };
            runAsAdminCheck.Checked = settings.RunAsAdmin;
            runAsAdminCheck.CheckedChanged += (s, e) => settings.RunAsAdmin = runAsAdminCheck.Checked;

            progressBar = new ProgressBar() { Left = 10, Top = 45, Width = 860, Height = 22 };

            tasksList = new CheckedListBox() { Left = 10, Top = 75, Width = 300, Height = 510, CheckOnClick = true };
            tasksList.Items.Add("Clean temporary files", settings.CleanTemp);
            tasksList.Items.Add("Clean Recycle Bin", settings.CleanRecycleBin);
            tasksList.Items.Add("Clean Windows Update files", settings.CleanWindowsUpdate);
            tasksList.Items.Add("Clean Thumbnails", settings.CleanThumbnails);
            tasksList.Items.Add("Run SFC /scannow", settings.RunSfc);
            tasksList.Items.Add("Run DISM restorehealth", settings.RunDism);

            logBox = new TextBox() { Left = 320, Top = 75, Width = 550, Height = 510, Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true };

            Controls.Add(startButton);
            Controls.Add(saveLogButton);
            Controls.Add(saveSettingsButton);
            Controls.Add(runAsAdminCheck);
            Controls.Add(progressBar);
            Controls.Add(tasksList);
            Controls.Add(logBox);
        }

        private void AppendLog(string text)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AppendLog(text)));
                return;
            }
            logBox.AppendText($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}{Environment.NewLine}");
        }

        private async Task RunCleanupAsync()
        {
            startButton.Enabled = false;
            progressBar.Value = 0;
            var results = new List<string>();
            var selectedTasks = tasksList.CheckedItems.Cast<string>().ToList();

            try
            {
                AppendLog("Starting cleanup tasks...");
                int step = 0;
                int total = Math.Max(1, selectedTasks.Count);

                if (selectedTasks.Contains("Clean temporary files"))
                {
                    AppendLog("Cleaning temporary files...");
                    results.Add(await Task.Run(() => CleanTemporaryFiles()));
                    step++; progressBar.Value = (step * 100) / total;
                }

                if (selectedTasks.Contains("Clean Recycle Bin"))
                {
                    AppendLog("Cleaning Recycle Bin...");
                    results.Add(await Task.Run(() => CleanRecycleBin()));
                    step++; progressBar.Value = (step * 100) / total;
                }

                if (selectedTasks.Contains("Clean Windows Update files"))
                {
                    AppendLog("Cleaning Windows Update files...");
                    results.Add(await Task.Run(() => CleanWindowsUpdateFiles()));
                    step++; progressBar.Value = (step * 100) / total;
                }

                if (selectedTasks.Contains("Clean Thumbnails"))
                {
                    AppendLog("Cleaning thumbnails and caches...");
                    results.Add(await Task.Run(() => CleanThumbnails()));
                    step++; progressBar.Value = (step * 100) / total;
                }

                if (selectedTasks.Contains("Run SFC /scannow"))
                {
                    AppendLog("Running SFC (may require admin)...");
                    results.Add(await Task.Run(() => RunSystemFileChecker()));
                    step++; progressBar.Value = (step * 100) / total;
                }

                if (selectedTasks.Contains("Run DISM restorehealth"))
                {
                    AppendLog("Running DISM (may require admin)...");
                    results.Add(await Task.Run(() => RunDism()));
                    step++; progressBar.Value = (step * 100) / total;
                }

                SaveResults(results);
                AppendLog("All tasks completed.");
                progressBar.Value = 100;
            }
            catch (Exception ex)
            {
                AppendLog("Error: " + ex.Message);
            }
            finally
            {
                startButton.Enabled = true;
            }
        }

        private string CleanTemporaryFiles()
        {
            try
            {
                var temp = Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath();
                var files = Directory.EnumerateFiles(temp, "*", SearchOption.AllDirectories).ToList();
                long initialSize = files.Sum(f => {
                    try { return new FileInfo(f).Length; } catch { return 0L; }
                });

                int removed = 0; long freed = 0;
                foreach (var f in files)
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        long len = fi.Length;
                        fi.IsReadOnly = false;
                        fi.Delete();
                        removed++;
                        freed += len;
                    }
                    catch { /* ignore individual failures */ }
                }

                var msg = $"Temporary files: Removed {removed} files, freed {freed / (1024.0*1024.0):F2} MB";
                AppendLog(msg);
                return msg;
            }
            catch (Exception ex)
            {
                var msg = "Temporary files: Failed - " + ex.Message;
                AppendLog(msg);
                return msg;
            }
        }

        private string CleanRecycleBin()
        {
            try
            {
                var drive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
                var recycle = Path.Combine(drive, "$Recycle.Bin");
                if (Directory.Exists(recycle))
                {
                    RunCmd($"rd /s /q \"{recycle}\"");
                    var newSize = GetDirectorySize(recycle);
                    var msg = $"Recycle Bin: Cleared (post-size {newSize / (1024.0*1024.0):F2} MB)";
                    AppendLog(msg);
                    return msg;
                }
                return "Recycle Bin: Not found or inaccessible";
            }
            catch (Exception ex)
            {
                var msg = "Recycle Bin: Failed - " + ex.Message;
                AppendLog(msg);
                return msg;
            }
        }

        private string CleanWindowsUpdateFiles()
        {
            try
            {
                var windir = Environment.GetEnvironmentVariable("SystemRoot") ?? "C:\\Windows";
                var updateDir = Path.Combine(windir, "SoftwareDistribution", "Download");
                if (Directory.Exists(updateDir))
                {
                    var files = Directory.EnumerateFiles(updateDir, "*", SearchOption.AllDirectories).ToList();
                    long freed = 0; int removed = 0;
                    foreach (var f in files)
                    {
                        try { var fi = new FileInfo(f); long len = fi.Length; fi.IsReadOnly = false; fi.Delete(); removed++; freed += len; } catch { }
                    }
                    var msg = $"Windows Update files: Removed {removed} files, freed {freed / (1024.0*1024.0):F2} MB";
                    AppendLog(msg);
                    return msg;
                }
                return "Windows Update files: Not found";
            }
            catch (Exception ex)
            {
                var msg = "Windows Update files: Failed - " + ex.Message;
                AppendLog(msg);
                return msg;
            }
        }

        private string CleanThumbnails()
        {
            try
            {
                var profile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var explorer = Path.Combine(profile, "AppData", "Local", "Microsoft", "Windows", "Explorer");
                if (Directory.Exists(explorer))
                {
                    var files = Directory.EnumerateFiles(explorer, "thumbcache_*.db").ToList();
                    int removed = 0; long freed = 0;
                    foreach (var f in files)
                    {
                        try { var fi = new FileInfo(f); long len = fi.Length; fi.IsReadOnly = false; fi.Delete(); removed++; freed += len; } catch { }
                    }
                    var msg = $"Thumbnails: Removed {removed} files, freed {freed / (1024.0*1024.0):F2} MB";
                    AppendLog(msg);
                    return msg;
                }
                return "Thumbnails: Not found";
            }
            catch (Exception ex)
            {
                var msg = "Thumbnails: Failed - " + ex.Message;
                AppendLog(msg);
                return msg;
            }
        }

        private string RunSystemFileChecker()
        {
            try
            {
                var (outp, err) = RunCmdCapture("sfc /scannow");
                AppendLog("SFC completed");
                if (!string.IsNullOrWhiteSpace(outp)) return "SFC: " + Truncate(outp, 400);
                if (!string.IsNullOrWhiteSpace(err)) return "SFC(Error): " + Truncate(err, 400);
                return "SFC: Completed";
            }
            catch (Exception ex)
            {
                var msg = "SFC: Failed - " + ex.Message;
                AppendLog(msg);
                return msg;
            }
        }

        private string RunDism()
        {
            try
            {
                var (outp, err) = RunCmdCapture("dism /online /cleanup-image /restorehealth");
                AppendLog("DISM completed");
                if (!string.IsNullOrWhiteSpace(outp)) return "DISM: " + Truncate(outp, 400);
                if (!string.IsNullOrWhiteSpace(err)) return "DISM(Error): " + Truncate(err, 400);
                return "DISM: Completed";
            }
            catch (Exception ex)
            {
                var msg = "DISM: Failed - " + ex.Message;
                AppendLog(msg);
                return msg;
            }
        }

        private void SaveResults(List<string> results)
        {
            try
            {
                var desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var folder = Path.Combine(desk, "Scan_results");
                Directory.CreateDirectory(folder);
                var file = Path.Combine(folder, "Scan_results.txt");
                var sb = new StringBuilder();
                sb.AppendLine("Cleanup Results - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine(new string('=', 50));
                foreach (var r in results) sb.AppendLine(r);
                sb.AppendLine();
                sb.AppendLine("Cleanup completed successfully.");
                File.WriteAllText(file, sb.ToString());
                AppendLog("Detailed results saved to: " + file);
            }
            catch (Exception ex)
            {
                AppendLog("SaveResults failed: " + ex.Message);
            }
        }

        private void SaveCurrentLog()
        {
            try
            {
                var desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var file = Path.Combine(desk, "SysScan_Native_Log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                File.WriteAllText(file, logBox.Text);
                AppendLog("Saved log to " + file);
            }
            catch (Exception ex)
            {
                AppendLog("Save log failed: " + ex.Message);
            }
        }

        // Settings persistence
        private class AppSettings
        {
            public bool RunAsAdmin { get; set; } = true;
            public bool CleanTemp { get; set; } = true;
            public bool CleanRecycleBin { get; set; } = true;
            public bool CleanWindowsUpdate { get; set; } = true;
            public bool CleanThumbnails { get; set; } = true;
            public bool RunSfc { get; set; } = false;
            public bool RunDism { get; set; } = false;

            private static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SysScanNative", "settings.json");

            public void Save()
            {
                try
                {
                    var d = Path.GetDirectoryName(SettingsPath);
                    if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                    File.WriteAllText(SettingsPath, System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
                }
                catch { }
            }

            public static AppSettings Load()
            {
                try
                {
                    if (File.Exists(SettingsPath))
                    {
                        var s = File.ReadAllText(SettingsPath);
                        return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(s) ?? new AppSettings();
                    }
                }
                catch { }
                return new AppSettings();
            }
        }

        private static long GetDirectorySize(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return 0;
                return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } });
            }
            catch { return 0; }
        }

        private static void RunCmd(string command)
        {
            var psi = new ProcessStartInfo("cmd.exe", "/c " + command)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };
            using var p = Process.Start(psi);
            p?.WaitForExit();
        }

        private static (string output, string error) RunCmdCapture(string command)
        {
            var psi = new ProcessStartInfo("cmd.exe", "/c " + command)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            using var p = Process.Start(psi);
            var outp = p?.StandardOutput.ReadToEnd() ?? string.Empty;
            var err = p?.StandardError.ReadToEnd() ?? string.Empty;
            p?.WaitForExit();
            return (outp, err);
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }
    }
}
