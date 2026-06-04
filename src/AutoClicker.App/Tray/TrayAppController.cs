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

    /// <summary>Raised (UI thread) when the engine running state changes.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Raised (UI thread) when settings change outside the window (e.g. capture).</summary>
    public event EventHandler? SettingsChangedExternally;

    /// <summary>Raised (UI thread) when the global hook can't start (permissions).</summary>
    public event EventHandler<string>? HookFailed;

    public bool IsRunning => _engine.IsRunning;

    public TrayAppController(ConfigStore configStore, IGlobalInputService globalInput, IInputSimulator inputSimulator)
    {
        _configStore = configStore;
        _globalInput = globalInput;

        Settings = _configStore.Load();
        _engine = new ClickEngine(inputSimulator, new SystemClock(), new ClickPlanner(new SystemRandomSource()), Settings.Clone());
        _engine.Stopped += (_, _) => Dispatcher.UIThread.Post(() => StateChanged?.Invoke(this, EventArgs.Empty));

        _globalInput.SetStartStopHotkey(Settings.StartStopHotkey);
        _globalInput.SetCaptureHotkey(Settings.CaptureHotkey);
        _globalInput.HotkeyPressed += (_, _) => Dispatcher.UIThread.Post(Toggle);
        _globalInput.CaptureRequested += (_, _) => Dispatcher.UIThread.Post(CaptureCurrentPosition);
    }

    public void Initialize()
    {
        try { _globalInput.Start(); }
        catch (Exception ex) { HookFailed?.Invoke(this, BuildPermissionMessage(ex)); }
    }

    public void Toggle()
    {
        if (_engine.IsRunning) _engine.Stop();
        else _engine.Start();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Capture hotkey: append the cursor position as a new step and switch to Sequence mode.</summary>
    private void CaptureCurrentPosition()
    {
        var (x, y) = _globalInput.GetCursorPosition();
        var updated = Settings.Clone();
        updated.TargetMode = TargetMode.Sequence;
        updated.Sequence.Add(new SequenceStep { X = x, Y = y });
        ApplySettings(updated);
        SettingsChangedExternally?.Invoke(this, EventArgs.Empty);
    }

    public void BeginRecordHotkey(Action<Hotkey> onRecorded) => _globalInput.BeginRecording(onRecorded);
    public void CancelRecording() => _globalInput.CancelRecording();

    /// <summary>Persist edited settings, push to the engine, and re-register both hotkeys.</summary>
    public void ApplySettings(AppSettings updated)
    {
        Settings = updated;
        _configStore.Save(updated);
        _engine.UpdateSettings(updated.Clone());
        _globalInput.SetStartStopHotkey(updated.StartStopHotkey);
        _globalInput.SetCaptureHotkey(updated.CaptureHotkey);
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
