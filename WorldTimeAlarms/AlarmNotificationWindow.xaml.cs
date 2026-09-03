using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Resources;

namespace WorldTimeAlarms
{
    public enum AlarmPopupAction
    {
        None,
        Attended,
        Snoozed,
        Ignored
    }

    /// <summary>
    /// Ventana de notificación moderna para alarmas disparadas.
    /// Reproduce un sonido del sistema al abrirse y muestra todos
    /// los detalles de la alarma (hora, fecha, zona horaria y nota).
    /// </summary>
    public partial class AlarmNotificationWindow : Window
    {
        private readonly bool _playAlarmSound;
        private SoundPlayer? _alarmPlayer;
        private MemoryStream? _alarmWaveStream;

        public AlarmPopupAction SelectedAction { get; private set; } = AlarmPopupAction.None;
        public int SelectedSnoozeMinutes { get; private set; }

        public AlarmNotificationWindow(AlarmItem alarma, bool playAlarmSound)
        {
            InitializeComponent();
            _playAlarmSound = playAlarmSound;

            // Convertir la hora UTC de vuelta a la zona horaria original
            var horaLocal = TzdbUpdateService.ConvertFromUtc(
                alarma.HoraProgramadaUtc,
                alarma.ZonaHoraria.Id);

            TxtTime.Text = horaLocal.ToString(
                alarma.Use24HourFormat ? "HH:mm" : "hh:mm tt",
                CultureInfo.CurrentCulture);
            TxtDate.Text = horaLocal.ToString(
                "dddd, d MMMM yyyy",
                LocalizationManager.CurrentLanguage == LocalizationManager.English
                    ? CultureInfo.GetCultureInfo("en-US")
                    : CultureInfo.GetCultureInfo("es-ES"));
            TxtZone.Text = alarma.ZonaHoraria.DisplayName;

            // Nota: mostrar el panel sólo si hay contenido
            if (!string.IsNullOrWhiteSpace(alarma.Nota))
            {
                TxtNota.Text         = alarma.Nota;
                PanelNota.Visibility = Visibility.Visible;
            }

            if (!string.IsNullOrWhiteSpace(alarma.LinkUrl))
            {
                TxtLink.Text         = alarma.LinkUrl;
                PanelLink.Visibility = Visibility.Visible;
            }

            Loaded += AlarmNotificationWindow_Loaded;
            Closed += AlarmNotificationWindow_Closed;
        }

        private void AlarmNotificationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_playAlarmSound)
                return;

            StartAlarmSound();
        }

        private void AlarmNotificationWindow_Closed(object? sender, EventArgs e)
        {
            StopAlarmSound();
        }

        private void StartAlarmSound()
        {
            try
            {
                StopAlarmSound();
                _alarmWaveStream = LoadAlarmWaveStream();
                if (_alarmWaveStream is null)
                    return;

                _alarmPlayer = new SoundPlayer(_alarmWaveStream);
                _alarmPlayer.Load();
                _alarmPlayer.PlayLooping();
            }
            catch
            {
                // Si el audio falla, la alarma visual sigue funcionando.
            }
        }

        private void StopAlarmSound()
        {
            try
            {
                _alarmPlayer?.Stop();
            }
            catch
            {
            }

            _alarmPlayer?.Dispose();
            _alarmPlayer = null;

            _alarmWaveStream?.Dispose();
            _alarmWaveStream = null;
        }

        private static MemoryStream? LoadAlarmWaveStream()
        {
            var waveStream = new MemoryStream();

            StreamResourceInfo? resource = Application.GetResourceStream(
                new Uri("pack://application:,,,/alarma.wav", UriKind.Absolute));

            if (resource is null)
                return null;

            using (resource.Stream)
            {
                resource.Stream.CopyTo(waveStream);
            }

            waveStream.Position = 0;
            return waveStream;
        }

        private void BtnAceptar_Click(object sender, RoutedEventArgs e)
        {
            SelectedAction = AlarmPopupAction.Attended;
            Close();
        }

        private void BtnIgnorar_Click(object sender, RoutedEventArgs e)
        {
            SelectedAction = AlarmPopupAction.Ignored;
            Close();
        }

        private void BtnSnooze_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: string minutesText }
                || !int.TryParse(minutesText, out int minutes))
            {
                return;
            }

            SelectedAction = AlarmPopupAction.Snoozed;
            SelectedSnoozeMinutes = minutes;
            Close();
        }

        private void LnkWeb_Click(object sender, RoutedEventArgs e)
        {
            string url = TxtLink.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignorar errores al abrir el navegador.
            }
        }
    }
}
