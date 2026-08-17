using Framework.Interop;

namespace Framework.Input;

public static class Keyboard
{
    private static readonly bool[] _currentKeys =
        new bool[(int)SDL3.SDL_Scancode.SDL_SCANCODE_COUNT];

    private static readonly bool[] _previousKeys =
        new bool[(int)SDL3.SDL_Scancode.SDL_SCANCODE_COUNT];

    internal static void Update()
    {
        Array.Copy(_currentKeys, _previousKeys, _currentKeys.Length);

        var keyboardState = SDL3.SDL_GetKeyboardState();

        for (var i = 0; i < keyboardState.Length; i++)
        {
            _currentKeys[i] = keyboardState[i];
        }
    }

    public static bool IsKeyDown(Key key)
    {
        var scancode = GetScancode(key);

        if (scancode == SDL3.SDL_Scancode.SDL_SCANCODE_UNKNOWN)
        {
            return false;
        }

        return _currentKeys[(int)scancode];
    }

    public static bool IsKeyPressed(Key key)
    {
        var scancode = GetScancode(key);

        if (scancode == SDL3.SDL_Scancode.SDL_SCANCODE_UNKNOWN)
        {
            return false;
        }

        var index = (int)scancode;

        return _currentKeys[index] && !_previousKeys[index];
    }

    public static bool IsKeyReleased(Key key)
    {
        var scancode = GetScancode(key);

        if (scancode == SDL3.SDL_Scancode.SDL_SCANCODE_UNKNOWN)
        {
            return false;
        }

        var index = (int)scancode;

        return !_currentKeys[index] && _previousKeys[index];
    }

    private static SDL3.SDL_Scancode GetScancode(Key key)
    {
        var keycode = GetKeycode(key);

        if (keycode == SDL3.SDL_Keycode.SDLK_UNKNOWN)
        {
            return SDL3.SDL_Scancode.SDL_SCANCODE_UNKNOWN;
        }

        return SDL3.SDL_GetScancodeFromKey((uint)keycode, IntPtr.Zero);
    }

    private static SDL3.SDL_Keycode GetKeycode(Key key)
    {
        return key switch
        {
            Key.A => SDL3.SDL_Keycode.SDLK_A,
            Key.B => SDL3.SDL_Keycode.SDLK_B,
            Key.C => SDL3.SDL_Keycode.SDLK_C,
            Key.D => SDL3.SDL_Keycode.SDLK_D,
            Key.E => SDL3.SDL_Keycode.SDLK_E,
            Key.F => SDL3.SDL_Keycode.SDLK_F,
            Key.G => SDL3.SDL_Keycode.SDLK_G,
            Key.H => SDL3.SDL_Keycode.SDLK_H,
            Key.I => SDL3.SDL_Keycode.SDLK_I,
            Key.J => SDL3.SDL_Keycode.SDLK_J,
            Key.K => SDL3.SDL_Keycode.SDLK_K,
            Key.L => SDL3.SDL_Keycode.SDLK_L,
            Key.M => SDL3.SDL_Keycode.SDLK_M,
            Key.N => SDL3.SDL_Keycode.SDLK_N,
            Key.O => SDL3.SDL_Keycode.SDLK_O,
            Key.P => SDL3.SDL_Keycode.SDLK_P,
            Key.Q => SDL3.SDL_Keycode.SDLK_Q,
            Key.R => SDL3.SDL_Keycode.SDLK_R,
            Key.S => SDL3.SDL_Keycode.SDLK_S,
            Key.T => SDL3.SDL_Keycode.SDLK_T,
            Key.U => SDL3.SDL_Keycode.SDLK_U,
            Key.V => SDL3.SDL_Keycode.SDLK_V,
            Key.W => SDL3.SDL_Keycode.SDLK_W,
            Key.X => SDL3.SDL_Keycode.SDLK_X,
            Key.Y => SDL3.SDL_Keycode.SDLK_Y,
            Key.Z => SDL3.SDL_Keycode.SDLK_Z,

            Key.Num0 => SDL3.SDL_Keycode.SDLK_0,
            Key.Num1 => SDL3.SDL_Keycode.SDLK_1,
            Key.Num2 => SDL3.SDL_Keycode.SDLK_2,
            Key.Num3 => SDL3.SDL_Keycode.SDLK_3,
            Key.Num4 => SDL3.SDL_Keycode.SDLK_4,
            Key.Num5 => SDL3.SDL_Keycode.SDLK_5,
            Key.Num6 => SDL3.SDL_Keycode.SDLK_6,
            Key.Num7 => SDL3.SDL_Keycode.SDLK_7,
            Key.Num8 => SDL3.SDL_Keycode.SDLK_8,
            Key.Num9 => SDL3.SDL_Keycode.SDLK_9,

            Key.Escape => SDL3.SDL_Keycode.SDLK_ESCAPE,
            Key.Space => SDL3.SDL_Keycode.SDLK_SPACE,
            Key.Enter => SDL3.SDL_Keycode.SDLK_RETURN,
            Key.Tab => SDL3.SDL_Keycode.SDLK_TAB,
            Key.Backspace => SDL3.SDL_Keycode.SDLK_BACKSPACE,

            Key.Up => SDL3.SDL_Keycode.SDLK_UP,
            Key.Down => SDL3.SDL_Keycode.SDLK_DOWN,
            Key.Left => SDL3.SDL_Keycode.SDLK_LEFT,
            Key.Right => SDL3.SDL_Keycode.SDLK_RIGHT,

            Key.LeftShift => SDL3.SDL_Keycode.SDLK_LSHIFT,
            Key.RightShift => SDL3.SDL_Keycode.SDLK_RSHIFT,
            Key.LeftControl => SDL3.SDL_Keycode.SDLK_LCTRL,
            Key.RightControl => SDL3.SDL_Keycode.SDLK_RCTRL,
            Key.LeftAlt => SDL3.SDL_Keycode.SDLK_LALT,
            Key.RightAlt => SDL3.SDL_Keycode.SDLK_RALT,

            _ => SDL3.SDL_Keycode.SDLK_UNKNOWN
        };
    }
}
