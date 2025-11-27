using System;

namespace InazumaElevenVRSaveEditor.Features.Backup.Models
{
    public class SaveAccount
    {
        public string AccountId { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public int FileCount { get; set; }
        public DateTime LastModified { get; set; }

        public string DisplayName => string.IsNullOrEmpty(AccountName)
            ? $"{AccountId} ({FileCount} files)"
            : $"{AccountName} ({AccountId}) - {FileCount} files";

        public string ShortDisplay => string.IsNullOrEmpty(AccountName)
            ? AccountId
            : $"{AccountName} ({AccountId})";
    }
}
