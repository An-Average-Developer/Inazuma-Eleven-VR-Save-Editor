using System;
using System.IO;
using System.Text.Json;

namespace InazumaElevenVRSaveEditor.Configuration
{
    public class AppSettings
    {
        public string? ModsDirectory { get; set; }
    }

    public static class SettingsService
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "InazumaElevenVRSaveEditor",
            "settings.json"
        );

        public static AppSettings? LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    return null;
                }

                var json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                return null;
            }
        }

        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
                throw;
            }
        }

        public static string? GetModsDirectory()
        {
            var settings = LoadSettings();
            return settings?.ModsDirectory;
        }

        public static void SaveModsDirectory(string modsDirectory)
        {
            var settings = LoadSettings() ?? new AppSettings();
            settings.ModsDirectory = modsDirectory;
            SaveSettings(settings);
        }
    }
}
