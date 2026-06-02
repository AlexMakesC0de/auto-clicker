using AutoClicker.Core;
using AutoClicker.Core.Tests.Fakes;
using Xunit;

namespace AutoClicker.Core.Tests;

public class ClickPlannerTests
{
    [Fact]
    public void Delay_NoJitter_EqualsInterval()
    {
        var planner = new ClickPlanner(new FakeRandomSource(0.5));
        var s = new AppSettings { IntervalMs = 100, JitterEnabled = false };

        Assert.Equal(100, planner.ComputeDelayMs(s));
    }

    [Fact]
    public void Delay_NoJitter_ClampedToAtLeastOne()
    {
        var planner = new ClickPlanner(new FakeRandomSource(0.5));
        var s = new AppSettings { IntervalMs = 0, JitterEnabled = false };

        Assert.Equal(1, planner.ComputeDelayMs(s));
    }

    [Theory]
    [InlineData(0.0, 85)]   // -15%
    [InlineData(0.5, 100)]  //  0%
    [InlineData(1.0, 115)]  // +15%
    public void Delay_Jitter_ScalesWithinFifteenPercent(double rand, int expected)
    {
        var planner = new ClickPlanner(new FakeRandomSource(rand));
        var s = new AppSettings { IntervalMs = 100, JitterEnabled = true };

        Assert.Equal(expected, planner.ComputeDelayMs(s));
    }

    [Fact]
    public void Target_CurrentCursor_ReturnsNullCoordinates()
    {
        var planner = new ClickPlanner(new FakeRandomSource(0.5));
        var s = new AppSettings { TargetMode = TargetMode.CurrentCursor };

        var (x, y) = planner.ComputeTarget(s);

        Assert.Null(x);
        Assert.Null(y);
    }

    [Fact]
    public void Target_FixedNoJitter_ReturnsExactCoordinates()
    {
        var planner = new ClickPlanner(new FakeRandomSource(0.5));
        var s = new AppSettings
        {
            TargetMode = TargetMode.FixedCoordinate, FixedX = 400, FixedY = 250, JitterEnabled = false
        };

        var (x, y) = planner.ComputeTarget(s);

        Assert.Equal(400, x);
        Assert.Equal(250, y);
    }

    [Fact]
    public void Target_FixedWithJitter_StaysWithinThreePixels()
    {
        // NextDouble called twice (x then y); 1.0 => +3px, 0.0 => -3px
        var planner = new ClickPlanner(new FakeRandomSource(1.0, 0.0));
        var s = new AppSettings
        {
            TargetMode = TargetMode.FixedCoordinate, FixedX = 400, FixedY = 250, JitterEnabled = true
        };

        var (x, y) = planner.ComputeTarget(s);

        Assert.Equal(403, x);
        Assert.Equal(247, y);
    }
}
