using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Interop;
using ScreenshotApp.Native;

namespace ScreenshotApp.Services
{
    /// <summary>
    /// Dual-engine global keyboard hotkey listener combining Win32 RegisterHotKey with kernel GetAsyncKeyState polling.
    /// Deduplication prevents double-firing within 300ms.
    /// </summary>
    public class HotkeyListener : IDisposable
    {
        public event EventHandler? HotkeyPressed;

        private HwndSource? _hwndSource;
        private int _hotkeyId = 9000;
        private bool _isRegistered;

        private CancellationTokenSource? _cts;
        private ModifierKeys _modifiers;
        private Key _key;
        private bool _lastPressed;
        private long _lastTriggerTicks;

        public bool Register(IntPtr windowHandle, ModifierKeys modifiers, Key key)
        {
            Unregister();

            _modifiers = modifiers;
            _key = key;

            if (key == Key.None) return false;

            // Engine 1: Win32 RegisterHotKey for instant OS-level message handling
            if (windowHandle != IntPtr.Zero)
            {
                _hwndSource = HwndSource.FromHwnd(windowHandle);
                _hwndSource?.AddHook(HwndHook);

                uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
                uint mod = (uint)modifiers;
                _isRegistered = NativeMethods.RegisterHotKey(windowHandle, _hotkeyId, mod, virtualKey);
            }

            // Engine 2: Background kernel GetAsyncKeyState polling loop (bypasses window focus/DirectX exclusive modes)
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            Task.Run(() => PollKeyboardAsync(token));

            return true;
        }

        public void Unregister()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _lastPressed = false;

            if (_isRegistered && _hwndSource?.Handle is IntPtr hwnd && hwnd != IntPtr.Zero)
            {
                NativeMethods.UnregisterHotKey(hwnd, _hotkeyId);
                _hwndSource.RemoveHook(HwndHook);
                _isRegistered = false;
            }
        }

        private void TriggerHotkeyPressed()
        {
            long now = DateTime.UtcNow.Ticks;
            long last = Interlocked.Read(ref _lastTriggerTicks);
            if (now - last < TimeSpan.FromMilliseconds(300).Ticks)
            {
                return; // Suppress duplicate triggers within 300ms window
            }
            Interlocked.Exchange(ref _lastTriggerTicks, now);
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }

        private async Task PollKeyboardAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    bool pressed = IsTargetCombinationDown();

                    if (pressed && !_lastPressed)
                    {
                        TriggerHotkeyPressed();
                    }

                    _lastPressed = pressed;
                    await Task.Delay(16, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Keep loop alive
                }
            }
        }

        private bool IsTargetCombinationDown()
        {
            if (_key == Key.None) return false;

            // Check modifiers with Left/Right fallback
            if (_modifiers.HasFlag(ModifierKeys.Control) && !IsCtrlPressed()) return false;
            if (_modifiers.HasFlag(ModifierKeys.Alt) && !IsAltPressed()) return false;
            if (_modifiers.HasFlag(ModifierKeys.Shift) && !IsShiftPressed()) return false;
            if (_modifiers.HasFlag(ModifierKeys.Windows) && !IsWinPressed()) return false;

            int vk = KeyInterop.VirtualKeyFromKey(_key);
            if (vk <= 0) return false;

            return (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        private static bool IsKeyDown(int vk) => (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;
        private static bool IsCtrlPressed() => IsKeyDown(0x11) || IsKeyDown(0xA0) || IsKeyDown(0xA1);
        private static bool IsAltPressed() => IsKeyDown(0x12) || IsKeyDown(0xA4) || IsKeyDown(0xA5);
        private static bool IsShiftPressed() => IsKeyDown(0x10) || IsKeyDown(0xA2) || IsKeyDown(0xA3);
        private static bool IsWinPressed() => IsKeyDown(0x5B) || IsKeyDown(0x5C);

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == _hotkeyId)
            {
                TriggerHotkeyPressed();
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
