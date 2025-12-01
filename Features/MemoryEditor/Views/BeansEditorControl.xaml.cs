using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace InazumaElevenVRSaveEditor.Features.MemoryEditor.Views
{
    public partial class BeansEditorControl : UserControl
    {
        public BeansEditorControl()
        {
            InitializeComponent();
        }

        private void SpiritCard_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var grid = sender as Grid;
            if (grid != null)
            {
                var storyboard = Application.Current.FindResource("SpiritHoverEnter") as Storyboard;
                if (storyboard != null)
                {
                    storyboard.Begin(grid);
                }
            }
        }

        private void SpiritCard_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var grid = sender as Grid;
            if (grid != null)
            {
                var storyboard = Application.Current.FindResource("SpiritHoverLeave") as Storyboard;
                if (storyboard != null)
                {
                    storyboard.Begin(grid);
                }
            }
        }

        private void SpiritCard_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var grid = sender as Grid;
            if (grid != null)
            {
                var storyboard = Application.Current.FindResource("SpiritClickAnimation") as Storyboard;
                if (storyboard != null)
                {
                    storyboard.Begin(grid);
                }
            }
        }
    }
}
