using System.Windows;

namespace InazumaElevenVRSaveEditor.Features.MemoryEditor.Views
{
    public partial class SpiritsFreezeConfirmDialog : Window
    {
        public SpiritsFreezeConfirmDialog()
        {
            InitializeComponent();
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
