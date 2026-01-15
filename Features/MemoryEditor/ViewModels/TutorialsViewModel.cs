using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using InazumaElevenVRSaveEditor.Common.Infrastructure;

namespace InazumaElevenVRSaveEditor.Features.MemoryEditor.ViewModels
{
    public class TutorialsViewModel : INotifyPropertyChanged
    {
        private string _tutorialTitle = string.Empty;
        private ObservableCollection<VideoFile> _videoFiles = new();
        private Visibility _noVideosVisible;
        private bool _isPlayingVideo = false;
        private string _currentVideoPath = string.Empty;
        private string _currentVideoTitle = string.Empty;
        private double _currentPosition = 0;
        private double _totalDuration = 0;
        private string _currentTime = "00:00";
        private string _totalTime = "00:00";
        private double _volume = 0.5;
        private MediaElement? _videoPlayer;

        public event PropertyChangedEventHandler? PropertyChanged;

        public TutorialsViewModel(string featureName, Action closeAction)
        {
            TutorialTitle = $"{featureName} Tutorials";
            VideoFiles = new ObservableCollection<VideoFile>();
            CloseCommand = new RelayCommand(_ => closeAction());
            PlayVideoCommand = new RelayCommand(_ => PlayCurrentVideo());
            PauseVideoCommand = new RelayCommand(_ => PauseCurrentVideo());
            StopVideoCommand = new RelayCommand(_ => StopCurrentVideo());

            LoadVideos(featureName);
        }

