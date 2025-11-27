using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using InazumaElevenVRSaveEditor.Configuration;

namespace InazumaElevenVRSaveEditor.Features.Updates.Services
{
    public class UpdateInfo
    {
        public string LatestVersion { get; set; } = string.Empty;
        public string Changelog { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime UpdatedDate { get; set; }
        public bool IsUpdateAvailable { get; set; }
        public string ReleaseUrl { get; set; } = string.Empty;
    }

    public class UpdateService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private bool _disposed = false;

        public UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "InazumaElevenVRSaveEditor");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            var updateInfo = new UpdateInfo
            {
                LatestVersion = AppVersion.CURRENT_VERSION,
                IsUpdateAvailable = false
            };

            try
            {
                var versionUrl = $"https://raw.githubusercontent.com/An-Average-Developer/Inazuma-Eleven-VR-Save-Editor/main/version.txt";
                var latestVersion = await _httpClient.GetStringAsync(versionUrl);

                latestVersion = latestVersion?.Trim() ?? string.Empty;
                latestVersion = latestVersion.Replace("\r", "").Replace("\n", "").Replace("\t", "").Trim();

                if (string.IsNullOrWhiteSpace(latestVersion))
                {
                    throw new Exception("Version.txt is empty or invalid");
                }

                Debug.WriteLine($"[UpdateService] Current Version: {AppVersion.CURRENT_VERSION}");
                Debug.WriteLine($"[UpdateService] Latest Version from GitHub: {latestVersion}");

                updateInfo.LatestVersion = latestVersion;

                var changelogUrl = $"https://raw.githubusercontent.com/An-Average-Developer/Inazuma-Eleven-VR-Save-Editor/main/changelog.txt";
                var changelog = await _httpClient.GetStringAsync(changelogUrl);
                updateInfo.Changelog = changelog.Trim();

                updateInfo.IsUpdateAvailable = IsNewerVersion(AppVersion.CURRENT_VERSION, latestVersion);

                if (updateInfo.IsUpdateAvailable)
                {
                    var fileName = $"IEVR-SEv{latestVersion.Replace(".", "-")}.zip";
                    var downloadUrl = $"https://github.com/An-Average-Developer/Inazuma-Eleven-VR-Save-Editor/releases/latest/download/{fileName}";
                    
                    updateInfo.DownloadUrl = downloadUrl;
                    updateInfo.FileName = fileName;
                    updateInfo.ReleaseUrl = $"https://github.com/An-Average-Developer/Inazuma-Eleven-VR-Save-Editor/releases/latest";
                    updateInfo.UpdatedDate = DateTime.Now;

                    try
                    {
                        using var headRequest = new HttpRequestMessage(HttpMethod.Head, downloadUrl);
                        using var headResponse = await _httpClient.SendAsync(headRequest);

                        if (headResponse.IsSuccessStatusCode && headResponse.Content.Headers.ContentLength.HasValue)
                        {
                            updateInfo.FileSize = headResponse.Content.Headers.ContentLength.Value;
                        }
                        else
                        {
                            updateInfo.FileSize = 0;
                        }
                    }
                    catch
                    {
                        updateInfo.FileSize = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update check failed: {ex.Message}");
            }

            return updateInfo;
        }

        public async Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo updateInfo, IProgress<int>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(updateInfo.DownloadUrl))
                return false;

            string? tempDir = null;
            string? zipPath = null;

            try
            {
                tempDir = Path.Combine(Path.GetTempPath(), "InazumaElevenVRSaveEditor_Update");
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
                Directory.CreateDirectory(tempDir);

                zipPath = Path.Combine(tempDir, updateInfo.FileName);

                using (var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    var buffer = new byte[8192];
                    var bytesRead = 0L;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        int read;
                        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read);
                            bytesRead += read;

                            if (totalBytes > 0)
                            {
                                var progressPercentage = (int)((bytesRead * 100) / totalBytes);
                                progress?.Report(progressPercentage);
                            }
                        }
                    }
                }

                progress?.Report(100);
                var extractDir = Path.Combine(tempDir, "extracted");
                Directory.CreateDirectory(extractDir);

                ZipFile.ExtractToDirectory(zipPath, extractDir);

                var newExePath = FindExecutable(extractDir);

                if (string.IsNullOrEmpty(newExePath) || !File.Exists(newExePath))
                {
                    Debug.WriteLine("Could not find executable in extracted files");
                    return false;
                }

                var currentExePath = GetCurrentExecutablePath();
                if (string.IsNullOrEmpty(currentExePath))
                {
                    Debug.WriteLine("Could not determine current executable path");
                    return false;
                }

                var currentDirectory = Path.GetDirectoryName(currentExePath);
                if (string.IsNullOrEmpty(currentDirectory))
                {
                    Debug.WriteLine("Could not determine current directory");
                    return false;
                }

                var batchPath = Path.Combine(tempDir, "update.bat");

                var extractedExeDir = Path.GetDirectoryName(newExePath);
                if (string.IsNullOrEmpty(extractedExeDir))
                {
                    Debug.WriteLine("Could not determine extracted executable directory");
                    return false;
                }

                var batchContent = $@"
