using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JobWidget.Services;

namespace JobWidget
{
    public partial class SettingsWindow : Window
    {
        private readonly ConfigService _configService;
        private readonly Action? _onClose;

        public SettingsWindow(ConfigService configService, Action? onClose = null)
        {
            InitializeComponent();
            _configService = configService;
            _onClose = onClose;

            LoadSettingsIntoUI();
        }

        private void LoadSettingsIntoUI()
        {
            var config = _configService.Config;
            KeywordTextBox.Text = config.SearchKeywords;
            LocationTextBox.Text = config.SearchLocation;
            SalaryTextBox.Text = config.SalaryMin?.ToString() ?? "";
            RefreshIntervalTextBox.Text = config.RefreshIntervalMinutes.ToString();
            AutoRefreshEnabledCheckBox.IsChecked = config.AutoRefreshEnabled;
            SetWorkModeSelection(config.WorkMode);

            AdzunaEnabledCheckBox.IsChecked = config.Adzuna.Enabled;
            AdzunaAppIdTextBox.Text = config.Adzuna.AppId;
            AdzunaAppKeyTextBox.Text = config.Adzuna.AppKey;
            AdzunaKeysPanel.Visibility = config.Adzuna.Enabled ? Visibility.Visible : Visibility.Collapsed;

            RemotiveEnabledCheckBox.IsChecked = config.Remotive.Enabled;

            JoobleEnabledCheckBox.IsChecked = config.Jooble.Enabled;
            JoobleApiKeyTextBox.Text = config.Jooble.ApiKey;
            JoobleKeyPanel.Visibility = config.Jooble.Enabled ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetWorkModeSelection(string? workMode)
        {
            foreach (ComboBoxItem item in WorkModeComboBox.Items)
            {
                if (string.Equals(item.Content?.ToString(), workMode, StringComparison.OrdinalIgnoreCase))
                {
                    WorkModeComboBox.SelectedItem = item;
                    return;
                }
            }

            // Default to "Any" if no match (empty, null, or unrecognized value)
            WorkModeComboBox.SelectedIndex = 0;
        }

        private void AdzunaEnabledCheckBox_Click(object sender, RoutedEventArgs e)
        {
            AdzunaKeysPanel.Visibility = (AdzunaEnabledCheckBox.IsChecked ?? false)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void JoobleEnabledCheckBox_Click(object sender, RoutedEventArgs e)
        {
            JoobleKeyPanel.Visibility = (JoobleEnabledCheckBox.IsChecked ?? false)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Read values from UI and save to config
            var config = _configService.Config;
            config.SearchKeywords = KeywordTextBox.Text;
            config.SearchLocation = LocationTextBox.Text;

            if (int.TryParse(SalaryTextBox.Text, out var salary))
            {
                config.SalaryMin = salary;
            }
            else
            {
                config.SalaryMin = null;
            }

            config.WorkMode = (WorkModeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Any";

            if (int.TryParse(RefreshIntervalTextBox.Text, out var refreshInterval))
            {
                config.RefreshIntervalMinutes = refreshInterval;
            }

            config.AutoRefreshEnabled = AutoRefreshEnabledCheckBox.IsChecked ?? true;

            config.Adzuna.Enabled = AdzunaEnabledCheckBox.IsChecked ?? false;
            config.Adzuna.AppId = AdzunaAppIdTextBox.Text;
            config.Adzuna.AppKey = AdzunaAppKeyTextBox.Text;

            config.Remotive.Enabled = RemotiveEnabledCheckBox.IsChecked ?? false;

            config.Jooble.Enabled = JoobleEnabledCheckBox.IsChecked ?? false;
            config.Jooble.ApiKey = JoobleApiKeyTextBox.Text;

            _configService.Save();

            // Notify the main window that settings changed
            _onClose?.Invoke();

            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
