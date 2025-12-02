using System.Windows;
using InazumaElevenVRSaveEditor.Features.MemoryEditor.ViewModels;

namespace InazumaElevenVRSaveEditor.Features.MemoryEditor.Views
{
    public partial class ItemListWindow : Window
    {
        public ItemListWindow()
        {
            InitializeComponent();
            DataContext = new ItemListWindowViewModel();
        }
    }
}
