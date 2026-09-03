using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace WorldTimeAlarms
{
    // ---------------------------------------------------------------------------
    //  AboutWindow — Ventana informativa "Acerca de".
    // ---------------------------------------------------------------------------
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            UpdateVersionText();
            LocalizationManager.LanguageChanged += UpdateVersionText;
            Closed += AboutWindow_Closed;
        }

        private void AboutWindow_Closed(object? sender, EventArgs e)
        {
            LocalizationManager.LanguageChanged -= UpdateVersionText;
            Closed -= AboutWindow_Closed;
        }

        private void UpdateVersionText()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            var version = string.IsNullOrWhiteSpace(informationalVersion)
                ? assembly.GetName().Version?.ToString(3) ?? "1.0.0"
                : informationalVersion;

            TxtVersion.Text = LocalizationManager.T("Str_AppVersion", version);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
