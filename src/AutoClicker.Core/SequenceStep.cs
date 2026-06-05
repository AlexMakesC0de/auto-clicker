namespace AutoClicker.Core;

/// <summary>One position in a click sequence, with its own action and the delay before its click.</summary>
public sealed class SequenceStep
{
    public int X { get; set; }
    public int Y { get; set; }
    public ClickButton Button { get; set; } = ClickButton.Left;
    public ClickType ClickType { get; set; } = ClickType.Single;

    /// <summary>Wait (ms) before this position's click. In a loop this is the inter-action delay.</summary>
    public int DelayMs { get; set; } = 100;

    public SequenceStep Clone() => (SequenceStep)MemberwiseClone();
}
