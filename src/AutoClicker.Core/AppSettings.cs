namespace AutoClicker.Core;

/// <summary>User-configurable clicking behavior. Persisted between runs.</summary>
public sealed class AppSettings
{
    /// <summary>Delay between clicks (ms) in CurrentCursor mode. 100ms ~= 10 CPS.</summary>
    public int IntervalMs { get; set; } = 100;

    /// <summary>Button used in CurrentCursor mode (sequence steps carry their own).</summary>
    public ClickButton Button { get; set; } = ClickButton.Left;

    /// <summary>Click type used in CurrentCursor mode.</summary>
    public ClickType ClickType { get; set; } = ClickType.Single;

    /// <summary>Auto-stop after this many clicks (counts every action). Null = run until stopped.</summary>
    public int? ClickLimit { get; set; }

    public bool JitterEnabled { get; set; }

    public TargetMode TargetMode { get; set; } = TargetMode.CurrentCursor;

    /// <summary>Ordered click positions used in Sequence mode.</summary>
    public List<SequenceStep> Sequence { get; set; } = new();

    public Hotkey StartStopHotkey { get; set; } = new(KeyId.F6);
    public Hotkey CaptureHotkey { get; set; } = new(KeyId.F8);

    /// <summary>Deep copy (sequence cloned) so the engine reads a stable snapshot.</summary>
    public AppSettings Clone()
    {
        var copy = (AppSettings)MemberwiseClone();
        copy.Sequence = Sequence.Select(step => step.Clone()).ToList();
        return copy; // Hotkey is an immutable record; safe to share
    }
}
