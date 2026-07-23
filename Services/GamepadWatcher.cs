using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Gaming.Input;

namespace ScreenshotApp.Services
{
    /// <summary>
    /// Monitors Xbox gamepad input in a background polling loop using XInput API (with Ordinal #100 Guide button support)
    /// and WinRT fallback.
    /// Dual-engine approach guarantees global background button detection (including Xbox Home/Guide button)
    /// regardless of WPF window focus.
    /// </summary>
    public class GamepadWatcher
    {
        public event EventHandler<GamepadButtons>? GamepadCombinationPressed;
        public event EventHandler<GamepadButtons>? GamepadCombinationRecorded;

        private Gamepad? _activeGamepad;
        private bool _isListening;
        private GamepadButtons _lastButtons = GamepadButtons.None;
        private bool _isRecordingMode;

        public bool IsRecordingMode
        {
            get => _isRecordingMode;
            set => _isRecordingMode = value;
        }

        public void Start()
        {
            if (_isListening) return;
            _isListening = true;

            Gamepad.GamepadAdded += OnGamepadAdded;
            Gamepad.GamepadRemoved += OnGamepadRemoved;

            if (Gamepad.Gamepads.Count > 0)
            {
                _activeGamepad = Gamepad.Gamepads[0];
            }

            Task.Run(PollGamepadLoop);
        }

        public void Stop()
        {
            _isListening = false;
            Gamepad.GamepadAdded -= OnGamepadAdded;
            Gamepad.GamepadRemoved -= OnGamepadRemoved;
            _activeGamepad = null;
        }

        private void OnGamepadAdded(object? sender, Gamepad e)
        {
            _activeGamepad ??= e;
        }

        private void OnGamepadRemoved(object? sender, Gamepad e)
        {
            if (_activeGamepad == e)
            {
                _activeGamepad = Gamepad.Gamepads.Count > 0 ? Gamepad.Gamepads[0] : null;
            }
        }

        private async Task PollGamepadLoop()
        {
            GamepadButtons recordingMaxCombo = GamepadButtons.None;
            bool waitingForRelease = false;

            while (_isListening)
            {
                _activeGamepad ??= Gamepad.Gamepads.Count > 0 ? Gamepad.Gamepads[0] : null;

                try
                {
                    // Primary: Read global XInput state with Ordinal #100 (supports Home/Guide button globally in background)
                    var currentButtons = XInputHelper.GetGlobalGamepadButtons();

                    // Secondary: Fallback to WinRT Gamepad reading
                    if (currentButtons == GamepadButtons.None && _activeGamepad != null)
                    {
                        try
                        {
                            currentButtons = _activeGamepad.GetCurrentReading().Buttons;
                        }
                        catch { }
                    }

                    if (_isRecordingMode)
                    {
                        if (!waitingForRelease)
                        {
                            // Phase 1: Accumulate pressed buttons into max combo
                            if (currentButtons != GamepadButtons.None)
                            {
                                recordingMaxCombo |= currentButtons;
                            }
                            else if (recordingMaxCombo != GamepadButtons.None)
                            {
                                // All buttons released with a valid combo captured → enter wait-for-release phase
                                waitingForRelease = true;
                            }
                        }
                        else
                        {
                            // Phase 2: Confirm all buttons are released before firing the recording event
                            if (currentButtons == GamepadButtons.None)
                            {
                                var recorded = recordingMaxCombo;
                                recordingMaxCombo = GamepadButtons.None;
                                waitingForRelease = false;

                                GamepadCombinationRecorded?.Invoke(this, recorded);
                                _isRecordingMode = false;
                                _lastButtons = GamepadButtons.None;
                            }
                        }
                    }
                    else
                    {
                        // Reset recording state when not in recording mode
                        recordingMaxCombo = GamepadButtons.None;
                        waitingForRelease = false;

                        // Trigger capture on newly pressed buttons (rising edge detection)
                        var newlyPressed = currentButtons & ~_lastButtons;
                        if (newlyPressed != GamepadButtons.None)
                        {
                            GamepadCombinationPressed?.Invoke(this, currentButtons);
                        }

                        _lastButtons = currentButtons;
                    }
                }
                catch
                {
                    _activeGamepad = null;
                }

                await Task.Delay(16); // ~60 Hz polling
            }
        }

        public static string FormatGamepadButtons(GamepadButtons buttons)
        {
            if (buttons == GamepadButtons.None) return "None";

            var list = new List<string>();
            foreach (GamepadButtons val in Enum.GetValues(typeof(GamepadButtons)))
            {
                if (val != GamepadButtons.None && (buttons & val) == val)
                {
                    if ((int)val == 1024)
                    {
                        list.Add("Home (Guide)");
                    }
                    else
                    {
                        list.Add(val.ToString());
                    }
                }
            }

            if (list.Count == 0 && ((int)buttons & 1024) != 0)
            {
                list.Add("Home (Guide)");
            }

            return list.Count > 0 ? string.Join(" + ", list) : buttons.ToString();
        }
    }

    /// <summary>
    /// Low-level XInput Win32 P/Invoke helper.
    /// Uses Ordinal #100 (XInputGetStateEx) to read the Xbox Guide/Home button (bit 0x0400) globally in background.
    /// </summary>
    internal static class XInputHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_GAMEPAD
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        // Ordinal #100 in xinput1_4.dll / xinput1_3.dll is XInputGetStateEx, which includes 0x0400 (Xbox Guide / Home button)
        [DllImport("xinput1_4.dll", EntryPoint = "#100")]
        private static extern uint XInputGetStateEx14(uint dwUserIndex, out XINPUT_STATE pState);

        [DllImport("xinput1_3.dll", EntryPoint = "#100")]
        private static extern uint XInputGetStateEx13(uint dwUserIndex, out XINPUT_STATE pState);

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState14(uint dwUserIndex, out XINPUT_STATE pState);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState910(uint dwUserIndex, out XINPUT_STATE pState);

        public static GamepadButtons GetGlobalGamepadButtons()
        {
            for (uint i = 0; i < 4; i++)
            {
                if (TryGetState(i, out var state) && state.Gamepad.wButtons != 0)
                {
                    return (GamepadButtons)state.Gamepad.wButtons;
                }
            }
            return GamepadButtons.None;
        }

        private static bool TryGetState(uint userIndex, out XINPUT_STATE state)
        {
            try
            {
                if (XInputGetStateEx14(userIndex, out state) == 0) return true;
            }
            catch { }

            try
            {
                if (XInputGetStateEx13(userIndex, out state) == 0) return true;
            }
            catch { }

            try
            {
                if (XInputGetState14(userIndex, out state) == 0) return true;
            }
            catch { }

            try
            {
                if (XInputGetState910(userIndex, out state) == 0) return true;
            }
            catch { }

            state = default;
            return false;
        }
    }
}
