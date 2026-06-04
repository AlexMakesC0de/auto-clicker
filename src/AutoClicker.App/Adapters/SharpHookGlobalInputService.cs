using AutoClicker.Core;
using SharpHook;
using SharpHook.Data;

namespace AutoClicker.App.Adapters;

/// <summary>Global hook via SharpHook: matches the two hotkeys, tracks the cursor, records combos.</summary>
public sealed class SharpHookGlobalInputService : IGlobalInputService
{
    private readonly TaskPoolGlobalHook _hook = new();
    private readonly HashSet<KeyCode> _held = new();

    private Hotkey _startStop = new(KeyId.F6);
    private Hotkey _capture = new(KeyId.F8);
    private Action<Hotkey>? _recordCallback;

    private int _cursorX;
    private int _cursorY;

    public event EventHandler? HotkeyPressed;
    public event EventHandler? CaptureRequested;

    public SharpHookGlobalInputService()
    {
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
        _hook.MouseMoved += OnMouseMoved;
        _hook.MouseDragged += OnMouseMoved;
    }

    public void Start() => _hook.RunAsync();

    public void SetStartStopHotkey(Hotkey hotkey) => _startStop = hotkey;
    public void SetCaptureHotkey(Hotkey hotkey) => _capture = hotkey;

    public void BeginRecording(Action<Hotkey> onRecorded) => _recordCallback = onRecorded;
    public void CancelRecording() => _recordCallback = null;

    public (int X, int Y) GetCursorPosition() => (_cursorX, _cursorY);

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        KeyCode code = e.Data.KeyCode;
        if (SharpHookMappings.IsModifierKey(code)) return; // wait for a real base key

        // Auto-repeat guard: ignore until the key is released.
        if (!_held.Add(code)) return;

        KeyId? id = SharpHookMappings.ToKeyId(code);
        if (id is null) return; // unsupported key

        var pressed = SharpHookMappings.ToHotkey(id.Value, e.RawEvent.Mask);

        var record = _recordCallback;
        if (record is not null)
        {
            _recordCallback = null;
            if (pressed.Key != KeyId.Escape) // Esc cancels recording
                record(pressed);
            return;
        }

        if (pressed == _startStop) HotkeyPressed?.Invoke(this, EventArgs.Empty);
        else if (pressed == _capture) CaptureRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e) => _held.Remove(e.Data.KeyCode);

    private void OnMouseMoved(object? sender, MouseHookEventArgs e)
    {
        _cursorX = e.Data.X;
        _cursorY = e.Data.Y;
    }

    public void Dispose() => _hook.Dispose();
}
