using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using InazumaElevenVRSaveEditor.Features.ModManager.Models;

namespace InazumaElevenVRSaveEditor.Features.ModManager.Services
{
    internal class ModInfoJson
    {
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("mod_page")]
        public string? ModPage { get; set; }
    }

    internal class GameBananaModInfo
    {
        [JsonPropertyName("_sVersion")]
        public string? Version { get; set; }

        [JsonPropertyName("_idRow")]
        public int? Id { get; set; }

        [JsonPropertyName("_sName")]
        public string? Name { get; set; }

        [JsonPropertyName("_aFiles")]
        public GameBananaFile[]? Files { get; set; }
    }

    public class GameBananaFile
    {
        [JsonPropertyName("_sFile")]
        public string? FileName { get; set; }

        [JsonPropertyName("_sDownloadUrl")]
        public string? DownloadUrl { get; set; }

        [JsonPropertyName("_nFilesize")]
        public long? FileSize { get; set; }
    }

    public class ModManagerService
    {
        private readonly string _modsDirectory;
        private static readonly HttpClient _httpClient;
        private static bool _gb403ErrorShown = false;

        static ModManagerService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://gamebanana.com");
            _httpClient.DefaultRequestHeaders.Add("Origin", "https://gamebanana.com");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public ModManagerService(string modsDirectory)
        {
            _modsDirectory = modsDirectory;
        }

        public string ModsDirectory => _modsDirectory;

        public List<ModInfo> ScanForMods()
        {
            var mods = new List<ModInfo>();

            if (!Directory.Exists(_modsDirectory))
            {
                return mods;
            }

            try
            {
                var modFolders = Directory.GetDirectories(_modsDirectory);

                foreach (var folder in modFolders)
                {
                    var folderName = Path.GetFileName(folder);

                    var (priority, cleanName) = ExtractPriorityFromFolderName(folderName);

                    var allFilesInFolder = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
                        .Where(f => !f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (allFilesInFolder.Count == 0)
                        continue;

                    var offFiles = allFilesInFolder.Where(f => f.EndsWith(".off", StringComparison.OrdinalIgnoreCase)).ToList();
                    var activeFiles = allFilesInFolder.Where(f => !f.EndsWith(".off", StringComparison.OrdinalIgnoreCase)).ToList();

                    long totalSize = 0;
                    foreach (var file in allFilesInFolder)
                    {
                        var fileInfo = new FileInfo(file);
                        totalSize += fileInfo.Length;
                    }

                    bool isEnabled = offFiles.Count == 0 && activeFiles.Count > 0;

                    string version = string.Empty;
                    string modPageUrl = string.Empty;

                    var infoJsonPath = Path.Combine(folder, "info.json");
                    if (File.Exists(infoJsonPath))
                    {
                        try
                        {
                            var jsonContent = File.ReadAllText(infoJsonPath);
                            var modInfoJson = JsonSerializer.Deserialize<ModInfoJson>(jsonContent);
                            if (modInfoJson != null)
                            {
                                version = modInfoJson.Version ?? string.Empty;
                                modPageUrl = modInfoJson.ModPage ?? string.Empty;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error reading info.json for {folderName}: {ex.Message}");
                        }
                    }

                    mods.Add(new ModInfo
                    {
                        Name = cleanName,
                        FolderPath = folder,
                        Files = allFilesInFolder,
                        IsEnabled = isEnabled,
                        TotalSize = totalSize,
                        FileCount = allFilesInFolder.Count,
                        Version = version,
                        ModPageUrl = modPageUrl,
                        Priority = priority
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error scanning for mods: {ex.Message}");
                throw;
            }


            return mods.OrderBy(m => m.Priority >= 0 ? m.Priority : int.MaxValue)
                       .ThenBy(m => m.Name)
                       .ToList();
        }

        public void EnableMod(ModInfo mod)
        {
            if (mod.IsEnabled)
                return;

            if (!Directory.Exists(mod.FolderPath))
            {
                throw new DirectoryNotFoundException($"Mod folder not found: {mod.FolderPath}");
            }

            var offFiles = Directory.GetFiles(mod.FolderPath, "*.off", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".json.off", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var file in offFiles)
            {
                var newPath = file.Substring(0, file.Length - 4);
                File.Move(file, newPath);
            }

            mod.IsEnabled = true;

            mod.Files = Directory.GetFiles(mod.FolderPath, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public void DisableMod(ModInfo mod)
        {
            if (!mod.IsEnabled)
                return;

            if (!Directory.Exists(mod.FolderPath))
            {
                throw new DirectoryNotFoundException($"Mod folder not found: {mod.FolderPath}");
            }

            var allFiles = Directory.GetFiles(mod.FolderPath, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                           !f.EndsWith(".off", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var file in allFiles)
            {
                var newPath = file + ".off";
                File.Move(file, newPath);
            }

            mod.IsEnabled = false;

            mod.Files = Directory.GetFiles(mod.FolderPath, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public void ToggleMod(ModInfo mod)
        {
            if (mod.IsEnabled)
            {
                DisableMod(mod);
            }
            else
            {
                EnableMod(mod);
            }
        }

        public void OpenModsFolder()
        {
            if (Directory.Exists(_modsDirectory))
            {
                Process.Start("explorer.exe", _modsDirectory);
            }
            else
            {
                throw new DirectoryNotFoundException($"Mods directory not found: {_modsDirectory}");
            }
        }

        public bool ModsDirectoryExists()
        {
            return Directory.Exists(_modsDirectory);
        }

        public async Task CheckForUpdateAsync(ModInfo mod)
        {
            if (string.IsNullOrEmpty(mod.ModPageUrl))
            {
                return;
            }

            try
            {
                var (itemType, itemId) = ExtractGameBananaItemDetails(mod.ModPageUrl);
                if (string.IsNullOrEmpty(itemType) || string.IsNullOrEmpty(itemId))
                {
                    Debug.WriteLine($"Could not extract valid item details from URL: {mod.ModPageUrl}");
                    return;
                }

                var apiItemType = itemType.TrimEnd('s');
                if (!string.IsNullOrEmpty(apiItemType))
                {
                    apiItemType = char.ToUpper(apiItemType[0]) + apiItemType.Substring(1);
                }

                var apiUrl = $"https://gamebanana.com/apiv11/{apiItemType}/{itemId}?_csvProperties=_sVersion";

                var response = await _httpClient.GetAsync(apiUrl);

                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    if (!_gb403ErrorShown)
                    {
                        _gb403ErrorShown = true;
                        Debug.WriteLine("GameBanana's API returned a 403 Forbidden error. This usually means their servers are temporarily blocking requests.");
                    }
                    return;
                }

                response.EnsureSuccessStatusCode();

                var jsonContent = await response.Content.ReadAsStringAsync();
                var modInfo = JsonSerializer.Deserialize<GameBananaModInfo>(jsonContent);

                if (modInfo != null && !string.IsNullOrEmpty(modInfo.Version))
                {
                    var latestVersion = modInfo.Version;
                    mod.LatestVersion = latestVersion;

                    if (!string.IsNullOrEmpty(mod.Version) && !string.IsNullOrEmpty(latestVersion))
                    {
                        mod.HasUpdate = IsNewerVersion(mod.Version, latestVersion);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking for update for {mod.Name}: {ex.Message}");
            }
        }

        public async Task CheckForUpdatesAsync(List<ModInfo> mods)
        {
            var tasks = new List<Task>();
            foreach (var mod in mods.Where(m => !string.IsNullOrEmpty(m.ModPageUrl)))
            {
                tasks.Add(CheckForUpdateAsync(mod));
            }
            await Task.WhenAll(tasks);
        }

        private (string? itemType, string? itemId) ExtractGameBananaItemDetails(string url)
        {
            try
            {
                if (!url.Contains("gamebanana.com/"))
                    return (null, null);

                var parts = url.Split('/');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (i + 1 < parts.Length && parts[i] == "gamebanana.com")
                    {
                        var itemType = parts[i + 1];
                        var itemId = i + 2 < parts.Length ? parts[i + 2].TrimEnd('/') : null;
                        return (itemType, itemId);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting GameBanana item details from URL: {ex.Message}");
            }
            return (null, null);
        }

        private bool IsNewerVersion(string local, string remote)
        {
            local = local ?? "0";
            remote = remote ?? "0";

            var localNums = VersionToNumbers(local);
            var remoteNums = VersionToNumbers(remote);

            int maxLength = Math.Max(localNums.Count, remoteNums.Count);
            while (localNums.Count < maxLength) localNums.Add(0);
            while (remoteNums.Count < maxLength) remoteNums.Add(0);

            for (int i = 0; i < maxLength; i++)
            {
                if (remoteNums[i] > localNums[i])
                    return true;
                if (remoteNums[i] < localNums[i])
                    return false;
            }

            return false;
        }

        private List<int> VersionToNumbers(string version)
        {
            var cleaned = Regex.Replace(version, @"[^0-9.]", "");

            var parts = cleaned.Split('.')
                .Where(x => !string.IsNullOrEmpty(x) && int.TryParse(x, out _))
                .Select(x => int.Parse(x))
                .ToList();

            return parts.Any() ? parts : new List<int> { 0 };
        }

        private void CopyDirectoryContents(string sourcePath, string destinationPath, bool excludeInfoJson = false)
        {
            if (!Directory.Exists(destinationPath))
            {
                Directory.CreateDirectory(destinationPath);
            }

            foreach (var file in Directory.GetFiles(sourcePath))
            {
                var fileName = Path.GetFileName(file);

                if (excludeInfoJson && fileName.Equals("info.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var destFile = Path.Combine(destinationPath, fileName);
                try
                {
                    File.Copy(file, destFile, true);
                    Debug.WriteLine($"Copied: {fileName}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error copying file {fileName}: {ex.Message}");
                    throw;
                }
            }

            foreach (var directory in Directory.GetDirectories(sourcePath))
            {
                var dirName = Path.GetFileName(directory);
                var destDir = Path.Combine(destinationPath, dirName);
                CopyDirectoryContents(directory, destDir, excludeInfoJson);
            }
        }

        public async Task<GameBananaFile[]?> GetAvailableFilesAsync(ModInfo mod)
        {
            if (string.IsNullOrEmpty(mod.ModPageUrl))
            {
                return null;
            }

            var (itemType, itemId) = ExtractGameBananaItemDetails(mod.ModPageUrl);
            if (string.IsNullOrEmpty(itemType) || string.IsNullOrEmpty(itemId))
            {
                return null;
            }

            var apiItemType = itemType.TrimEnd('s');
            if (!string.IsNullOrEmpty(apiItemType))
            {
                apiItemType = char.ToUpper(apiItemType[0]) + apiItemType.Substring(1);
            }


            var apiUrl = $"https://gamebanana.com/apiv11/{apiItemType}/{itemId}?_csvProperties=_aFiles,_sVersion";

            var response = await _httpClient.GetAsync(apiUrl);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            var modInfo = JsonSerializer.Deserialize<GameBananaModInfo>(jsonContent);

            return modInfo?.Files;
        }

        public async Task DownloadAndUpdateModAsync(ModInfo mod, GameBananaFile[] selectedFiles, string latestVersion, IProgress<(double percentage, string status)>? progress = null)
        {
            if (selectedFiles == null || selectedFiles.Length == 0)
            {
                throw new InvalidOperationException("No files selected for download");
            }

            bool wasEnabled = mod.IsEnabled;
            if (wasEnabled)
            {
                progress?.Report((5, "Disabling mod..."));
                DisableMod(mod);
            }

            var downloadedFiles = new List<string>();
            var tempExtractPath = Path.Combine(Path.GetTempPath(), $"mod_extract_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempExtractPath);

            try
            {
                int fileIndex = 0;
                foreach (var fileToDownload in selectedFiles)
                {
                    fileIndex++;
                    if (string.IsNullOrEmpty(fileToDownload.DownloadUrl))
                    {
                        continue;
                    }

                    var progressOffset = 5.0 + ((double)(fileIndex - 1) / selectedFiles.Length * 60.0);
                    var progressRange = 60.0 / selectedFiles.Length;

                    progress?.Report((progressOffset, $"Downloading file {fileIndex}/{selectedFiles.Length}: {fileToDownload.FileName}"));

                    var tempPath = Path.Combine(Path.GetTempPath(), fileToDownload.FileName ?? $"mod_download_{fileIndex}.zip");

                    using (var downloadResponse = await _httpClient.GetAsync(fileToDownload.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        downloadResponse.EnsureSuccessStatusCode();

                        var totalBytes = downloadResponse.Content.Headers.ContentLength ?? -1L;
                        var canReportProgress = totalBytes != -1;

                        using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                        using var contentStream = await downloadResponse.Content.ReadAsStreamAsync();

                        var buffer = new byte[8192];
                        long totalBytesRead = 0;
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;

                            if (canReportProgress)
                            {
                                var downloadProgress = (double)totalBytesRead / totalBytes * progressRange;
                                progress?.Report((progressOffset + downloadProgress, $"Downloading {fileToDownload.FileName}... {totalBytesRead / 1024.0 / 1024.0:F1} MB / {totalBytes / 1024.0 / 1024.0:F1} MB"));
                            }
                        }
                    }

                    downloadedFiles.Add(tempPath);
                }

                progress?.Report((70, "Download complete, extracting files..."));

                foreach (var downloadedFile in downloadedFiles)
                {
                    if (downloadedFile.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        ZipFile.ExtractToDirectory(downloadedFile, tempExtractPath, true);
                    }
                    else
                    {
                        var fileName = Path.GetFileName(downloadedFile);
                        File.Copy(downloadedFile, Path.Combine(tempExtractPath, fileName), true);
                    }
                }

                string sourcePath = tempExtractPath;

                var topLevelItems = Directory.GetFileSystemEntries(tempExtractPath);
                if (topLevelItems.Length == 1 && Directory.Exists(topLevelItems[0]))
                {
                    sourcePath = topLevelItems[0];
                    Debug.WriteLine($"Found nested folder in zip: {Path.GetFileName(sourcePath)}");
                }

                progress?.Report((85, "Removing old files..."));

                var filesToDelete = Directory.GetFiles(mod.FolderPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => !f.EndsWith("info.json", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var dirsToDelete = Directory.GetDirectories(mod.FolderPath, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length)
                    .ToList();

                foreach (var file in filesToDelete)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error deleting file {file}: {ex.Message}");
                    }
                }

                foreach (var dir in dirsToDelete)
                {
                    try
                    {
                        if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                        {
                            Directory.Delete(dir, false);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error deleting directory {dir}: {ex.Message}");
                    }
                }

                progress?.Report((90, "Installing new files..."));

                CopyDirectoryContents(sourcePath, mod.FolderPath, excludeInfoJson: true);

                progress?.Report((95, "Updating mod information..."));

                var infoJsonPath = Path.Combine(mod.FolderPath, "info.json");
                if (File.Exists(infoJsonPath))
                {
                    var jsonContent2 = File.ReadAllText(infoJsonPath);
                    var infoJson = JsonSerializer.Deserialize<ModInfoJson>(jsonContent2);
                    if (infoJson != null)
                    {
                        infoJson.Version = latestVersion;
                        var updatedJson = JsonSerializer.Serialize(infoJson, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(infoJsonPath, updatedJson);
                    }
                }

                if (wasEnabled)
                {
                    progress?.Report((97, "Re-enabling mod..."));
                    EnableMod(mod);
                }
                else
                {
                    progress?.Report((97, "Applying disabled state..."));
                    DisableMod(mod);
                }

                progress?.Report((100, "Update completed successfully!"));
            }
            finally
            {
                foreach (var downloadedFile in downloadedFiles)
                {
                    if (File.Exists(downloadedFile))
                    {
                        try
                        {
                            File.Delete(downloadedFile);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error deleting temp file {downloadedFile}: {ex.Message}");
                        }
                    }
                }

                if (Directory.Exists(tempExtractPath))
                {
                    try
                    {
                        Directory.Delete(tempExtractPath, true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error deleting temp directory: {ex.Message}");
                    }
                }
            }
        }

        private (int priority, string cleanName) ExtractPriorityFromFolderName(string folderName)
        {
            var match = Regex.Match(folderName, @"^(\d+)\s*-\s*(.+)$");
            if (match.Success)
            {
                if (int.TryParse(match.Groups[1].Value, out int priority))
                {
                    return (priority - 1, match.Groups[2].Value.Trim());
                }
            }

            return (-1, folderName);
        }

        public void EnablePriorityMode(List<ModInfo> mods)
        {
            for (int i = 0; i < mods.Count; i++)
            {
                var mod = mods[i];
                if (mod.Priority < 0)
                {
                    UpdateModPriority(mod, i, mods);
                }
            }
        }

        public void DisablePriorityMode(List<ModInfo> mods)
        {
            foreach (var mod in mods)
            {
                if (mod.Priority >= 0)
                {
                    var currentFolderName = Path.GetFileName(mod.FolderPath);
                    var (_, cleanName) = ExtractPriorityFromFolderName(currentFolderName);

                    var newFolderPath = Path.Combine(_modsDirectory, cleanName);

                    if (mod.FolderPath != newFolderPath && Directory.Exists(mod.FolderPath))
                    {
                        Directory.Move(mod.FolderPath, newFolderPath);
                        mod.FolderPath = newFolderPath;
                        mod.Priority = -1;
                    }
                }
            }
        }

        public void UpdateModPriority(ModInfo mod, int newPriority, List<ModInfo> allMods)
        {
            var currentFolderName = Path.GetFileName(mod.FolderPath);
            var (_, cleanName) = ExtractPriorityFromFolderName(currentFolderName);

            var newFolderName = $"{newPriority + 1:D2} - {cleanName}";
            var newFolderPath = Path.Combine(_modsDirectory, newFolderName);

            if (mod.FolderPath != newFolderPath && Directory.Exists(mod.FolderPath))
            {
                if (Directory.Exists(newFolderPath))
                {
                    var tempFolderPath = Path.Combine(_modsDirectory, $"_temp_{Guid.NewGuid()}");
                    Directory.Move(mod.FolderPath, tempFolderPath);
                    mod.FolderPath = tempFolderPath;
                }

                Directory.Move(mod.FolderPath, newFolderPath);
                mod.FolderPath = newFolderPath;
            }

            mod.Priority = newPriority;
        }

        public void ReorderMods(List<ModInfo> mods)
        {
            for (int i = 0; i < mods.Count; i++)
            {
                if (mods[i].Priority >= 0)
                {
                    UpdateModPriority(mods[i], i, mods);
                }
            }
        }

        public bool IsPriorityModeEnabled(List<ModInfo> mods)
        {
            return mods.Any(m => m.Priority >= 0);
        }
    }
}
