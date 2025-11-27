using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InazumaElevenVRSaveEditor.Features.ModManager.Models
{
    public class ModInfo : INotifyPropertyChanged
    {
        private bool _isEnabled;
        private string _name = string.Empty;
        private string _folderPath = string.Empty;
        private long _totalSize;
        private int _fileCount;
        private List<string> _files = new();
        private string _version = string.Empty;
        private string _modPageUrl = string.Empty;
        private bool _hasUpdate;
        private string _latestVersion = string.Empty;
        private int _priority = -1; // -1 means no priority set

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public string FolderPath
        {
            get => _folderPath;
            set
            {
                _folderPath = value;
                OnPropertyChanged();
            }
        }

        public List<string> Files
        {
            get => _files;
            set
            {
                _files = value;
                OnPropertyChanged();
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        public long TotalSize
        {
            get => _totalSize;
            set
            {
                _totalSize = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FileSizeFormatted));
            }
        }

        public int FileCount
        {
            get => _fileCount;
            set
            {
                _fileCount = value;
                OnPropertyChanged();
            }
        }

        public string FileSizeFormatted
        {
            get
            {
                if (TotalSize == 0)
                    return "0 KB";

                double bytes = TotalSize;
                string[] sizes = { "B", "KB", "MB", "GB" };
                int order = 0;
                while (bytes >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    bytes /= 1024;
                }
                return $"{bytes:0.##} {sizes[order]}";
            }
        }

        public string StatusText => IsEnabled ? "Enabled" : "Disabled";

        public string StatusColor => IsEnabled ? "#4CAF50" : "#FF6B00";

        public string Version
        {
            get => _version;
            set
            {
                _version = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VersionDisplay));
            }
        }

        public string ModPageUrl
        {
            get => _modPageUrl;
            set
            {
                _modPageUrl = value;
                OnPropertyChanged();
            }
        }

        public bool HasUpdate
        {
            get => _hasUpdate;
            set
            {
                _hasUpdate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UpdateStatusText));
                OnPropertyChanged(nameof(UpdateStatusColor));
                OnPropertyChanged(nameof(VersionDisplay));
            }
        }

        public string LatestVersion
        {
            get => _latestVersion;
            set
            {
                _latestVersion = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UpdateStatusText));
                OnPropertyChanged(nameof(VersionDisplay));
            }
        }

        public string VersionDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(Version))
                    return "-";

                if (HasUpdate && !string.IsNullOrEmpty(LatestVersion))
                    return $"{Version} → {LatestVersion}";

                return Version;
            }
        }

        public int Priority
        {
            get => _priority;
            set
            {
                _priority = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PriorityDisplay));
                OnPropertyChanged(nameof(HasPriority));
            }
        }

        public string PriorityDisplay => Priority >= 0 ? $"{Priority + 1:D2}" : "-";

        public bool HasPriority => Priority >= 0;

        public string UpdateStatusText
        {
            get
            {
                if (string.IsNullOrEmpty(Version))
                    return "-"; // No version info available
                return HasUpdate ? "Update Available" : "Up to Date";
            }
        }

        public string UpdateStatusColor
        {
            get
            {
                if (string.IsNullOrEmpty(Version))
                    return "#808080"; // Gray for no version info
                return HasUpdate ? "#FF6B00" : "#4CAF50";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
