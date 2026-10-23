using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TheRacer
{
    /// <summary>Toggles obstacle-free test mode with the backslash key.</summary>
    public sealed class TestModeController : MonoBehaviour
    {
        public static bool IsActive { get; private set; }
        public static event System.Action<bool> Changed;

        private void Update()
        {
            bool pressed = false;
#if ENABLE_INPUT_SYSTEM
            pressed = Keyboard.current != null && Keyboard.current.backslashKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            pressed = Input.GetKeyDown(KeyCode.Backslash);
#endif
            if (pressed) SetActive(!IsActive);
        }

        public static void SetActive(bool active)
        { if (IsActive == active) return; IsActive = active; Changed?.Invoke(active); }

    }
}
