using AutoClicker.Core;
using Avalonia.Threading;

namespace AutoClicker.App.Tray;

/// <summary>Composition root: owns engine + services + settings and coordinates the UI.</summary>
public sealed class TrayAppController : IDisposable
{
    private readonly ConfigStore _configStore;
    private readonly IGlobalInputService _globalInput;
    private readonly ClickEngine _engine;

    public AppSettings Settings { get; private set; }

    /// <summary>Raised (on the UI thread) when running state changes, so the tray menu can update.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Raised (on the UI thread) when the global hook can't start (permissions).</summary>
    public event EventHandler<string>? HookFailed;

    public bool IsRunning => _engine.IsRunning;

    public TrayAppController(ConfigStore configStore, IGlobalInputService globalInput, IInputSimulator inputSimulator)
    {
        _configStore = configStore;
        _globalInput = globalInput;

        Settings = _configStore.Load();
        _engine = new ClickEngine(inputSimulator, new SystemClock(), new ClickPlanner(new SystemRandomSource()), Settings.Clone());
        _engine.Stopped += (_, _) => Dispatcher.UIThread.Post(() => StateChanged?.Invoke(this, EventArgs.Empty));

        _globalInput.SetHotkey(Settings.Hotkey);
        _globalInput.HotkeyPressed += (_, _) => Dispatcher.UIThread.Post(Toggle);
    }

    public void Initialize()
    {
        try
        {
            _globalInput.Start();
        }
        catch (Exception ex)
        {
            HookFailed?.Invoke(this, BuildPermissionMessage(ex));
        }
    }

    public void Toggle()
    {
        if (_engine.IsRunning) _engine.Stop();
        else _engine.Start();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public (int X, int Y) GetCursorPosition() => _globalInput.GetCursorPosition();

    /// <summary>Applies edited settings: persist, push to engine, re-register hotkey.</summary>
    public void ApplySettings(AppSettings updated)
    {
        Settings = updated;
        _configStore.Save(updated);
        _engine.UpdateSettings(updated.Clone());
        _globalInput.SetHotkey(updated.Hotkey);
    }

    private static string BuildPermissionMessage(Exception ex) =>
        "Could not start the global input hook.\n\n" +
        "macOS: grant Accessibility AND Input Monitoring to this app in " +
        "System Settings > Privacy & Security, then restart it.\n" +
        "Linux: this requires an X11 session (Wayland is not supported).\n\n" +
        $"Details: {ex.Message}";

    public void Dispose()
    {
        _engine.Stop();
        _globalInput.Dispose();
    }
}
