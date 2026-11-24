using UnityEngine;

namespace GeneratorTrainer.Multiplayer
{
    // Keyboard and mouse reading for the desktop player, in one place.
    // Works with the new Input System and with the legacy Input Manager (the project has both enabled),
    // the same way the existing GeneratorTraining / TrainerOrbit scripts do it.
    public static class DesktopInput
    {
        // WASD (or arrow keys): x = strafe, y = forward. Not normalised.
        public static Vector2 Move()
        {
            Vector2 v = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k != null)
            {
                if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
            return v;
        }

        // Mouse movement this frame, roughly in screen pixels.
        public static Vector2 Look()
        {
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null ? m.delta.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
#else
            return Vector2.zero;
#endif
        }

        // Mouse position in GUI coordinates (pixels, origin top-left), for checking clicks on on-screen buttons.
        public static Vector2 PointerGuiPosition()
        {
            Vector2 p = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m != null) p = m.position.ReadValue();
#elif ENABLE_LEGACY_INPUT_MANAGER
            p = Input.mousePosition;
#endif
            return new Vector2(p.x, Screen.height - p.y);
        }

        public static bool Fast
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = UnityEngine.InputSystem.Keyboard.current;
                return k != null && k.leftShiftKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKey(KeyCode.LeftShift);
#else
                return false;
#endif
            }
        }

        public static bool EscapePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = UnityEngine.InputSystem.Keyboard.current;
                return k != null && k.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.Escape);
#else
                return false;
#endif
            }
        }

        public static bool ClickDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var m = UnityEngine.InputSystem.Mouse.current;
                return m != null && m.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetMouseButtonDown(0);
#else
                return false;
#endif
            }
        }

        public static bool ClickHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var m = UnityEngine.InputSystem.Mouse.current;
                return m != null && m.leftButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetMouseButton(0);
#else
                return false;
#endif
            }
        }
    }
}
