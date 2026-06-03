using System.Collections.Generic;
using AutoClicker.Core;

namespace AutoClicker.Core.Tests.Fakes;

public sealed class FakeInputSimulator : IInputSimulator
{
    public readonly record struct Recorded(int? X, int? Y, ClickButton Button, ClickType Type);

    public List<Recorded> Clicks { get; } = new();

    public void Click(int? x, int? y, ClickButton button, ClickType type) =>
        Clicks.Add(new Recorded(x, y, button, type));
}
