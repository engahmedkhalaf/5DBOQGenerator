using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace QicBoqMapper
{
    public static class UpdateChecker
    {
        // GitHub repo hosting releases. The installer must be uploaded as a release asset
        // named exactly InstallerAssetName (case-insensitive match).
        private const string GitHubOwner = "engahmedkhalaf";
        private const string GitHubRepo = "QIC_5D_BOQ_Manager_Setup";
        private const string InstallerAssetName = "QIC_5D_BOQ_Manager_Setup.exe";

        private const string RegistryPath = @"Software\QicTools\QicBoqMapper";
        private const string SkippedVersionValueName = "SkippedUpdateVersion";

        // Fire-and-forget. Never throws, never blocks Revit startup.
        public static void CheckAsync()
        {
            Task.Run(async () =>
            {
                try { await CheckCoreAsync().ConfigureAwait(false); }
                catch { /* silent — updates must never break the add-in */ }
            });
        }

        private static async Task CheckCoreAsync()
        {
            Version current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            string apiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

            string json;
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) })
            {
                client.DefaultRequestHeaders.Add("User-Agent", $"QicBoqMapper/{current}");
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
                var resp = await client.GetAsync(apiUrl).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return;
                json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            var tagMatch = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            if (!tagMatch.Success) return;
            string tag = tagMatch.Groups[1].Value.TrimStart('v', 'V');
            if (!Version.TryParse(NormalizeVersion(tag), out Version latest)) return;
            if (latest <= current) return;

            string skipped = ReadRegistry(SkippedVersionValueName);
            if (!string.IsNullOrEmpty(skipped) && Version.TryParse(skipped, out Version skip) && skip >= latest)
                return;

            var assetMatch = Regex.Match(
                json,
                "\"name\"\\s*:\\s*\"" + Regex.Escape(InstallerAssetName) + "\"[\\s\\S]*?\"browser_download_url\"\\s*:\\s*\"([^\"]+)\"",
                RegexOptions.IgnoreCase);
            if (!assetMatch.Success) return;
            string downloadUrl = assetMatch.Groups[1].Value;

            var notesMatch = Regex.Match(json, "\"body\"\\s*:\\s*\"([\\s\\S]*?)\"\\s*,\\s*\"");
            string releaseNotes = notesMatch.Success
                ? Regex.Unescape(notesMatch.Groups[1].Value).Replace("\\n", "\n").Replace("\\r", "")
                : string.Empty;

            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                PromptUser(current, latest, downloadUrl, releaseNotes)));
        }

        private static void PromptUser(Version current, Version latest, string downloadUrl, string releaseNotes)
        {
            string body = $"A new version of QIC 5D BOQ Manager is available.\n\n" +
                          $"Installed: {current}\nLatest:    {latest}\n\n" +
                          (string.IsNullOrWhiteSpace(releaseNotes) ? "" : $"Release notes:\n{Truncate(releaseNotes, 600)}\n\n") +
                          "Download the installer now? You'll need to close Revit before running it.";

            var result = MessageBox.Show(
                body,
                "Update Available",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                Task.Run(() => DownloadAndLaunch(downloadUrl));
            }
            else if (result == MessageBoxResult.Cancel)
            {
                WriteRegistry(SkippedVersionValueName, latest.ToString());
            }
            // No = remind next session.
        }

        private static async Task DownloadAndLaunch(string url)
        {
            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), InstallerAssetName);
                using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "QicBoqMapper-Updater");
                    var bytes = await client.GetByteArrayAsync(url).ConfigureAwait(false);
                    File.WriteAllBytes(tempPath, bytes);
                }

                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBox.Show(
                        "Installer downloaded. Please close Revit when prompted by the installer, then complete the installation.",
                        "Update Ready",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    try
                    {
                        Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to launch installer:\n{ex.Message}\n\nLocation: {tempPath}",
                            "Update Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }));
            }
            catch (Exception ex)
            {
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    MessageBox.Show($"Failed to download update:\n{ex.Message}",
                        "Update Error", MessageBoxButton.OK, MessageBoxImage.Warning)));
            }
        }

        private static string NormalizeVersion(string s)
        {
            // Accept "1.2", "1.2.3", "1.2.3.4" — pad to at least 2 parts for Version.TryParse.
            int dotCount = 0;
            foreach (char c in s) if (c == '.') dotCount++;
            if (dotCount == 0) return s + ".0";
            return s;
        }

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s.Substring(0, max) + "…";

        private static string ReadRegistry(string name)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                    return key?.GetValue(name)?.ToString() ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        private static void WriteRegistry(string name, string value)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                    key?.SetValue(name, value);
            }
            catch { }
        }
    }
}
