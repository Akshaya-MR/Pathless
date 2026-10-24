#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TheRacer
{
    /// <summary>Single input boundary for main-menu navigation.</summary>
    public static class MenuInput
    {
        public static bool Left()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (keyboard != null && (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame))
                || (pad != null && pad.dpad.left.wasPressedThisFrame);
#else
            return false;
#endif
        }

        public static bool Right()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (keyboard != null && (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame))
                || (pad != null && pad.dpad.right.wasPressedThisFrame);
#else
            return false;
#endif
        }

        public static bool Back()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return false;
#endif
        }
    }
}
