using AutoClicker.Core;
using SharpHook.Data;

namespace AutoClicker.App.Adapters;

internal static class SharpHookMappings
{
    // Single source of truth; both directions derive from this.
    private static readonly (KeyId Id, KeyCode Code)[] Pairs =
    {
        (KeyId.F1, KeyCode.VcF1), (KeyId.F2, KeyCode.VcF2), (KeyId.F3, KeyCode.VcF3),
        (KeyId.F4, KeyCode.VcF4), (KeyId.F5, KeyCode.VcF5), (KeyId.F6, KeyCode.VcF6),
        (KeyId.F7, KeyCode.VcF7), (KeyId.F8, KeyCode.VcF8), (KeyId.F9, KeyCode.VcF9),
        (KeyId.F10, KeyCode.VcF10), (KeyId.F11, KeyCode.VcF11), (KeyId.F12, KeyCode.VcF12),
        (KeyId.A, KeyCode.VcA), (KeyId.B, KeyCode.VcB), (KeyId.C, KeyCode.VcC), (KeyId.D, KeyCode.VcD),
        (KeyId.E, KeyCode.VcE), (KeyId.F, KeyCode.VcF), (KeyId.G, KeyCode.VcG), (KeyId.H, KeyCode.VcH),
        (KeyId.I, KeyCode.VcI), (KeyId.J, KeyCode.VcJ), (KeyId.K, KeyCode.VcK), (KeyId.L, KeyCode.VcL),
        (KeyId.M, KeyCode.VcM), (KeyId.N, KeyCode.VcN), (KeyId.O, KeyCode.VcO), (KeyId.P, KeyCode.VcP),
        (KeyId.Q, KeyCode.VcQ), (KeyId.R, KeyCode.VcR), (KeyId.S, KeyCode.VcS), (KeyId.T, KeyCode.VcT),
        (KeyId.U, KeyCode.VcU), (KeyId.V, KeyCode.VcV), (KeyId.W, KeyCode.VcW), (KeyId.X, KeyCode.VcX),
        (KeyId.Y, KeyCode.VcY), (KeyId.Z, KeyCode.VcZ),
        (KeyId.D0, KeyCode.Vc0), (KeyId.D1, KeyCode.Vc1), (KeyId.D2, KeyCode.Vc2), (KeyId.D3, KeyCode.Vc3),
        (KeyId.D4, KeyCode.Vc4), (KeyId.D5, KeyCode.Vc5), (KeyId.D6, KeyCode.Vc6), (KeyId.D7, KeyCode.Vc7),
        (KeyId.D8, KeyCode.Vc8), (KeyId.D9, KeyCode.Vc9),
        (KeyId.Backquote, KeyCode.VcBackQuote), (KeyId.Minus, KeyCode.VcMinus), (KeyId.Equals, KeyCode.VcEquals),
        (KeyId.LeftBracket, KeyCode.VcOpenBracket), (KeyId.RightBracket, KeyCode.VcCloseBracket),
        (KeyId.Semicolon, KeyCode.VcSemicolon), (KeyId.Quote, KeyCode.VcQuote), (KeyId.Comma, KeyCode.VcComma),
        (KeyId.Period, KeyCode.VcPeriod), (KeyId.Slash, KeyCode.VcSlash), (KeyId.Backslash, KeyCode.VcBackSlash),
        (KeyId.Space, KeyCode.VcSpace), (KeyId.Enter, KeyCode.VcEnter), (KeyId.Tab, KeyCode.VcTab),
        (KeyId.Escape, KeyCode.VcEscape), (KeyId.Insert, KeyCode.VcInsert), (KeyId.Delete, KeyCode.VcDelete),
        (KeyId.Home, KeyCode.VcHome), (KeyId.End, KeyCode.VcEnd),
        (KeyId.PageUp, KeyCode.VcPageUp), (KeyId.PageDown, KeyCode.VcPageDown),
        (KeyId.Up, KeyCode.VcUp), (KeyId.Down, KeyCode.VcDown), (KeyId.Left, KeyCode.VcLeft), (KeyId.Right, KeyCode.VcRight),
    };

    private static readonly Dictionary<KeyCode, KeyId> CodeToId = BuildCodeToId();

    private static Dictionary<KeyCode, KeyId> BuildCodeToId()
    {
        var d = new Dictionary<KeyCode, KeyId>();
        foreach (var (id, code) in Pairs) d[code] = id;
        return d;
    }

    /// <summary>Maps a raw key to our KeyId, or null if it is a key we do not support as a hotkey.</summary>
    public static KeyId? ToKeyId(KeyCode code) => CodeToId.TryGetValue(code, out var id) ? id : null;

    public static bool IsModifierKey(KeyCode code) => code is
        KeyCode.VcLeftControl or KeyCode.VcRightControl or
        KeyCode.VcLeftShift or KeyCode.VcRightShift or
        KeyCode.VcLeftAlt or KeyCode.VcRightAlt or
        KeyCode.VcLeftMeta or KeyCode.VcRightMeta;

    /// <summary>Builds a Hotkey from a base key and the event's modifier mask.</summary>
    public static Hotkey ToHotkey(KeyId key, EventMask mask) => new(
        key,
        Ctrl: mask.HasFlag(EventMask.LeftCtrl) || mask.HasFlag(EventMask.RightCtrl),
        Shift: mask.HasFlag(EventMask.LeftShift) || mask.HasFlag(EventMask.RightShift),
        Alt: mask.HasFlag(EventMask.LeftAlt) || mask.HasFlag(EventMask.RightAlt),
        Meta: mask.HasFlag(EventMask.LeftMeta) || mask.HasFlag(EventMask.RightMeta));

    public static MouseButton ToMouseButton(ClickButton button) => button switch
    {
        ClickButton.Left => MouseButton.Button1,
        ClickButton.Right => MouseButton.Button2,
        ClickButton.Middle => MouseButton.Button3,
        _ => MouseButton.Button1,
    };
}
