using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using InazumaElevenVRSaveEditor.Common.Infrastructure;
using InazumaElevenVRSaveEditor.Features.ModManager.Models;
using InazumaElevenVRSaveEditor.Features.ModManager.Services;
using InazumaElevenVRSaveEditor.Features.ModManager.Views;

namespace InazumaElevenVRSaveEditor.Features.ModManager.ViewModels
{
    public class ModUpdateDialogViewModel : ViewModelBase
    {
        private readonly ModManagerService _modManagerService;
        private readonly ModInfo _mod;
        private string _modName = string.Empty;
        private string _currentVersion = string.Empty;
        private string _latestVersion = string.Empty;
        private bool _isDownloading;
        private double _progressPercentage;
        private string _progressText = string.Empty;
        private string _statusMessage = string.Empty;

        public ModUpdateDialogViewModel(ModManagerService modManagerService, ModInfo mod)
        {
            _modManagerService = modManagerService;
            _mod = mod;

            ModName = mod.Name;
            CurrentVersion = mod.Version;
            LatestVersion = mod.LatestVersion;
            StatusMessage = "Click 'Download & Update' to begin the update process.";

            UpdateCommand = new RelayCommand(async () => await StartUpdateAsync());
            CancelCommand = new RelayCommand(() => CancelUpdate());
        }

        public string ModName
        {
            get => _modName;
            set => SetProperty(ref _modName, value);
        }

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

        public bool IsDownloading
        {
            get => _isDownloading;
            set => SetProperty(ref _isDownloading, value);
        }

        public double ProgressPercentage
        {
            get => _progressPercentage;
            set => SetProperty(ref _progressPercentage, value);
        }

        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand UpdateCommand { get; }
        public ICommand CancelCommand { get; }

        public event EventHandler? UpdateCompleted;
        public event EventHandler? UpdateCancelled;

        private async Task StartUpdateAsync()
        {
            try
            {
                StatusMessage = "Fetching available files...";


                var availableFiles = await _modManagerService.GetAvailableFilesAsync(_mod);

                if (availableFiles == null || availableFiles.Length == 0)
                {
                    StatusMessage = "Error: No download files found for this mod";
                    return;
                }

                GameBananaFile[] selectedFiles;


                if (availableFiles.Length > 1)
                {
                    var fileItems = new ObservableCollection<DownloadFileItemViewModel>(
                        availableFiles.Select(f => new DownloadFileItemViewModel(
                            f.FileName ?? "Unknown",
                            f.DownloadUrl ?? string.Empty,
                            f.FileSize ?? 0,
                            isSelected: false
                        ))
                    );


                    if (fileItems.Count > 0)
                    {
                        fileItems[0].IsSelected = true;
                    }

                    var selectionViewModel = new FileSelectionDialogViewModel(_mod.Name, fileItems);
                    var selectionDialog = new FileSelectionDialog(selectionViewModel);
                    selectionDialog.Owner = Application.Current.MainWindow;

                    var result = selectionDialog.ShowDialog();

                    if (result != true)
                    {

                        return;
                    }


                    var selectedItems = fileItems.Where(f => f.IsSelected).ToList();
                    if (selectedItems.Count == 0)
                    {
                        StatusMessage = "Error: No files selected";
                        return;
                    }


                    selectedFiles = selectedItems.Select(item =>
                        availableFiles.First(f => f.FileName == item.FileName)
                    ).ToArray();
                }
                else
                {

                    selectedFiles = availableFiles;
                }


                IsDownloading = true;
                ProgressPercentage = 0;
                ProgressText = "Preparing download...";


                var progress = new Progress<(double percentage, string status)>(report =>
                {
                    ProgressPercentage = report.percentage;
                    ProgressText = report.status;
                });

                await _modManagerService.DownloadAndUpdateModAsync(_mod, selectedFiles, LatestVersion, progress);

                ProgressPercentage = 100;
                ProgressText = "Update completed!";


                await Task.Delay(500);

                UpdateCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                IsDownloading = false;
            }
        }

        private void CancelUpdate()
        {
            UpdateCancelled?.Invoke(this, EventArgs.Empty);
        }
    }
}
