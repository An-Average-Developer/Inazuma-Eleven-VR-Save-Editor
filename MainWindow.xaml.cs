using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using InazumaElevenVRSaveEditor.ViewModels;
using InazumaElevenVRSaveEditor.Common.Services;
using InazumaElevenVRSaveEditor.Configuration;
using System.Windows.Interop;

namespace InazumaElevenVRSaveEditor
{
    public partial class MainWindow : Window
    {
        private MainViewModel? ViewModel => DataContext as MainViewModel;
        private bool _isThemeAnimating = false;
        private bool _isInitializing = true;
        private bool _isDragging = false;

        // Loading screen fields
        private string[] _cardImages = Array.Empty<string>();
        private int _currentCardIndex = 0;
        private DispatcherTimer? _cardTimer;
        private DispatcherTimer? _progressTimer;
        private double _currentProgress = 0;
        private double _targetProgress = 0;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();

            this.Closing += MainWindow_Closing;
            this.Loaded += MainWindow_Loaded;

            // Prevent maximization
            this.MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
            this.MaxWidth = SystemParameters.MaximizedPrimaryScreenWidth;

            // Load loading screen preference
            var showLoadingScreen = SettingsService.GetShowLoadingScreen();
            LoadingScreenCheckBox.IsChecked = showLoadingScreen;

            // Card images are embedded as resources - use Pack URIs
            _cardImages = new[]
            {
                "pack://application:,,,/Resources/Cards/Spirits.png",
                "pack://application:,,,/Resources/Cards/Stars.png",
                "pack://application:,,,/Resources/Cards/God Hand Flower.png",
                "pack://application:,,,/Resources/Cards/Inazuma Flower.png",
                "pack://application:,,,/Resources/Cards/Kicking Power.png",
                "pack://application:,,,/Resources/Cards/VSTAR.png",
                "pack://application:,,,/Resources/Cards/NPOSS.png"
            };

            System.Diagnostics.Debug.WriteLine($"Loaded {_cardImages.Length} embedded card images");

            // Check if loading screen should be shown
            if (!showLoadingScreen)
            {
                // Hide loading screen immediately if disabled
                LoadingScreenOverlay.Visibility = Visibility.Collapsed;
            }

