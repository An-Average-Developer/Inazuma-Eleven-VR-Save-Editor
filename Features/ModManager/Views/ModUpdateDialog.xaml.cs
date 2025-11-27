using System.Windows;
using InazumaElevenVRSaveEditor.Features.ModManager.ViewModels;

namespace InazumaElevenVRSaveEditor.Features.ModManager.Views
{
    public partial class ModUpdateDialog : Window
    {
        public ModUpdateDialog(ModUpdateDialogViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;


            viewModel.UpdateCompleted += (s, e) =>
            {
                DialogResult = true;
                Close();
            };

            viewModel.UpdateCancelled += (s, e) =>
            {
                DialogResult = false;
                Close();
            };
        }
    }
}