@echo off
chcp 65001 >nul
echo Updating Sonic Racing Save Manager...
echo Waiting for application to close...
timeout /t 2 /nobreak >nul

:: Copy new files
echo Copying new files...
xcopy ""{extractedExeDir}\*"" ""{currentDirectory}"" /E /Y /I /Q

if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to copy files. Error code: %ERRORLEVEL%
    pause
    exit /b 1
)

echo Update completed successfully!
echo Starting application...

:: Start the new application
start """" ""{currentExePath}""

:: Clean up temporary files
rd /s /q ""{tempDir}""

exit
";

                await File.WriteAllTextAsync(batchPath, batchContent);

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{batchPath}\"",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(processStartInfo);

                await Task.Delay(1000);

                Environment.Exit(0);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update installation failed: {ex.Message}");

                try
                {
                    if (zipPath != null && File.Exists(zipPath))
                        File.Delete(zipPath);
                    if (tempDir != null && Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch
                {
                }

                return false;
            }
        }

        private string FindExecutable(string directory)
        {
            var exeNames = new[] 
            {
                "InazumaElevenVRSaveEditor.exe",
                "Sonic Racing Save Manager.exe",
                Path.GetFileName(GetCurrentExecutablePath()) ?? "InazumaElevenVRSaveEditor.exe"
            };

            foreach (var exeName in exeNames)
            {
                var exePath = Path.Combine(directory, exeName);
                if (File.Exists(exePath))
                    return exePath;
            }

            foreach (var exeName in exeNames)
            {
                var foundExe = Directory.GetFiles(directory, exeName, SearchOption.AllDirectories)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(foundExe))
                    return foundExe;
            }

            var anyExe = Directory.GetFiles(directory, "*.exe", SearchOption.AllDirectories)
                .FirstOrDefault();

            return anyExe ?? "";
        }

        private string GetCurrentExecutablePath()
        {
            try
            {
                var mainModulePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(mainModulePath))
                {
                    return mainModulePath;
                }
                var baseDir = AppContext.BaseDirectory;
                var exeName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "InazumaElevenVRSaveEditor") + ".exe";
                return Path.Combine(baseDir, exeName);
            }
            catch
            {
                var baseDir = AppContext.BaseDirectory;
                return Path.Combine(baseDir, "InazumaElevenVRSaveEditor.exe");
            }
        }

        private bool IsNewerVersion(string currentVersion, string newVersion)
        {
            try
            {
                var current = ParseVersion(currentVersion);
                var latest = ParseVersion(newVersion);

                if (latest.major > current.major) return true;
                if (latest.major < current.major) return false;

                if (latest.minor > current.minor) return true;
                if (latest.minor < current.minor) return false;

                if (latest.patch > current.patch) return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        private (int major, int minor, int patch) ParseVersion(string version)
        {
            var cleanVersion = new string(version.Where(c => char.IsDigit(c) || c == '.').ToArray());

            var parts = cleanVersion.Split('.');
            var major = parts.Length > 0 && int.TryParse(parts[0], out int m) ? m : 0;
            var minor = parts.Length > 1 && int.TryParse(parts[1], out int n) ? n : 0;
            var patch = parts.Length > 2 && int.TryParse(parts[2], out int p) ? p : 0;

            return (major, minor, patch);
        }

        public void OpenGitHubPage()
        {
            try
            {
                var url = AppVersion.GetGitHubRepoUrl();
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open GitHub page: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }
}