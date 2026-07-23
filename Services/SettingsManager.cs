using System;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using Windows.Gaming.Input;

namespace ScreenshotApp.Services
{
    public class AppSettings
    {
        public string SaveFolderPath { get; set; } = string.Empty;
        public ModifierKeys KeyboardModifiers { get; set; } = ModifierKeys.Control | ModifierKeys.Alt;
        public Key KeyboardKey { get; set; } = Key.S;
        public string KeyboardHotkeyText { get; set; } = "Ctrl + Alt + S";

        public GamepadButtons GamepadButtons { get; set; } = GamepadButtons.RightShoulder;
        public string GamepadHotkeyText { get; set; } = "RightShoulder";
    }

    public static class SettingsManager
    {
        private static readonly string SettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ScreenshotApp");

        private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.json");

        public static AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        if (string.IsNullOrWhiteSpace(settings.SaveFolderPath))
                        {
                            settings.SaveFolderPath = GetDefaultPicturesFolder();
                        }
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load Settings Error: {ex.Message}");
            }

            var defaultSettings = new AppSettings
            {
                SaveFolderPath = GetDefaultPicturesFolder()
            };
            SaveSettings(defaultSettings);
            return defaultSettings;
        }

        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                if (!Directory.Exists(SettingsFolder))
                {
                    Directory.CreateDirectory(SettingsFolder);
                }

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save Settings Error: {ex.Message}");
            }
        }

        private static string GetDefaultPicturesFolder()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "ScreenshotApp");
        }
    }
}
