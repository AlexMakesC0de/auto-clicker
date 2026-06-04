namespace AutoClicker.Core;

public enum ClickButton { Left, Middle, Right }

public enum ClickType { Single, Double }

public enum TargetMode { CurrentCursor, Sequence }

/// <summary>Platform-neutral keys that can be used in a hotkey. D0–D9 must stay contiguous.</summary>
public enum KeyId
{
    None = 0,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    Backquote, Minus, Equals, LeftBracket, RightBracket, Semicolon, Quote, Comma, Period, Slash, Backslash,
    Space, Enter, Tab, Escape, Insert, Delete, Home, End, PageUp, PageDown,
    Up, Down, Left, Right
}
