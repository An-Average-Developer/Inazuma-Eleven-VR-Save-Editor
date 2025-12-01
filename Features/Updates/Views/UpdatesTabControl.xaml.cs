using System.Windows.Controls;
using InazumaElevenVRSaveEditor.ViewModels;

namespace InazumaElevenVRSaveEditor.Features.Updates.Views
{
    public partial class UpdatesTabControl : UserControl
    {
        private MainViewModel? ViewModel => DataContext as MainViewModel;

        public UpdatesTabControl()
        {
            InitializeComponent();
        }

        private void CheckForUpdatesBanner_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ViewModel != null && ViewModel.CheckForUpdatesCommand.CanExecute(null))
            {
                ViewModel.CheckForUpdatesCommand.Execute(null);
            }
        }

        private void UpdateStatusButton_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ViewModel != null &&
                ViewModel.IsUpdateAvailable &&
                ViewModel.ShowInstallationViewCommand.CanExecute(null))
            {
                ViewModel.ShowInstallationViewCommand.Execute(null);
            }
        }
    }
}
