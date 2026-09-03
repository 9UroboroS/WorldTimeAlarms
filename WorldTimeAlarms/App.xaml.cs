using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using System.Threading;

namespace WorldTimeAlarms
{
    public partial class App : Application
    {
        private TaskbarIcon? _trayIcon;
        private Mutex? _singleInstanceMutex;
        private bool _ownsSingleInstanceMutex;
        private const string SingleInstanceMutexName = "WorldTimeAlarms.Singleton";

        protected override void OnStartup(StartupEventArgs e)
        {
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out bool createdNew);
            if (!createdNew)
            {
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                Shutdown();
                return;
            }

            _ownsSingleInstanceMutex = true;

            base.OnStartup(e);
            _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
            NotificationCenter.Load();

            // Iniciar descarga/actualización de TZDB en background (no bloquea la UI)
            _ = TzdbUpdateService.InitializeAsync();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trayIcon?.Dispose();

            if (_singleInstanceMutex is not null)
            {
                if (_ownsSingleInstanceMutex)
                {
                    _singleInstanceMutex.ReleaseMutex();
                    _ownsSingleInstanceMutex = false;
                }

                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }

            base.OnExit(e);
        }

        private void TrayOpen_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow is { } win)
            {
                win.Show();
                win.WindowState = WindowState.Normal;
                win.Activate();
            }
        }

        private void TrayExit_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow is MainWindow mw)
                mw.AllowClose = true;
            Shutdown();
        }
    }
}

