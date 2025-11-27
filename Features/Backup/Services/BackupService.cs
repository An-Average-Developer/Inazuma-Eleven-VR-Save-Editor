using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using InazumaElevenVRSaveEditor.Features.Backup.Models;

namespace InazumaElevenVRSaveEditor.Features.Backup.Services
{
    public class BackupService
    {
        private readonly string _baseSaveDir;
        private readonly string _backupDir;
        private readonly string[] _saveExtensions = { ".sav", ".vdf" };

        public string BaseSaveDirectory => _baseSaveDir;
        public string BackupDirectory => _backupDir;

        public BackupService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _baseSaveDir = Path.Combine(appData, "SEGA", "SonicRacingCrossWorlds", "steam");
            _backupDir = Path.Combine(appData, "SEGA", "SonicRacingCrossWorlds", "Backups");

            Directory.CreateDirectory(_backupDir);
        }

        public List<SaveAccount> GetSaveAccounts()
        {
            var accounts = new List<SaveAccount>();

            if (!Directory.Exists(_baseSaveDir))
                return accounts;

            foreach (var dir in Directory.GetDirectories(_baseSaveDir))
            {
                var dirName = Path.GetFileName(dir);

                if (long.TryParse(dirName, out _))
                {
                    var saveFiles = Directory.GetFiles(dir, "*.sav");
                    if (saveFiles.Length > 0)
                    {
                        var steamName = GetSteamAccountName(dirName);
                        accounts.Add(new SaveAccount
                        {
                            AccountId = dirName,
                            AccountName = steamName,
                            Path = dir,
                            FileCount = saveFiles.Length,
                            LastModified = Directory.GetLastWriteTime(dir)
                        });
                    }
                }
            }

            return accounts.OrderByDescending(a => a.LastModified).ToList();
        }

