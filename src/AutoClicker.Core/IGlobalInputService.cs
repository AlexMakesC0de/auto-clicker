namespace AutoClicker.Core;

/// <summary>Global hook: detects the two hotkeys, reports the cursor position, and records combos.</summary>
public interface IGlobalInputService : IDisposable
{
    /// <summary>Raised on the hook thread when the start/stop hotkey is pressed.</summary>
    event EventHandler? HotkeyPressed;

    /// <summary>Raised on the hook thread when the capture hotkey is pressed.</summary>
    event EventHandler? CaptureRequested;

    void Start();

    void SetStartStopHotkey(Hotkey hotkey);
    void SetCaptureHotkey(Hotkey hotkey);

    /// <summary>Capture the next pressed combo into <paramref name="onRecorded"/>. Esc cancels.</summary>
    void BeginRecording(Action<Hotkey> onRecorded);
    void CancelRecording();

    /// <summary>Last known cursor position from the global hook (0,0 until the mouse moves).</summary>
    (int X, int Y) GetCursorPosition();
}
