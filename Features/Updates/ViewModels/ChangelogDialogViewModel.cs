using System.Windows;
using System.Windows.Input;
using InazumaElevenVRSaveEditor.Common.Infrastructure;
using InazumaElevenVRSaveEditor.Features.Updates.Views;

namespace InazumaElevenVRSaveEditor.Features.Updates.ViewModels
{
    public class ChangelogDialogViewModel : ViewModelBase
    {
        private readonly Window _window;
        private string _changelog = string.Empty;
        private string _versionInfo = string.Empty;
        private string _fileSizeText = string.Empty;

        public ChangelogDialogViewModel(Window window, string changelog, string latestVersion, long fileSize)
        {
            _window = window;
            _changelog = changelog;
            var versionText = latestVersion.StartsWith("v", System.StringComparison.OrdinalIgnoreCase)
                ? latestVersion
                : $"v{latestVersion}";
            _versionInfo = $"Version {versionText}";

            if (fileSize > 0)
            {
                var sizeInMB = fileSize / (1024.0 * 1024.0);
                _fileSizeText = $"Download size: {sizeInMB:F2} MB";
            }
            else
            {
                _fileSizeText = "Download size: Unknown";
            }

            UpdateCommand = new RelayCommand(OnUpdate);
            CancelCommand = new RelayCommand(OnCancel);
        }

        public string Changelog
        {
            get => _changelog;
            set => SetProperty(ref _changelog, value);
        }

        public string VersionInfo
        {
            get => _versionInfo;
            set => SetProperty(ref _versionInfo, value);
        }

        public string FileSizeText
        {
            get => _fileSizeText;
            set => SetProperty(ref _fileSizeText, value);
        }

        public ICommand UpdateCommand { get; }
        public ICommand CancelCommand { get; }

        private void OnUpdate()
        {
            if (_window is ChangelogDialog dialog)
            {
                dialog.UserAccepted = true;
            }
            _window.DialogResult = true;
            _window.Close();
        }

        private void OnCancel()
        {
            if (_window is ChangelogDialog dialog)
            {
                dialog.UserAccepted = false;
            }
            _window.DialogResult = false;
            _window.Close();
        }
    }
}
