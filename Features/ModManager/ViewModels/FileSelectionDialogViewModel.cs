using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using InazumaElevenVRSaveEditor.Common.Infrastructure;

namespace InazumaElevenVRSaveEditor.Features.ModManager.ViewModels
{
    public class FileSelectionDialogViewModel : ViewModelBase
    {
        private string _modName = string.Empty;

        public FileSelectionDialogViewModel(string modName, ObservableCollection<DownloadFileItemViewModel> availableFiles)
        {
            ModName = modName;
            AvailableFiles = availableFiles;

            SelectAllCommand = new RelayCommand(() => SelectAll());
            DeselectAllCommand = new RelayCommand(() => DeselectAll());
            ConfirmCommand = new RelayCommand(() => OnConfirm(), () => AvailableFiles.Any(f => f.IsSelected));
            CancelCommand = new RelayCommand(() => OnCancel());
        }

        public string ModName
        {
            get => _modName;
            set => SetProperty(ref _modName, value);
        }

        public ObservableCollection<DownloadFileItemViewModel> AvailableFiles { get; }

        public ICommand SelectAllCommand { get; }
        public ICommand DeselectAllCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public event EventHandler? Confirmed;
        public event EventHandler? Cancelled;

        private void SelectAll()
        {
            foreach (var file in AvailableFiles)
            {
                file.IsSelected = true;
            }
        }

        private void DeselectAll()
        {
            foreach (var file in AvailableFiles)
            {
                file.IsSelected = false;
            }
        }

        private void OnConfirm()
        {
            Confirmed?.Invoke(this, EventArgs.Empty);
        }

        private void OnCancel()
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
        }
    }
}
