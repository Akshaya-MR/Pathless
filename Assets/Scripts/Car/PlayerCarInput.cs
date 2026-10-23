using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace TheRacer
{
    /// <summary>Reads keyboard and gamepad controls without requiring an Input Actions asset.</summary>
    public sealed class PlayerCarInput : MonoBehaviour, ICarInput
    {
        private float _throttle;
        private float _brake;
        private float _steer;
        private bool _handbrake;
        private bool _boost;
        private bool _resetPressed;
        private bool _pausePressed;
        private bool _cameraTogglePressed;
        private bool _devVisualCyclePressed;
        private bool _devTrackRebuildPressed;
        private bool _raceStartPressed;
        private bool _raceReleased = true;

        public float Throttle => _raceReleased ? _throttle : 0f;
        public float Brake => _raceReleased ? _brake : 0f;
        public float Steer => _raceReleased ? _steer : 0f;
        public bool Handbrake => _raceReleased && _handbrake;
        public bool Boost => _raceReleased && _boost;
        public bool ResetPressed => Consume(ref _resetPressed);
        public bool PausePressed => Consume(ref _pausePressed);
        public bool CameraTogglePressed => Consume(ref _cameraTogglePressed);
        /// <summary>Enter edge used to release the player and all rivals.</summary>
        public bool RaceStartPressed => Consume(ref _raceStartPressed);
        /// <summary>Dev-only F1 edge flag, consumed when read.</summary>
        public bool DevVisualCyclePressed => Consume(ref _devVisualCyclePressed);
        /// <summary>Dev-only F2 edge flag, consumed when read.</summary>
        public bool DevTrackRebuildPressed => Consume(ref _devTrackRebuildPressed);

        public void SetRaceReleased(bool released) { _raceReleased = released; }

        private void Update()
        {
            SampleInput();
        }

        private void SampleInput()
        {
            float keyboardThrottle = 0f;
            float keyboardBrake = 0f;
            float keyboardSteer = 0f;
            bool keyboardHandbrake = false;
            bool keyboardBoost = false;
            bool resetPressed = false;
            bool pausePressed = false;
            bool cameraPressed = false;
            bool devVisualCyclePressed = false;
            bool devTrackRebuildPressed = false;
            bool raceStartPressed = false;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                keyboardThrottle = IsPressed(keyboard.wKey, keyboard.upArrowKey) ? 1f : 0f;
                keyboardBrake = IsPressed(keyboard.sKey, keyboard.downArrowKey) ? 1f : 0f;
                keyboardSteer = (IsPressed(keyboard.dKey, keyboard.rightArrowKey) ? 1f : 0f)
                    - (IsPressed(keyboard.aKey, keyboard.leftArrowKey) ? 1f : 0f);
                keyboardHandbrake = keyboard.spaceKey.isPressed;
                keyboardBoost = keyboard.eKey.isPressed || keyboard.leftShiftKey.isPressed;
                resetPressed = keyboard.rKey.wasPressedThisFrame;
                pausePressed = keyboard.escapeKey.wasPressedThisFrame;
                cameraPressed = keyboard.cKey.wasPressedThisFrame;
                devVisualCyclePressed = keyboard.f1Key.wasPressedThisFrame;
                devTrackRebuildPressed = keyboard.f2Key.wasPressedThisFrame;
                raceStartPressed = keyboard.enterKey.wasPressedThisFrame;
            }

            Gamepad gamepad = Gamepad.current;
            float padThrottle = gamepad != null ? gamepad.rightTrigger.ReadValue() : 0f;
            float padBrake = gamepad != null ? gamepad.leftTrigger.ReadValue() : 0f;
            Vector2 stick = gamepad != null ? gamepad.leftStick.ReadValue() : Vector2.zero;
            _throttle = Mathf.Max(keyboardThrottle, padThrottle);
            _brake = Mathf.Max(keyboardBrake, padBrake);
            _steer = Mathf.Abs(stick.x) > Mathf.Abs(keyboardSteer) ? stick.x : keyboardSteer;
            _handbrake = keyboardHandbrake || (gamepad != null && gamepad.buttonSouth.isPressed);
            _boost = keyboardBoost || (gamepad != null && gamepad.buttonWest.isPressed);
            resetPressed |= gamepad != null && gamepad.leftStickButton.wasPressedThisFrame;
            pausePressed |= gamepad != null && gamepad.startButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            keyboardThrottle = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f;
            keyboardBrake = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f;
            keyboardSteer = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            keyboardHandbrake = Input.GetKey(KeyCode.Space);
            keyboardBoost = Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.LeftShift);
            resetPressed = Input.GetKeyDown(KeyCode.R);
            pausePressed = Input.GetKeyDown(KeyCode.Escape);
            cameraPressed = Input.GetKeyDown(KeyCode.C);
            devVisualCyclePressed = Input.GetKeyDown(KeyCode.F1);
            devTrackRebuildPressed = Input.GetKeyDown(KeyCode.F2);
            raceStartPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            float legacyVertical = Input.GetAxis(ThemeText.VerticalAxis);
            float legacyHorizontal = Input.GetAxis(ThemeText.HorizontalAxis);
            _throttle = Mathf.Max(keyboardThrottle, Mathf.Max(0f, legacyVertical));
            _brake = Mathf.Max(keyboardBrake, Mathf.Max(0f, -legacyVertical));
            _steer = Mathf.Abs(legacyHorizontal) > Mathf.Abs(keyboardSteer) ? legacyHorizontal : keyboardSteer;
            _handbrake = keyboardHandbrake || Input.GetKey(KeyCode.JoystickButton0);
            _boost = keyboardBoost || Input.GetKey(KeyCode.JoystickButton2);
            resetPressed |= Input.GetKeyDown(KeyCode.JoystickButton8);
            pausePressed |= Input.GetKeyDown(KeyCode.JoystickButton7);
#endif
            _resetPressed |= resetPressed;
            _pausePressed |= pausePressed;
            _cameraTogglePressed |= cameraPressed;
            _devVisualCyclePressed |= devVisualCyclePressed;
            _devTrackRebuildPressed |= devTrackRebuildPressed;
            _raceStartPressed |= raceStartPressed;
        }

#if ENABLE_INPUT_SYSTEM
        private static bool IsPressed(KeyControl first, KeyControl second)
        {
            return first.isPressed || second.isPressed;
        }
#endif

        private static bool Consume(ref bool flag)
        {
            bool pressed = flag;
            flag = false;
            return pressed;
        }
    }
}
