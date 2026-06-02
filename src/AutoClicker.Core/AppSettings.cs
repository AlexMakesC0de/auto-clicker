namespace AutoClicker.Core;

/// <summary>User-configurable clicking behavior. Persisted between runs.</summary>
public sealed class AppSettings
{
    /// <summary>Delay between clicks in milliseconds (>= 1). 100ms ~= 10 CPS.</summary>
    public int IntervalMs { get; set; } = 100;

    public ClickButton Button { get; set; } = ClickButton.Left;

    public ClickType ClickType { get; set; } = ClickType.Single;

    /// <summary>Auto-stop after this many clicks. Null = run until stopped.</summary>
    public int? ClickLimit { get; set; }

    public bool JitterEnabled { get; set; }

    public TargetMode TargetMode { get; set; } = TargetMode.CurrentCursor;

    /// <summary>Used only when <see cref="TargetMode"/> is FixedCoordinate.</summary>
    public int FixedX { get; set; }
    public int FixedY { get; set; }

    public HotKey Hotkey { get; set; } = HotKey.F6;

    /// <summary>Returns a shallow copy so the engine can read a stable snapshot.</summary>
    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
