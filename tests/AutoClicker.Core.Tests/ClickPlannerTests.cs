using AutoClicker.Core;
using AutoClicker.Core.Tests.Fakes;
using Xunit;

namespace AutoClicker.Core.Tests;

public class ClickPlannerTests
{
    [Theory]
    [InlineData(0.0, 85)]
    [InlineData(0.5, 100)]
    [InlineData(1.0, 115)]
    public void ComputeDelayMs_Jitter_ScalesWithinFifteenPercent(double rand, int expected)
    {
        var planner = new ClickPlanner(new FakeRandomSource(rand));
        Assert.Equal(expected, planner.ComputeDelayMs(100, jitter: true));
    }

    [Fact]
    public void ComputeDelayMs_NoJitter_ClampsToAtLeastOne()
    {
        var planner = new ClickPlanner(new FakeRandomSource(0.5));
        Assert.Equal(1, planner.ComputeDelayMs(0, jitter: false));
        Assert.Equal(100, planner.ComputeDelayMs(100, jitter: false));
    }

    [Fact]
    public void Next_CurrentCursor_ReturnsGlobalActionWithNullCoords()
    {
        var planner = new ClickPlanner(new FakeRandomSource(0.5));
        var s = new AppSettings { TargetMode = TargetMode.CurrentCursor, IntervalMs = 50,
            Button = ClickButton.Right, ClickType = ClickType.Double };

        var result = planner.Next(s, index: 0);

        Assert.NotNull(result);
        var (action, nextIndex) = result!.Value;
        Assert.Equal(50, action.DelayMs);
        Assert.Null(action.X);
        Assert.Null(action.Y);
        Assert.Equal(ClickButton.Right, action.Button);
        Assert.Equal(ClickType.Double, action.Type);
        Assert.Equal(0, nextIndex);
    }

    [Fact]
    public void Next_SequenceEmpty_ReturnsNull()
    {
        var planner = new ClickPlanner(new FakeRandomSource(0.5));
        var s = new AppSettings { TargetMode = TargetMode.Sequence };

        Assert.Null(planner.Next(s, index: 0));
    }

    [Fact]
    public void Next_Sequence_CyclesStepsWithTheirOwnActionAndDelay()
    {
        var planner = new ClickPlanner(new FakeRandomSource(0.5));
        var s = new AppSettings { TargetMode = TargetMode.Sequence };
        s.Sequence.Add(new SequenceStep { X = 10, Y = 20, Button = ClickButton.Left,  ClickType = ClickType.Single, DelayMs = 200 });
        s.Sequence.Add(new SequenceStep { X = 30, Y = 40, Button = ClickButton.Right, ClickType = ClickType.Double, DelayMs = 300 });

        var first = planner.Next(s, 0)!.Value;
        Assert.Equal(new ClickAction(200, 10, 20, ClickButton.Left, ClickType.Single), first.Action);
        Assert.Equal(1, first.NextIndex);

        var second = planner.Next(s, first.NextIndex)!.Value;
        Assert.Equal(new ClickAction(300, 30, 40, ClickButton.Right, ClickType.Double), second.Action);
        Assert.Equal(0, second.NextIndex); // wraps
    }

    [Fact]
    public void Next_Sequence_WithJitter_PerturbsDelayThenPosition()
    {
        // order of NextDouble calls: delay (1.0 => +15%), x (1.0 => +3), y (0.0 => -3)
        var planner = new ClickPlanner(new FakeRandomSource(1.0, 1.0, 0.0));
        var s = new AppSettings { TargetMode = TargetMode.Sequence, JitterEnabled = true };
        s.Sequence.Add(new SequenceStep { X = 100, Y = 100, DelayMs = 100 });

        var action = planner.Next(s, 0)!.Value.Action;

        Assert.Equal(115, action.DelayMs);
        Assert.Equal(103, action.X);
        Assert.Equal(97, action.Y);
    }
}
