using System.Windows;

namespace InazumaElevenVRSaveEditor.Features.Backup.Views
{
    public partial class AccountInputDialog : Window
    {
        public string AccountId { get; private set; } = string.Empty;

        public AccountInputDialog()
        {
            InitializeComponent();
            AccountIdTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            AccountId = AccountIdTextBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
