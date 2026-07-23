using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Gaming.Input;

namespace ScreenshotApp.Services
{
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
                            if (currentButtons != GamepadButtons.None)
                            {
                                // Wait briefly to collect multi-button combinations (e.g. LB + RB)
                                GamepadButtons maxCombo = currentButtons;
                                for (int i = 0; i < 6; i++)
                                {
                                    await Task.Delay(20);
                                    var currentReading = _activeGamepad.GetCurrentReading();
                                    maxCombo |= currentReading.Buttons;
                                }

                                GamepadCombinationRecorded?.Invoke(this, maxCombo);
                                _isRecordingMode = false;
                                _lastButtons = maxCombo;
                            }
                        }
                        else
                        {
                            // Trigger when a new button combination press starts
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
                }

                await Task.Delay(16); // High frequency polling (~60Hz) for instant background response
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
