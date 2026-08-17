namespace Framework.Input;

/// <summary>
/// Identifies the logical keyboard keys currently exposed by the framework.
/// </summary>
public enum Key
{
    Unknown,

    // Alphabetic keys.
    A,
    B,
    C,
    D,
    E,
    F,
    G,
    H,
    I,
    J,
    K,
    L,
    M,
    N,
    O,
    P,
    Q,
    R,
    S,
    T,
    U,
    V,
    W,
    X,
    Y,
    Z,

    // Number-row keys; these do not represent numeric keypad keys.
    Num0,
    Num1,
    Num2,
    Num3,
    Num4,
    Num5,
    Num6,
    Num7,
    Num8,
    Num9,

    // Common control and editing keys.
    Escape,
    Space,
    Enter,
    Tab,
    Backspace,

    // Directional navigation keys.
    Up,
    Down,
    Left,
    Right,

    // Side-specific modifier keys.
    LeftShift,
    RightShift,
    LeftControl,
    RightControl,
    LeftAlt,
    RightAlt
}
