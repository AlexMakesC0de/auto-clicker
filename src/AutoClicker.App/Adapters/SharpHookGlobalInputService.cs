using AutoClicker.Core;
using SharpHook;
using SharpHook.Data;

namespace AutoClicker.App.Adapters;

/// <summary>Global hook via SharpHook: detects the hotkey and tracks the cursor position.</summary>
public sealed class SharpHookGlobalInputService : IGlobalInputService
{
    private readonly TaskPoolGlobalHook _hook = new();
    private KeyCode _hotkey = KeyCode.VcF6;
    private int _cursorX;
    private int _cursorY;

    public event EventHandler? HotkeyPressed;

    public SharpHookGlobalInputService()
    {
        _hook.KeyPressed += OnKeyPressed;
        _hook.MouseMoved += OnMouseMoved;
        _hook.MouseDragged += OnMouseMoved;
    }

    public void Start()
    {
        // Throws if the OS denied access (e.g. macOS permissions not granted).
        _hook.RunAsync();
    }

    public void SetHotkey(HotKey hotkey) => _hotkey = SharpHookMappings.ToKeyCode(hotkey);

    public (int X, int Y) GetCursorPosition() => (_cursorX, _cursorY);

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (e.Data.KeyCode == _hotkey)
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
    }

    private void OnMouseMoved(object? sender, MouseHookEventArgs e)
    {
        _cursorX = e.Data.X;
        _cursorY = e.Data.Y;
    }

    public void Dispose() => _hook.Dispose();
}
