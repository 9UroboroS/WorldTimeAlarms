using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using Hardcodet.Wpf.TaskbarNotification;
using TimeZoneConverter;

namespace WorldTimeAlarms
{
    public enum AlarmHandlingState
    {
        Pending,
        Snoozed,
        Attended,
        Missed
    }

    public sealed class AlarmStorageItem
    {
        public DateTime HoraProgramadaUtc { get; set; }
        public string TimeZoneId { get; set; } = string.Empty;
        public string Nota { get; set; } = string.Empty;
        public string LinkUrl { get; set; } = string.Empty;
        public bool Use24HourFormat { get; set; }
        public AlarmHandlingState HandlingState { get; set; }
        public int LastSnoozeMinutes { get; set; }
        public bool EsActiva { get; set; }
    }

    public sealed class AlarmHistoryItem
    {
        public DateTime HoraProgramadaUtc { get; set; }
        public DateTime FinalizedAtUtc { get; set; }
        public TimeZoneInfo ZonaHoraria { get; set; } = TimeZoneInfo.Local;
        public string Nota { get; set; } = string.Empty;
        public string LinkUrl { get; set; } = string.Empty;
        public bool Use24HourFormat { get; set; }
        public AlarmHandlingState FinalState { get; set; }
        public bool IsFavorite { get; set; }
        public bool IsSelected { get; set; }

        public string TimeText => TzdbUpdateService.ConvertFromUtc(HoraProgramadaUtc, ZonaHoraria.Id)
            .ToString(Use24HourFormat ? "HH:mm" : "hh:mm tt", CultureInfo.CurrentCulture);

        public string DateText => TzdbUpdateService.ConvertFromUtc(HoraProgramadaUtc, ZonaHoraria.Id)
            .ToString("ddd d MMM yyyy", CultureInfo.CurrentCulture);

        public string ZoneName => ZonaHoraria.DisplayName;

        public string EstadoLabel => FinalState switch
        {
            AlarmHandlingState.Attended => LocalizationManager.T("Str_StateAttended"),
            AlarmHandlingState.Missed => LocalizationManager.T("Str_StateNotAttended"),
            AlarmHandlingState.Snoozed => LocalizationManager.T("Str_StateSnoozed"),
            _ => LocalizationManager.T("Str_StatePending")
        };

