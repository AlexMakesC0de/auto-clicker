namespace AutoClicker.Core;

/// <summary>One engine tick: wait <see cref="DelayMs"/>, then click. Null X/Y = current cursor.</summary>
public readonly record struct ClickAction(int DelayMs, int? X, int? Y, ClickButton Button, ClickType Type);
