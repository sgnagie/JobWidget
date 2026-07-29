using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using JobWidget.Models;
using JobWidget.Services;

namespace JobWidget
{
    public partial class MainWindow : Window
    {
        private readonly ConfigService _configService;
        private readonly JobAggregator _jobAggregator;
        private readonly ObservableCollection<JobPosting> _jobs = new();

        private readonly DispatcherTimer _scrollTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
        private const double ScrollStepPixels = 0.6;

        private DispatcherTimer? _refreshTimer;
        private SettingsWindow? _settingsWindow;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize services
            _configService = new ConfigService("config.json");

            // Create job sources and pass to aggregator
            var adzunaSource = new AdzunaJobSource(_configService);
            var remotiveSource = new RemotiveJobSource(_configService);
            var joobleSource = new JoobleJobSource(_configService);
            _jobAggregator = new JobAggregator(adzunaSource, remotiveSource, joobleSource);
            

            JobItemsControl.ItemsSource = _jobs;

            // Start in the top-right corner of the primary screen
            // Restore last window position/size, or default to top-right corner on first run
            var config = _configService.Config;
            if (config.WindowLeft.HasValue && config.WindowTop.HasValue)
            {
                Left = config.WindowLeft.Value;
                Top = config.WindowTop.Value;
            }
            else
            {
                Left = SystemParameters.WorkArea.Width - Width - 20;
                Top = 20;
            }

            if (config.WindowWidth.HasValue && config.WindowHeight.HasValue)
            {
                Width = config.WindowWidth.Value;
                Height = config.WindowHeight.Value;
            }

            _scrollTimer.Tick += ScrollTimer_Tick;
            _scrollTimer.Start();

            LocationChanged += (_, _) => SaveWindowPosition();
            SizeChanged += (_, _) => SaveWindowPosition();

            Loaded += async (_, _) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            // Load jobs immediately on startup
            await LoadJobsAsync();

            // Set up refresh timer based on config
            var refreshInterval = TimeSpan.FromMinutes(_configService.Config.RefreshIntervalMinutes);
            _refreshTimer = new DispatcherTimer { Interval = refreshInterval };
            _refreshTimer.Tick += async (_, _) => await LoadJobsAsync();

            if (_configService.Config.AutoRefreshEnabled)
            {
                _refreshTimer.Start();
            }
        }

        private async Task LoadJobsAsync()
        {
            StatusText.Text = "Refreshing...";

            var config = _configService.Config;
            var results = await _jobAggregator.FetchJobsAsync(
                config.SearchKeywords,
                config.SearchLocation,
                salaryMin: config.SalaryMin,
                workMode: config.WorkMode);

            _jobs.Clear();
            foreach (var job in results)
            {
                _jobs.Add(job);
            }

            StatusText.Text = $"Updated {DateTime.Now:h:mm tt} • {_jobs.Count} postings";
        }

        private void ScrollTimer_Tick(object? sender, EventArgs e)
        {
            if (JobScrollViewer.ExtentHeight <= JobScrollViewer.ViewportHeight)
            {
                // Not enough content to scroll yet
                return;
            }

            var newOffset = JobScrollViewer.VerticalOffset + ScrollStepPixels;

            // Loop back to the top once we reach the bottom
            if (newOffset >= JobScrollViewer.ExtentHeight - JobScrollViewer.ViewportHeight)
            {
                newOffset = 0;
            }

            JobScrollViewer.ScrollToVerticalOffset(newOffset);
        }

        private void SaveWindowPosition()
        {
            var config = _configService.Config;
            config.WindowLeft = Left;
            config.WindowTop = Top;
            config.WindowWidth = Width;
            config.WindowHeight = Height;
            _configService.Save();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Lets you drag the borderless window around by clicking anywhere on it
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void JobItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Mark handled so this click doesn't bubble up and trigger the
            // window-drag handler instead of opening the link.
            e.Handled = true;

            if (sender is FrameworkElement { DataContext: JobPosting job } && !string.IsNullOrWhiteSpace(job.Url))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(job.Url) { UseShellExecute = true });
                }
                catch
                {
                    // If the link fails to open for some reason, just ignore it
                    // rather than crashing the widget.
                }
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // If a settings window is already open, just bring it to the front
                // instead of opening a second one
                if (_settingsWindow is not null)
                {
                    _settingsWindow.Activate();
                    return;
                }

                _settingsWindow = new SettingsWindow(_configService, ReloadOnSettingsClose);
                _settingsWindow.Closed += (_, _) => _settingsWindow = null;
                _settingsWindow.Show();
        }

        private async void ReloadOnSettingsClose()
        {
            // When settings window closes, reload the jobs with new config values
            await LoadJobsAsync();

            // Apply any change to auto-refresh enabled/disabled or the interval itself
            if (_refreshTimer is not null)
            {
                _refreshTimer.Stop();
                _refreshTimer.Interval = TimeSpan.FromMinutes(_configService.Config.RefreshIntervalMinutes);

                if (_configService.Config.AutoRefreshEnabled)
                {
                    _refreshTimer.Start();
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
