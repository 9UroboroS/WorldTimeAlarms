using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NodaTime.TimeZones;
using TimeZoneConverter;

namespace WorldTimeAlarms
{
    public partial class TzPickerControl : UserControl
    {
        // ── Dependency property ────────────────────────────────────────────────
        public static readonly DependencyProperty SelectedTzProperty =
            DependencyProperty.Register(
                nameof(SelectedTz),
                typeof(TimeZoneInfo),
                typeof(TzPickerControl),
                new FrameworkPropertyMetadata(
                    TimeZoneInfo.Local,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedTzChanged));

        public TimeZoneInfo? SelectedTz
        {
            get => (TimeZoneInfo?)GetValue(SelectedTzProperty);
            set => SetValue(SelectedTzProperty, value);
        }

        private static void OnSelectedTzChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TzPickerControl ctrl && e.NewValue is TimeZoneInfo tz)
            {
                ctrl.SelectById(tz.Id);
                ctrl.UpdateSelectedText(tz.DisplayName);
            }
        }

        // ── Datos ─────────────────────────────────────────────────────────────
        private readonly List<TzEntry> _all;
        private readonly ObservableCollection<TzEntry> _view = [];
        private System.Windows.Controls.Primitives.Popup? _popup;

        // ── Constructor ───────────────────────────────────────────────────────
        public TzPickerControl()
        {
            InitializeComponent();
            _all = BuildEntries();

            LstZones.ItemsSource = _view;
            ApplyFilter(string.Empty);

            // Preseleccionar zona local por defecto
            SelectById(TimeZoneInfo.Local.Id);
            Loaded += (_, _) =>
            {
                _popup = FindName("DropPopup") as System.Windows.Controls.Primitives.Popup;
                // Cerrar popup al hacer click fuera (en la ventana padre)
                var window = Window.GetWindow(this);
                if (window is not null)
                    window.PreviewMouseDown += Window_PreviewMouseDown;
            };
        }

        // ── Construcción de entradas con ciudades IANA ────────────────────────────

        private static List<TzEntry> BuildEntries()
        {
            // Construir índice: IANA id → lista de ciudades limpias
            var cityMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var source = TzdbDateTimeZoneSource.Default;
                var locations = source.ZoneLocations;
                if (locations is not null)
                {
                    foreach (var loc in locations)
                    {
                        string city = CleanCityName(loc.ZoneId);
                        if (!cityMap.TryGetValue(loc.ZoneId, out var list))
                        {
                            list = [];
                            cityMap[loc.ZoneId] = list;
                        }
                        if (!list.Contains(city, StringComparer.OrdinalIgnoreCase))
                            list.Add(city);
                    }
                }
            }
            catch { /* si TZDB falla, continuamos sin ciudades */ }

            // Crear TzEntry para cada zona de Windows y asignarle sus ciudades
            var entries = new List<TzEntry>();
            foreach (var tz in TimeZoneInfo.GetSystemTimeZones())
            {
                var entry = new TzEntry(tz);
                try
                {
                    string ianaId = TZConvert.WindowsToIana(tz.Id);
                    if (cityMap.TryGetValue(ianaId, out var cities))
                        entry.Cities.AddRange(cities);
                }
                catch { }
                entries.Add(entry);
            }
            return entries;
        }

        /// <summary>Extrae el nombre de ciudad del ID IANA (ej. "America/New_York" → "New York").</summary>
        private static string CleanCityName(string ianaId)
        {
            int slash = ianaId.LastIndexOf('/');
            string raw = slash >= 0 ? ianaId[(slash + 1)..] : ianaId;
            return raw.Replace('_', ' ');
        }

        // ── API pública ───────────────────────────────────────────────────────

        /// <summary>Preselecciona la zona con el ID indicado.</summary>
        public void Preselect(TimeZoneInfo tz)
        {
            SetValue(SelectedTzProperty, tz);  // dispara OnSelectedTzChanged
        }

        // ── Lógica de filtrado ────────────────────────────────────────────────

        private void ApplyFilter(string query)
        {
            query = query.Trim();

            IEnumerable<TzEntry> source = string.IsNullOrEmpty(query)
                ? _all
                : _all.Where(e =>
                    e.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    e.Cities.Any(c => c.Contains(query, StringComparison.OrdinalIgnoreCase)));

            var favs = source.Where(e =>  e.IsFavorite).OrderBy(e => e.DisplayName);
            var rest = source.Where(e => !e.IsFavorite).OrderBy(e => e.DisplayName);

            var previousId = (LstZones.SelectedItem as TzEntry)?.Tz.Id;

            _view.Clear();
            foreach (var e in favs)
            {
                e.UpdateCityHint(query);
                _view.Add(e);
            }
            foreach (var e in rest)
            {
                e.UpdateCityHint(query);
                _view.Add(e);
            }

            if (previousId is not null)
            {
                var match = _view.FirstOrDefault(e => e.Tz.Id == previousId);
                if (match is not null) LstZones.SelectedItem = match;
            }
        }

        private void SelectById(string id)
        {
            var entry = _view.FirstOrDefault(e => e.Tz.Id == id);
            if (entry is not null)
            {
                LstZones.SelectedItem = entry;
                LstZones.ScrollIntoView(entry);
                UpdateSelectedText(entry.DisplayName);
            }
        }

        private void UpdateSelectedText(string text)
            => TxtSelected.Text = text;

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current is not null)
            {
                if (current is T match)
                    return match;

                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        // ── Eventos ───────────────────────────────────────────────────────────

        private void SelectedRow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (FindAncestor<Button>(e.OriginalSource as DependencyObject) is not null)
                return;

            TogglePanel();
        }

        private void BtnTogglePanel_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            TogglePanel();
        }

        private void TogglePanel()
        {
            if (_popup is null) return;
            bool isOpen = _popup.IsOpen;
            _popup.IsOpen = !isOpen;
            ChevronIcon.Kind = !isOpen
                ? MahApps.Metro.IconPacks.PackIconMaterialKind.ChevronUp
                : MahApps.Metro.IconPacks.PackIconMaterialKind.ChevronDown;

            if (!isOpen)
            {
                _userClicking = false;
                TxtSearch.Focus();
            }
            else
            {
                _userClicking = false;
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter(TxtSearch.Text);
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Clear();
            TxtSearch.Focus();
        }

        private void BtnToggleFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: TzEntry entry }) return;

            entry.IsFavorite = !entry.IsFavorite;
            ApplyFilter(TxtSearch.Text);

            var target = _view.FirstOrDefault(x => x.Tz.Id == entry.Tz.Id);
            if (target is not null)
            {
                LstZones.SelectedItem = target;
                LstZones.ScrollIntoView(target);
            }
        }

        private bool _userClicking;

        private void LstZones_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => _userClicking = true;

        private void LstZones_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => _userClicking = false;

        private void Window_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_popup is null || !_popup.IsOpen) return;
            // Si el click fue dentro del popup o del selector, no cerrar
            if (_popup.Child is not null && _popup.Child.IsMouseOver) return;
            if (SelectorBorder.IsMouseOver) return;
            _popup.IsOpen = false;
            _userClicking = false;
            ChevronIcon.Kind = MahApps.Metro.IconPacks.PackIconMaterialKind.ChevronDown;
        }

        private void LstZones_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_userClicking) return;
            if (LstZones.SelectedItem is TzEntry entry)
            {
                SelectedTz = entry.Tz;
                UpdateSelectedText(entry.DisplayName);
                if (_popup is not null) _popup.IsOpen = false;
                _userClicking = false;
                ChevronIcon.Kind = MahApps.Metro.IconPacks.PackIconMaterialKind.ChevronDown;
                TxtSearch.Clear();
            }
        }
    }
}
