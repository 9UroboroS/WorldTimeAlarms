using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace WorldTimeAlarms
{
    public sealed class ReuseAlarmResult
    {
        public DateTime HoraProgramada { get; init; }
        public DateTime HoraProgramadaUtc { get; init; }
        public TimeZoneInfo ZonaHoraria { get; init; } = TimeZoneInfo.Local;
        public string Nota { get; init; } = string.Empty;
        public string LinkUrl { get; init; } = string.Empty;
        public bool Use24HourFormat { get; init; }
    }

    public partial class ReuseAlarmWindow : Window
    {
        private readonly bool _use24HourFormat;
        private bool _formattingTime;

        public ReuseAlarmResult? Result { get; private set; }

        public ReuseAlarmWindow(AlarmHistoryItem item)
            : this(item, item.Use24HourFormat)
        {
        }

        public ReuseAlarmWindow(AlarmHistoryItem item, bool use24HourFormat)
        {
            InitializeComponent();
            _use24HourFormat = use24HourFormat;

            UpdateTimeInputHints();

            DateTime localTime = TzdbUpdateService.ConvertFromUtc(item.HoraProgramadaUtc, item.ZonaHoraria.Id);
            DpkAlarmDate.SelectedDate = localTime.Date;
            TxtAlarmTime.Text = localTime.ToString(_use24HourFormat ? "HH:mm" : "hh:mm tt", CultureInfo.CurrentCulture);
            AlarmTzPicker.Preselect(item.ZonaHoraria);
            TxtAlarmNote.Text = item.Nota;
            TxtAlarmLink.Text = item.LinkUrl;
            TxtSourceSummary.Text = $"{item.TimeText} · {item.DateText} · {item.ZoneName} · {item.EstadoLabel}";
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            TxtValidation.Visibility = Visibility.Collapsed;
            string linkText = TxtAlarmLink.Text.Trim();

            if (DpkAlarmDate.SelectedDate is not DateTime selectedDate)
            {
                ShowValidation("Por favor selecciona una fecha.");
                return;
            }

            if (!TryParseTime(TxtAlarmTime.Text.Trim(), out TimeSpan timeSpan))
            {
                ShowValidation(_use24HourFormat
                    ? "Hora inválida. Usa el formato HH:MM (ej. 07:30)."
                    : "Hora inválida. Usa el formato hh:mm AM/PM.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(linkText)
                && (!Uri.TryCreate(linkText, UriKind.Absolute, out Uri? uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            {
                ShowValidation("El enlace web debe ser una URL válida http o https.");
                return;
            }

            TimeZoneInfo tz = AlarmTzPicker.SelectedTz ?? TimeZoneInfo.Local;
            DateTime horaProgramada = DateTime.SpecifyKind(selectedDate.Date + timeSpan, DateTimeKind.Unspecified);
            DateTime ahoraEnZona = TzdbUpdateService.ConvertFromUtc(DateTime.UtcNow, tz.Id);

            if (horaProgramada <= ahoraEnZona)
            {
                ShowValidation("La hora programada ya pasó en la zona horaria seleccionada. Elige una fecha/hora futura.");
                return;
            }

            DateTime horaProgramadaUtc = new DateTimeOffset(
                horaProgramada,
                tz.GetUtcOffset(horaProgramada)).UtcDateTime;

            Result = new ReuseAlarmResult
            {
                HoraProgramada = horaProgramada,
                HoraProgramadaUtc = horaProgramadaUtc,
                ZonaHoraria = tz,
                Nota = TxtAlarmNote.Text.Trim(),
                LinkUrl = linkText,
                Use24HourFormat = _use24HourFormat
            };

            DialogResult = true;
            Close();
        }

        private void TxtAlarmTime_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_formattingTime) return;
            _formattingTime = true;

            string oldTextBeforeCaret = TxtAlarmTime.Text[..TxtAlarmTime.CaretIndex];
            int digitsBefore = CountDigits(oldTextBeforeCaret);
            int lettersBefore = CountLetters(oldTextBeforeCaret);

            string formatted = FormatTimeInput(TxtAlarmTime.Text, _use24HourFormat);

            TxtAlarmTime.Text = formatted;
            TxtAlarmTime.CaretIndex = CaretIndexForCounts(formatted, digitsBefore, lettersBefore);

            _formattingTime = false;
        }

        private static int CountDigits(string text)
        {
            int count = 0;
            foreach (char c in text)
                if (char.IsDigit(c)) count++;
            return count;
        }

        private static int CountLetters(string text)
        {
            int count = 0;
            foreach (char c in text)
                if (char.IsLetter(c)) count++;
            return count;
        }

        private static int CaretIndexForCounts(string text, int digitCount, int letterCount)
        {
            if (letterCount > 0)
            {
                int seenLetters = 0;
                for (int i = 0; i < text.Length; i++)
                {
                    if (char.IsLetter(text[i]))
                    {
                        seenLetters++;
                        if (seenLetters == letterCount) return i + 1;
                    }
                }
                return text.Length;
            }

            if (digitCount <= 0) return 0;

            int seenDigits = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsDigit(text[i]))
                {
                    seenDigits++;
                    if (seenDigits == digitCount) return i + 1;
                }
            }
            return text.Length;
        }

        private static string FormatTimeInput(string raw, bool use24HourFormat)
        {
            string digits = Regex.Replace(raw, @"\D", string.Empty);
            if (digits.Length > 4) digits = digits[..4];

            string timePart = digits.Length >= 3
                ? digits[..2] + ":" + digits[2..]
                : digits;

            if (use24HourFormat)
                return timePart;

            string letters = new string(raw.Where(char.IsLetter).ToArray()).ToUpperInvariant();
            if (letters.Length > 2) letters = letters[..2];

            return letters switch
            {
                "A" or "P" or "AM" or "PM" => timePart + " " + letters,
                "" => timePart,
                _ => timePart
            };
        }

        private void UpdateTimeInputHints()
        {
            TxtAlarmTimeLabel.Text = _use24HourFormat
                ? "Hora (HH:MM)"
                : "Hora (hh:mm AM/PM)";
        }

        private bool TryParseTime(string input, out TimeSpan result)
        {
            result = default;

            if (!_use24HourFormat)
            {
                string normalized = Regex.Replace(input.Trim(), @"\s+", " ").ToUpperInvariant();

                if (DateTime.TryParseExact(
                        normalized,
                        ["h:mm tt", "hh:mm tt", "htt", "h tt", "hhmm tt", "hmmtt", "hhmmtt"],
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime parsed12))
                {
                    result = parsed12.TimeOfDay;
                    return true;
                }

                return false;
            }

            string digits = Regex.Replace(input, @"\D", string.Empty);

            if (digits.Length is < 3 or > 4)
                return false;

            int hh = int.Parse(digits.Length == 3 ? digits[..1] : digits[..2]);
            int mm = int.Parse(digits.Length == 3 ? digits[1..] : digits[2..]);

            if (hh > 23 || mm > 59)
                return false;

            result = new TimeSpan(hh, mm, 0);
            return true;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowValidation(string message)
        {
            TxtValidation.Text = message;
            TxtValidation.Visibility = Visibility.Visible;
        }
    }
}
