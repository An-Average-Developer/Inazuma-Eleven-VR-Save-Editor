using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using InazumaElevenVRSaveEditor.Common.Infrastructure;
using InazumaElevenVRSaveEditor.Configuration;
using InazumaElevenVRSaveEditor.Features.Backup.Models;
using InazumaElevenVRSaveEditor.Features.Backup.Services;
using InazumaElevenVRSaveEditor.Features.Updates.Services;
using InazumaElevenVRSaveEditor.Features.Updates.Views;
using InazumaElevenVRSaveEditor.Features.MemoryEditor.ViewModels;
using InazumaElevenVRSaveEditor.Features.ModManager.ViewModels;

namespace InazumaElevenVRSaveEditor.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly BackupService _backupService;
        private readonly UpdateService _updateService;
        private readonly MemoryEditorViewModel _memoryEditor;
        private ModManagerViewModel? _modManager;

        private ObservableCollection<SaveAccount> _accounts = new();
        private ObservableCollection<BackupInfo> _backups = new();
        private SaveAccount? _selectedAccount;
        private SaveAccount? _selectedRestoreAccount;
        private BackupInfo? _selectedBackup;
        private BackupInfo? _selectedManageBackup;
        private string _backupName = string.Empty;
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
            _backupService = new BackupService();
            _updateService = new UpdateService();
            _memoryEditor = new MemoryEditorViewModel();

            _currentVersion = AppVersion.GetDisplayVersion();
            OnPropertyChanged(nameof(CurrentVersion));

            RefreshAccountsCommand = new RelayCommand(async () => await RefreshAccountsAsync());
            RefreshBackupsCommand = new RelayCommand(async () => await RefreshBackupsAsync());
            CreateBackupCommand = new RelayCommand(async () => await CreateBackupAsync(), () => SelectedAccount != null);
            RestoreBackupCommand = new RelayCommand(async () => await RestoreBackupAsync(), () => SelectedBackup != null);
            RestoreBackupToCustomCommand = new RelayCommand(async () => await RestoreBackupToCustomAsync(), () => SelectedBackup != null);
            DeleteBackupCommand = new RelayCommand(async () => await DeleteBackupAsync(), () => SelectedManageBackup != null);
            ExportBackupCommand = new RelayCommand(async () => await ExportBackupAsync(), () => SelectedManageBackup != null);
            ImportBackupCommand = new RelayCommand(async () => await ImportBackupAsync());
            ImportFilesDirectlyCommand = new RelayCommand(async () => await ImportFilesDirectlyAsync());
            OpenBackupFolderCommand = new RelayCommand(() => _backupService.OpenBackupFolder());
            OpenSaveFolderCommand = new RelayCommand(() => _backupService.OpenSaveFolder());

            CheckForUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesAsync());
            DownloadUpdateCommand = new RelayCommand(async () => await DownloadUpdateAsync(), () => IsUpdateAvailable && !IsDownloading);
            OpenGitHubCommand = new RelayCommand(() => _updateService.OpenGitHubPage());
            ShowInstallationViewCommand = new RelayCommand(() => ShowInstallationView(), () => IsUpdateAvailable);
            CancelInstallationCommand = new RelayCommand(() => CancelInstallation());
            AcceptInstallationCommand = new RelayCommand(async () => await AcceptInstallationAsync(), () => !IsDownloading);

            _ = InitializeAsync();
        }

        public ObservableCollection<SaveAccount> Accounts
        {
            get => _accounts;
            set => SetProperty(ref _accounts, value);
        }

        public ObservableCollection<BackupInfo> Backups
        {
            get => _backups;
            set => SetProperty(ref _backups, value);
        }

        public SaveAccount? SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                SetProperty(ref _selectedAccount, value);
                ((RelayCommand)CreateBackupCommand).RaiseCanExecuteChanged();
            }
        }

        public SaveAccount? SelectedRestoreAccount
        {
            get => _selectedRestoreAccount;
            set
            {
                SetProperty(ref _selectedRestoreAccount, value);
                ((RelayCommand)RestoreBackupToCustomCommand).RaiseCanExecuteChanged();
            }
        }

        public BackupInfo? SelectedBackup
        {
            get => _selectedBackup;
            set
            {
                SetProperty(ref _selectedBackup, value);
                ((RelayCommand)RestoreBackupCommand).RaiseCanExecuteChanged();
                ((RelayCommand)RestoreBackupToCustomCommand).RaiseCanExecuteChanged();
            }
        }

        public BackupInfo? SelectedManageBackup
        {
            get => _selectedManageBackup;
            set
            {
                SetProperty(ref _selectedManageBackup, value);
                ((RelayCommand)DeleteBackupCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ExportBackupCommand).RaiseCanExecuteChanged();
            }
        }

        public string BackupName
        {
            get => _backupName;
            set => SetProperty(ref _backupName, value);
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

        public string SaveDirectory => _backupService.BaseSaveDirectory;
        public string BackupDirectory => _backupService.BackupDirectory;

        public ICommand RefreshAccountsCommand { get; }
        public ICommand RefreshBackupsCommand { get; }
        public ICommand CreateBackupCommand { get; }
        public ICommand RestoreBackupCommand { get; }
        public ICommand RestoreBackupToCustomCommand { get; }
        public ICommand DeleteBackupCommand { get; }
        public ICommand ExportBackupCommand { get; }
        public ICommand ImportBackupCommand { get; }
        public ICommand ImportFilesDirectlyCommand { get; }
        public ICommand OpenBackupFolderCommand { get; }
        public ICommand OpenSaveFolderCommand { get; }


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
            await RefreshAccountsAsync();
            await RefreshBackupsAsync();

            if (Accounts.Any())
            {
                SelectedAccount = Accounts.First();
                SelectedRestoreAccount = Accounts.First();
            }

            await CheckForUpdatesAsync(silent: true);
        }

        private async Task RefreshAccountsAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading accounts...";

            try
            {
                await Task.Run(() =>
                {
                    var accounts = _backupService.GetSaveAccounts();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Accounts.Clear();
                        foreach (var account in accounts)
                        {
                            Accounts.Add(account);
                        }
                    });
                });

                StatusMessage = Accounts.Any()
                    ? $"Ready - {Accounts.Count} account(s) found"
                    : "No save files found";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to load accounts:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshBackupsAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading backups...";

            try
            {
                await Task.Run(() =>
                {
                    var backups = _backupService.ListBackups();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Backups.Clear();
                        foreach (var backup in backups)
                        {
                            Backups.Add(backup);
                        }
                    });
                });

                StatusMessage = "Ready";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to load backups:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CreateBackupAsync()
        {
            if (SelectedAccount == null) return;

            IsLoading = true;
            StatusMessage = "Creating backup...";

            try
            {
                var customName = string.IsNullOrWhiteSpace(BackupName) ? null : BackupName;

                var (backupPath, fileCount) = await Task.Run(() =>
                    _backupService.CreateBackup(SelectedAccount.AccountId, customName));

                BackupName = string.Empty;
                await RefreshBackupsAsync();

                StatusMessage = "Backup created successfully!";
                MessageBox.Show($"Backup created successfully!\n\nLocation: {backupPath}\nFiles backed up: {fileCount}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to create backup:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RestoreBackupAsync()
        {
            if (SelectedBackup == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to restore backup '{SelectedBackup.Name}'?\n\n" +
                "This will overwrite existing save files!",
                "Confirm Restore",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            IsLoading = true;
            StatusMessage = "Restoring backup...";

            try
            {
                var fileCount = await Task.Run(() =>
                    _backupService.RestoreBackup(SelectedBackup.Name));

                StatusMessage = "Backup restored successfully!";
                MessageBox.Show($"Backup restored successfully!\n\n{fileCount} files restored.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to restore backup:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RestoreBackupToCustomAsync()
        {
            if (SelectedBackup == null || SelectedRestoreAccount == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to restore backup '{SelectedBackup.Name}' to account:\n{SelectedRestoreAccount.ShortDisplay}\n\n" +
                "This will overwrite existing save files!",
                "Confirm Restore",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            IsLoading = true;
            StatusMessage = "Restoring backup...";

            try
            {
                var fileCount = await Task.Run(() =>
                    _backupService.RestoreBackup(SelectedBackup.Name, SelectedRestoreAccount.AccountId));

                StatusMessage = "Backup restored successfully!";
                MessageBox.Show($"Backup restored successfully!\n\n{fileCount} files restored to {SelectedRestoreAccount.ShortDisplay}.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to restore backup:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task DeleteBackupAsync()
        {
            if (SelectedManageBackup == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete backup '{SelectedManageBackup.Name}'?\n\n" +
                "This action cannot be undone!",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            IsLoading = true;
            StatusMessage = "Deleting backup...";

            try
            {
                await Task.Run(() => _backupService.DeleteBackup(SelectedManageBackup.Name));

                await RefreshBackupsAsync();
                StatusMessage = "Backup deleted successfully!";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to delete backup:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ExportBackupAsync()
        {
            if (SelectedManageBackup == null) return;

            var dialog = new SaveFileDialog
            {
                Title = "Export Backup",
                Filter = "Zip files (*.zip)|*.zip|All files (*.*)|*.*",
                FileName = $"{SelectedManageBackup.Name}.zip",
                DefaultExt = ".zip"
            };

            if (dialog.ShowDialog() != true) return;

            IsLoading = true;
            StatusMessage = "Exporting backup...";

            try
            {
                var exportPath = await Task.Run(() =>
                    _backupService.ExportBackup(SelectedManageBackup.Name, dialog.FileName));

                StatusMessage = "Backup exported successfully!";
                MessageBox.Show($"Backup exported successfully!\n\nLocation: {exportPath}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to export backup:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ImportBackupAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import Backup",
                Filter = "Zip files (*.zip)|*.zip|All files (*.*)|*.*",
                DefaultExt = ".zip"
            };

            if (dialog.ShowDialog() != true) return;

            IsLoading = true;
            StatusMessage = "Importing backup...";

            try
            {
                var backupName = await Task.Run(() =>
                    _backupService.ImportBackup(dialog.FileName));

                await RefreshBackupsAsync();
                StatusMessage = "Backup imported successfully!";
                MessageBox.Show($"Backup imported successfully!\n\nBackup name: {backupName}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to import backup:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ImportFilesDirectlyAsync()
        {
            if (SelectedAccount == null)
            {
                MessageBox.Show("No account selected. Cannot import files.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Import Save Files",
                Filter = "Save Files (*.sav;*.zip)|*.sav;*.zip|Zip files (*.zip)|*.zip|Save files (*.sav)|*.sav|All files (*.*)|*.*",
                DefaultExt = ".sav"
            };

            if (dialog.ShowDialog() != true) return;

            var result = MessageBox.Show(
                $"This will replace the current save files for account:\n{SelectedAccount.ShortDisplay}\n\n" +
                "Are you sure you want to continue?",
                "Confirm Import",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            IsLoading = true;
            StatusMessage = "Importing files...";

            try
            {
                var fileCount = await Task.Run(() =>
                    _backupService.ImportFilesDirectly(dialog.FileName, SelectedAccount.AccountId));

                await RefreshAccountsAsync();
                StatusMessage = "Files imported successfully!";
                MessageBox.Show($"Successfully imported {fileCount} save file(s)!\n\n" +
                    "Your save files have been replaced.",
                    "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to import files:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
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
