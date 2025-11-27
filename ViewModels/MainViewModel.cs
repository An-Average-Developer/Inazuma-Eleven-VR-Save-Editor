using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using InazumaElevenVRSaveEditor.Common.Infrastructure;
using InazumaElevenVRSaveEditor.Configuration;
using InazumaElevenVRSaveEditor.Features.Updates.Services;
using InazumaElevenVRSaveEditor.Features.Updates.Views;
using InazumaElevenVRSaveEditor.Features.MemoryEditor.ViewModels;
using InazumaElevenVRSaveEditor.Features.ModManager.ViewModels;

namespace InazumaElevenVRSaveEditor.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly UpdateService _updateService;
        private readonly MemoryEditorViewModel _memoryEditor;
        private ModManagerViewModel? _modManager;

        private string _statusMessage = "Ready";
        private bool _isLoading;

        private string _currentVersion = string.Empty;
        private string _latestVersion = string.Empty;
        private string _updateFileName = string.Empty;
        private string _updateFileSize = string.Empty;
        private bool _isUpdateAvailable;
        private bool _hasCheckedForUpdates;
        private bool _isDownloading;
        private int _downloadProgress;
        private string _downloadProgressText = string.Empty;
        private string _updateStatusTitle = string.Empty;
        private string _updateStatusMessage = string.Empty;
        private string _updateStatusIcon = "CheckCircle";
        private Brush _updateStatusColor = new SolidColorBrush(Colors.Gray);
        private string _updateStatusCursor = "Arrow";
        private UpdateInfo? _currentUpdateInfo;
        private bool _showingInstallationView = false;
        private string _changelog = string.Empty;

        public MainViewModel()
        {
            _updateService = new UpdateService();
            _memoryEditor = new MemoryEditorViewModel();

            _currentVersion = AppVersion.GetDisplayVersion();
            OnPropertyChanged(nameof(CurrentVersion));

            CheckForUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesAsync());
            DownloadUpdateCommand = new RelayCommand(async () => await DownloadUpdateAsync(), () => IsUpdateAvailable && !IsDownloading);
            OpenGitHubCommand = new RelayCommand(() => _updateService.OpenGitHubPage());
            ShowInstallationViewCommand = new RelayCommand(() => ShowInstallationView(), () => IsUpdateAvailable);
            CancelInstallationCommand = new RelayCommand(() => CancelInstallation());
            AcceptInstallationCommand = new RelayCommand(async () => await AcceptInstallationAsync(), () => !IsDownloading);

            _ = InitializeAsync();
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public MemoryEditorViewModel MemoryEditor => _memoryEditor;

        public ModManagerViewModel ModManager
        {
            get
            {
                if (_modManager == null)
                {
                    _modManager = new ModManagerViewModel();
                    _ = _modManager.EnsureInitializedAsync();
                }
                return _modManager;
            }
        }

        public ICommand CheckForUpdatesCommand { get; }
        public ICommand DownloadUpdateCommand { get; }
        public ICommand OpenGitHubCommand { get; }
        public ICommand ShowInstallationViewCommand { get; }
        public ICommand CancelInstallationCommand { get; }
        public ICommand AcceptInstallationCommand { get; }

        public string CurrentVersion
        {
            get => _currentVersion;
            set => SetProperty(ref _currentVersion, value);
        }

        public string LatestVersion
        {
            get => _latestVersion;
            set => SetProperty(ref _latestVersion, value);
        }

        public string UpdateFileName
        {
            get => _updateFileName;
            set => SetProperty(ref _updateFileName, value);
        }

        public string UpdateFileSize
        {
            get => _updateFileSize;
            set => SetProperty(ref _updateFileSize, value);
        }

        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            set
            {
                SetProperty(ref _isUpdateAvailable, value);
                ((RelayCommand)DownloadUpdateCommand).RaiseCanExecuteChanged();
            }
        }

        public bool HasCheckedForUpdates
        {
            get => _hasCheckedForUpdates;
            set => SetProperty(ref _hasCheckedForUpdates, value);
        }

        public bool IsDownloading
        {
            get => _isDownloading;
            set
            {
                SetProperty(ref _isDownloading, value);
                ((RelayCommand)DownloadUpdateCommand).RaiseCanExecuteChanged();
            }
        }

        public int DownloadProgress
        {
            get => _downloadProgress;
            set => SetProperty(ref _downloadProgress, value);
        }

        public string DownloadProgressText
        {
            get => _downloadProgressText;
            set => SetProperty(ref _downloadProgressText, value);
        }

        public string UpdateStatusTitle
        {
            get => _updateStatusTitle;
            set => SetProperty(ref _updateStatusTitle, value);
        }

        public string UpdateStatusMessage
        {
            get => _updateStatusMessage;
            set => SetProperty(ref _updateStatusMessage, value);
        }

        public string UpdateStatusIcon
        {
            get => _updateStatusIcon;
            set => SetProperty(ref _updateStatusIcon, value);
        }

        public Brush UpdateStatusColor
        {
            get => _updateStatusColor;
            set => SetProperty(ref _updateStatusColor, value);
        }

        public string UpdateStatusCursor
        {
            get => _updateStatusCursor;
            set => SetProperty(ref _updateStatusCursor, value);
        }

        public bool ShowingInstallationView
        {
            get => _showingInstallationView;
            set => SetProperty(ref _showingInstallationView, value);
        }

        public string Changelog
        {
            get => _changelog;
            set => SetProperty(ref _changelog, value);
        }

        private async Task InitializeAsync()
        {
            await CheckForUpdatesAsync(silent: true);
        }

        private async Task CheckForUpdatesAsync(bool silent = false)
        {
            if (!silent)
            {
                IsLoading = true;
                StatusMessage = "Checking for updates...";
            }

            try
            {
                var updateInfo = await _updateService.CheckForUpdatesAsync();
                _currentUpdateInfo = updateInfo;

                HasCheckedForUpdates = true;

                var latestVersionFormatted = updateInfo.LatestVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                    ? updateInfo.LatestVersion
                    : $"v{updateInfo.LatestVersion}";

                _latestVersion = latestVersionFormatted;
                OnPropertyChanged(nameof(LatestVersion));

                System.Diagnostics.Debug.WriteLine($"[MainViewModel] CurrentVersion: {CurrentVersion}");
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] LatestVersion: {LatestVersion}");

                UpdateFileName = updateInfo.FileName;

                if (updateInfo.FileSize > 0)
                {
                    var sizeInMB = updateInfo.FileSize / (1024.0 * 1024.0);
                    UpdateFileSize = $"{sizeInMB:F2} MB";
                }
                else
                {
                    UpdateFileSize = "Unknown";
                }

                IsUpdateAvailable = updateInfo.IsUpdateAvailable;

                if (updateInfo.IsUpdateAvailable)
                {
                    _updateStatusTitle = "Update Available!";
                    _updateStatusMessage = $"A new version ({LatestVersion}) is available for download. Click here to update!";
                    _updateStatusIcon = "Cloud";
                    _updateStatusColor = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    _updateStatusCursor = "Hand";

                    OnPropertyChanged(nameof(UpdateStatusTitle));
                    OnPropertyChanged(nameof(UpdateStatusMessage));
                    OnPropertyChanged(nameof(UpdateStatusIcon));
                    OnPropertyChanged(nameof(UpdateStatusColor));
                    OnPropertyChanged(nameof(UpdateStatusCursor));

                    if (!silent)
                    {
                        StatusMessage = $"Update available: {LatestVersion}";
                    }
                }
                else
                {
                    _updateStatusTitle = "You're up to date!";
                    _updateStatusMessage = $"You have the latest version ({CurrentVersion}).";
                    _updateStatusIcon = "CheckCircle";
                    _updateStatusColor = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                    _updateStatusCursor = "Arrow";

                    OnPropertyChanged(nameof(UpdateStatusTitle));
                    OnPropertyChanged(nameof(UpdateStatusMessage));
                    OnPropertyChanged(nameof(UpdateStatusIcon));
                    OnPropertyChanged(nameof(UpdateStatusColor));
                    OnPropertyChanged(nameof(UpdateStatusCursor));

                    if (!silent)
                    {
                        StatusMessage = "No updates available";
                    }
                }
            }
            catch (Exception ex)
            {
                _updateStatusTitle = "Update Check Failed";
                _updateStatusMessage = $"Could not check for updates. Error: {ex.Message}";
                _updateStatusIcon = "AlertCircle";
                _updateStatusColor = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                _updateStatusCursor = "Arrow";

                OnPropertyChanged(nameof(UpdateStatusTitle));
                OnPropertyChanged(nameof(UpdateStatusMessage));
                OnPropertyChanged(nameof(UpdateStatusIcon));
                OnPropertyChanged(nameof(UpdateStatusColor));
                OnPropertyChanged(nameof(UpdateStatusCursor));

                HasCheckedForUpdates = true;

                if (!silent)
                {
                    StatusMessage = "Failed to check for updates";
                    MessageBox.Show($"Failed to check for updates:\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}", "Update Check Failed",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            finally
            {
                if (!silent)
                {
                    IsLoading = false;
                }
            }
        }

        private async Task DownloadUpdateAsync()
        {
            if (_currentUpdateInfo == null || !IsUpdateAvailable)
                return;

            var changelogDialog = new ChangelogDialog(
                _currentUpdateInfo.Changelog,
                _currentUpdateInfo.LatestVersion,
                _currentUpdateInfo.FileSize
            );

            var result = changelogDialog.ShowDialog();

            if (result != true || !changelogDialog.UserAccepted)
            {
                StatusMessage = "Update cancelled";
                return;
            }

            IsDownloading = true;
            DownloadProgress = 0;
            DownloadProgressText = "Starting download...";
            StatusMessage = "Downloading update...";

            try
            {
                var progress = new Progress<int>(percent =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        DownloadProgress = percent;
                        if (percent <= 50)
                        {
                            DownloadProgressText = $"Downloading... {percent * 2}%";
                        }
                        else if (percent <= 75)
                        {
                            DownloadProgressText = "Extracting files...";
                        }
                        else if (percent <= 90)
                        {
                            DownloadProgressText = "Preparing update...";
                        }
                        else
                        {
                            DownloadProgressText = "Finalizing...";
                        }
                    });
                });

                var success = await _updateService.DownloadAndInstallUpdateAsync(_currentUpdateInfo, progress);

                if (!success)
                {
                    StatusMessage = "Failed to download update";
                    MessageBox.Show(
                        "Failed to install the update. Please try again or download manually from GitHub.",
                        "Update Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Error downloading update";
                MessageBox.Show($"Error downloading update:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsDownloading = false;
                DownloadProgress = 0;
                DownloadProgressText = string.Empty;
            }
        }

        private void ShowInstallationView()
        {
            if (_currentUpdateInfo == null || !IsUpdateAvailable)
                return;

            Changelog = _currentUpdateInfo.Changelog;
            ShowingInstallationView = true;
        }

        private void CancelInstallation()
        {
            ShowingInstallationView = false;
            StatusMessage = "Update cancelled";
        }

        private async Task AcceptInstallationAsync()
        {
            if (_currentUpdateInfo == null || !IsUpdateAvailable)
                return;

            IsDownloading = true;
            DownloadProgress = 0;
            DownloadProgressText = "Starting download...";
            StatusMessage = "Downloading update...";

            try
            {
                var progress = new Progress<int>(percent =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        DownloadProgress = percent;
                        if (percent < 100)
                        {
                            DownloadProgressText = $"Downloading... {percent}%";
                        }
                        else
                        {
                            DownloadProgressText = "Installing...";
                        }
                    });
                });

                var success = await _updateService.DownloadAndInstallUpdateAsync(_currentUpdateInfo, progress);

                if (!success)
                {
                    StatusMessage = "Failed to download update";
                    MessageBox.Show(
                        "Failed to install the update. Please try again or download manually from GitHub.",
                        "Update Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    ShowingInstallationView = false;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Error downloading update";
                MessageBox.Show($"Error downloading update:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                ShowingInstallationView = false;
            }
            finally
            {
                IsDownloading = false;
                DownloadProgress = 0;
                DownloadProgressText = string.Empty;
            }
        }
    }
}
