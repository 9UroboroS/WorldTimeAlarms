using System;
using System.Windows;

namespace WorldTimeAlarms
{
    public partial class AddClockWindow : Window
    {
        public WorldClockItem? Result { get; private set; }

        private readonly WorldClockItem? _editing;

        // ── Agregar nuevo reloj ────────────────────────────────────────────────
        public AddClockWindow()
        {
            InitializeComponent();
            // TzPickerControl ya preselecciona TimeZoneInfo.Local por defecto
        }

        // ── Editar reloj existente ─────────────────────────────────────────────
        public AddClockWindow(WorldClockItem item)
        {
            InitializeComponent();
            _editing      = item;
            TxtTitle.Text = LocalizationManager.T("Str_EditClockTitle");
            TxtLabel.Text = item.Label;
            TzPicker.Preselect(item.TimeZone);
        }

        // ── Eventos ───────────────────────────────────────────────────────────

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string label = TxtLabel.Text.Trim();
            if (string.IsNullOrEmpty(label))
            {
                ShowError("Escribe un nombre o ciudad.");
                return;
            }

            if (TzPicker.SelectedTz is not TimeZoneInfo tz)
            {
                ShowError("Selecciona una zona horaria.");
                return;
            }

            if (_editing is not null)
            {
                _editing.Label    = label;
                _editing.TimeZone = tz;
                Result = _editing;
            }
            else
            {
                Result = new WorldClockItem { Label = label, TimeZone = tz };
            }

            DialogResult = true;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowError(string msg)
        {
            TxtError.Text       = msg;
            TxtError.Visibility = Visibility.Visible;
        }
    }
}
