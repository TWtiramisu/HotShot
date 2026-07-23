using System;
using System.IO;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenshotApp.Services;
using Windows.Gaming.Input;

namespace ScreenshotApp.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _saveFolderPath = string.Empty;

        [ObservableProperty]
        private string _keyboardHotkeyText = "Ctrl + Alt + S";

        [ObservableProperty]
        private string _gamepadHotkeyText = "RightShoulder";

        [ObservableProperty]
        private ModifierKeys _targetModifiers = ModifierKeys.Control | ModifierKeys.Alt;

        [ObservableProperty]
        private Key _targetKey = Key.S;

        [ObservableProperty]
        private GamepadButtons _targetGamepadButton = GamepadButtons.RightShoulder;

        [ObservableProperty]
        private bool _isRecordingKeyboard;

        [ObservableProperty]
        private bool _isRecordingGamepad;

        public event EventHandler<string>? SaveFolderPathChanged;

        public SettingsViewModel()
        {
            LoadSavedSettings();
        }

        public void LoadSavedSettings()
        {
            var settings = SettingsManager.LoadSettings();
            SaveFolderPath = settings.SaveFolderPath;
            TargetModifiers = settings.KeyboardModifiers;
            TargetKey = settings.KeyboardKey;
            KeyboardHotkeyText = settings.KeyboardHotkeyText;

            TargetGamepadButton = settings.GamepadButtons;
            GamepadHotkeyText = settings.GamepadHotkeyText;
        }

        public void SaveCurrentSettings()
        {
            var settings = new AppSettings
            {
                SaveFolderPath = SaveFolderPath,
                KeyboardModifiers = TargetModifiers,
                KeyboardKey = TargetKey,
                KeyboardHotkeyText = KeyboardHotkeyText,
                GamepadButtons = TargetGamepadButton,
                GamepadHotkeyText = GamepadHotkeyText
            };
            SettingsManager.SaveSettings(settings);
        }

        [RelayCommand]
        private void BrowseFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "選擇截圖儲存資料夾"
            };

            if (!string.IsNullOrWhiteSpace(SaveFolderPath) && Directory.Exists(SaveFolderPath))
            {
                dialog.InitialDirectory = SaveFolderPath;
            }

            if (dialog.ShowDialog() == true)
            {
                SaveFolderPath = dialog.FolderName;
                SaveCurrentSettings();
                SaveFolderPathChanged?.Invoke(this, SaveFolderPath);
            }
        }

        [RelayCommand]
        private void StartRecordKeyboard()
        {
            IsRecordingKeyboard = true;
            KeyboardHotkeyText = "請按下組合鍵 (如 Ctrl+Alt+S)...";
        }

        [RelayCommand]
        private void StartRecordGamepad()
        {
            IsRecordingGamepad = true;
            GamepadHotkeyText = "請按下手把按鍵...";
        }

        public void SetKeyboardHotkey(ModifierKeys modifiers, Key key)
        {
            TargetModifiers = modifiers;
            TargetKey = key;
            KeyboardHotkeyText = FormatHotkeyText(modifiers, key);
            IsRecordingKeyboard = false;
            SaveCurrentSettings();
        }

        public void SetGamepadHotkey(GamepadButtons button)
        {
            TargetGamepadButton = button;
            GamepadHotkeyText = GamepadWatcher.FormatGamepadButtons(button);
            IsRecordingGamepad = false;
            SaveCurrentSettings();
        }

        public static string FormatHotkeyText(ModifierKeys modifiers, Key key)
        {
            var parts = new System.Collections.Generic.List<string>();
            if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
            
            parts.Add(key.ToString());
            return string.Join(" + ", parts);
        }
    }
}
