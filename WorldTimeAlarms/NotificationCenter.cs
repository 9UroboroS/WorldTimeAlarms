using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Text.Json;
using System.Windows;
using System.Windows.Resources;
using System.Windows.Media;

namespace WorldTimeAlarms
{
    public enum AppNotificationKind
    {
        Info,
        Warning,
        Success,
        Error
    }

    public sealed class AppNotificationItem : INotifyPropertyChanged
    {
        private bool _isRead;

        public Guid Id { get; } = Guid.NewGuid();
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public DateTime CreatedAtLocal { get; init; } = DateTime.Now;
        public AppNotificationKind Kind { get; init; } = AppNotificationKind.Info;
        public string Category { get; init; } = string.Empty;
        public string DeduplicationKey { get; init; } = string.Empty;

        public bool IsRead
        {
            get => _isRead;
            set
            {
                if (_isRead == value) return;
                _isRead = value;
                OnPropertyChanged(nameof(IsRead));
                OnPropertyChanged(nameof(ItemOpacity));
            }
        }

        internal void RefreshLocalizedText()
        {
            OnPropertyChanged(nameof(CategoryDisplay));
            OnPropertyChanged(nameof(FullDateText));
        }

        public string TimeText => CreatedAtLocal.ToString("HH:mm");
        public string FullDateText => LocalizationManager.T("Str_NotificationFullDate",
            CreatedAtLocal.ToString("dddd, d MMMM yyyy", CultureInfo.CurrentCulture),
            CreatedAtLocal.ToString("HH:mm"));
        public string CategoryDisplay => Category switch
        {
            "Alarmas" => LocalizationManager.T("Str_CategoryAlarms"),
            "TZDB" => LocalizationManager.T("Str_CategoryTzdb"),
            _ => Category
        };
        public double ItemOpacity => IsRead ? 0.72 : 1.0;
        public string IconKind => Kind switch
        {
            AppNotificationKind.Success => "CheckCircleOutline",
            AppNotificationKind.Warning => "BellAlertOutline",
            AppNotificationKind.Error => "AlertCircleOutline",
            _ => "BellOutline"
        };

