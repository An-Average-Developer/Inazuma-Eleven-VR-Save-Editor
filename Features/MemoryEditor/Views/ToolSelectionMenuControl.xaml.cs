using System.Windows;
using System.Windows.Controls;
using InazumaElevenVRSaveEditor.ViewModels;

namespace InazumaElevenVRSaveEditor.Features.MemoryEditor.Views
{
    public partial class ToolSelectionMenuControl : UserControl
    {
        private MainViewModel? ViewModel => DataContext as MainViewModel;

        public ToolSelectionMenuControl()
        {
            InitializeComponent();
        }

        private void TicketEditorCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ViewModel != null && ViewModel.MemoryEditor.SelectTicketEditorCommand.CanExecute(null))
            {
                ViewModel.MemoryEditor.SelectTicketEditorCommand.Execute(null);
            }
        }

        private void InaFlowersEditorCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ViewModel != null && ViewModel.MemoryEditor.SelectInaFlowersEditorCommand.CanExecute(null))
            {
                ViewModel.MemoryEditor.SelectInaFlowersEditorCommand.Execute(null);
            }
        }

        private void SpiritsEditorCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ViewModel != null && ViewModel.MemoryEditor.SelectSpiritsEditorCommand.CanExecute(null))
            {
                ViewModel.MemoryEditor.SelectSpiritsEditorCommand.Execute(null);
            }
        }

        private void BeansEditorCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ViewModel != null && ViewModel.MemoryEditor.SelectBeansEditorCommand.CanExecute(null))
            {
                ViewModel.MemoryEditor.SelectBeansEditorCommand.Execute(null);
            }
        }

        private void VictoryItemsEditorCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ViewModel != null && ViewModel.MemoryEditor.SelectVictoryItemsEditorCommand.CanExecute(null))
            {
                ViewModel.MemoryEditor.SelectVictoryItemsEditorCommand.Execute(null);
            }
        }

        private void PassiveValuesEditorCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ViewModel != null && ViewModel.MemoryEditor.SelectPassiveValuesEditorCommand.CanExecute(null))
            {
                ViewModel.MemoryEditor.SelectPassiveValuesEditorCommand.Execute(null);
            }
        }
    }
}
