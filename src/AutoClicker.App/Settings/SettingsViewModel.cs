using AutoClicker.App.Tray;
using AutoClicker.Core;

namespace AutoClicker.App.Settings;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly TrayAppController _controller;

    private int _intervalMs;
    private ClickButton _button;
    private ClickType _clickType;
    private bool _hasClickLimit;
    private int _clickLimit;
    private bool _jitterEnabled;
    private TargetMode _targetMode;
    private int _fixedX;
    private int _fixedY;
    private HotKey _hotkey;
    private string _status = "";

    public SettingsViewModel(TrayAppController controller)
    {
        _controller = controller;
        var s = controller.Settings;
        _intervalMs = s.IntervalMs;
        _button = s.Button;
        _clickType = s.ClickType;
        _hasClickLimit = s.ClickLimit is not null;
        _clickLimit = s.ClickLimit ?? 100;
        _jitterEnabled = s.JitterEnabled;
        _targetMode = s.TargetMode;
        _fixedX = s.FixedX;
        _fixedY = s.FixedY;
        _hotkey = s.Hotkey;
    }

    public IReadOnlyList<ClickButton> Buttons { get; } =
        new[] { ClickButton.Left, ClickButton.Middle, ClickButton.Right };
    public IReadOnlyList<ClickType> ClickTypes { get; } =
        new[] { ClickType.Single, ClickType.Double };
    public IReadOnlyList<TargetMode> TargetModes { get; } =
        new[] { TargetMode.CurrentCursor, TargetMode.FixedCoordinate };
    public IReadOnlyList<HotKey> Hotkeys { get; } = (HotKey[])Enum.GetValues(typeof(HotKey));

    public int IntervalMs { get => _intervalMs; set => Set(ref _intervalMs, value); }
    public ClickButton Button { get => _button; set => Set(ref _button, value); }
    public ClickType ClickType { get => _clickType; set => Set(ref _clickType, value); }
    public bool HasClickLimit { get => _hasClickLimit; set => Set(ref _hasClickLimit, value); }
    public int ClickLimit { get => _clickLimit; set => Set(ref _clickLimit, value); }
    public bool JitterEnabled { get => _jitterEnabled; set => Set(ref _jitterEnabled, value); }
    public TargetMode TargetMode { get => _targetMode; set => Set(ref _targetMode, value); }
    public int FixedX { get => _fixedX; set => Set(ref _fixedX, value); }
    public int FixedY { get => _fixedY; set => Set(ref _fixedY, value); }
    public HotKey Hotkey { get => _hotkey; set => Set(ref _hotkey, value); }
    public string Status { get => _status; set => Set(ref _status, value); }

    public void CapturePosition()
    {
        var (x, y) = _controller.GetCursorPosition();
        FixedX = x;
        FixedY = y;
        Status = $"Captured ({x}, {y}).";
    }

    public void Save()
    {
        var updated = new AppSettings
        {
            IntervalMs = Math.Max(1, IntervalMs),
            Button = Button,
            ClickType = ClickType,
            ClickLimit = HasClickLimit ? Math.Max(1, ClickLimit) : null,
            JitterEnabled = JitterEnabled,
            TargetMode = TargetMode,
            FixedX = FixedX,
            FixedY = FixedY,
            Hotkey = Hotkey,
        };
        _controller.ApplySettings(updated);
        Status = "Saved.";
    }
}