        public Brush AccentBrush => Kind switch
        {
            AppNotificationKind.Success => new SolidColorBrush(Color.FromRgb(0x86, 0xEF, 0xAC)),
            AppNotificationKind.Warning => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
            AppNotificationKind.Error => new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71)),
            _ => new SolidColorBrush(Color.FromRgb(0x96, 0xB8, 0xFF))
        };

        public Brush AccentBackgroundBrush => Kind switch
        {
            AppNotificationKind.Success => new SolidColorBrush(Color.FromRgb(0x14, 0x33, 0x20)),
            AppNotificationKind.Warning => new SolidColorBrush(Color.FromRgb(0x3D, 0x28, 0x00)),
            AppNotificationKind.Error => new SolidColorBrush(Color.FromRgb(0x45, 0x1A, 0x1A)),
            _ => new SolidColorBrush(Color.FromRgb(0x1E, 0x2D, 0x52))
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public static class NotificationCenter
    {
        private static readonly ObservableCollection<AppNotificationItem> _items = [];
        private static readonly string AppDataFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WorldTimeAlarms");
        private static readonly string NotificationsFilePath = Path.Combine(AppDataFolderPath, "notifications.json");
        private static readonly string NotificationsBackupFilePath = NotificationsFilePath + ".bak";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };
        private const int DefaultMaxStoredNotifications = 40;
        private const int MinStoredNotifications = 10;
        private const int MaxStoredNotificationsUpperBound = 300;

        private sealed class NotificationStorageItem
        {
            public Guid Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public DateTime CreatedAtLocal { get; set; }
            public AppNotificationKind Kind { get; set; }
            public string Category { get; set; } = string.Empty;
            public string DeduplicationKey { get; set; } = string.Empty;
            public bool IsRead { get; set; }
        }

        private static readonly HashSet<string> _suppressedKeys = [];
        private static readonly string SuppressedKeysFilePath = Path.Combine(AppDataFolderPath, "notifications_suppressed.json");
        private static readonly string SuppressedKeysBackupFilePath = SuppressedKeysFilePath + ".bak";

        static NotificationCenter()
        {
            LocalizationManager.LanguageChanged += () =>
            {
                foreach (var item in _items)
                    item.RefreshLocalizedText();
            };
        }


        public static ObservableCollection<AppNotificationItem> Items => _items;
        public static event EventHandler? NotificationsChanged;

        public static int UnreadCount => _items.Count(x => !x.IsRead);
        public static AppNotificationItem? LatestNotification => _items.FirstOrDefault();
        public static int MaxStoredNotifications { get; private set; } = DefaultMaxStoredNotifications;

        public static void ConfigureRetention(int maxStoredNotifications)
        {
            MaxStoredNotifications = Math.Clamp(maxStoredNotifications, MinStoredNotifications, MaxStoredNotificationsUpperBound);
            int beforeCount = _items.Count;
            Trim();
            if (_items.Count != beforeCount)
                Save();
            NotificationsChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void Publish(string title, string message, AppNotificationKind kind,
            string category, string? deduplicationKey = null)
        {
            string key = deduplicationKey ?? string.Empty;

            // Skip if this key was previously dismissed by the user
            if (!string.IsNullOrWhiteSpace(key) && _suppressedKeys.Contains(key))
                return;

            if (!string.IsNullOrWhiteSpace(key))
            {
                var existing = _items.FirstOrDefault(x => x.DeduplicationKey == key);
                if (existing is not null)
                    _items.Remove(existing);
            }

            _items.Insert(0, new AppNotificationItem
            {
                Title = title,
                Message = message,
                Kind = kind,
                Category = category,
                DeduplicationKey = key,
                CreatedAtLocal = DateTime.Now
            });

            Trim();
            Save();
            PlayNotificationSound();
            NotificationsChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void Load()
        {
            try
            {
                Directory.CreateDirectory(AppDataFolderPath);

                // Load suppressed keys first
                if (File.Exists(SuppressedKeysFilePath) || File.Exists(SuppressedKeysBackupFilePath))
                {
                    var keys = DeserializeWithBackup<string[]>(SuppressedKeysFilePath, SuppressedKeysBackupFilePath);
                    if (keys is not null)
                    {
                        foreach (var k in keys)
                            _suppressedKeys.Add(k);
                    }
                }

                if (!File.Exists(NotificationsFilePath) && !File.Exists(NotificationsBackupFilePath))
                    return;

                var saved = DeserializeWithBackup<NotificationStorageItem[]>(NotificationsFilePath, NotificationsBackupFilePath);
                if (saved is null)
                    return;

                _items.Clear();
                foreach (var item in saved.OrderByDescending(x => x.CreatedAtLocal))
                {
                    _items.Add(new AppNotificationItem
                    {
                        Title = item.Title,
                        Message = item.Message,
                        CreatedAtLocal = item.CreatedAtLocal,
                        Kind = item.Kind,
                        Category = item.Category,
                        DeduplicationKey = item.DeduplicationKey,
                        IsRead = item.IsRead
                    });
                }

                NotificationsChanged?.Invoke(null, EventArgs.Empty);
            }
            catch
            {
            }
        }

        public static void MarkAllAsRead()
        {
            foreach (var item in _items.Where(x => !x.IsRead))
            {
                item.IsRead = true;
                if (!string.IsNullOrWhiteSpace(item.DeduplicationKey))
                    _suppressedKeys.Add(item.DeduplicationKey);
            }

            SaveSuppressedKeys();
            Save();
            NotificationsChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void MarkAsRead(AppNotificationItem item)
        {
            if (item.IsRead)
                return;

            item.IsRead = true;

            if (!string.IsNullOrWhiteSpace(item.DeduplicationKey))
            {
                _suppressedKeys.Add(item.DeduplicationKey);
                SaveSuppressedKeys();
            }

            Save();
            NotificationsChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void Remove(AppNotificationItem item)
        {
            if (_items.Remove(item))
            {
                if (!string.IsNullOrWhiteSpace(item.DeduplicationKey))
                {
                    _suppressedKeys.Add(item.DeduplicationKey);
                    SaveSuppressedKeys();
                }

                Save();
                NotificationsChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static void Clear()
        {
            if (_items.Count == 0)
                return;

            foreach (var item in _items)
            {
                if (!string.IsNullOrWhiteSpace(item.DeduplicationKey))
                    _suppressedKeys.Add(item.DeduplicationKey);
            }

            _items.Clear();
            SaveSuppressedKeys();
            Save();
            NotificationsChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void Trim()
        {
            while (_items.Count > MaxStoredNotifications)
                _items.RemoveAt(_items.Count - 1);
        }

        private static void PlayNotificationSound()
        {
            try
            {
                StreamResourceInfo? resource = Application.GetResourceStream(
                    new Uri("pack://application:,,,/notificaciones.wav", UriKind.Absolute));

                if (resource is null)
                    return;

                using (resource.Stream)
                {
                    using var memory = new MemoryStream();
                    resource.Stream.CopyTo(memory);
                    memory.Position = 0;

                    using var player = new SoundPlayer(memory);
                    player.Load();
                    player.Play();
                }
            }
            catch
            {
            }
        }

        private static void SaveSuppressedKeys()
        {
            try
            {
                Directory.CreateDirectory(AppDataFolderPath);
                WriteJsonAtomic(SuppressedKeysFilePath, SuppressedKeysBackupFilePath, _suppressedKeys.ToArray());
            }
            catch
            {
            }
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(AppDataFolderPath);
                var data = _items.Select(x => new NotificationStorageItem
                {
                    Id = x.Id,
                    Title = x.Title,
                    Message = x.Message,
                    CreatedAtLocal = x.CreatedAtLocal,
                    Kind = x.Kind,
                    Category = x.Category,
                    DeduplicationKey = x.DeduplicationKey,
                    IsRead = x.IsRead
                }).ToArray();

                WriteJsonAtomic(NotificationsFilePath, NotificationsBackupFilePath, data);
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
    }
}
