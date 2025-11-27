using System.Windows;
using InazumaElevenVRSaveEditor.Features.ModManager.ViewModels;

namespace InazumaElevenVRSaveEditor.Features.ModManager.Views
{
    public partial class FileSelectionDialog : Window
    {
        public FileSelectionDialog(FileSelectionDialogViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.Confirmed += (s, e) => DialogResult = true;
            viewModel.Cancelled += (s, e) => DialogResult = false;
        }
    }
}
