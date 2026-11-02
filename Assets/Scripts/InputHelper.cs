using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

/// <summary>
/// Reads the keyboard for the rest of the game.
/// Works with both Unity's old Input Manager and the newer Input System package,
/// so the project runs no matter which one is enabled in Player Settings.
/// </summary>
public static class InputHelper
{
    /// <summary>WASD movement as a vector. Diagonals are normalized so they aren't faster.</summary>
    public static Vector2 GetMoveInput()
    {
        Vector2 move = Vector2.zero;
        if (IsHeld(KeyCode.W)) move.y += 1f;
        if (IsHeld(KeyCode.S)) move.y -= 1f;
        if (IsHeld(KeyCode.D)) move.x += 1f;
        if (IsHeld(KeyCode.A)) move.x -= 1f;
        return move.sqrMagnitude > 1f ? move.normalized : move;
    }

    /// <summary>True while Space is held (flashlight on).</summary>
    public static bool FlashlightHeld => IsHeld(KeyCode.Space);

    /// <summary>True on the frame Enter is pressed (start / pause / restart).</summary>
    public static bool EnterPressed => WasPressed(KeyCode.Return) || WasPressed(KeyCode.KeypadEnter);

    static bool IsHeld(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        KeyControl control = ToControl(key);
        return control != null && control.isPressed;
#else
        return Input.GetKey(key);
#endif
    }

    static bool WasPressed(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        KeyControl control = ToControl(key);
        return control != null && control.wasPressedThisFrame;
#else
        return Input.GetKeyDown(key);
#endif
    }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
    // Maps the few keys this game uses onto the new Input System's keyboard controls.
    static KeyControl ToControl(KeyCode key)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return null;

        switch (key)
        {
            case KeyCode.W: return keyboard.wKey;
            case KeyCode.A: return keyboard.aKey;
            case KeyCode.S: return keyboard.sKey;
            case KeyCode.D: return keyboard.dKey;
            case KeyCode.Space: return keyboard.spaceKey;
            case KeyCode.Return: return keyboard.enterKey;
            case KeyCode.KeypadEnter: return keyboard.numpadEnterKey;
            default: return null;
        }
    }
#endif
}
