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
                // Find Steam game folder
                _gameFolderPath = FindSteamGameFolder();
                if (string.IsNullOrEmpty(_gameFolderPath) || !Directory.Exists(_gameFolderPath))
                {
                    return false;
                }

                // Find EACLauncher.exe in game folder
                _eacLauncherPath = Path.Combine(_gameFolderPath, "EACLauncher.exe");
                if (!File.Exists(_eacLauncherPath))
                {
                    return false;
                }

                // Create temp folder
                _tempFolderPath = Path.Combine(Path.GetTempPath(), $"InazumaElevenVR_{Guid.NewGuid()}");
                Directory.CreateDirectory(_tempFolderPath);

                // Extract embedded zip to temp folder
                string zipPath = Path.Combine(_tempFolderPath, "EACLauncher.zip");
                ExtractEmbeddedResource("InazumaElevenVRSaveEditor.Resources.EACLauncher.zip", zipPath);

                // Extract zip contents to temp folder
                string extractFolder = Path.Combine(_tempFolderPath, "extracted");
                Directory.CreateDirectory(extractFolder);
                ZipFile.ExtractToDirectory(zipPath, extractFolder);

                // Backup original EACLauncher
                _eacLauncherBackupPath = _eacLauncherPath + ".bak";

                // If backup already exists, delete it
                if (File.Exists(_eacLauncherBackupPath))
                {
                    File.Delete(_eacLauncherBackupPath);
                }

                File.Move(_eacLauncherPath, _eacLauncherBackupPath);

                // Copy patched EACLauncher from extracted folder
                string patchedLauncher = Path.Combine(extractFolder, "EACLauncher.exe");
                if (!File.Exists(patchedLauncher))
                {
                    // Restore backup if patch file not found
                    File.Move(_eacLauncherBackupPath, _eacLauncherPath);
                    return false;
                }

                File.Copy(patchedLauncher, _eacLauncherPath);
                _isPatched = true;

                return true;
            }
            catch (Exception)
            {
                // If anything fails, try to restore backup
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
                // Delete patched file
                if (!string.IsNullOrEmpty(_eacLauncherPath) && File.Exists(_eacLauncherPath))
                {
                    File.Delete(_eacLauncherPath);
                }

                // Restore backup
                if (!string.IsNullOrEmpty(_eacLauncherBackupPath) &&
                    !string.IsNullOrEmpty(_eacLauncherPath) &&
                    File.Exists(_eacLauncherBackupPath))
                {
                    File.Move(_eacLauncherBackupPath, _eacLauncherPath);
                }

                // Clean up temp folder
                if (!string.IsNullOrEmpty(_tempFolderPath) && Directory.Exists(_tempFolderPath))
                {
                    Directory.Delete(_tempFolderPath, true);
                }

                _isPatched = false;
            }
            catch (Exception)
            {
                // Silent fail on cleanup
            }
        }

        private string? FindSteamGameFolder()
        {
            try
            {
                // Try to find Steam installation path from registry
                string? steamPath = null;

                // Check 64-bit registry
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                {
                    steamPath = key?.GetValue("InstallPath") as string;
                }

                // Check 32-bit registry if not found
                if (string.IsNullOrEmpty(steamPath))
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                    {
                        steamPath = key?.GetValue("InstallPath") as string;
                    }
                }

                if (string.IsNullOrEmpty(steamPath))
                    return null;

                // Check default library folder
                string defaultLibrary = Path.Combine(steamPath, "steamapps", "common", GAME_NAME);
                if (Directory.Exists(defaultLibrary))
                {
                    return defaultLibrary;
                }

                // Check libraryfolders.vdf for additional library locations
                string libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(libraryFoldersPath))
                {
                    string[] lines = File.ReadAllLines(libraryFoldersPath);
                    foreach (string line in lines)
                    {
                        if (line.Contains("\"path\""))
                        {
                            // Extract path from "path"		"C:\\SteamLibrary"
                            string[] parts = line.Split(new[] { '"' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                            {
                                string libraryPath = parts[1].Replace("\\\\", "\\");
                                string gamePath = Path.Combine(libraryPath, "steamapps", "common", GAME_NAME);
                                if (Directory.Exists(gamePath))
                                {
                                    return gamePath;
                                }
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
