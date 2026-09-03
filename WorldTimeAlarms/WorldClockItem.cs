using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WorldTimeAlarms
{
    /// <summary>
    /// Representa un reloj mundial visible en el footer.
    /// Es observable para que los bindings de la UI se actualicen automáticamente.
    /// </summary>
    public partial class WorldClockItem : ObservableObject
    {
        [ObservableProperty] private string _label = string.Empty;
        [ObservableProperty] private TimeZoneInfo _timeZone = TimeZoneInfo.Local;

        // Actualizado cada segundo por UpdateClocks()
        [ObservableProperty] private string _timeText  = "--:--:--";
        [ObservableProperty] private string _dateText  = string.Empty;
        [ObservableProperty] private string _offsetText = string.Empty;
    }
}
