using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Gaming.Input;

namespace ScreenshotApp.Services
{
    /// <summary>
    /// Monitors Xbox gamepad input in a background polling loop.
    /// Supports combination hotkey recording and focus-aware capture triggering.
    ///
    /// Recording logic (on-release):
    ///   When IsRecordingMode is true, collects the maximum button combination
    ///   held during the press, then fires GamepadCombinationRecorded only AFTER
    ///   all buttons have been fully released (prevents accidental re-triggers).
    ///
    /// Capture trigger logic (focus-gated):
    ///   GamepadCombinationPressed is only fired when IsAppFocused returns true,
    ///   ensuring the hotkey does not fire when other apps have focus.
    /// </summary>
    public class GamepadWatcher
    {
        /// <summary>Fired when a matching combination is fully pressed (in non-recording mode).</summary>
        public event EventHandler<GamepadButtons>? GamepadCombinationPressed;

        /// <summary>Fired when a new hotkey combination has been recorded and all buttons released.</summary>
        public event EventHandler<GamepadButtons>? GamepadCombinationRecorded;

        /// <summary>Delegate used by MainViewModel to provide real-time focus state.</summary>
        public Func<bool>? IsAppFocused { get; set; }

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
                if (_activeGamepad != null)
                {
                    try
                    {
                        var reading = _activeGamepad.GetCurrentReading();
                        var currentButtons = reading.Buttons;

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
                                    // We need to confirm all buttons are truly released for one more tick
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
                                // Only trigger if our app has focus
                                bool appHasFocus = IsAppFocused?.Invoke() ?? false;
                                if (appHasFocus)
                                {
                                    GamepadCombinationPressed?.Invoke(this, currentButtons);
                                }
                            }

                            _lastButtons = currentButtons;
                        }
                    }
                    catch
                    {
                        _activeGamepad = null;
                    }
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
                    list.Add(val.ToString());
                }
            }
            return list.Count > 0 ? string.Join(" + ", list) : buttons.ToString();
        }
    }
}
