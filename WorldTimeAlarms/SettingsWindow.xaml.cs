using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace WorldTimeAlarms
{
    // ---------------------------------------------------------------------------
    //  AppSettings — DTO para transportar la configuración entre ventanas.
    // ---------------------------------------------------------------------------
    public class AppSettings
    {
        public TimeZoneInfo? LocalTimeZone   { get; set; }
        public bool          StartWithWindows { get; set; }
        public bool          MinimizeOnClose  { get; set; }
        public bool          PlayAlarmSound   { get; set; }
        public bool          Use24HourFormat  { get; set; }
        public int           MaxStoredNotifications { get; set; } = 40;
        public string        Language { get; set; } = LocalizationManager.Spanish;
    }

    // ---------------------------------------------------------------------------
    //  SettingsWindow — Ventana modal de configuración.
    // ---------------------------------------------------------------------------
    public partial class SettingsWindow : Window
    {
        private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupValueName    = "WorldTimeAlarms";

        /// <summary>Resultado de la ventana. Relleno sólo si el usuario guardó.</summary>
        public AppSettings? Result { get; private set; }

        private readonly AppSettings _initial;

        public SettingsWindow(AppSettings current)
        {
            InitializeComponent();
            _initial = current;
            PopulateControls(current);
        }

        // ------------------------------------------------------------------
        //  Carga
        // ------------------------------------------------------------------
        private sealed class LanguageOption
        {
            public string Code { get; init; } = string.Empty;
            public string Display { get; init; } = string.Empty;
        }

        private static readonly LanguageOption[] LanguageOptions =
        [
            new LanguageOption { Code = LocalizationManager.Spanish, Display = "Español" },
            new LanguageOption { Code = LocalizationManager.English, Display = "English" },
        ];

        private void PopulateControls(AppSettings s)
        {
            // Zona horaria
            var zones = TimeZoneInfo.GetSystemTimeZones();
            CboTimeZone.ItemsSource       = zones;
            CboTimeZone.DisplayMemberPath = nameof(TimeZoneInfo.DisplayName);
            CboTimeZone.SelectedItem      = zones.FirstOrDefault(z =>
                z.Id == (s.LocalTimeZone?.Id ?? TimeZoneInfo.Local.Id))
                ?? TimeZoneInfo.Local;

            // Checkboxes
            ChkStartup.IsChecked        = s.StartWithWindows;
            ChkMinimizeOnClose.IsChecked = s.MinimizeOnClose;
            ChkSound.IsChecked           = s.PlayAlarmSound;
            ChkUse24HourFormat.IsChecked = s.Use24HourFormat;
            TxtNotificationLimit.Text    = s.MaxStoredNotifications.ToString();

            // Idioma
            CboLanguage.ItemsSource  = LanguageOptions;
            CboLanguage.SelectedItem = LanguageOptions.FirstOrDefault(o => o.Code == s.Language)
                ?? LanguageOptions[0];
        }

        // ------------------------------------------------------------------
        //  Eventos
        // ------------------------------------------------------------------
        private void ChkStartup_Changed(object sender, RoutedEventArgs e)
        {
            // Previsualiza el efecto de la opción en el registro en tiempo real.
            // La escritura definitiva ocurre al guardar.
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (CboTimeZone.SelectedItem is not TimeZoneInfo tz)
            {
                ShowError("Selecciona una zona horaria válida.");
                return;
            }

            bool startup = ChkStartup.IsChecked == true;

            if (!int.TryParse(TxtNotificationLimit.Text.Trim(), out int notificationLimit))
            {
                ShowError("El límite de notificaciones debe ser un número válido.");
                return;
            }

            // Escribe la clave de registro para inicio con Windows
            try
            {
                SetStartupEnabled(startup);
            }
            catch (Exception ex)
            {
                ShowError($"Error al actualizar inicio con Windows: {ex.Message}");
                // Revertimos el checkbox al estado real
                ChkStartup.IsChecked = IsStartupEnabled();
                return;
            }

            Result = new AppSettings
            {
                LocalTimeZone    = tz,
                StartWithWindows = startup,
                MinimizeOnClose  = ChkMinimizeOnClose.IsChecked == true,
                PlayAlarmSound   = ChkSound.IsChecked           == true,
                Use24HourFormat  = ChkUse24HourFormat.IsChecked == true,
                MaxStoredNotifications = notificationLimit,
                Language = (CboLanguage.SelectedItem as LanguageOption)?.Code ?? LocalizationManager.Spanish,
            };

            DialogResult = true;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            var about = new AboutWindow { Owner = this };
            about.ShowDialog();
        }

        private async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
                element.IsEnabled = false;

            try
            {
                await AppUpdateService.CheckForUpdatesAsync(this, silentMode: false, showNoUpdateMessage: true);
            }
            finally
            {
                if (sender is FrameworkElement toEnable)
                    toEnable.IsEnabled = true;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        }

        // ------------------------------------------------------------------
        //  Helpers
        // ------------------------------------------------------------------
        private void ShowError(string msg)
        {
            TxtMsg.Text       = msg;
            TxtMsg.Visibility = Visibility.Visible;
        }

        // ---- Registro ----
        private static bool IsStartupEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryPath, false);
            return key?.GetValue(StartupValueName) is not null;
        }

        private static void SetStartupEnabled(bool enabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryPath, true)
                ?? throw new InvalidOperationException("No se pudo abrir la clave de registro.");

            if (enabled)
            {
                string exe = Environment.ProcessPath ?? throw new InvalidOperationException("No se pudo obtener la ruta del ejecutable.");
                key.SetValue(StartupValueName, $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue(StartupValueName, throwOnMissingValue: false);
            }
        }
    }
}
