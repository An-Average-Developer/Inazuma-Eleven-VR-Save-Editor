using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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

        private void SkipStoryInfo_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Skip Story Mod\n\n" +
                "Credits to: iNerfthisGame\n\n" +
                "Would you like to visit the mod page?",
                "Skip Story - Credits",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://gamebanana.com/mods/641663",
                        UseShellExecute = true
                    });
                }
                catch { }
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

        private void SpiritsEditorCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ViewModel != null && ViewModel.MemoryEditor.SelectSpiritsEditorCommand.CanExecute(null))
            {
                ViewModel.MemoryEditor.SelectSpiritsEditorCommand.Execute(null);
            }
        }

        private void PlayerSpiritsEditorCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ViewModel != null && ViewModel.MemoryEditor.SelectPlayerSpiritsEditorCommand.CanExecute(null))
            {
                ViewModel.MemoryEditor.SelectPlayerSpiritsEditorCommand.Execute(null);
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

        private void SpecialMovesEditorCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ViewModel != null && ViewModel.MemoryEditor.SelectSpecialMovesEditorCommand.CanExecute(null))
            {
                ViewModel.MemoryEditor.SelectSpecialMovesEditorCommand.Execute(null);
            }
        }

        private void CustomPassivesEditorCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                // Show custom dialog with three options
                var dialogResult = ShowCustomPassivesDialog();

                if (dialogResult == "Install")
                {
                    if (!InstallCheatEngine())
                    {
                        MessageBox.Show(
                            "Failed to install Cheat Engine.",
                            "Installation Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }

                    MessageBox.Show(
                        "Cheat Engine installer has been launched!\n\n" +
                        "Please complete the installation and then click the Custom Passives button again.",
                        "Installation Started",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }
                else if (dialogResult == "Tutorial")
                {
                    // Show tutorial steps with images
                    ShowTutorialSteps();

                    // Final confirmation
                    var openResult = MessageBox.Show(
                        "Do you want to open Cheat Engine?",
                        "Open Cheat Engine",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (openResult == MessageBoxResult.Yes)
                    {
                        OpenCheatTableFile();
                    }
                }
                // If "No", do nothing
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private string ShowCustomPassivesDialog()
        {
            string result = "No";

            var dialog = new Window
            {
                Title = "Custom Passives Tutorial",
                Width = 500,
                Height = 280,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowStyle = WindowStyle.SingleBorderWindow,
                ResizeMode = ResizeMode.NoResize,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240)),
                Style = null,
                Resources = new ResourceDictionary()
            };

            var mainGrid = new Grid
            {
                Margin = new Thickness(20),
                Style = null,
                Resources = new ResourceDictionary()
            };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var messageText = new TextBlock
            {
                Text = "This will open the Custom Passives Cheat Table in Cheat Engine.\n\n" +
                       "To attach to the game, follow the next steps.\n\n" +
                       "Do you want to see the tutorial?",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Style = null,
                Resources = new ResourceDictionary()
            };
            Grid.SetRow(messageText, 0);
            mainGrid.Children.Add(messageText);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0),
                Style = null,
                Resources = new ResourceDictionary()
            };

            var yesButton = new Button
            {
                Content = "Yes",
                Width = 100,
                Height = 35,
                FontSize = 14,
                Margin = new Thickness(5),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215)),
                Cursor = System.Windows.Input.Cursors.Hand,
                Style = null,
                Resources = new ResourceDictionary()
            };
            yesButton.Click += (s, e) => { result = "Tutorial"; dialog.Close(); };

            var noButton = new Button
            {
                Content = "No",
                Width = 100,
                Height = 35,
                FontSize = 14,
                Margin = new Thickness(5),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 180)),
                Foreground = System.Windows.Media.Brushes.Black,
                BorderThickness = new Thickness(1),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(160, 160, 160)),
                Cursor = System.Windows.Input.Cursors.Hand,
                Style = null,
                Resources = new ResourceDictionary()
            };
            noButton.Click += (s, e) => { result = "No"; dialog.Close(); };

            var installButton = new Button
            {
                Content = "Install Cheat Engine",
                Width = 150,
                Height = 35,
                FontSize = 14,
                Margin = new Thickness(5),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 150, 136)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 150, 136)),
                Cursor = System.Windows.Input.Cursors.Hand,
                Style = null,
                Resources = new ResourceDictionary()
            };
            installButton.Click += (s, e) => { result = "Install"; dialog.Close(); };

            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);
            buttonPanel.Children.Add(installButton);
            Grid.SetRow(buttonPanel, 1);
            mainGrid.Children.Add(buttonPanel);

            dialog.Content = mainGrid;
            dialog.ShowDialog();

            return result;
        }

        private void ShowTutorialSteps()
        {
            string[] tutorialImages = { "1.png", "2.png", "3.png" };

            for (int i = 0; i < tutorialImages.Length; i++)
            {
                // Use pack URI to load embedded resource images
                string imageUri = $"pack://application:,,,/Resources/Cheat Engine/{tutorialImages[i]}";

                try
                {
                    ShowImageWindow(imageUri, i + 1, tutorialImages.Length);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error showing step {i + 1}:\n\n{ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void ShowImageWindow(string imagePath, int stepNumber, int totalSteps)
        {
            // Create a simple window without Material Design themes
            var window = new Window
            {
                Title = $"Tutorial - Step {stepNumber} of {totalSteps}",
                Width = 900,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowStyle = WindowStyle.SingleBorderWindow,
                ResizeMode = ResizeMode.CanResize,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240)),
                Style = null, // Remove any inherited styles
                Resources = new ResourceDictionary() // Use empty resources to avoid Material Design
            };

            // Create layout
            var mainGrid = new Grid
            {
                Style = null,
                Resources = new ResourceDictionary()
            };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Image container with scrollviewer
            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(10),
                Style = null,
                Resources = new ResourceDictionary()
            };

            var image = new System.Windows.Controls.Image
            {
                Stretch = System.Windows.Media.Stretch.Uniform,
                Style = null,
                Resources = new ResourceDictionary()
            };

            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                image.Source = bitmap;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            scrollViewer.Content = image;
            Grid.SetRow(scrollViewer, 0);
            mainGrid.Children.Add(scrollViewer);

            // Button
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240)),
                Style = null,
                Resources = new ResourceDictionary()
            };

            var nextButton = new Button
            {
                Content = stepNumber < totalSteps ? "Next Step" : "Finish Tutorial",
                Width = 120,
                Height = 35,
                FontSize = 14,
                Margin = new Thickness(5),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215)),
                Cursor = System.Windows.Input.Cursors.Hand,
                Style = null, // Critical: Remove Material Design button style
                Resources = new ResourceDictionary()
            };
            nextButton.Click += (s, e) => window.Close();

            buttonPanel.Children.Add(nextButton);
            Grid.SetRow(buttonPanel, 1);
            mainGrid.Children.Add(buttonPanel);

            window.Content = mainGrid;
            window.ShowDialog();
        }

        private void OpenCheatTableFile()
        {
            try
            {
                // Extract embedded resource to temp file
                string tempDir = Path.Combine(Path.GetTempPath(), "InazumaElevenVR");
                Directory.CreateDirectory(tempDir);
                string ctFilePath = Path.Combine(tempDir, "Custom Passives.CT");

                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (Stream? stream = assembly.GetManifestResourceStream("InazumaElevenVRSaveEditor.Resources.Custom Passives.CT"))
                {
                    if (stream == null)
                    {
                        MessageBox.Show(
                            "Custom Passives.CT embedded resource not found.",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }

                    using (FileStream fileStream = File.Create(ctFilePath))
                    {
                        stream.CopyTo(fileStream);
                    }
                }

                // Open the .CT file with default program (Cheat Engine if installed)
                Process.Start(new ProcessStartInfo
                {
                    FileName = ctFilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error opening Cheat Table file:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool InstallCheatEngine()
        {
            try
            {
                // Create temp directory
                string tempDir = Path.Combine(Path.GetTempPath(), "CheatEngineInstaller_" + Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);
                string zipPath = Path.Combine(tempDir, "CheatEngineInstaller.zip");

                // Extract embedded resource to temp file
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (Stream? stream = assembly.GetManifestResourceStream("InazumaElevenVRSaveEditor.Resources.CheatEngineInstaller.zip"))
                {
                    if (stream == null)
                    {
                        MessageBox.Show(
                            "CheatEngineInstaller.zip embedded resource not found.",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return false;
                    }

                    using (FileStream fileStream = File.Create(zipPath))
                    {
                        stream.CopyTo(fileStream);
                    }
                }

                try
                {
                    // Extract zip
                    string extractDir = Path.Combine(tempDir, "extracted");
                    Directory.CreateDirectory(extractDir);
                    ZipFile.ExtractToDirectory(zipPath, extractDir);

                    // Find installer executable
                    string[] installerFiles = Directory.GetFiles(tempDir, "*.exe", SearchOption.AllDirectories);
                    if (installerFiles.Length == 0)
                    {
                        MessageBox.Show(
                            "No installer executable found in CheatEngineInstaller.zip",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return false;
                    }

                    string installerPath = installerFiles[0];

                    // Run installer
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = installerPath,
                        UseShellExecute = true
                    };

                    Process.Start(processInfo);
                    return true;
                }
                finally
                {
                    // Clean up temp directory after a delay (don't block)
                    System.Threading.Tasks.Task.Delay(5000).ContinueWith(_ =>
                    {
                        try
                        {
                            if (Directory.Exists(tempDir))
                                Directory.Delete(tempDir, true);
                        }
                        catch { }
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error installing Cheat Engine:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

    }
}