            // Initialization complete
            _isInitializing = false;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            App.EACLauncherService?.RestoreEACLauncher();
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                _isDragging = true;
                try
                {
                    this.DragMove();
                }
                finally
                {
                    _isDragging = false;
                }
            }
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            // Prevent maximization
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
        }

        private void Window_LocationChanged(object? sender, EventArgs e)
        {
            // Don't interfere with dragging
            if (_isDragging)
                return;

            // Ensure the title bar is always visible
            EnsureWindowIsVisible();
        }

        private void EnsureWindowIsVisible()
        {
            // Get the screen working area
            double screenWidth = SystemParameters.WorkArea.Width;
            double screenHeight = SystemParameters.WorkArea.Height;
            double screenLeft = SystemParameters.WorkArea.Left;
            double screenTop = SystemParameters.WorkArea.Top;

            // Minimum visible height of the title bar (in pixels)
            const double minVisibleHeight = 50;

            // Check if window is going off the top of the screen
            if (this.Top < screenTop)
            {
                this.Top = screenTop;
            }

            // Check if window is going off the bottom
            if (this.Top + minVisibleHeight > screenTop + screenHeight)
            {
                this.Top = screenTop + screenHeight - minVisibleHeight;
            }

            // Check if window is going off the left
            if (this.Left + minVisibleHeight > screenLeft + screenWidth)
            {
                this.Left = screenLeft + screenWidth - minVisibleHeight;
            }

            // Check if window is going off the right
            if (this.Left + this.ActualWidth < screenLeft + minVisibleHeight)
            {
                this.Left = screenLeft + minVisibleHeight - this.ActualWidth;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
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

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsPopup.IsOpen = !SettingsPopup.IsOpen;
        }

        private void LoadingScreenCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // Don't save during initialization
            if (_isInitializing)
                return;

            if (LoadingScreenCheckBox.IsChecked.HasValue)
            {
                SettingsService.SaveShowLoadingScreen(LoadingScreenCheckBox.IsChecked.Value);
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Only show loading screen if enabled
            bool showLoadingScreen = SettingsService.GetShowLoadingScreen();
            if (!showLoadingScreen)
            {
                // If loading screen is disabled, still check for updates after a delay
                await Task.Delay(1000);
                await CheckAndNotifyUpdates();
                return;
            }

            // Start loading screen animations
            StartLoadingAnimations();

            // Simulate loading process - longer delays to see text better
            await Task.Delay(600);
            UpdateLoadingProgress(30, "Loading resources...");
            await Task.Delay(800);
            UpdateLoadingProgress(60, "Initializing...");
            await Task.Delay(800);
            UpdateLoadingProgress(100, "Ready!");
            await Task.Delay(1000);

            // Complete loading and transition to main content
            await CompleteLoading();
        }

        private void StartLoadingAnimations()
        {
            // Load first card image
            if (_cardImages.Length > 0)
            {
                LoadCardImage(_cardImages[0]);

                // Start card changing animation
                _cardTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(600)
                };
                _cardTimer.Tick += (s, e) =>
                {
                    if (_cardImages.Length > 0)
                    {
                        _currentCardIndex = (_currentCardIndex + 1) % _cardImages.Length;
                        LoadCardImage(_cardImages[_currentCardIndex]);
                    }
                };
                _cardTimer.Start();
            }

            // Start pulsing animation for card
            var pulseAnimation = new DoubleAnimationUsingKeyFrames
            {
                RepeatBehavior = RepeatBehavior.Forever,
                Duration = TimeSpan.FromMilliseconds(1200)
            };
            pulseAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
            pulseAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(1.15, KeyTime.FromPercent(0.5)));
            pulseAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));

            LoadingCardScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulseAnimation);
            LoadingCardScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulseAnimation);

            // Gentle rotation animation
            var rotateAnimation = new DoubleAnimation
            {
                From = -5,
                To = 5,
                Duration = TimeSpan.FromSeconds(2),
                RepeatBehavior = RepeatBehavior.Forever,
                AutoReverse = true,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            LoadingCardRotate.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);

            // Glow animation
            var glowAnimation = new DoubleAnimationUsingKeyFrames
            {
                RepeatBehavior = RepeatBehavior.Forever,
                Duration = TimeSpan.FromMilliseconds(1500)
            };
            glowAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(25, KeyTime.FromPercent(0)));
            glowAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(50, KeyTime.FromPercent(0.5)));
            glowAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(25, KeyTime.FromPercent(1.0)));

            LoadingCardGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, glowAnimation);

            // Progress bar smooth animation
            _progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            _progressTimer.Tick += (s, e) =>
            {
                if (_currentProgress < _targetProgress)
                {
                    _currentProgress += (_targetProgress - _currentProgress) * 0.1;
                    if (_targetProgress - _currentProgress < 0.5)
                        _currentProgress = _targetProgress;

                    LoadingProgressBar.Width = (ActualWidth * 0.6) * (_currentProgress / 100.0);
                }
            };
            _progressTimer.Start();
        }

        private void LoadCardImage(string imagePackUri)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Loading image: {imagePackUri}");

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePackUri);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                LoadingCardImage.Source = bitmap;
                System.Diagnostics.Debug.WriteLine($"Successfully loaded image from Pack URI");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load image {imagePackUri}: {ex.Message}");
            }
        }

        private void UpdateLoadingProgress(double progress, string status)
        {
            _targetProgress = progress;
            LoadingStatusText.Text = status;
        }

        private async Task CompleteLoading()
        {
            // Stop animations
            _cardTimer?.Stop();

            // Stop existing animations
            LoadingCardScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            LoadingCardScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            LoadingCardRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            LoadingCardGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, null);

            // Final scale animation
            var finalScale = new DoubleAnimation
            {
                From = 1,
                To = 1.3,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 }
            };

            LoadingCardScale.BeginAnimation(ScaleTransform.ScaleXProperty, finalScale);
            LoadingCardScale.BeginAnimation(ScaleTransform.ScaleYProperty, finalScale);

            await Task.Delay(500);

            // Fade out loading screen
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500));
            fadeOut.Completed += async (s, e) =>
            {
                LoadingScreenOverlay.Visibility = Visibility.Collapsed;
                _progressTimer?.Stop();

                // Check for updates and show notification after loading screen is hidden
                await CheckAndNotifyUpdates();
            };
            LoadingScreenOverlay.BeginAnimation(OpacityProperty, fadeOut);
        }

        private async Task CheckAndNotifyUpdates()
        {
            // Wait a bit for the UI to settle
            await Task.Delay(500);

            // Check if an update is available
            if (ViewModel != null && ViewModel.IsUpdateAvailable)
            {
                var result = MessageBox.Show(
                    $"A new version ({ViewModel.LatestVersion}) is available!\n\n" +
                    "Would you like to view the update details?",
                    "Update Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes && ViewModel.ShowInstallationViewCommand.CanExecute(null))
                {
                    ViewModel.ShowInstallationViewCommand.Execute(null);
                }
            }
        }
    }
}