        private string GetSteamAccountName(string steamId)
        {
            try
            {
                var steamPath = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (steamPath != null)
                {
                    var steamDir = steamPath.GetValue("SteamPath") as string;
                    if (!string.IsNullOrEmpty(steamDir))
                    {
                        var configPath = Path.Combine(steamDir, "userdata", steamId, "config", "localconfig.vdf");
                        if (File.Exists(configPath))
                        {
                            var content = File.ReadAllText(configPath);
                            var personaIndex = content.IndexOf("\"PersonaName\"");
                            if (personaIndex != -1)
                            {
                                var startQuote = content.IndexOf("\"", personaIndex + 13);
                                if (startQuote != -1)
                                {
                                    var endQuote = content.IndexOf("\"", startQuote + 1);
                                    if (endQuote != -1)
                                    {
                                        var name = content.Substring(startQuote + 1, endQuote - startQuote - 1);
                                        if (!string.IsNullOrWhiteSpace(name))
                                            return name;
                                    }
                                }
                            }
                        }

                        var loginUsersPath = Path.Combine(steamDir, "config", "loginusers.vdf");
                        if (File.Exists(loginUsersPath))
                        {
                            var content = File.ReadAllText(loginUsersPath);
                            var idIndex = content.IndexOf(steamId);
                            if (idIndex != -1)
                            {
                                var personaIndex = content.IndexOf("\"PersonaName\"", idIndex);
                                if (personaIndex != -1 && personaIndex - idIndex < 500)
                                {
                                    var startQuote = content.IndexOf("\"", personaIndex + 13);
                                    if (startQuote != -1)
                                    {
                                        var endQuote = content.IndexOf("\"", startQuote + 1);
                                        if (endQuote != -1)
                                        {
                                            var name = content.Substring(startQuote + 1, endQuote - startQuote - 1);
                                            if (!string.IsNullOrWhiteSpace(name))
                                                return name;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        public List<string> GetSaveFiles(string accountPath)
        {
            var saveFiles = new List<string>();

            foreach (var ext in _saveExtensions)
            {
                saveFiles.AddRange(Directory.GetFiles(accountPath, $"*{ext}"));
            }

            return saveFiles.OrderBy(f => Path.GetFileName(f)).ToList();
        }

        public (string BackupPath, int FileCount) CreateBackup(string accountId, string? customName = null)
        {
            var accounts = GetSaveAccounts();
            var account = accounts.FirstOrDefault(a => a.AccountId == accountId);

            if (account == null)
                throw new Exception($"Account {accountId} not found");

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupName = string.IsNullOrWhiteSpace(customName)
                ? $"backup_{accountId}_{timestamp}"
                : $"{customName}_{timestamp}";

            var backupPath = Path.Combine(_backupDir, backupName);
            Directory.CreateDirectory(backupPath);

            var saveFiles = GetSaveFiles(account.Path);
            var copiedFiles = new List<string>();

            foreach (var saveFile in saveFiles)
            {
                var fileName = Path.GetFileName(saveFile);
                var destFile = Path.Combine(backupPath, fileName);
                File.Copy(saveFile, destFile, true);
                copiedFiles.Add(fileName);
            }

            var metadata = new BackupMetadata
            {
                AccountId = accountId,
                BackupDate = DateTime.Now.ToString("o"),
                Files = copiedFiles,
                BackupName = backupName
            };

            var metadataPath = Path.Combine(backupPath, "backup_info.json");
            File.WriteAllText(metadataPath, JsonConvert.SerializeObject(metadata, Formatting.Indented));

            return (backupPath, copiedFiles.Count);
        }

        public List<BackupInfo> ListBackups()
        {
            var backups = new List<BackupInfo>();

            if (!Directory.Exists(_backupDir))
                return backups;

            foreach (var backupFolder in Directory.GetDirectories(_backupDir))
            {
                var metadataFile = Path.Combine(backupFolder, "backup_info.json");

                if (File.Exists(metadataFile))
                {
                    try
                    {
                        var json = File.ReadAllText(metadataFile);
                        var metadata = JsonConvert.DeserializeObject<BackupMetadata>(json);

                        if (metadata != null)
                        {
                            backups.Add(new BackupInfo
                            {
                                Name = Path.GetFileName(backupFolder),
                                Path = backupFolder,
                                AccountId = metadata.AccountId,
                                BackupDate = DateTime.Parse(metadata.BackupDate),
                                FileCount = metadata.Files.Count,
                                Files = metadata.Files
                            });
                        }
                    }
                    catch
                    {
                    }
                }
                else
                {
                    var saveFiles = Directory.GetFiles(backupFolder, "*.sav");
                    if (saveFiles.Length > 0)
                    {
                        backups.Add(new BackupInfo
                        {
                            Name = Path.GetFileName(backupFolder),
                            Path = backupFolder,
                            AccountId = "Unknown",
                            BackupDate = Directory.GetLastWriteTime(backupFolder),
                            FileCount = saveFiles.Length,
                            Files = saveFiles.Select(Path.GetFileName).ToList()!
                        });
                    }
                }
            }

            return backups.OrderByDescending(b => b.BackupDate).ToList();
        }

        public int RestoreBackup(string backupName, string? targetAccountId = null)
        {
            var backupPath = Path.Combine(_backupDir, backupName);

            if (!Directory.Exists(backupPath))
                throw new Exception($"Backup {backupName} not found");

            var metadataFile = Path.Combine(backupPath, "backup_info.json");
            string? originalAccount = null;

            if (File.Exists(metadataFile))
            {
                var json = File.ReadAllText(metadataFile);
                var metadata = JsonConvert.DeserializeObject<BackupMetadata>(json);
                originalAccount = metadata?.AccountId;
            }

            targetAccountId ??= originalAccount;

            if (string.IsNullOrEmpty(targetAccountId))
                throw new Exception("Cannot determine target account. Please specify account ID.");

            var targetDir = Path.Combine(_baseSaveDir, targetAccountId);
            Directory.CreateDirectory(targetDir);

            var saveFiles = new List<string>();
            foreach (var ext in _saveExtensions)
            {
                saveFiles.AddRange(Directory.GetFiles(backupPath, $"*{ext}"));
            }

            var restoredCount = 0;
            foreach (var saveFile in saveFiles)
            {
                var fileName = Path.GetFileName(saveFile);
                var destFile = Path.Combine(targetDir, fileName);
                File.Copy(saveFile, destFile, true);
                restoredCount++;
            }

            return restoredCount;
        }

        public void DeleteBackup(string backupName)
        {
            var backupPath = Path.Combine(_backupDir, backupName);

            if (!Directory.Exists(backupPath))
                throw new Exception($"Backup {backupName} not found");

            Directory.Delete(backupPath, true);
        }

        public string ExportBackup(string backupName, string exportPath)
        {
            var backupPath = Path.Combine(_backupDir, backupName);

            if (!Directory.Exists(backupPath))
                throw new Exception($"Backup {backupName} not found");

            if (Directory.Exists(exportPath))
            {
                exportPath = Path.Combine(exportPath, $"{backupName}.zip");
            }

            if (!exportPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                exportPath += ".zip";
            }

            if (File.Exists(exportPath))
                File.Delete(exportPath);

            ZipFile.CreateFromDirectory(backupPath, exportPath);

            return exportPath;
        }

        public string ImportBackup(string importPath)
        {
            if (!File.Exists(importPath) && !Directory.Exists(importPath))
                throw new Exception($"Import path {importPath} not found");

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupName = $"imported_{timestamp}";
            var backupPath = Path.Combine(_backupDir, backupName);

            if (Path.GetExtension(importPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(importPath, backupPath);
            }
            else if (Directory.Exists(importPath))
            {
                CopyDirectory(importPath, backupPath);
            }
            else
            {
                throw new Exception("Import path must be a zip file or directory");
            }

            return backupName;
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }

        public void OpenBackupFolder()
        {
            if (Directory.Exists(_backupDir))
            {
                System.Diagnostics.Process.Start("explorer.exe", _backupDir);
            }
        }

        public void OpenSaveFolder()
        {
            if (Directory.Exists(_baseSaveDir))
            {
                System.Diagnostics.Process.Start("explorer.exe", _baseSaveDir);
            }
        }

        public int ImportFilesDirectly(string filePath, string targetAccountId)
        {
            if (!File.Exists(filePath) && !Directory.Exists(filePath))
                throw new Exception($"File or directory not found: {filePath}");

            var targetDir = Path.Combine(_baseSaveDir, targetAccountId);
            if (!Directory.Exists(targetDir))
                throw new Exception($"Target account directory not found: {targetDir}");

            var importedCount = 0;
            var tempDir = Path.Combine(Path.GetTempPath(), $"SonicSaveImport_{Guid.NewGuid()}");

            try
            {
                var fileExtension = Path.GetExtension(filePath).ToLower();

                if (fileExtension == ".zip")
                {
                    Directory.CreateDirectory(tempDir);
                    ZipFile.ExtractToDirectory(filePath, tempDir);

                    var savFiles = Directory.GetFiles(tempDir, "*.sav", SearchOption.AllDirectories);
                    foreach (var savFile in savFiles)
                    {
                        var fileName = Path.GetFileName(savFile);
                        var destFile = Path.Combine(targetDir, fileName);
                        File.Copy(savFile, destFile, true);
                        importedCount++;
                    }
                }
                else if (fileExtension == ".sav")
                {
                    var fileName = Path.GetFileName(filePath);
                    var destFile = Path.Combine(targetDir, fileName);
                    File.Copy(filePath, destFile, true);
                    importedCount++;
                }
                else
                {
                    throw new Exception($"Unsupported file type: {fileExtension}. Please use .zip or .sav files.");
                }

                if (importedCount == 0)
                {
                    throw new Exception("No .sav files found in the imported file.");
                }

                return importedCount;
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
