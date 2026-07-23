using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ScreenshotApp.Services;
using ScreenshotApp.ViewModels;

namespace ScreenshotApp
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; } = new();
        private readonly HotkeyListener _hotkeyListener = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = ViewModel;

            Loaded += MainWindow_Loaded;
            KeyDown += MainWindow_KeyDown;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            ViewModel.Initialize(hwnd);

            // Register default combination hotkey (Ctrl + Alt + S)
            _hotkeyListener.HotkeyPressed += OnHotkeyPressed;
            _hotkeyListener.Register(hwnd, ViewModel.SettingsVM.TargetModifiers, ViewModel.SettingsVM.TargetKey);

            ViewModel.SettingsVM.PropertyChanged += (s, ev) =>
            {
                if (ev.PropertyName == nameof(ViewModel.SettingsVM.TargetModifiers) ||
                    ev.PropertyName == nameof(ViewModel.SettingsVM.TargetKey))
                {
                    _hotkeyListener.Register(hwnd, ViewModel.SettingsVM.TargetModifiers, ViewModel.SettingsVM.TargetKey);
                }
            };
        }

        private void OnHotkeyPressed(object? sender, EventArgs e)
        {
            if (!ViewModel.IsCapturing) return;

            Dispatcher.InvokeAsync(async () =>
            {
                await ViewModel.TriggerCaptureAsync();
            });
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (ViewModel.SettingsVM.IsRecordingKeyboard)
            {
                Key key = e.Key == Key.System ? e.SystemKey : e.Key;

                // Ignore pure modifier presses until a non-modifier key is pressed
                if (key != Key.None &&
                    key != Key.LeftShift && key != Key.RightShift &&
                    key != Key.LeftCtrl && key != Key.RightCtrl &&
                    key != Key.LeftAlt && key != Key.RightAlt &&
                    key != Key.LWin && key != Key.RWin)
                {
                    ModifierKeys modifiers = Keyboard.Modifiers;

                    ViewModel.SettingsVM.SetKeyboardHotkey(modifiers, key);

                    // Re-register hotkey with Windows API
                    IntPtr hwnd = new WindowInteropHelper(this).Handle;
                    _hotkeyListener.Register(hwnd, modifiers, key);

                    e.Handled = true;
                }
            }
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _hotkeyListener.Dispose();
            base.OnClosed(e);
        }
    }
}
