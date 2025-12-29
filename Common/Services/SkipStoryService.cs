using System;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Reflection;
using System.Linq;
using Microsoft.Win32;

namespace InazumaElevenVRSaveEditor.Common.Services
{
    public static class SkipStoryService
    {
        private const string GAME_NAME = "INAZUMA ELEVEN Victory Road";
        private const string EmbeddedResourceName = "InazumaElevenVRSaveEditor.Resources.SkipStory.zip";

        private static string? GetGameDataFolder()
        {
            string? gamePath = FindSteamGameFolder();
            if (string.IsNullOrEmpty(gamePath))
                return null;

            return Path.Combine(gamePath, "data");
        }

        private static string? GetTargetFilePath()
        {
            string? dataFolder = GetGameDataFolder();
            if (string.IsNullOrEmpty(dataFolder))
                return null;

            return Path.Combine(
                dataFolder,
                "common",
                "gamedata",
                "soccer",
                "game_quest_config_1.02.33.cfg.bin"
            );
        }

        private static string? GetCpkListFilePath()
        {
            string? dataFolder = GetGameDataFolder();
            if (string.IsNullOrEmpty(dataFolder))
                return null;

            return Path.Combine(dataFolder, "cpk_list.cfg.bin");
        }

        private static string? FindSteamGameFolder()
        {
            try
            {
                string? steamPath = null;

                using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                {
                    steamPath = key?.GetValue("InstallPath") as string;
                }

                if (string.IsNullOrEmpty(steamPath))
                {
                    using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                    {
                        steamPath = key?.GetValue("InstallPath") as string;
                    }
                }

                if (string.IsNullOrEmpty(steamPath))
                    return null;

                string defaultLibrary = Path.Combine(steamPath, "steamapps", "common", GAME_NAME);
                if (Directory.Exists(defaultLibrary))
                    return defaultLibrary;

                string libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(libraryFoldersPath))
                {
                    string content = File.ReadAllText(libraryFoldersPath);
                    System.Text.RegularExpressions.Regex pathRegex = new System.Text.RegularExpressions.Regex(
                        @"""path""\s*""([^""]+)""",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    );

                    var matches = pathRegex.Matches(content);
                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        if (match.Success && match.Groups.Count > 1)
                        {
                            string libraryPath = match.Groups[1].Value.Replace("\\\\", "\\");
                            string gamePath = Path.Combine(libraryPath, "steamapps", "common", GAME_NAME);

                            if (Directory.Exists(gamePath))
                                return gamePath;
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Enables Skip Story by removing the .off extension or extracting from zip
        /// </summary>
        public static bool EnableSkipStory()
        {
            try
            {
                string? targetPath = GetTargetFilePath();
                if (string.IsNullOrEmpty(targetPath))
                    return false;

                string targetPathWithOff = targetPath + ".off";

                // Backup cpk_list.cfg.bin: 1) Copy to .bak, 2) Delete original
                string? cpkListPath = GetCpkListFilePath();
                if (!string.IsNullOrEmpty(cpkListPath) && File.Exists(cpkListPath))
                {
                    string cpkListBakPath = cpkListPath + ".bak";
                    if (!File.Exists(cpkListBakPath))
                    {
                        File.Copy(cpkListPath, cpkListBakPath, true);
                    }
                    File.Delete(cpkListPath);
                }

                // Handle game_quest_config file
                if (File.Exists(targetPathWithOff))
                {
                    // File exists with .off - just rename it
                    if (File.Exists(targetPath))
                        File.Delete(targetPath);
                    File.Move(targetPathWithOff, targetPath);
                }
                else if (!File.Exists(targetPath))
                {
                    // File doesn't exist at all - need to extract from zip
                    if (!ExtractAsActive())
                        return false;
                    // ExtractAsActive extracts BOTH files, so we're done
                    return true;
                }

                // At this point, game_quest_config is active, but we still need to extract cpk_list
                // Extract just the cpk_list.cfg.bin from zip
                if (!ExtractCpkListFromZip())
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkipStory] Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Disables Skip Story by adding .off extension or extracting from zip if needed
        /// </summary>
        public static bool DisableSkipStory()
        {
            try
            {
                string? targetPath = GetTargetFilePath();
                if (string.IsNullOrEmpty(targetPath))
                    return false;

                string targetPathWithOff = targetPath + ".off";

                // Handle game_quest_config file
                if (File.Exists(targetPath))
                {
                    if (File.Exists(targetPathWithOff))
                        File.Delete(targetPathWithOff);

                    File.Move(targetPath, targetPathWithOff);
                }

                // Handle cpk_list.cfg.bin file - restore from .bak when disabling
                string? cpkListPath = GetCpkListFilePath();
                if (!string.IsNullOrEmpty(cpkListPath))
                {
                    string cpkListBakPath = cpkListPath + ".bak";
                    if (File.Exists(cpkListBakPath))
                    {
                        if (File.Exists(cpkListPath))
                            File.Delete(cpkListPath);

                        File.Move(cpkListBakPath, cpkListPath);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkipStory] Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Extracts only cpk_list.cfg.bin from the zip
        /// </summary>
        private static bool ExtractCpkListFromZip()
        {
            string? tempZipPath = null;
            string? tempExtractPath = null;

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (Stream? resourceStream = assembly.GetManifestResourceStream(EmbeddedResourceName))
                {
                    if (resourceStream == null)
                        return false;

                    tempZipPath = Path.Combine(Path.GetTempPath(), "SkipStory_" + Guid.NewGuid().ToString() + ".zip");
                    using (FileStream fileStream = File.Create(tempZipPath))
                    {
                        resourceStream.CopyTo(fileStream);
                    }
                }

                tempExtractPath = Path.Combine(Path.GetTempPath(), "SkipStory_Extract_" + Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempExtractPath);
                ZipFile.ExtractToDirectory(tempZipPath, tempExtractPath);

                string? gameDataFolder = GetGameDataFolder();
                string? cpkListPath = GetCpkListFilePath();
                if (string.IsNullOrEmpty(gameDataFolder) || string.IsNullOrEmpty(cpkListPath))
                    return false;

                string extractedCpkPath = Path.Combine(tempExtractPath, "cpk_list.cfg.bin");
                if (File.Exists(extractedCpkPath))
                {
                    if (File.Exists(cpkListPath))
                        File.Delete(cpkListPath);

                    File.Copy(extractedCpkPath, cpkListPath, false);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkipStory] Error: {ex.Message}");
                return false;
            }
            finally
            {
                try
                {
                    if (tempZipPath != null && File.Exists(tempZipPath))
                        File.Delete(tempZipPath);

                    if (tempExtractPath != null && Directory.Exists(tempExtractPath))
                        Directory.Delete(tempExtractPath, true);
                }
                catch { }
            }
        }

        /// <summary>
        /// Extracts the file from embedded SkipStory.zip resource as an active file (no .off extension)
        /// </summary>
        private static bool ExtractAsActive()
        {
            string? tempZipPath = null;
            string? tempExtractPath = null;

            try
            {
                // Get the embedded resource
                var assembly = Assembly.GetExecutingAssembly();
                using (Stream? resourceStream = assembly.GetManifestResourceStream(EmbeddedResourceName))
                {
                    if (resourceStream == null)
                        return false;

                    // Copy embedded zip to temp location
                    tempZipPath = Path.Combine(Path.GetTempPath(), "SkipStory_" + Guid.NewGuid().ToString() + ".zip");
                    using (FileStream fileStream = File.Create(tempZipPath))
                    {
                        resourceStream.CopyTo(fileStream);
                    }
                }

                // Extract to temp folder first
                tempExtractPath = Path.Combine(Path.GetTempPath(), "SkipStory_Extract_" + Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempExtractPath);
                ZipFile.ExtractToDirectory(tempZipPath, tempExtractPath);

                string? gameDataFolder = GetGameDataFolder();
                if (string.IsNullOrEmpty(gameDataFolder))
                    return false;

                if (!Directory.Exists(gameDataFolder))
                    Directory.CreateDirectory(gameDataFolder);

                CopyDirectory(tempExtractPath, gameDataFolder);

                string? targetPath = GetTargetFilePath();
                return !string.IsNullOrEmpty(targetPath) && File.Exists(targetPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkipStory] Error: {ex.Message}");
                return false;
            }
            finally
            {
                // Clean up temp files
                try
                {
                    if (tempZipPath != null && File.Exists(tempZipPath))
                        File.Delete(tempZipPath);

                    if (tempExtractPath != null && Directory.Exists(tempExtractPath))
                        Directory.Delete(tempExtractPath, true);
                }
                catch { }
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            foreach (string file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            {
                string relativePath = file.Substring(sourceDir.Length + 1);
                string destFile = Path.Combine(destinationDir, relativePath);

                string? destDirectory = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDirectory) && !Directory.Exists(destDirectory))
                {
                    Directory.CreateDirectory(destDirectory);
                }

                if (File.Exists(destFile))
                {
                    File.Delete(destFile);
                }

                File.Copy(file, destFile, false);
            }
        }

        /// <summary>
        /// Gets the current state of Skip Story (true = enabled, false = disabled)
        /// </summary>
        public static bool IsSkipStoryEnabled()
        {
            string? targetPath = GetTargetFilePath();
            if (string.IsNullOrEmpty(targetPath))
                return false;

            return File.Exists(targetPath);
        }
    }
}
