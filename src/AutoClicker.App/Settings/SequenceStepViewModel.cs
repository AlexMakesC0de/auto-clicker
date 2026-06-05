using System.Collections.Generic;
using AutoClicker.Core;

namespace AutoClicker.App.Settings;

/// <summary>Editable row for one sequence step.</summary>
public sealed class SequenceStepViewModel : ViewModelBase
{
    private int _x, _y, _delayMs;
    private ClickButton _button;
    private ClickType _clickType;

    public SequenceStepViewModel(SequenceStep step)
    {
        _x = step.X;
        _y = step.Y;
        _button = step.Button;
        _clickType = step.ClickType;
        _delayMs = step.DelayMs;
    }

    public IReadOnlyList<ClickButton> Buttons { get; } =
        new[] { ClickButton.Left, ClickButton.Middle, ClickButton.Right };
    public IReadOnlyList<ClickType> ClickTypes { get; } =
        new[] { ClickType.Single, ClickType.Double };

    public int X { get => _x; set => Set(ref _x, value); }
    public int Y { get => _y; set => Set(ref _y, value); }
    public ClickButton Button { get => _button; set => Set(ref _button, value); }
    public ClickType ClickType { get => _clickType; set => Set(ref _clickType, value); }
    public int DelayMs { get => _delayMs; set => Set(ref _delayMs, value); }

    public SequenceStep ToModel() => new()
    {
        X = X, Y = Y, Button = Button, ClickType = ClickType, DelayMs = DelayMs,
    };
}
