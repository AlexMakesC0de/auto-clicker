namespace AutoClicker.Core;

/// <summary>Global hook: raises an event when the configured hotkey is pressed,
/// and reports the current cursor position (for "capture position").</summary>
public interface IGlobalInputService : IDisposable
{
    /// <summary>Raised on the global hook thread when the registered hotkey is pressed.</summary>
    event EventHandler? HotkeyPressed;

    /// <summary>Begins listening. Throws if the OS denied input access (permissions).</summary>
    void Start();

    /// <summary>Sets/changes which key toggles clicking. Safe to call after Start.</summary>
    void SetHotkey(HotKey hotkey);

    /// <summary>Last known cursor position from the global hook (0,0 until the mouse moves).</summary>
    (int X, int Y) GetCursorPosition();
}
