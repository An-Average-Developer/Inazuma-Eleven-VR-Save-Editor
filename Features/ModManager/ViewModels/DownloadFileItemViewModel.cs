using InazumaElevenVRSaveEditor.Common.Infrastructure;

namespace InazumaElevenVRSaveEditor.Features.ModManager.ViewModels
{
    public class DownloadFileItemViewModel : ViewModelBase
    {
        private bool _isSelected;
        private readonly string _fileName;
        private readonly string _downloadUrl;
        private readonly long _fileSize;

        public DownloadFileItemViewModel(string fileName, string downloadUrl, long fileSize, bool isSelected = false)
        {
            _fileName = fileName;
            _downloadUrl = downloadUrl;
            _fileSize = fileSize;
            _isSelected = isSelected;
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string FileName => _fileName;
        public string DownloadUrl => _downloadUrl;
        public long FileSize => _fileSize;

        public string FileSizeFormatted
        {
            get
            {
                if (_fileSize < 1024)
                    return $"{_fileSize} B";
                else if (_fileSize < 1024 * 1024)
                    return $"{_fileSize / 1024.0:F2} KB";
                else if (_fileSize < 1024 * 1024 * 1024)
                    return $"{_fileSize / 1024.0 / 1024.0:F2} MB";
                else
                    return $"{_fileSize / 1024.0 / 1024.0 / 1024.0:F2} GB";
            }
        }
    }
}
