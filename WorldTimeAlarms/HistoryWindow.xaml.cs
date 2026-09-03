using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace WorldTimeAlarms
{
    public partial class HistoryWindow : Window
    {
        private const int PageSize = 8;
        private readonly ObservableCollection<AlarmHistoryItem> _source;
        private readonly ObservableCollection<AlarmHistoryItem> _filtered = [];
        private readonly ObservableCollection<AlarmHistoryItem> _view = [];
        private readonly Action _onHistoryChanged;
        private readonly Action<ReuseAlarmResult> _onReuseAlarm;
        private int _currentPage = 1;
        private bool _eventsHooked;

        public HistoryWindow(
            ObservableCollection<AlarmHistoryItem> items,
            Action onHistoryChanged,
            Action<ReuseAlarmResult> onReuseAlarm)
        {
            InitializeComponent();
            _source = items;
            _onHistoryChanged = onHistoryChanged;
            _onReuseAlarm = onReuseAlarm;
            IcHistory.ItemsSource = _view;
            Loaded += HistoryWindow_Loaded;
        }

        private void HistoryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_eventsHooked)
            {
                CboFilter.SelectionChanged += CboFilter_SelectionChanged;
                CboSort.SelectionChanged += CboSort_SelectionChanged;
                DpkFrom.SelectedDateChanged += DateFilter_Changed;
                DpkTo.SelectedDateChanged += DateFilter_Changed;
                _eventsHooked = true;
            }

            Dispatcher.BeginInvoke(new Action(ApplyFilters), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
            => ApplyFilters();

        private void CboFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_eventsHooked) return;
            ApplyFilters();
        }

        private void CboSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_eventsHooked) return;
            ApplyFilters();
        }

        private void DateFilter_Changed(object? sender, SelectionChangedEventArgs e)
        {
            if (!_eventsHooked) return;
            ApplyFilters();
        }

        private void BtnPreviousPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage <= 1)
                return;

            _currentPage--;
            ApplyPagination();
        }

        private void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            int totalPages = GetTotalPages();
            if (_currentPage >= totalPages)
                return;

            _currentPage++;
            ApplyPagination();
        }

        private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                    LocalizationManager.T("Str_ClearHistoryConfirm"),
                    LocalizationManager.T("Str_Confirm"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            _source.Clear();
            _onHistoryChanged();
            ApplyFilters();
        }

        private void BtnExportJson_Click(object sender, RoutedEventArgs e)
        {
            var exportItems = GetExportItems().ToList();
            if (exportItems.Count == 0)
            {
                MessageBox.Show(LocalizationManager.T("Str_NoExportItems"), LocalizationManager.T("Str_ExportHistory"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"alarm-history-{DateTime.Now:yyyyMMdd-HHmmss}.json"
            };

            if (dlg.ShowDialog(this) != true)
                return;

            string json = JsonSerializer.Serialize(exportItems.Select(ToExportModel).ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dlg.FileName, json, Encoding.UTF8);
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var exportItems = GetExportItems().ToList();
            if (exportItems.Count == 0)
            {
                MessageBox.Show(LocalizationManager.T("Str_NoExportItems"), LocalizationManager.T("Str_ExportHistory"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"alarm-history-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
            };

            if (dlg.ShowDialog(this) != true)
                return;

            StringBuilder sb = new();
            sb.AppendLine("Time,Date,Zone,State,Note,Link,FinalizedAtUtc");

            foreach (AlarmHistoryItem item in exportItems)
            {
                sb.AppendLine(string.Join(",",
                    Csv(item.TimeText),
                    Csv(item.DateText),
                    Csv(item.ZoneName),
                    Csv(item.EstadoLabel),
                    Csv(item.Nota),
                    Csv(item.LinkUrl),
                    Csv(item.FinalizedAtUtc.ToString("O"))));
            }

            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        }

        private void ApplyFilters()
        {
            if (TxtSearch is null
                || CboFilter is null
                || CboSort is null
                || DpkFrom is null
                || DpkTo is null
                || TxtResultsCount is null
                || TxtPageInfo is null
                || TxtEmpty is null)
            {
                return;
            }

            string search = TxtSearch.Text.Trim();
            int filterIndex = CboFilter.SelectedIndex;
            int sortIndex = CboSort.SelectedIndex;
            DateTime? from = DpkFrom.SelectedDate?.Date;
            DateTime? to = DpkTo.SelectedDate?.Date;

            var query = _source.AsEnumerable();

            query = filterIndex switch
            {
                1 => query.Where(x => x.FinalState == AlarmHandlingState.Attended),
                2 => query.Where(x => x.FinalState == AlarmHandlingState.Missed),
                _ => query
            };

            if (from.HasValue)
                query = query.Where(x => TzdbUpdateService.ConvertFromUtc(x.HoraProgramadaUtc, x.ZonaHoraria.Id).Date >= from.Value);

            if (to.HasValue)
                query = query.Where(x => TzdbUpdateService.ConvertFromUtc(x.HoraProgramadaUtc, x.ZonaHoraria.Id).Date <= to.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    x.TimeText.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || x.DateText.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || x.ZoneName.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || x.EstadoLabel.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || x.Nota.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || x.LinkUrl.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            query = sortIndex switch
            {
                1 => query.OrderBy(x => x.HoraProgramadaUtc).ThenBy(x => x.FinalizedAtUtc),
                2 => query.OrderBy(x => x.FinalState).ThenByDescending(x => x.FinalizedAtUtc),
                _ => query.OrderByDescending(x => x.IsFavorite).ThenByDescending(x => x.HoraProgramadaUtc).ThenByDescending(x => x.FinalizedAtUtc)
            };

            _filtered.Clear();
            foreach (AlarmHistoryItem item in query)
                _filtered.Add(item);

            _currentPage = 1;
            ApplyPagination();
        }

        private void ApplyPagination()
        {
            _view.Clear();

            int totalPages = GetTotalPages();
            if (_currentPage > totalPages)
                _currentPage = totalPages;

            foreach (AlarmHistoryItem item in _filtered.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                _view.Add(item);

            TxtResultsCount.Text = _filtered.Count == 1
                ? LocalizationManager.T("Str_ResultSingular")
                : LocalizationManager.T("Str_ResultsPlural", _filtered.Count);

            TxtPageInfo.Text = LocalizationManager.T("Str_PageInfo", _currentPage, totalPages);

            TxtEmpty.Visibility = _view.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (TxtSubtitleLabel is not null)
                TxtSubtitleLabel.Text = _source.Count == 0
                    ? LocalizationManager.T("Str_NoSavedEntries")
                    : (_source.Count == 1
                        ? LocalizationManager.T("Str_AlarmInHistorySingular", _source.Count)
                        : LocalizationManager.T("Str_AlarmsInHistoryPlural", _source.Count));
        }

        private int GetTotalPages()
            => Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));

        private IEnumerable<AlarmHistoryItem> GetExportItems()
        {
            int scopeIndex = CboExportScope.SelectedIndex;

            return scopeIndex switch
            {
                1 => _filtered.Where(x => x.IsSelected),
                2 => _filtered,
                _ => _view
            };
        }

        private static object ToExportModel(AlarmHistoryItem item) => new
        {
            item.TimeText,
            item.DateText,
            item.ZoneName,
            State = item.EstadoLabel,
            item.Nota,
            item.LinkUrl,
            item.FinalizedAtUtc
        };

        private static string Csv(string? value)
        {
            string text = (value ?? string.Empty).Replace("\"", "\"\"");
            return $"\"{text}\"";
        }

        private void BtnOpenLink_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not AlarmHistoryItem item || string.IsNullOrWhiteSpace(item.LinkUrl))
                return;

            try
            {
                Process.Start(new ProcessStartInfo(item.LinkUrl) { UseShellExecute = true });
            }
            catch
            {
                MessageBox.Show(
                    LocalizationManager.T("Str_CannotOpenLink"),
                    LocalizationManager.T("Str_InvalidLink"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void BtnCopyLink_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not AlarmHistoryItem item || string.IsNullOrWhiteSpace(item.LinkUrl))
                return;

            try
            {
                Clipboard.SetText(item.LinkUrl);
            }
            catch
            {
                MessageBox.Show(
                    LocalizationManager.T("Str_CannotCopyLink"),
                    LocalizationManager.T("Str_CopyLink"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not AlarmHistoryItem item)
                return;

            if (MessageBox.Show(
                    LocalizationManager.T("Str_DeleteEntryConfirm"),
                    LocalizationManager.T("Str_Confirm"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            _source.Remove(item);
            _onHistoryChanged();
            ApplyFilters();
        }

        private void BtnToggleFavorite_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not AlarmHistoryItem item)
                return;

            item.IsFavorite = !item.IsFavorite;
            _onHistoryChanged();
            ApplyFilters();
        }

        private void ChkSelectItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox { DataContext: AlarmHistoryItem item, IsChecked: bool isChecked })
                return;

            item.IsSelected = isChecked;
        }

        private void BtnReuseAlarm_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not AlarmHistoryItem item)
                return;

            var window = new ReuseAlarmWindow(item) { Owner = this };
            if (window.ShowDialog() == true && window.Result is ReuseAlarmResult result)
            {
                _onReuseAlarm(result);
            }
        }

        private void BtnDuplicateAlarm_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not AlarmHistoryItem item)
                return;

            var window = new ReuseAlarmWindow(item) { Owner = this };
            if (window.ShowDialog() == true && window.Result is ReuseAlarmResult result)
            {
                _onReuseAlarm(result);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}
