using System;
using System.Windows.Input;
using System.Windows.Interop;
using ScreenshotApp.Native;

namespace ScreenshotApp.Services
{
    public class HotkeyListener : IDisposable
    {
        public event EventHandler? HotkeyPressed;

        private HwndSource? _hwndSource;
        private int _hotkeyId = 9000;
        private bool _isRegistered;

        public bool Register(IntPtr windowHandle, ModifierKeys modifiers, Key key)
        {
            Unregister();

            if (windowHandle == IntPtr.Zero) return false;

            _hwndSource = HwndSource.FromHwnd(windowHandle);
            _hwndSource?.AddHook(HwndHook);

            uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
            uint mod = (uint)modifiers;

            _isRegistered = NativeMethods.RegisterHotKey(windowHandle, _hotkeyId, mod, virtualKey);
            return _isRegistered;
        }

        public void Unregister()
        {
            if (_isRegistered && _hwndSource?.Handle != null)
            {
                NativeMethods.UnregisterHotKey(_hwndSource.Handle, _hotkeyId);
                _hwndSource.RemoveHook(HwndHook);
                _isRegistered = false;
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == _hotkeyId)
            {
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Unregister();
        }
    }
}