        public SolidColorBrush EstadoLabelBrush => FinalState switch
        {
            AlarmHandlingState.Snoozed => new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA)),
            AlarmHandlingState.Attended => new SolidColorBrush(Color.FromRgb(0x86, 0xEF, 0xAC)),
            AlarmHandlingState.Missed => new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71)),
            _ => new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80))
        };

        public SolidColorBrush EstadoLabelBackgroundBrush => FinalState switch
        {
            AlarmHandlingState.Snoozed => new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x5F)),
            AlarmHandlingState.Attended => new SolidColorBrush(Color.FromRgb(0x14, 0x33, 0x20)),
            AlarmHandlingState.Missed => new SolidColorBrush(Color.FromRgb(0x45, 0x1A, 0x1A)),
            _ => new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37))
        };

        public Visibility NoteVisibility => string.IsNullOrWhiteSpace(Nota)
            ? Visibility.Collapsed : Visibility.Visible;

        public Visibility LinkVisibility => string.IsNullOrWhiteSpace(LinkUrl)
            ? Visibility.Collapsed : Visibility.Visible;

        public string FavoriteIcon => IsFavorite ? "Star" : "StarOutline";
        public SolidColorBrush FavoriteBrush => IsFavorite
            ? new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24))
            : new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
    }

    public sealed class AlarmHistoryStorageItem
    {
        public DateTime HoraProgramadaUtc { get; set; }
        public DateTime FinalizedAtUtc { get; set; }
        public string TimeZoneId { get; set; } = string.Empty;
        public string Nota { get; set; } = string.Empty;
        public string LinkUrl { get; set; } = string.Empty;
        public bool Use24HourFormat { get; set; }
        public AlarmHandlingState FinalState { get; set; }
        public bool IsFavorite { get; set; }
    }

    public sealed class PersistedAppSettings
    {
        public bool MinimizeOnClose { get; set; } = true;
        public bool PlayAlarmSound { get; set; } = true;
        public bool Use24HourFormat { get; set; } = true;
        public int MaxStoredNotifications { get; set; } = 40;
        public string Language { get; set; } = LocalizationManager.Spanish;
    }

    // ---------------------------------------------------------------------------
    //  AlarmItem — Modelo de datos de una alarma individual.
    //  Hereda de ObservableObject (CommunityToolkit.Mvvm) para eliminar el
    //  boilerplate de INotifyPropertyChanged.
    // ---------------------------------------------------------------------------
    public partial class AlarmItem : ObservableObject
    {
        public DateTime     HoraProgramada    { get; set; }
        public DateTime     HoraProgramadaUtc { get; set; }
        public TimeZoneInfo ZonaHoraria       { get; set; } = TimeZoneInfo.Local;
        public string       Nota              { get; set; } = string.Empty;
        public string       LinkUrl           { get; set; } = string.Empty;

        private bool _use24HourFormat = true;

        public bool Use24HourFormat
        {
            get => _use24HourFormat;
            set
            {
                if (SetProperty(ref _use24HourFormat, value))
                    OnPropertyChanged(nameof(TimeText));
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BadgeColor))]
        [NotifyPropertyChangedFor(nameof(ItemOpacity))]
        [NotifyPropertyChangedFor(nameof(EstadoLabel))]
        [NotifyPropertyChangedFor(nameof(EstadoLabelVisibility))]
        [NotifyPropertyChangedFor(nameof(CountdownText))]
        [NotifyPropertyChangedFor(nameof(CountdownVisibility))]
        private bool _esActiva = true;

        private TimeSpan _remaining = TimeSpan.Zero;

        public TimeSpan Remaining
        {
            get => _remaining;
            set
            {
                if (SetProperty(ref _remaining, value))
                {
                    OnPropertyChanged(nameof(BadgeColor));
                    OnPropertyChanged(nameof(CountdownText));
                }
            }
        }

        public string TimeText => HoraProgramada.ToString(Use24HourFormat ? "HH:mm" : "hh:mm tt", CultureInfo.CurrentCulture);
        public string DateText => HoraProgramada.ToString("ddd d MMM yyyy", CultureInfo.CurrentCulture);
        public string ZoneName => ZonaHoraria.DisplayName;

        public Visibility NoteVisibility => string.IsNullOrWhiteSpace(Nota)
            ? Visibility.Collapsed : Visibility.Visible;

        public Visibility LinkVisibility => string.IsNullOrWhiteSpace(LinkUrl)
            ? Visibility.Collapsed : Visibility.Visible;

        private AlarmHandlingState _handlingState = AlarmHandlingState.Pending;
        public AlarmHandlingState HandlingState
        {
            get => _handlingState;
            set
            {
                if (SetProperty(ref _handlingState, value))
                {
                    OnPropertyChanged(nameof(EstadoLabel));
                    OnPropertyChanged(nameof(EstadoLabelBrush));
                    OnPropertyChanged(nameof(EstadoLabelBackgroundBrush));
                    OnPropertyChanged(nameof(EstadoLabelVisibility));
                    OnPropertyChanged(nameof(ManageActionsVisibility));
                }
            }
        }

        private int _lastSnoozeMinutes;
        public int LastSnoozeMinutes
        {
            get => _lastSnoozeMinutes;
            set
            {
                if (SetProperty(ref _lastSnoozeMinutes, value))
                {
                    OnPropertyChanged(nameof(EstadoLabel));
                    OnPropertyChanged(nameof(EstadoLabelBrush));
                    OnPropertyChanged(nameof(EstadoLabelBackgroundBrush));
                }
            }
        }

        public string CountdownText
        {
            get
            {
                if (!EsActiva)
                    return string.Empty;

                TimeSpan safeRemaining = Remaining < TimeSpan.Zero ? TimeSpan.Zero : Remaining;

                if (safeRemaining >= TimeSpan.FromDays(1))
                    return LocalizationManager.T("Str_CountdownDaysHours", safeRemaining.Days, safeRemaining.Hours.ToString("00"));

                if (safeRemaining >= TimeSpan.FromHours(1))
                    return LocalizationManager.T("Str_CountdownHoursMin", safeRemaining.Hours.ToString("00"), safeRemaining.Minutes.ToString("00"));

                if (safeRemaining >= TimeSpan.FromMinutes(1))
                    return LocalizationManager.T("Str_CountdownMinSec", safeRemaining.Minutes.ToString("00"), safeRemaining.Seconds.ToString("00"));

                return LocalizationManager.T("Str_CountdownSec", safeRemaining.Seconds.ToString("00"));
            }
        }

        public Visibility CountdownVisibility => EsActiva
            ? Visibility.Visible : Visibility.Collapsed;

        public Visibility ManageActionsVisibility => HandlingState == AlarmHandlingState.Attended
            ? Visibility.Collapsed : Visibility.Visible;

        public SolidColorBrush BadgeColor
        {
            get
            {
                if (!EsActiva)
                    return new SolidColorBrush(Color.FromRgb(0x4B, 0x55, 0x63));

                if (Remaining > TimeSpan.FromHours(1))
                    return new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));

                if (Remaining > TimeSpan.FromMinutes(20))
                    return new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));

                return new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            }
        }

        public double     ItemOpacity           => EsActiva ? 1.0 : 0.45;
        public string EstadoLabel => HandlingState switch
        {
            AlarmHandlingState.Snoozed  => LocalizationManager.T("Str_StateSnoozedMin", LastSnoozeMinutes),
            AlarmHandlingState.Attended => LocalizationManager.T("Str_StateAttended"),
            AlarmHandlingState.Missed   => LocalizationManager.T("Str_StateNotAttended"),
            _                           => string.Empty
        };

        public SolidColorBrush EstadoLabelBrush => HandlingState switch
        {
            AlarmHandlingState.Snoozed  => new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA)),
            AlarmHandlingState.Attended => new SolidColorBrush(Color.FromRgb(0x86, 0xEF, 0xAC)),
            AlarmHandlingState.Missed   => new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71)),
            _                           => new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80))
        };

        public SolidColorBrush EstadoLabelBackgroundBrush => HandlingState switch
        {
            AlarmHandlingState.Snoozed  => new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x5F)),
            AlarmHandlingState.Attended => new SolidColorBrush(Color.FromRgb(0x14, 0x33, 0x20)),
            AlarmHandlingState.Missed   => new SolidColorBrush(Color.FromRgb(0x45, 0x1A, 0x1A)),
            _                           => new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37))
        };

        public Visibility EstadoLabelVisibility => string.IsNullOrWhiteSpace(EstadoLabel)
            ? Visibility.Collapsed : Visibility.Visible;

        public void MarkAttended()
        {
            EsActiva      = false;
            Remaining     = TimeSpan.Zero;
            HandlingState = AlarmHandlingState.Attended;
        }

        public void MarkMissed()
        {
            EsActiva      = false;
            Remaining     = TimeSpan.Zero;
            HandlingState = AlarmHandlingState.Missed;
        }

        public void Snooze(int minutes)
        {
            HoraProgramadaUtc = HoraProgramadaUtc.AddMinutes(minutes);
            HoraProgramada    = TzdbUpdateService.ConvertFromUtc(HoraProgramadaUtc, ZonaHoraria.Id);
            Remaining         = HoraProgramadaUtc - DateTime.UtcNow;
            LastSnoozeMinutes = minutes;
            HandlingState     = AlarmHandlingState.Snoozed;
            EsActiva          = true;

            OnPropertyChanged(nameof(TimeText));
            OnPropertyChanged(nameof(DateText));
        }
    }

    // ---------------------------------------------------------------------------
    //  MainWindow — Code-behind principal
    // ---------------------------------------------------------------------------
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _timer = new()
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        private readonly ObservableCollection<AlarmItem> _alarms = [];
        private readonly ObservableCollection<AlarmHistoryItem> _alarmHistory = [];
        private readonly ObservableCollection<WorldClockItem> _worldClocks = [];
        private readonly HashSet<string> _nearAlarmNotificationKeys = [];
        private HistoryWindow? _historyWindow;
        private Popup? _notificationsPopup;
        private ListBox? _notificationsList;
        private Border? _notificationBadge;
        private TextBlock? _notificationBadgeText;
        private TextBlock? _noNotificationsText;
        private TextBlock? _notificationPreviewText;
        private Button? _notificationsButton;
        private ComboBox? _notificationFilterCombo;
        private ComboBox? _notificationScopeCombo;
        private ICollectionView? _notificationsView;

        private bool _dialogOpen;
        private bool _allowClose;
        private bool _minimizeOnClose = true;
        private bool _playAlarmSound  = true;
        private bool _use24HourFormat = true;
        public  bool AllowClose { get => _allowClose; set => _allowClose = value; }

        private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupValueName = "WorldTimeAlarms";
        private static readonly string AppDataFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WorldTimeAlarms");
        private static readonly string AlarmsFilePath = Path.Combine(AppDataFolderPath, "alarms.json");
        private static readonly string HistoryFilePath = Path.Combine(AppDataFolderPath, "history.json");
        private static readonly string SettingsFilePath = Path.Combine(AppDataFolderPath, "settings.json");
        private static readonly string AlarmsBackupFilePath = AlarmsFilePath + ".bak";
        private static readonly string HistoryBackupFilePath = HistoryFilePath + ".bak";
        private static readonly string SettingsBackupFilePath = SettingsFilePath + ".bak";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        // ----------------------------------------------------------------------
        //  Constructor
        // ----------------------------------------------------------------------
        public MainWindow()
        {
            InitializeComponent();
            InitializeTimeZoneCombos();
            InitializeAlarmList();
            InitializeWorldClocks();
            InitializeNotifications();
            LoadSettings();
            UpdateTimeInputHints();
            LoadPersistedData();

            UpdateClocks();
            _timer.Tick += Timer_Tick;
            _timer.Start();

            Dispatcher.BeginInvoke(new Action(async () =>
            {
                await AppUpdateService.CheckForUpdatesAsync(this, silentMode: true, showNoUpdateMessage: false);
            }), DispatcherPriority.Background);
        }

        // ----------------------------------------------------------------------
        //  Inicializacion
        // ----------------------------------------------------------------------
        private void InitializeWorldClocks()
        {
            _worldClocks.Add(new WorldClockItem
            {
                Label    = "Nueva York",
                TimeZone = GetTz("Eastern Standard Time", "America/New_York")
            });
            _worldClocks.Add(new WorldClockItem
            {
                Label    = "Chicago",
                TimeZone = GetTz("Central Standard Time", "America/Chicago")
            });
            _worldClocks.Add(new WorldClockItem
            {
                Label    = "Los \u00C1ngeles",
                TimeZone = GetTz("Pacific Standard Time", "America/Los_Angeles")
            });

            IcWorldClocks.ItemsSource = _worldClocks;
        }

        private void InitializeTimeZoneCombos()
        {
            var allZones = TimeZoneInfo.GetSystemTimeZones();

            CboLocalZone.ItemsSource       = allZones;
            CboLocalZone.DisplayMemberPath = nameof(TimeZoneInfo.DisplayName);

            CboLocalZone.SelectedItem = TimeZoneInfo.Local;
        }

        private void InitializeAlarmList()
        {
            LstAlarms.ItemsSource = _alarms;
            _alarms.CollectionChanged += (_, _) => RefreshAlarmCount();
            RefreshAlarmCount();
            DpkAlarmDate.SelectedDate = DateTime.Today;
        }

        private void InitializeNotifications()
        {
            _notificationsPopup = FindName("NotificationsPopup") as Popup;
            _notificationsList = FindName("LstNotifications") as ListBox;
            _notificationBadge = FindName("NotificationBadge") as Border;
            _notificationBadgeText = FindName("TxtNotificationBadge") as TextBlock;
            _noNotificationsText = FindName("TxtNoNotifications") as TextBlock;
            _notificationPreviewText = FindName("TxtNotificationPreview") as TextBlock;
            _notificationsButton = FindName("BtnNotifications") as Button;
            _notificationFilterCombo = FindName("CboNotificationFilter") as ComboBox;
            _notificationScopeCombo = FindName("CboNotificationScope") as ComboBox;

            _notificationsView = CollectionViewSource.GetDefaultView(NotificationCenter.Items);
            if (_notificationsView is not null)
                _notificationsView.Filter = FilterNotificationItem;

            if (_notificationsList is not null)
                _notificationsList.ItemsSource = _notificationsView is not null
                    ? _notificationsView
                    : NotificationCenter.Items;

            NotificationCenter.NotificationsChanged += NotificationCenter_NotificationsChanged;
            UpdateNotificationUi();
        }

        private bool FilterNotificationItem(object obj)
        {
            if (obj is not AppNotificationItem item)
                return false;

            int scopeIndex = _notificationScopeCombo?.SelectedIndex ?? 0;
            int filterIndex = _notificationFilterCombo?.SelectedIndex ?? 0;
            bool categoryMatch = filterIndex switch
            {
                1 => item.Category.Equals("Alarmas", StringComparison.OrdinalIgnoreCase),
                2 => item.Category.Equals("TZDB", StringComparison.OrdinalIgnoreCase),
                _ => true
            };

            bool scopeMatch = scopeIndex switch
            {
                0 => !item.IsRead,
                _ => true
            };

            return categoryMatch && scopeMatch;
        }

        private void NotificationCenter_NotificationsChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(UpdateNotificationUi);
        }

        private void UpdateNotificationUi()
        {
            int unread = NotificationCenter.UnreadCount;
            _notificationsView?.Refresh();

            if (_notificationBadge is not null)
                _notificationBadge.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;

            if (_notificationBadgeText is not null)
                _notificationBadgeText.Text = unread > 99 ? "99+" : unread.ToString();

            UpdateTaskbarNotificationOverlay(unread);

            if (_notificationsButton is not null)
            {
                _notificationsButton.ToolTip = unread > 0
                    ? $"Notificaciones ({unread} sin leer)"
                    : "Notificaciones";
            }

            if (_notificationPreviewText is not null)
            {
                AppNotificationItem? latest = NotificationCenter.LatestNotification;
                _notificationPreviewText.Text = latest is null
                    ? "Sin novedades"
                    : $"{latest.Title}: {latest.Message}";
            }

            if (_noNotificationsText is not null)
            {
                int visibleCount = _notificationsView?.Cast<object>().Count() ?? NotificationCenter.Items.Count;
                _noNotificationsText.Visibility = visibleCount == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        // ----------------------------------------------------------------------
        //  Timer tick
        // ----------------------------------------------------------------------
        private void Timer_Tick(object? sender, EventArgs e) => UpdateClocks();

        private void UpdateClocks()
        {
            DateTime utcNow = DateTime.UtcNow;

            TimeZoneInfo localTz  = CboLocalZone.SelectedItem as TimeZoneInfo ?? TimeZoneInfo.Local;
            DateTime     localNow = TzdbUpdateService.ConvertFromUtc(utcNow, localTz.Id);

            TxtLocalTime.Text     = FormatClockTime(localNow, includeSeconds: true);
            TxtLocalDate.Text     = FormatLongDate(localNow);
            TxtLocalZoneName.Text = localTz.DisplayName;

            foreach (WorldClockItem wc in _worldClocks)
                UpdateWorldClock(wc, utcNow, _use24HourFormat);

            foreach (AlarmItem alarm in _alarms)
            {
                UpdateAlarmCountdown(alarm, utcNow);
                PublishNearAlarmNotification(alarm);
            }

            if (_dialogOpen) return;

            var disparadas = _alarms
                .Where(a => a.EsActiva && utcNow >= a.HoraProgramadaUtc)
                .ToList();

            foreach (AlarmItem alarma in disparadas)
                FireAlarm(alarma);
        }

        private void PublishNearAlarmNotification(AlarmItem alarm)
        {
            if (!alarm.EsActiva)
                return;

            TimeSpan remaining = alarm.HoraProgramadaUtc - DateTime.UtcNow;
            string key = $"near-alarm:{alarm.HoraProgramadaUtc.Ticks}:{alarm.ZonaHoraria.Id}:{alarm.Nota}";

            if (remaining > TimeSpan.FromMinutes(15) || remaining <= TimeSpan.Zero)
            {
                if (remaining <= TimeSpan.Zero)
                    _nearAlarmNotificationKeys.Remove(key);

                return;
            }

            if (!_nearAlarmNotificationKeys.Add(key))
                return;

            string title = string.IsNullOrWhiteSpace(alarm.Nota)
                ? LocalizationManager.T("Str_UpcomingAlarm")
                : LocalizationManager.T("Str_UpcomingAlarmNote", alarm.Nota);

            string message = LocalizationManager.T("Str_UpcomingAlarmMessage",
                Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes)), alarm.TimeText);

            NotificationCenter.Publish(
                title,
                message,
                AppNotificationKind.Warning,
                "Alarmas",
                key);
        }

        // ----------------------------------------------------------------------
        //  FireAlarm — muestra la ventana de notificacion (modal)
        // ----------------------------------------------------------------------
        private void FireAlarm(AlarmItem alarma)
        {
            _dialogOpen = true;
            try
            {
                // BalloonTip cuando la app esta minimizada en la bandeja
                if (WindowState == WindowState.Minimized
                    && Application.Current.FindResource("TrayIcon") is TaskbarIcon tray)
                {
                    tray.ShowBalloonTip(
                        title:   LocalizationManager.T("Str_AlarmFired"),
                        message: string.IsNullOrWhiteSpace(alarma.Nota)
                                     ? $"{alarma.ZonaHoraria.Id} — {alarma.HoraProgramada:HH:mm}"
                                     : alarma.Nota,
                        symbol:  BalloonIcon.Warning);
                }

                var dlg = new AlarmNotificationWindow(alarma, _playAlarmSound) { Owner = this };
                dlg.ShowDialog();

                switch (dlg.SelectedAction)
                {
                    case AlarmPopupAction.Attended:
                        alarma.MarkAttended();
                        MoveAlarmToHistory(alarma);
                        break;

                    case AlarmPopupAction.Snoozed:
                        alarma.Snooze(dlg.SelectedSnoozeMinutes);
                        SaveActiveAlarms();
                        break;

                    case AlarmPopupAction.Ignored:
                        alarma.MarkMissed();
                        MoveAlarmToHistory(alarma);
                        break;

                    default:
                        alarma.MarkMissed();
                        MoveAlarmToHistory(alarma);
                        break;
                }
            }
            finally
            {
                _dialogOpen = false;
            }
        }

        // Minimizar a bandeja desde el boton de la barra
        private void BtnMinimizeToTray_Click(object sender, RoutedEventArgs e) => Hide();

        // Ocultar en lugar de minimizar si el usuario usa el botón del SO
        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized && _minimizeOnClose)
                Hide();
        }

        // Interceptar cierre — solo cerrar realmente cuando AllowClose = true o no minimiza
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_allowClose && _minimizeOnClose)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnClosing(e);
            NotificationCenter.NotificationsChanged -= NotificationCenter_NotificationsChanged;
        }

        private void BtnNotifications_Click(object sender, RoutedEventArgs e)
        {
            if (_notificationsPopup is null)
                return;

            _notificationsPopup.IsOpen = !_notificationsPopup.IsOpen;
            if (_notificationsPopup.IsOpen)
                UpdateNotificationUi();
        }

        private void BtnDismissNotification_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: AppNotificationItem item })
                NotificationCenter.Remove(item);
        }

        private void BtnClearNotifications_Click(object sender, RoutedEventArgs e)
        {
            NotificationCenter.Clear();
        }

        private void BtnMarkAllNotificationsRead_Click(object sender, RoutedEventArgs e)
        {
            NotificationCenter.MarkAllAsRead();
        }

        private void BtnEmptyNotifications_Click(object sender, RoutedEventArgs e)
        {
            NotificationCenter.Clear();
        }

        private void BtnMarkNotificationRead_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: AppNotificationItem item })
                NotificationCenter.MarkAsRead(item);
        }

        private void CboNotificationFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateNotificationUi();
        }

        private void CboNotificationScope_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateNotificationUi();
        }

        private void UpdateTaskbarNotificationOverlay(int unread)
        {
            if (TaskbarItemInfo is null)
                return;

            if (unread <= 0)
            {
                TaskbarItemInfo.Overlay = null;
                return;
            }

            string badgeText = unread > 99 ? "99+" : unread.ToString();
            const int size = 64;

            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawEllipse(
                    new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                    new Pen(new SolidColorBrush(Color.FromRgb(0x10, 0x14, 0x1F)), 4),
                    new Point(size / 2.0, size / 2.0),
                    28,
                    28);

                var formatted = new FormattedText(
                    badgeText,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    badgeText.Length > 2 ? 22 : 28,
                    Brushes.White,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                dc.DrawText(formatted,
                    new Point((size - formatted.Width) / 2, (size - formatted.Height) / 2 - 1));
            }

            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            TaskbarItemInfo.Overlay = bitmap;
        }

        // ----------------------------------------------------------------------
        //  Relojes mundiales
        // ----------------------------------------------------------------------
        private static void UpdateWorldClock(WorldClockItem wc, DateTime utcNow, bool use24HourFormat)
        {
            DateTime local  = TzdbUpdateService.ConvertFromUtc(utcNow, wc.TimeZone.Id);
            wc.TimeText     = local.ToString(use24HourFormat ? "HH:mm:ss" : "hh:mm:ss tt", CultureInfo.CurrentCulture);
            wc.DateText     = local.ToString("ddd d MMM", CultureInfo.CurrentCulture);

            TimeSpan offset = wc.TimeZone.GetUtcOffset(utcNow);
            string   sign   = offset < TimeSpan.Zero ? "-" : "+";
            int      hours  = Math.Abs((int)offset.TotalHours);
            wc.OffsetText   = $"UTC{sign}{hours}";
        }

        private static void UpdateAlarmCountdown(AlarmItem alarm, DateTime utcNow)
        {
            if (!alarm.EsActiva)
            {
                alarm.Remaining = TimeSpan.Zero;
                return;
            }

            alarm.Remaining = alarm.HoraProgramadaUtc - utcNow;
        }

        private void BtnAddClock_Click(object sender, RoutedEventArgs e)
        {
            var win = new AddClockWindow { Owner = this };
            if (win.ShowDialog() == true && win.Result is WorldClockItem item)
            {
                _worldClocks.Add(item);
                UpdateWorldClock(item, DateTime.UtcNow, _use24HourFormat);
            }
        }

        private void BtnEditClock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: WorldClockItem item }) return;
            var win = new AddClockWindow(item) { Owner = this };
            win.ShowDialog();
            // El item ya fue modificado en el constructor de AddClockWindow (modo edit)
        }

        private void BtnDeleteClock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: WorldClockItem item })
                _worldClocks.Remove(item);
        }

        // ----------------------------------------------------------------------
        //  Eventos UI
        // ----------------------------------------------------------------------
        private void CboLocalZone_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => UpdateClocks();

        private void BtnSaveAlarm_Click(object sender, RoutedEventArgs e)
        {
            TxtValidation.Visibility = Visibility.Collapsed;
            string linkText = TxtAlarmLink.Text.Trim();

            if (DpkAlarmDate.SelectedDate is not DateTime selectedDate)
            {
                ShowValidation(LocalizationManager.T("Str_ValSelectDate"));
                return;
            }

            if (!TryParseTime(TxtAlarmTime.Text.Trim(), out TimeSpan timeSpan))
            {
                ShowValidation(LocalizationManager.T("Str_ValInvalidTime"));
                return;
            }

            if (!string.IsNullOrWhiteSpace(linkText)
                && (!Uri.TryCreate(linkText, UriKind.Absolute, out Uri? uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            {
                ShowValidation(LocalizationManager.T("Str_ValInvalidLink"));
                return;
            }

            TimeZoneInfo tz = AlarmTzPicker.SelectedTz ?? TimeZoneInfo.Local;

            DateTime horaProgramada = DateTime.SpecifyKind(
                selectedDate.Date + timeSpan,
                DateTimeKind.Unspecified);

            DateTime ahoraEnZona = TzdbUpdateService.ConvertFromUtc(DateTime.UtcNow, tz.Id);

            if (horaProgramada <= ahoraEnZona)
            {
                ShowValidation(LocalizationManager.T("Str_ValPastTime"));
                return;
            }

            DateTime horaProgramadaUtc = new DateTimeOffset(
                horaProgramada,
                tz.GetUtcOffset(horaProgramada)).UtcDateTime;

            _alarms.Add(new AlarmItem
            {
                HoraProgramada    = horaProgramada,
                HoraProgramadaUtc = horaProgramadaUtc,
                ZonaHoraria       = tz,
                Nota              = TxtAlarmNote.Text.Trim(),
                LinkUrl           = linkText,
                Use24HourFormat   = _use24HourFormat,
                Remaining         = horaProgramadaUtc - DateTime.UtcNow,
                EsActiva          = true
            });

            SaveActiveAlarms();

            TxtAlarmTime.Text         = string.Empty;
            TxtAlarmNote.Text         = string.Empty;
            TxtAlarmLink.Text         = string.Empty;
            DpkAlarmDate.SelectedDate = DateTime.Today;
            AlarmTzPicker.Preselect(TimeZoneInfo.Local);
        }

        private void BtnDeleteAlarm_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: AlarmItem item })
            {
                _alarms.Remove(item);
                SaveActiveAlarms();
            }
        }

        private void BtnMarkAttended_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: AlarmItem item })
            {
                item.MarkAttended();
                MoveAlarmToHistory(item);
            }
        }

        private void BtnOpenSnoozeMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.ContextMenu is null)
                return;

            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
        }

        private void SnoozeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem
                || menuItem.DataContext is not AlarmItem item
                || menuItem.CommandParameter is not string minutesText
                || !int.TryParse(minutesText, out int minutes))
            {
                return;
            }

            item.Snooze(minutes);
            SaveActiveAlarms();
        }

        private void AlarmLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Hyperlink { NavigateUri: Uri uri })
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignorar errores al abrir el navegador.
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var current = new AppSettings
            {
                LocalTimeZone    = CboLocalZone.SelectedItem as TimeZoneInfo ?? TimeZoneInfo.Local,
                StartWithWindows = IsStartupEnabled(),
                MinimizeOnClose  = _minimizeOnClose,
                PlayAlarmSound   = _playAlarmSound,
                Use24HourFormat  = _use24HourFormat,
                MaxStoredNotifications = NotificationCenter.MaxStoredNotifications,
                Language = LocalizationManager.CurrentLanguage,
            };

            var win = new SettingsWindow(current) { Owner = this };
            if (win.ShowDialog() == true && win.Result is AppSettings result)
            {
                // Aplicar zona horaria local
                if (result.LocalTimeZone is not null)
                    CboLocalZone.SelectedItem = result.LocalTimeZone;

                _minimizeOnClose = result.MinimizeOnClose;
                _playAlarmSound  = result.PlayAlarmSound;
                _use24HourFormat = result.Use24HourFormat;
                NotificationCenter.ConfigureRetention(result.MaxStoredNotifications);
                LocalizationManager.ApplyLanguage(result.Language);
                SaveSettings();

                foreach (AlarmItem alarm in _alarms)
                    alarm.Use24HourFormat = _use24HourFormat;

                UpdateTimeInputHints();
                UpdateClocks();
            }
        }

        private bool _formattingTime;

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
            string digits = Regex.Replace(raw, @"\D", "");
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

        // ----------------------------------------------------------------------
        //  Helpers
        // ----------------------------------------------------------------------
        private void RefreshAlarmCount()
        {
            TxtAlarmCount.Text     = _alarms.Count.ToString();
            TxtNoAlarms.Visibility = _alarms.Count == 0
                                     ? Visibility.Visible
                                     : Visibility.Collapsed;
        }

        private void LoadPersistedData()
        {
            Directory.CreateDirectory(AppDataFolderPath);
            LoadAlarmHistory();
            LoadActiveAlarms();
        }

        private void SavePersistedData()
        {
            SaveActiveAlarms();
            SaveAlarmHistory();
        }

        private void LoadActiveAlarms()
        {
            if (!File.Exists(AlarmsFilePath) && !File.Exists(AlarmsBackupFilePath))
                return;

            try
            {
                var items = DeserializeWithBackup<List<AlarmStorageItem>>(AlarmsFilePath, AlarmsBackupFilePath);

                if (items is null)
                    return;

                bool persistenceChanged = false;

                foreach (AlarmStorageItem stored in items)
                {
                    TimeZoneInfo tz = ResolveTimeZone(stored.TimeZoneId);
                    DateTime horaProgramada = TzdbUpdateService.ConvertFromUtc(stored.HoraProgramadaUtc, tz.Id);

                    if (stored.EsActiva && stored.HoraProgramadaUtc <= DateTime.UtcNow)
                    {
                        _alarmHistory.Insert(0, new AlarmHistoryItem
                        {
                            HoraProgramadaUtc = stored.HoraProgramadaUtc,
                            FinalizedAtUtc = DateTime.UtcNow,
                            ZonaHoraria = tz,
                            Nota = stored.Nota,
                            LinkUrl = stored.LinkUrl,
                            Use24HourFormat = stored.Use24HourFormat,
                            FinalState = AlarmHandlingState.Missed
                        });

                        PublishMissedAlarmAfterOffline(stored, tz);
                        persistenceChanged = true;
                        continue;
                    }

                    _alarms.Add(new AlarmItem
                    {
                        HoraProgramada = horaProgramada,
                        HoraProgramadaUtc = stored.HoraProgramadaUtc,
                        ZonaHoraria = tz,
                        Nota = stored.Nota,
                        LinkUrl = stored.LinkUrl,
                        Use24HourFormat = stored.Use24HourFormat,
                        HandlingState = stored.HandlingState,
                        LastSnoozeMinutes = stored.LastSnoozeMinutes,
                        EsActiva = stored.EsActiva,
                        Remaining = stored.HoraProgramadaUtc - DateTime.UtcNow
                    });
                }

                if (persistenceChanged)
                    SavePersistedData();
            }
            catch
            {
            }
        }

        private void PublishMissedAlarmAfterOffline(AlarmStorageItem stored, TimeZoneInfo tz)
        {
            DateTime scheduledLocal = TzdbUpdateService.ConvertFromUtc(stored.HoraProgramadaUtc, tz.Id);
            string timeFormat = stored.Use24HourFormat ? "HH:mm" : "hh:mm tt";
            string dateText = scheduledLocal.ToString("dddd, d MMMM yyyy", CultureInfo.CurrentCulture);
            string timeText = scheduledLocal.ToString(timeFormat, CultureInfo.CurrentCulture);

            string title = LocalizationManager.T("Str_MissedAlarmWhileOffTitle");
            string message = string.IsNullOrWhiteSpace(stored.Nota)
                ? LocalizationManager.T("Str_MissedAlarmWhileOffMessage", dateText, timeText, tz.DisplayName)
                : LocalizationManager.T("Str_MissedAlarmWhileOffMessageWithNote", dateText, timeText, tz.DisplayName, stored.Nota);

            string key = $"missed-offline:{stored.HoraProgramadaUtc.Ticks}:{stored.TimeZoneId}:{stored.Nota}";
            NotificationCenter.Publish(title, message, AppNotificationKind.Warning, "Alarmas", key);
        }

        private void SaveActiveAlarms()
        {
            Directory.CreateDirectory(AppDataFolderPath);

            var items = _alarms.Select(a => new AlarmStorageItem
            {
                HoraProgramadaUtc = a.HoraProgramadaUtc,
                TimeZoneId = a.ZonaHoraria.Id,
                Nota = a.Nota,
                LinkUrl = a.LinkUrl,
                Use24HourFormat = a.Use24HourFormat,
                HandlingState = a.HandlingState,
                LastSnoozeMinutes = a.LastSnoozeMinutes,
                EsActiva = a.EsActiva
            }).ToList();

            WriteJsonAtomic(AlarmsFilePath, AlarmsBackupFilePath, items);
        }

        private void LoadAlarmHistory()
        {
            if (!File.Exists(HistoryFilePath) && !File.Exists(HistoryBackupFilePath))
                return;

            try
            {
                var items = DeserializeWithBackup<List<AlarmHistoryStorageItem>>(HistoryFilePath, HistoryBackupFilePath);

                if (items is null)
                    return;

                foreach (AlarmHistoryStorageItem stored in items)
                {
                    _alarmHistory.Add(new AlarmHistoryItem
                    {
                        HoraProgramadaUtc = stored.HoraProgramadaUtc,
                        FinalizedAtUtc = stored.FinalizedAtUtc,
                        ZonaHoraria = ResolveTimeZone(stored.TimeZoneId),
                        Nota = stored.Nota,
                        LinkUrl = stored.LinkUrl,
                        Use24HourFormat = stored.Use24HourFormat,
                        FinalState = stored.FinalState,
                        IsFavorite = stored.IsFavorite
                    });
                }
            }
            catch
            {
            }
        }

        private void SaveAlarmHistory()
        {
            Directory.CreateDirectory(AppDataFolderPath);

            var items = _alarmHistory.Select(a => new AlarmHistoryStorageItem
            {
                HoraProgramadaUtc = a.HoraProgramadaUtc,
                FinalizedAtUtc = a.FinalizedAtUtc,
                TimeZoneId = a.ZonaHoraria.Id,
                Nota = a.Nota,
                LinkUrl = a.LinkUrl,
                Use24HourFormat = a.Use24HourFormat,
                FinalState = a.FinalState,
                IsFavorite = a.IsFavorite
            }).ToList();

            WriteJsonAtomic(HistoryFilePath, HistoryBackupFilePath, items);
        }

        private void MoveAlarmToHistory(AlarmItem item)
        {
            _alarmHistory.Insert(0, new AlarmHistoryItem
            {
                HoraProgramadaUtc = item.HoraProgramadaUtc,
                FinalizedAtUtc = DateTime.UtcNow,
                ZonaHoraria = item.ZonaHoraria,
                Nota = item.Nota,
                LinkUrl = item.LinkUrl,
                Use24HourFormat = item.Use24HourFormat,
                FinalState = item.HandlingState,
                IsFavorite = false
            });

            _alarms.Remove(item);
            SavePersistedData();
        }

        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            try
            {
                if (_historyWindow is { IsLoaded: true })
                {
                    if (!_historyWindow.IsVisible)
                        _historyWindow.Show();

                    if (_historyWindow.WindowState == WindowState.Minimized)
                        _historyWindow.WindowState = WindowState.Normal;

                    _historyWindow.Activate();
                    _historyWindow.Topmost = true;
                    _historyWindow.Topmost = false;
                    _historyWindow.Focus();
                    return;
                }

                _historyWindow = new HistoryWindow(_alarmHistory, SaveAlarmHistory, ReuseAlarmFromHistory)
                {
                    ShowInTaskbar = true
                };

                _historyWindow.Closed += (_, _) => _historyWindow = null;
                _historyWindow.Topmost = true;
                _historyWindow.Show();
                _historyWindow.Activate();
                _historyWindow.Topmost = false;
                _historyWindow.Focus();
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void ReuseAlarmFromHistory(ReuseAlarmResult result)
        {
            _alarms.Add(new AlarmItem
            {
                HoraProgramada = result.HoraProgramada,
                HoraProgramadaUtc = result.HoraProgramadaUtc,
                ZonaHoraria = result.ZonaHoraria,
                Nota = result.Nota,
                LinkUrl = result.LinkUrl,
                Use24HourFormat = result.Use24HourFormat,
                Remaining = result.HoraProgramadaUtc - DateTime.UtcNow,
                EsActiva = true
            });

            SaveActiveAlarms();
            RefreshAlarmCount();
        }

        private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
        {
            try { return TZConvert.GetTimeZoneInfo(timeZoneId); } catch { }
            return TimeZoneInfo.Local;
        }

        private void LoadSettings()
        {
            _minimizeOnClose = true;
            _playAlarmSound  = true;
            _use24HourFormat = true;
            NotificationCenter.ConfigureRetention(40);
            LocalizationManager.ApplyLanguage(LocalizationManager.Spanish);

            if (!File.Exists(SettingsFilePath))
                return;

            try
            {
                var settings = DeserializeWithBackup<PersistedAppSettings>(SettingsFilePath, SettingsBackupFilePath);

                if (settings is null)
                    return;

                _minimizeOnClose = settings.MinimizeOnClose;
                _playAlarmSound = settings.PlayAlarmSound;
                _use24HourFormat = settings.Use24HourFormat;
                NotificationCenter.ConfigureRetention(settings.MaxStoredNotifications);
                LocalizationManager.ApplyLanguage(settings.Language);
            }
            catch
            {
            }
        }

        private void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(AppDataFolderPath);
                var settings = new PersistedAppSettings
                {
                    MinimizeOnClose = _minimizeOnClose,
                    PlayAlarmSound = _playAlarmSound,
                    Use24HourFormat = _use24HourFormat,
                    MaxStoredNotifications = NotificationCenter.MaxStoredNotifications,
                    Language = LocalizationManager.CurrentLanguage
                };

                WriteJsonAtomic(SettingsFilePath, SettingsBackupFilePath, settings);
            }
            catch
            {
            }
        }

        private static T? DeserializeWithBackup<T>(string filePath, string backupFilePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var parsed = JsonSerializer.Deserialize<T>(json, JsonOptions);
                    if (parsed is not null)
                        return parsed;
                }
            }
            catch
            {
            }

            try
            {
                if (!File.Exists(backupFilePath))
                    return default;

                string backupJson = File.ReadAllText(backupFilePath);
                var backupParsed = JsonSerializer.Deserialize<T>(backupJson, JsonOptions);
                if (backupParsed is not null)
                {
                    try
                    {
                        WriteJsonAtomic(filePath, backupFilePath, backupParsed);
                    }
                    catch
                    {
                    }

                    return backupParsed;
                }
            }
            catch
            {
            }

            return default;
        }

        private static void WriteJsonAtomic<T>(string filePath, string backupFilePath, T data)
        {
            string tempFilePath = filePath + ".tmp";
            string json = JsonSerializer.Serialize(data, JsonOptions);

            File.WriteAllText(tempFilePath, json);

            if (File.Exists(filePath))
            {
                File.Copy(filePath, backupFilePath, true);
                File.Copy(tempFilePath, filePath, true);
            }
            else
            {
                File.Copy(tempFilePath, filePath, true);
                File.Copy(filePath, backupFilePath, true);
            }

            File.Delete(tempFilePath);
        }

        private void UpdateTimeInputHints()
        {
            TxtAlarmTimeLabel.Text = _use24HourFormat
                ? "Hora (HH:MM)"
                : "Hora (hh:mm AM/PM)";
        }

        private string FormatClockTime(DateTime value, bool includeSeconds)
        {
            string format = _use24HourFormat
                ? (includeSeconds ? "HH:mm:ss" : "HH:mm")
                : (includeSeconds ? "hh:mm:ss tt" : "hh:mm tt");

            return value.ToString(format, CultureInfo.CurrentCulture);
        }

        private static string FormatLongDate(DateTime value)
        {
            CultureInfo culture = LocalizationManager.CurrentLanguage == LocalizationManager.English
                ? CultureInfo.GetCultureInfo("en-US")
                : CultureInfo.GetCultureInfo("es-ES");

            return value.ToString("dddd, d MMMM yyyy", culture);
        }

        private static bool IsStartupEnabled()
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(StartupRegistryPath, writable: false);
            string? value = key?.GetValue(StartupValueName) as string;
            return string.Equals(value, GetStartupCommand(), StringComparison.OrdinalIgnoreCase);
        }

        private static void SetStartupEnabled(bool enabled)
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(StartupRegistryPath)
                ?? throw new InvalidOperationException("No se pudo abrir la clave de inicio de Windows.");

            if (enabled)
            {
                key.SetValue(StartupValueName, GetStartupCommand());
            }
            else
            {
                key.DeleteValue(StartupValueName, throwOnMissingValue: false);
            }
        }

        private static string GetStartupCommand()
        {
            string executablePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("No se pudo resolver la ruta del ejecutable actual.");

            return $"\"{executablePath}\"";
        }

        private void ShowValidation(string msg)
        {
            TxtValidation.Text       = msg;
            TxtValidation.Visibility = Visibility.Visible;
        }

        /// <summary>Parsea hora flexible en 24h o 12h AM/PM según la configuración actual.</summary>
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

            string digits = Regex.Replace(input, @"\D", "");

            if (digits.Length is < 3 or > 4) return false;

            int hh = int.Parse(digits.Length == 3 ? digits[..1] : digits[..2]);
            int mm = int.Parse(digits.Length == 3 ? digits[1..] : digits[2..]);

            if (hh > 23 || mm > 59) return false;

            result = new TimeSpan(hh, mm, 0);
            return true;
        }

        /// <summary>Resuelve TimeZoneInfo usando TZConvert (soporta IDs Windows e IANA).</summary>
        private static TimeZoneInfo GetTz(string windowsId, string ianaId)
        {
            try { return TZConvert.GetTimeZoneInfo(windowsId); } catch { }
            try { return TZConvert.GetTimeZoneInfo(ianaId);    } catch { }
            return TimeZoneInfo.Utc;
        }
    }
}
