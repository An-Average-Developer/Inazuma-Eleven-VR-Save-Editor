using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using MaterialDesignThemes.Wpf;
using InazumaElevenVRSaveEditor.ViewModels;
using InazumaElevenVRSaveEditor.Common.Services;

namespace InazumaElevenVRSaveEditor
{
    public partial class MainWindow : Window
    {
        private MainViewModel? ViewModel => DataContext as MainViewModel;
        private bool _isThemeAnimating = false;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
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

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isThemeAnimating)
                return;

            _isThemeAnimating = true;

            var renderBitmap = new RenderTargetBitmap(
                (int)MainContent.ActualWidth,
                (int)MainContent.ActualHeight,
                96, 96,
                PixelFormats.Pbgra32);
            renderBitmap.Render(MainContent);
            OldThemeSnapshot.Source = renderBitmap;

            var button = ThemeToggleButton;
            var buttonPosition = button.TransformToAncestor(MainContent).Transform(new Point(0, 0));
            var buttonCenter = new Point(
                buttonPosition.X + button.ActualWidth / 2,
                buttonPosition.Y + button.ActualHeight / 2
            );

            var distanceToCorners = new[]
            {
                Math.Sqrt(Math.Pow(buttonCenter.X, 2) + Math.Pow(buttonCenter.Y, 2)),
                Math.Sqrt(Math.Pow(MainContent.ActualWidth - buttonCenter.X, 2) + Math.Pow(buttonCenter.Y, 2)),
                Math.Sqrt(Math.Pow(buttonCenter.X, 2) + Math.Pow(MainContent.ActualHeight - buttonCenter.Y, 2)),
                Math.Sqrt(Math.Pow(MainContent.ActualWidth - buttonCenter.X, 2) + Math.Pow(MainContent.ActualHeight - buttonCenter.Y, 2))
            };
            var maxDistance = distanceToCorners.Max();
            var maxDimension = Math.Max(MainContent.ActualWidth, MainContent.ActualHeight);

            var ringMask = (RadialGradientBrush)this.Resources["RingMaskBrush"];
            var innerEdge = ringMask.GradientStops[1];
            var outerEdge = ringMask.GradientStops[2];

            var normalizedX = buttonCenter.X / MainContent.ActualWidth;
            var normalizedY = buttonCenter.Y / MainContent.ActualHeight;
            ringMask.GradientOrigin = new Point(normalizedX, normalizedY);
            ringMask.Center = new Point(normalizedX, normalizedY);

            innerEdge.Offset = 0;
            outerEdge.Offset = 0.02;

            ThemeService.Instance.ToggleTheme();

            var rotationAnimation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromMilliseconds(600),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            if (ThemeIcon.RenderTransform == null || !(ThemeIcon.RenderTransform is TransformGroup))
            {
                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(new ScaleTransform(1, 1, ThemeIcon.Width / 2, ThemeIcon.Height / 2));
                transformGroup.Children.Add(new RotateTransform(0, ThemeIcon.Width / 2, ThemeIcon.Height / 2));
                ThemeIcon.RenderTransform = transformGroup;
            }

            var transforms = (TransformGroup)ThemeIcon.RenderTransform;
            var scaleTransform = transforms.Children[0] as ScaleTransform;
            var rotateTransform = transforms.Children[1] as RotateTransform;

            var scaleDownAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            var scaleUpAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            scaleDownAnimation.Completed += (s, args) =>
            {
                ThemeIcon.Kind = ThemeService.Instance.IsDarkTheme ? PackIconKind.WeatherSunny : PackIconKind.WeatherNight;

                if (scaleTransform != null)
                {
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleUpAnimation);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleUpAnimation);
                }
            };

            if (rotateTransform != null && scaleTransform != null)
            {
                rotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotationAnimation);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleDownAnimation);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleDownAnimation);
            }

            ThemeTransitionOverlay.Visibility = Visibility.Visible;

            var maxOffset = 1.0;

            var ringAnimation = new DoubleAnimation
            {
                From = 0,
                To = maxOffset,
                Duration = TimeSpan.FromMilliseconds(800),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            ringAnimation.Completed += (s, args) =>
            {
                ThemeTransitionOverlay.Visibility = Visibility.Collapsed;
                OldThemeSnapshot.Source = null;
                innerEdge.Offset = 0;
                outerEdge.Offset = 0.02;
                _isThemeAnimating = false;
            };

            innerEdge.BeginAnimation(GradientStop.OffsetProperty, ringAnimation);

            var outerAnimation = new DoubleAnimation
            {
                From = 0.02,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(800),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            outerEdge.BeginAnimation(GradientStop.OffsetProperty, outerAnimation);
        }
    }
}
