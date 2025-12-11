using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Win32;

namespace InazumaElevenVRSaveEditor.Common.Services
{
    public class EACLauncherService
    {
        private const string STEAM_APP_ID = "2799860";
        private const string GAME_NAME = "INAZUMA ELEVEN Victory Road";
        private string? _gameFolderPath;
        private string? _tempFolderPath;
        private string? _eacLauncherPath;
        private string? _eacLauncherBackupPath;
        private bool _isPatched = false;

        public bool PatchEACLauncher()
        {
            try
            {
                _gameFolderPath = FindSteamGameFolder();
                if (string.IsNullOrEmpty(_gameFolderPath) || !Directory.Exists(_gameFolderPath))
                {
                    return false;
                }

                _eacLauncherPath = Path.Combine(_gameFolderPath, "EACLauncher.exe");
                if (!File.Exists(_eacLauncherPath))
                {
                    return false;
                }

                _tempFolderPath = Path.Combine(Path.GetTempPath(), $"InazumaElevenVR_{Guid.NewGuid()}");
                Directory.CreateDirectory(_tempFolderPath);

                string zipPath = Path.Combine(_tempFolderPath, "EACLauncher.zip");
                ExtractEmbeddedResource("InazumaElevenVRSaveEditor.Resources.EACLauncher.zip", zipPath);

                string extractFolder = Path.Combine(_tempFolderPath, "extracted");
                Directory.CreateDirectory(extractFolder);
                ZipFile.ExtractToDirectory(zipPath, extractFolder);

                _eacLauncherBackupPath = _eacLauncherPath + ".bak";

                if (File.Exists(_eacLauncherBackupPath))
                {
                    File.Delete(_eacLauncherBackupPath);
                }

                File.Move(_eacLauncherPath, _eacLauncherBackupPath);

                string patchedLauncher = Path.Combine(extractFolder, "EACLauncher.exe");
                if (!File.Exists(patchedLauncher))
                {
                    File.Move(_eacLauncherBackupPath, _eacLauncherPath);
                    return false;
                }

                File.Copy(patchedLauncher, _eacLauncherPath);
                _isPatched = true;

                return true;
            }
            catch (Exception)
            {
                RestoreEACLauncher();
                return false;
            }
        }

        public void RestoreEACLauncher()
        {
            if (!_isPatched)
                return;

            try
            {
                if (!string.IsNullOrEmpty(_eacLauncherPath) && File.Exists(_eacLauncherPath))
                {
                    File.Delete(_eacLauncherPath);
                }

                if (!string.IsNullOrEmpty(_eacLauncherBackupPath) &&
                    !string.IsNullOrEmpty(_eacLauncherPath) &&
                    File.Exists(_eacLauncherBackupPath))
                {
                    File.Move(_eacLauncherBackupPath, _eacLauncherPath);
                }

                if (!string.IsNullOrEmpty(_tempFolderPath) && Directory.Exists(_tempFolderPath))
                {
                    Directory.Delete(_tempFolderPath, true);
                }

                _isPatched = false;
            }
            catch (Exception)
            {
            }
        }

        private string? FindSteamGameFolder()
        {
            try
            {
                string? steamPath = null;

                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                {
                    steamPath = key?.GetValue("InstallPath") as string;
                }

                if (string.IsNullOrEmpty(steamPath))
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                    {
                        steamPath = key?.GetValue("InstallPath") as string;
                    }
                }

                if (string.IsNullOrEmpty(steamPath))
                    return null;

                string defaultLibrary = Path.Combine(steamPath, "steamapps", "common", GAME_NAME);
                if (Directory.Exists(defaultLibrary))
                {
                    return defaultLibrary;
                }

                string libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(libraryFoldersPath))
                {
                    string content = File.ReadAllText(libraryFoldersPath);

                    // Parse all "path" entries from VDF file - handles multiple formats
                    System.Text.RegularExpressions.Regex pathRegex = new System.Text.RegularExpressions.Regex(
                        @"""path""\s*""([^""]+)""",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    );

                    var matches = pathRegex.Matches(content);
                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        if (match.Groups.Count >= 2)
                        {
                            string libraryPath = match.Groups[1].Value.Replace("\\\\", "\\");
                            string gamePath = Path.Combine(libraryPath, "steamapps", "common", GAME_NAME);

                            System.Diagnostics.Debug.WriteLine($"Checking Steam library: {libraryPath}");

                            if (Directory.Exists(gamePath))
                            {
                                System.Diagnostics.Debug.WriteLine($"Found game at: {gamePath}");
                                return gamePath;
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void ExtractEmbeddedResource(string resourceName, string outputPath)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new Exception($"Embedded resource not found: {resourceName}");

                using (FileStream fileStream = File.Create(outputPath))
                {
                    stream.CopyTo(fileStream);
                }
            }
        }
    }
}