        public string TutorialTitle
        {
            get => _tutorialTitle;
            set
            {
                _tutorialTitle = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<VideoFile> VideoFiles
        {
            get => _videoFiles;
            set
            {
                _videoFiles = value;
                OnPropertyChanged();
                UpdateNoVideosVisibility();
            }
        }

        public Visibility NoVideosVisible
        {
            get => _noVideosVisible;
            set
            {
                _noVideosVisible = value;
                OnPropertyChanged();
            }
        }

        public bool IsPlayingVideo
        {
            get => _isPlayingVideo;
            set
            {
                _isPlayingVideo = value;
                OnPropertyChanged();
            }
        }

        public string CurrentVideoPath
        {
            get => _currentVideoPath;
            set
            {
                _currentVideoPath = value;
                OnPropertyChanged();
            }
        }

        public string CurrentVideoTitle
        {
            get => _currentVideoTitle;
            set
            {
                _currentVideoTitle = value;
                OnPropertyChanged();
            }
        }

        public double CurrentPosition
        {
            get => _currentPosition;
            set
            {
                _currentPosition = value;
                OnPropertyChanged();
            }
        }

        public double TotalDuration
        {
            get => _totalDuration;
            set
            {
                _totalDuration = value;
                OnPropertyChanged();
            }
        }

        public string CurrentTime
        {
            get => _currentTime;
            set
            {
                _currentTime = value;
                OnPropertyChanged();
            }
        }

        public string TotalTime
        {
            get => _totalTime;
            set
            {
                _totalTime = value;
                OnPropertyChanged();
            }
        }

        public double Volume
        {
            get => _volume;
            set
            {
                _volume = value;
                OnPropertyChanged();
            }
        }

        public ICommand CloseCommand { get; }
        public ICommand PlayVideoCommand { get; }
        public ICommand PauseVideoCommand { get; }
        public ICommand StopVideoCommand { get; }

        private void LoadVideos(string featureName)
        {
            try
            {
                // Supported video formats
                string[] videoExtensions = { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm" };

                // Create temp directory for extracted videos
                string tempDir = Path.Combine(Path.GetTempPath(), "InazumaElevenVR", "Tutorials", featureName);
                Directory.CreateDirectory(tempDir);

                // Get embedded resources matching the feature name
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                string resourcePrefix = $"InazumaElevenVRSaveEditor.Resources.Tutorials.{featureName.Replace(" ", "_")}.";

                var resourceNames = assembly.GetManifestResourceNames()
                    .Where(r => r.StartsWith(resourcePrefix, StringComparison.OrdinalIgnoreCase))
                    .Where(r => videoExtensions.Any(ext => r.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                var videoFilesList = new System.Collections.Generic.List<VideoFile>();

                foreach (var resourceName in resourceNames)
                {
                    // Extract filename from resource name (remove prefix and get the rest)
                    string fileName = resourceName.Substring(resourcePrefix.Length);
                    // Replace underscores back to spaces for display, but keep extension
                    string displayName = Path.GetFileNameWithoutExtension(fileName).Replace("_", " ");
                    string extension = Path.GetExtension(fileName);

                    // Extract to temp file
                    string tempFilePath = Path.Combine(tempDir, displayName + extension);

                    using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            using (FileStream fileStream = File.Create(tempFilePath))
                            {
                                stream.CopyTo(fileStream);
                            }

                            videoFilesList.Add(new VideoFile
                            {
                                FilePath = tempFilePath,
                                FileName = displayName,
                                FileSize = GetFileSizeString(tempFilePath),
                                Thumbnail = GenerateThumbnail(tempFilePath)
                            });
                        }
                    }
                }

                VideoFiles.Clear();
                foreach (var video in videoFilesList.OrderBy(v => v.FileName))
                {
                    VideoFiles.Add(video);
                }

                UpdateNoVideosVisibility();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading tutorial videos:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UpdateNoVideosVisibility()
        {
            NoVideosVisible = VideoFiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private string GetFileSizeString(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                long bytes = fileInfo.Length;

                if (bytes >= 1073741824)
                    return $"{bytes / 1073741824.0:F2} GB";
                if (bytes >= 1048576)
                    return $"{bytes / 1048576.0:F2} MB";
                if (bytes >= 1024)
                    return $"{bytes / 1024.0:F2} KB";
                return $"{bytes} bytes";
            }
            catch
            {
                return "Unknown size";
            }
        }

        public void SetVideoPlayer(MediaElement player)
        {
            _videoPlayer = player;
        }

        public void PlayVideo(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    MessageBox.Show(
                        "Video file not found.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                CurrentVideoPath = filePath;
                CurrentVideoTitle = Path.GetFileNameWithoutExtension(filePath);
                IsPlayingVideo = true;

                if (_videoPlayer != null)
                {
                    _videoPlayer.Play();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error opening video:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void PlayCurrentVideo()
        {
            if (_videoPlayer != null)
            {
                _videoPlayer.Play();
            }
        }

        private void PauseCurrentVideo()
        {
            if (_videoPlayer != null)
            {
                _videoPlayer.Pause();
            }
        }

        private void StopCurrentVideo()
        {
            if (_videoPlayer != null)
            {
                _videoPlayer.Stop();
            }
            IsPlayingVideo = false;
            CurrentPosition = 0;
            CurrentTime = "00:00";
        }

        public void UpdatePosition(TimeSpan current, TimeSpan total)
        {
            CurrentPosition = current.TotalSeconds;
            CurrentTime = FormatTime(current);
        }

        public void SetTotalDuration(TimeSpan duration)
        {
            TotalDuration = duration.TotalSeconds;
            TotalTime = FormatTime(duration);
        }

        private string FormatTime(TimeSpan time)
        {
            return $"{(int)time.TotalMinutes:D2}:{time.Seconds:D2}";
        }

        private ImageSource? GenerateThumbnail(string videoPath)
        {
            try
            {
                IntPtr hBitmap = IntPtr.Zero;
                IShellItem? shellItem = null;
                IShellItemImageFactory? imageFactory = null;

                try
                {
                    // Create shell item from file path
                    SHCreateItemFromParsingName(videoPath, IntPtr.Zero, typeof(IShellItem).GUID, out shellItem);

                    if (shellItem != null)
                    {
                        imageFactory = (IShellItemImageFactory)shellItem;
                        SIZE size = new SIZE { cx = 320, cy = 240 };

                        // Get the thumbnail bitmap
                        imageFactory.GetImage(size, SIIGBF.SIIGBF_BIGGERSIZEOK, out hBitmap);

                        if (hBitmap != IntPtr.Zero)
                        {
                            var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                                hBitmap,
                                IntPtr.Zero,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());

                            bitmapSource.Freeze(); // Make it thread-safe
                            return bitmapSource;
                        }
                    }
                }
                finally
                {
                    if (hBitmap != IntPtr.Zero)
                        DeleteObject(hBitmap);
                    if (imageFactory != null)
                        Marshal.ReleaseComObject(imageFactory);
                    if (shellItem != null)
                        Marshal.ReleaseComObject(shellItem);
                }
            }
            catch
            {
                // Thumbnail generation failed, return null to use placeholder
            }

            return null;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            [In] IntPtr pbc,
            [In][MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [Out][MarshalAs(UnmanagedType.Interface, IidParameterIndex = 2)] out IShellItem? ppv);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        private interface IShellItem
        {
        }

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(
                [In, MarshalAs(UnmanagedType.Struct)] SIZE size,
                [In] SIIGBF flags,
                [Out] out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
        }

        [Flags]
        private enum SIIGBF
        {
            SIIGBF_RESIZETOFIT = 0x00,
            SIIGBF_BIGGERSIZEOK = 0x01,
            SIIGBF_MEMORYONLY = 0x02,
            SIIGBF_ICONONLY = 0x04,
            SIIGBF_THUMBNAILONLY = 0x08,
            SIIGBF_INCACHEONLY = 0x10,
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class VideoFile
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;
        public ImageSource? Thumbnail { get; set; }
    }
}
