using System;
using System.Collections.Generic;

namespace InazumaElevenVRSaveEditor.Features.Backup.Models
{
    public class BackupInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public DateTime BackupDate { get; set; }
        public int FileCount { get; set; }
        public List<string> Files { get; set; } = new();

        public string DisplayDate => BackupDate.ToString("yyyy-MM-dd HH:mm:ss");
        public string DisplayName => Name;
        public string ShortDate => BackupDate.ToString("MMM dd, yyyy");
        public string TimeAgo
        {
            get
            {
                var span = DateTime.Now - BackupDate;
                if (span.TotalMinutes < 1) return "Just now";
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
                if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
                return DisplayDate;
            }
        }
    }

    public class BackupMetadata
    {
        public string AccountId { get; set; } = string.Empty;
        public string BackupDate { get; set; } = string.Empty;
        public List<string> Files { get; set; } = new();
        public string BackupName { get; set; } = string.Empty;
    }
}
