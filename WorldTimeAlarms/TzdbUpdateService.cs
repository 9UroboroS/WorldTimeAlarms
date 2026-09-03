using NodaTime;
using NodaTime.TimeZones;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace WorldTimeAlarms
{
    /// <summary>
    /// Gestiona la descarga y caché del archivo TZDB de NodaTime.
    /// Al iniciar, comprueba si el archivo local tiene más de 30 días;
    /// si es así (o no existe) lo descarga desde GitHub Releases de NodaTime.
    /// Siempre provee un IDateTimeZoneProvider funcional, aunque sea el de Windows como fallback.
    /// </summary>
    public static class TzdbUpdateService
    {
        // ── Rutas y constantes ────────────────────────────────────────────────────
        private static readonly string AppDataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "WorldTimeAlarms");

        private static readonly string CachedNzdPath =
            Path.Combine(AppDataDir, "tzdb.nzd");

        private const int RefreshDays      = 30;   // actualizar cada 30 días
        private const string GitHubApiUrl  =
            "https://api.github.com/repos/nodatime/nodatime/releases/latest";

        // ── Estado público ────────────────────────────────────────────────────────
        public static IDateTimeZoneProvider Provider { get; private set; }
            = DateTimeZoneProviders.Tzdb;           // builtin como valor inicial

        public static DateTime? LastUpdated { get; private set; }
        public static string    ProviderSource { get; private set; } = "builtin";

        // ── Inicialización ────────────────────────────────────────────────────────

        /// <summary>
        /// Llama a esto al iniciar la app (no bloquea la UI).
        /// Si la descarga falla, el servicio usa la base builtin de NodaTime.
        /// </summary>
        public static async Task InitializeAsync()
        {
            Directory.CreateDirectory(AppDataDir);
            NotificationCenter.Publish(
                title: LocalizationManager.T("Str_TzdbTitle"),
                message: LocalizationManager.T("Str_TzdbChecking"),
                kind: AppNotificationKind.Info,
                category: "TZDB",
                deduplicationKey: "tzdb-check");

            bool needsDownload = ShouldDownload();

            if (needsDownload)
            {
                NotificationCenter.Publish(
                    title: LocalizationManager.T("Str_TzdbTitle"),
                    message: LocalizationManager.T("Str_TzdbDownloading"),
                    kind: AppNotificationKind.Warning,
                    category: "TZDB",
                    deduplicationKey: "tzdb-download");

                bool ok = await TryDownloadLatestAsync();
                if (!ok)
                {
                    System.Diagnostics.Debug.WriteLine("[TZDB] Descarga fallida, usando fallback.");
                    NotificationCenter.Publish(
                        title: LocalizationManager.T("Str_TzdbTitle"),
                        message: LocalizationManager.T("Str_TzdbDownloadFailed"),
                        kind: AppNotificationKind.Error,
                        category: "TZDB",
                        deduplicationKey: "tzdb-status");
                }
            }

            LoadFromDisk();

            NotificationCenter.Publish(
                title: LocalizationManager.T("Str_TzdbTitle"),
                message: LocalizationManager.T("Str_TzdbActiveSource", ProviderSource),
                kind: needsDownload ? AppNotificationKind.Success : AppNotificationKind.Info,
                category: "TZDB",
                deduplicationKey: "tzdb-source");
        }

        // ── Lógica interna ────────────────────────────────────────────────────────

        private static bool ShouldDownload()
        {
            if (!File.Exists(CachedNzdPath)) return true;
            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(CachedNzdPath);
            return age.TotalDays >= RefreshDays;
        }

        private static void LoadFromDisk()
        {
            if (!File.Exists(CachedNzdPath))
            {
                ProviderSource = "builtin";
                return;
            }

            try
            {
                using var stream = File.OpenRead(CachedNzdPath);
                var source   = TzdbDateTimeZoneSource.FromStream(stream);
                Provider     = new DateTimeZoneCache(source);
                LastUpdated  = File.GetLastWriteTimeUtc(CachedNzdPath);
                ProviderSource = $"archivo local ({LastUpdated:dd/MM/yyyy})";
                System.Diagnostics.Debug.WriteLine($"[TZDB] Cargado desde disco: {CachedNzdPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TZDB] Error cargando .nzd: {ex.Message}");
                ProviderSource = "builtin (error al cargar archivo)";
            }
        }

        private static async Task<bool> TryDownloadLatestAsync()
        {
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(15);
                http.DefaultRequestHeaders.Add("User-Agent", "WorldTimeAlarms/1.0");

                // 1. Obtener URL del .nzd más reciente vía GitHub API
                string json     = await http.GetStringAsync(GitHubApiUrl);
                string? nzdUrl  = ParseNzdUrl(json);

                if (nzdUrl is null)
                {
                    System.Diagnostics.Debug.WriteLine("[TZDB] No se encontró asset .nzd en el release.");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[TZDB] Descargando desde: {nzdUrl}");

                // 2. Descargar el archivo
                byte[] data = await http.GetByteArrayAsync(nzdUrl);

                // 3. Validar que es un .nzd válido antes de sobreescribir
                using (var ms = new MemoryStream(data))
                    TzdbDateTimeZoneSource.FromStream(ms);  // lanza si no es válido

                // 4. Guardar en disco
                await File.WriteAllBytesAsync(CachedNzdPath, data);
                System.Diagnostics.Debug.WriteLine($"[TZDB] Guardado en: {CachedNzdPath}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TZDB] Error en descarga: {ex.Message}");
                return false;
            }
        }

        private static string? ParseNzdUrl(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("assets", out var assets)) return null;

                foreach (var asset in assets.EnumerateArray())
                {
                    if (!asset.TryGetProperty("name", out var name)) continue;
                    if (!asset.TryGetProperty("browser_download_url", out var url)) continue;
                    if (name.GetString()?.EndsWith(".nzd", StringComparison.OrdinalIgnoreCase) == true)
                        return url.GetString();
                }
            }
            catch { }
            return null;
        }

        // ── API de conversión (reemplaza TimeZoneInfo) ────────────────────────────

        /// <summary>
        /// Convierte una hora UTC a la zona horaria indicada por su ID de Windows o IANA.
        /// </summary>
        public static DateTime ConvertFromUtc(DateTime utc, string windowsOrIanaId)
        {
            try
            {
                string ianaId = TimeZoneConverter.TZConvert.WindowsToIana(windowsOrIanaId);
                var zone      = Provider.GetZoneOrNull(ianaId)
                             ?? Provider.GetZoneOrNull(windowsOrIanaId);

                if (zone is null)
                    return TimeZoneInfo.ConvertTimeFromUtc(utc,
                        TimeZoneConverter.TZConvert.GetTimeZoneInfo(windowsOrIanaId));

                var instant = Instant.FromDateTimeUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
                return instant.InZone(zone).ToDateTimeUnspecified();
            }
            catch
            {
                // fallback a TimeZoneInfo si algo falla
                return TimeZoneInfo.ConvertTimeFromUtc(utc,
                    TimeZoneConverter.TZConvert.GetTimeZoneInfo(windowsOrIanaId));
            }
        }

        /// <summary>
        /// Convierte una hora local en la zona indicada a UTC.
        /// </summary>
        public static DateTime ConvertToUtc(DateTime local, string windowsOrIanaId)
        {
            try
            {
                string ianaId = TimeZoneConverter.TZConvert.WindowsToIana(windowsOrIanaId);
                var zone      = Provider.GetZoneOrNull(ianaId)
                             ?? Provider.GetZoneOrNull(windowsOrIanaId);

                if (zone is null)
                    return TimeZoneInfo.ConvertTimeToUtc(local,
                        TimeZoneConverter.TZConvert.GetTimeZoneInfo(windowsOrIanaId));

                var localDate  = LocalDateTime.FromDateTime(local);
                var zonedOrAmbiguous = zone.AtLeniently(localDate);
                return zonedOrAmbiguous.ToDateTimeUtc();
            }
            catch
            {
                return TimeZoneInfo.ConvertTimeToUtc(local,
                    TimeZoneConverter.TZConvert.GetTimeZoneInfo(windowsOrIanaId));
            }
        }

        /// <summary>
        /// Devuelve el offset UTC actual para la zona indicada, en formato "+HH:mm".
        /// </summary>
        public static string GetUtcOffsetString(string windowsOrIanaId)
        {
            try
            {
                string ianaId = TimeZoneConverter.TZConvert.WindowsToIana(windowsOrIanaId);
                var zone      = Provider.GetZoneOrNull(ianaId)
                             ?? Provider.GetZoneOrNull(windowsOrIanaId);

                if (zone is null) throw new InvalidOperationException("Zona no encontrada");

                var offset = zone.GetUtcOffset(SystemClock.Instance.GetCurrentInstant());
                return offset.ToString("+HH:mm", null);
            }
            catch
            {
                var tz     = TimeZoneConverter.TZConvert.GetTimeZoneInfo(windowsOrIanaId);
                var offset = tz.GetUtcOffset(DateTime.UtcNow);
                return (offset >= TimeSpan.Zero ? "+" : "") + offset.ToString(@"hh\:mm");
            }
        }
    }
}
