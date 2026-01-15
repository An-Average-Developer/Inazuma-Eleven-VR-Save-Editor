using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using InazumaElevenVRSaveEditor.Features.MemoryEditor.ViewModels;

namespace InazumaElevenVRSaveEditor.Features.MemoryEditor.Views
{
    public partial class TutorialsControl : UserControl
    {
        private DispatcherTimer _timer;
        private bool _isDragging = false;

        public TutorialsControl()
        {
            InitializeComponent();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _timer.Tick += Timer_Tick;
        }

        private void VideoCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string filePath)
            {
                var viewModel = DataContext as TutorialsViewModel;
                if (viewModel != null)
                {
                    viewModel.SetVideoPlayer(VideoPlayer);
                    viewModel.PlayVideo(filePath);
                    _timer.Start();
                }
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (VideoPlayer.NaturalDuration.HasTimeSpan && !_isDragging)
            {
                var viewModel = DataContext as TutorialsViewModel;
                if (viewModel != null)
                {
                    viewModel.UpdatePosition(VideoPlayer.Position, VideoPlayer.NaturalDuration.TimeSpan);
                }
            }
        }

        private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as TutorialsViewModel;
            if (viewModel != null && VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                viewModel.SetTotalDuration(VideoPlayer.NaturalDuration.TimeSpan);
            }
        }

        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            VideoPlayer.Stop();
            _timer.Stop();
        }

        private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider)
            {
                // Check if user is dragging
                if (slider.IsMouseCaptureWithin)
                {
                    _isDragging = true;
                }
                else if (_isDragging)
                {
                    // User released the slider
                    _isDragging = false;
                    VideoPlayer.Position = TimeSpan.FromSeconds(e.NewValue);
                }
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (VideoPlayer != null)
            {
                VideoPlayer.Volume = e.NewValue;
            }
        }
    }
}
