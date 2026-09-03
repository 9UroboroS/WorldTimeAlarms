using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace WorldTimeAlarms
{
    /// <summary>ViewModel de una fila de zona horaria en TzPickerControl.</summary>
    public class TzEntry : INotifyPropertyChanged
    {
        // Favoritos compartidos entre todas las instancias durante la sesión
        private static readonly HashSet<string> _favIds = [];

        public System.TimeZoneInfo Tz          { get; }
        public string              DisplayName => Tz.DisplayName;

        /// <summary>Ciudades IANA que pertenecen a esta zona (ej. "London", "Lisbon").</summary>
        public List<string> Cities { get; } = [];

        private string _cityHint = string.Empty;

        /// <summary>
        /// Texto de ciudades a mostrar en el item (se actualiza al filtrar
        /// para resaltar solo las que coinciden con la búsqueda).
        /// </summary>
        public string CityHint
        {
            get => _cityHint;
            set
            {
                if (_cityHint == value) return;
                _cityHint = value;
                OnPropertyChanged(nameof(CityHint));
                OnPropertyChanged(nameof(CityHintVisibility));
            }
        }

        public Visibility CityHintVisibility =>
            string.IsNullOrEmpty(_cityHint) ? Visibility.Collapsed : Visibility.Visible;

        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite == value) return;
                _isFavorite = value;
                if (value) _favIds.Add(Tz.Id);
                else       _favIds.Remove(Tz.Id);

                OnPropertyChanged(nameof(IsFavorite));
                OnPropertyChanged(nameof(StarIcon));
                OnPropertyChanged(nameof(StarColor));
                OnPropertyChanged(nameof(StarVisibility));
                OnPropertyChanged(nameof(FavTooltip));
            }
        }

        public string     StarIcon       => IsFavorite ? "Star" : "StarOutline";
        public Brush      StarColor      => IsFavorite
            ? new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24))
            : new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
        public Visibility StarVisibility => IsFavorite ? Visibility.Visible : Visibility.Collapsed;
        public string     FavTooltip     => IsFavorite ? LocalizationManager.T("Str_RemoveFavorite") : LocalizationManager.T("Str_AddFavorite");

        public TzEntry(System.TimeZoneInfo tz)
        {
            Tz          = tz;
            _isFavorite = _favIds.Contains(tz.Id);
        }

        /// <summary>
        /// Actualiza CityHint mostrando las ciudades que coinciden con el query,
        /// o todas las ciudades si no hay búsqueda activa.
        /// </summary>
        public void UpdateCityHint(string query)
        {
            if (Cities.Count == 0)
            {
                CityHint = string.Empty;
                return;
            }

            IEnumerable<string> relevant = string.IsNullOrWhiteSpace(query)
                ? Cities.Take(4)   // muestra hasta 4 ciudades representativas
                : Cities.Where(c => c.Contains(query, System.StringComparison.OrdinalIgnoreCase));

            CityHint = string.Join(" · ", relevant.Take(5));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
