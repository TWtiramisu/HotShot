using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenshotApp.Models;
using ScreenshotApp.Native;
using ScreenshotApp.Services;

namespace ScreenshotApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<WindowInfo> _activeWindows = new();

        [ObservableProperty]
        private WindowInfo? _selectedWindow;

        [ObservableProperty]
        private bool _isCapturing;

        [ObservableProperty]
        private bool _isSettingsOpen;

        [ObservableProperty]
        private bool _isFolderConfigured;

        [ObservableProperty]
        private ObservableCollection<GalleryGroup> _galleryGroups = new();

        public SettingsViewModel SettingsVM { get; } = new();

        private FileSystemWatcher? _fileWatcher;
        private readonly GamepadWatcher _gamepadWatcher = new();
        private IntPtr _mainWindowHandle = IntPtr.Zero;

        public MainViewModel()
        {
            SettingsVM.SaveFolderPathChanged += OnSaveFolderPathChanged;

            // Initial folder check
            UpdateFolderConfiguredState(SettingsVM.SaveFolderPath);

            // Initialize Gamepad Listener
            _gamepadWatcher.GamepadCombinationPressed += OnGamepadCombinationPressed;
            _gamepadWatcher.GamepadCombinationRecorded += OnGamepadCombinationRecorded;

            _gamepadWatcher.Start();

            // Observe recording mode
            SettingsVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SettingsVM.IsRecordingGamepad))
                {
                    _gamepadWatcher.IsRecordingMode = SettingsVM.IsRecordingGamepad;
                }
            };
        }

        public void Initialize(IntPtr windowHandle)
        {
            _mainWindowHandle = windowHandle;
            RefreshWindows();
            LoadGallery();
        }

        [RelayCommand]
        public void RefreshWindows()
        {
            var windows = new List<WindowInfo>();

            NativeMethods.EnumWindows((hWnd, lParam) =>
            {
                if (hWnd == _mainWindowHandle) return true;
                if (!NativeMethods.IsWindowVisible(hWnd)) return true;

                int length = NativeMethods.GetWindowTextLength(hWnd);
                if (length == 0) return true;

                StringBuilder sb = new StringBuilder(length + 1);
                NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString();

                if (string.IsNullOrWhiteSpace(title)) return true;

                // Filter out system windows
                if (title == "Program Manager" || title == "Default IME" || title == "MSCTFIME UI") return true;

                // Filter by style / cloaked status
                IntPtr exStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE);
                long exStyleVal = exStyle.ToInt64();

                if ((exStyleVal & NativeMethods.WS_EX_TOOLWINDOW) != 0 && (exStyleVal & NativeMethods.WS_EX_APPWINDOW) == 0)
                {
                    return true;
                }

                // Check DWM Cloaked attribute
                int cloakedResult = NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out NativeMethods.RECT _, (uint)Marshal.SizeOf<NativeMethods.RECT>());
                if (cloakedResult == 0)
                {
                    // If DwmGetWindowAttribute succeeds with 0, cloaked check can be inspected
                }

                NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
                string processName = "Unknown";
                try
                {
                    using var proc = Process.GetProcessById((int)processId);
                    processName = proc.ProcessName;
                }
                catch { }

                windows.Add(new WindowInfo
                {
                    Hwnd = hWnd,
                    Title = title,
                    ProcessName = processName
                });

                return true;
            }, IntPtr.Zero);

            ActiveWindows = new ObservableCollection<WindowInfo>(windows.OrderBy(w => w.DisplayTitle));

            if (SelectedWindow == null || !ActiveWindows.Any(w => w.Hwnd == SelectedWindow.Hwnd))
            {
                SelectedWindow = ActiveWindows.FirstOrDefault();
            }
        }

        [RelayCommand]
        private void ToggleCapture()
        {
            IsCapturing = !IsCapturing;
        }

        [RelayCommand]
        public async Task TriggerCaptureAsync()
        {
            if (!IsFolderConfigured) return;

            // Auto-refresh and select top window if SelectedWindow is missing or closed
            if (SelectedWindow == null || !NativeMethods.IsWindow(SelectedWindow.Hwnd))
            {
                RefreshWindows();
                SelectedWindow = ActiveWindows.FirstOrDefault();
            }

            if (SelectedWindow == null) return;

            string folder = SettingsVM.SaveFolderPath;
            string? capturedFile = await CaptureEngine.CaptureWindowAsync(SelectedWindow.Hwnd, SelectedWindow.Title, folder);

            if (!string.IsNullOrEmpty(capturedFile))
            {
                await Application.Current.Dispatcher.InvokeAsync(() => AddFileToGallery(capturedFile));
            }
        }

        [RelayCommand]
        private void OpenFolder()
        {
            if (Directory.Exists(SettingsVM.SaveFolderPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = SettingsVM.SaveFolderPath,
                    UseShellExecute = true
                });
            }
        }

        [RelayCommand]
        private void OpenSettings()
        {
            IsSettingsOpen = true;
        }

        [RelayCommand]
        private void CloseSettings()
        {
            IsSettingsOpen = false;
        }

        [RelayCommand]
        private void DoubleClickItem(GalleryItem? item)
        {
            if (item != null && File.Exists(item.FilePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = item.FilePath,
                    UseShellExecute = true
                });
            }
        }

        [RelayCommand]
        private async Task DeleteItemAsync(GalleryItem? item)
        {
            if (item == null) return;

            try
            {
                if (File.Exists(item.FilePath))
                {
                    await Task.Run(() => File.Delete(item.FilePath));
                }

                // Remove from gallery group
                RemoveFileFromGallery(item.FilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刪除截圖失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnSaveFolderPathChanged(object? sender, string newPath)
        {
            UpdateFolderConfiguredState(newPath);
            LoadGallery();
        }

        private void UpdateFolderConfiguredState(string path)
        {
            IsFolderConfigured = !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);
        }

        private void SetupFileWatcher(string folderPath)
        {
            _fileWatcher?.Dispose();
            _fileWatcher = null;

            if (!Directory.Exists(folderPath)) return;

            _fileWatcher = new FileSystemWatcher(folderPath, "*.png")
            {
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };

            _fileWatcher.Created += (s, e) =>
            {
                Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await Task.Delay(200); // Brief pause to ensure write completion
                    AddFileToGallery(e.FullPath);
                });
            };

            _fileWatcher.Deleted += (s, e) =>
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    RemoveFileFromGallery(e.FullPath);
                });
            };
        }

        public void LoadGallery()
        {
            GalleryGroups.Clear();

            string folder = SettingsVM.SaveFolderPath;
            if (!Directory.Exists(folder)) return;

            SetupFileWatcher(folder);

            var files = Directory.GetFiles(folder, "*.png")
                .Select(f => GalleryItem.CreateFromFile(f))
                .Where(item => item != null)
                .Cast<GalleryItem>()
                .OrderByDescending(item => item.CreatedTime)
                .ToList();

            // Group by date (yyyy-MM-dd)
            var grouped = files.GroupBy(item => item.CreatedTime.ToString("yyyy-MM-dd"));

            foreach (var group in grouped)
            {
                var galleryGroup = new GalleryGroup
                {
                    DateTitle = group.Key,
                    Items = new ObservableCollection<GalleryItem>(group)
                };
                GalleryGroups.Add(galleryGroup);
            }
        }

        private void AddFileToGallery(string filePath)
        {
            if (GalleryGroups.Any(g => g.Items.Any(i => i.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase))))
            {
                return; // Already present
            }

            var item = GalleryItem.CreateFromFile(filePath);
            if (item == null) return;

            string dateTitle = item.CreatedTime.ToString("yyyy-MM-dd");
            var group = GalleryGroups.FirstOrDefault(g => g.DateTitle == dateTitle);

            if (group == null)
            {
                group = new GalleryGroup
                {
                    DateTitle = dateTitle,
                    Items = new ObservableCollection<GalleryItem>()
                };
                GalleryGroups.Insert(0, group); // Insert newest date group at top
            }

            group.Items.Insert(0, item); // Insert newest photo at top of date group
        }

        private void RemoveFileFromGallery(string filePath)
        {
            foreach (var group in GalleryGroups.ToList())
            {
                var itemToRemove = group.Items.FirstOrDefault(i => i.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
                if (itemToRemove != null)
                {
                    group.Items.Remove(itemToRemove);
                    if (group.Items.Count == 0)
                    {
                        GalleryGroups.Remove(group);
                    }
                    break;
                }
            }
        }

        private void OnGamepadCombinationRecorded(object? sender, Windows.Gaming.Input.GamepadButtons buttons)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                SettingsVM.SetGamepadHotkey(buttons);
            });
        }

        private void OnGamepadCombinationPressed(object? sender, Windows.Gaming.Input.GamepadButtons buttons)
        {
            var target = SettingsVM.TargetGamepadButton;
            if (IsCapturing && target != Windows.Gaming.Input.GamepadButtons.None && (buttons & target) == target)
            {
                Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await TriggerCaptureAsync();
                });
            }
        }
    }
}
