using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace WorldTimeAlarms
{
    public sealed class AppUpdateManifest
    {
        public string Version { get; set; } = string.Empty;
        public string InstallerUrl { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public static class AppUpdateService
    {
        // Publica aquí un JSON con: { "version": "1.0.5", "installerUrl": "https://...exe", "notes": "..." }
        private const string UpdateRepositoryOwner = "9UroboroS";
        private const string UpdateRepositoryName = "WorldTimeAlarms";
        private const string UpdateRepositoryBranch = "main";
        private static readonly string[] UpdateManifestUrls =
        [
            $"https://raw.githubusercontent.com/{UpdateRepositoryOwner}/{UpdateRepositoryName}/{UpdateRepositoryBranch}/update.json",
            $"https://cdn.jsdelivr.net/gh/{UpdateRepositoryOwner}/{UpdateRepositoryName}@{UpdateRepositoryBranch}/update.json"
        ];
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

        public static async Task CheckForUpdatesAsync(Window? owner, bool silentMode, bool showNoUpdateMessage)
        {
            try
            {
                var manifest = await GetLatestManifestAsync();
                if (manifest is null)
                {
                    if (!silentMode)
                        MessageBox.Show(owner,
                            LocalizationManager.T("Str_UpdateCheckFailed"),
                            LocalizationManager.T("Str_AppTitle"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    return;
                }

                Version current = GetCurrentVersion();
                Version latest = ParseVersion(manifest.Version);

                if (latest <= current)
                {
                    if (showNoUpdateMessage && !silentMode)
                    {
                        MessageBox.Show(owner,
                            LocalizationManager.T("Str_UpdateNoUpdates", current.ToString(3)),
                            LocalizationManager.T("Str_AppTitle"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }

                    return;
                }

                var prompt = LocalizationManager.T("Str_UpdateAvailablePrompt", latest.ToString(3));
                if (!string.IsNullOrWhiteSpace(manifest.Notes))
                    prompt += Environment.NewLine + Environment.NewLine + manifest.Notes.Trim();

                MessageBoxResult result = MessageBox.Show(owner,
                    prompt,
                    LocalizationManager.T("Str_AppTitle"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result != MessageBoxResult.Yes)
                    return;

                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = manifest.InstallerUrl,
                        UseShellExecute = true
                    });

                    MessageBox.Show(owner,
                        LocalizationManager.T("Str_UpdateInstallNotice"),
                        LocalizationManager.T("Str_AppTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    if (Application.Current.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.AllowClose = true;
                        mainWindow.Close();
                    }
                    else
                    {
                        Application.Current.Shutdown();
                    }
                }
                catch
                {
                    if (!silentMode)
                    {
                        MessageBox.Show(owner,
                            LocalizationManager.T("Str_UpdateOpenFailed"),
                            LocalizationManager.T("Str_AppTitle"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch
            {
                if (!silentMode)
                {
                    MessageBox.Show(owner,
                        LocalizationManager.T("Str_UpdateCheckFailed"),
                        LocalizationManager.T("Str_AppTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }

        private static async Task<AppUpdateManifest?> GetLatestManifestAsync()
        {
            foreach (string url in UpdateManifestUrls)
            {
                try
                {
                    string json = await Http.GetStringAsync(url);
                    var manifest = JsonSerializer.Deserialize<AppUpdateManifest>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (manifest is not null
                        && !string.IsNullOrWhiteSpace(manifest.Version)
                        && !string.IsNullOrWhiteSpace(manifest.InstallerUrl))
                    {
                        return manifest;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static Version GetCurrentVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            string? informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
                return ParseVersion(informational);

            return ParseVersion(assembly.GetName().Version?.ToString(3));
        }

        private static Version ParseVersion(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new Version(0, 0, 0);

            string clean = value.Trim();
            int plus = clean.IndexOf('+');
            if (plus >= 0)
                clean = clean[..plus];

            int dash = clean.IndexOf('-');
            if (dash >= 0)
                clean = clean[..dash];

            return Version.TryParse(clean, out Version? parsed)
                ? parsed
                : new Version(0, 0, 0);
        }
    }
}
