using System.Threading;
using System.Threading.Tasks;
using AutoClicker.Core;
using AutoClicker.Core.Tests.Fakes;
using Xunit;

namespace AutoClicker.Core.Tests;

public class ClickEngineTests
{
    private static ClickEngine NewEngine(
        AppSettings settings, FakeInputSimulator input, FakeClock clock) =>
        new(input, clock, new ClickPlanner(new FakeRandomSource(0.5)), settings);

    [Fact]
    public async Task RunAsync_WithClickLimit_StopsAtExactlyN()
    {
        var input = new FakeInputSimulator();
        var clock = new FakeClock(); // delays complete immediately
        var settings = new AppSettings { ClickLimit = 5, IntervalMs = 10 };
        var engine = NewEngine(settings, input, clock);

        await engine.RunAsync(CancellationToken.None);

        Assert.Equal(5, input.Clicks.Count);
        Assert.Equal(5, engine.ClicksPerformed);
        Assert.False(engine.IsRunning);
    }

    [Fact]
    public async Task RunAsync_UsesButtonTypeAndTargetFromSettings()
    {
        var input = new FakeInputSimulator();
        var clock = new FakeClock();
        var settings = new AppSettings
        {
            ClickLimit = 1,
            Button = ClickButton.Right,
            ClickType = ClickType.Double,
            TargetMode = TargetMode.FixedCoordinate,
            FixedX = 400,
            FixedY = 250,
        };
        var engine = NewEngine(settings, input, clock);

        await engine.RunAsync(CancellationToken.None);

        var click = Assert.Single(input.Clicks);
        Assert.Equal(400, click.X);
        Assert.Equal(250, click.Y);
        Assert.Equal(ClickButton.Right, click.Button);
        Assert.Equal(ClickType.Double, click.Type);
    }

    [Fact]
    public async Task RunAsync_CurrentCursorMode_ClicksWithNullCoordinates()
    {
        var input = new FakeInputSimulator();
        var settings = new AppSettings { ClickLimit = 1, TargetMode = TargetMode.CurrentCursor };
        var engine = NewEngine(settings, input, new FakeClock());

        await engine.RunAsync(CancellationToken.None);

        var click = Assert.Single(input.Clicks);
        Assert.Null(click.X);
        Assert.Null(click.Y);
    }

    [Fact]
    public async Task RunAsync_Cancellation_StopsLoopAndRaisesStopped()
    {
        var input = new FakeInputSimulator();
        // Park on the first delay until the loop is cancelled.
        var clock = new FakeClock { OnDelay = (_, ct) => Task.Delay(Timeout.Infinite, ct) };
        var settings = new AppSettings { ClickLimit = null }; // unlimited
        var engine = NewEngine(settings, input, clock);
        bool stoppedRaised = false;
        engine.Stopped += (_, _) => stoppedRaised = true;

        using var cts = new CancellationTokenSource();
        var task = engine.RunAsync(cts.Token);
        cts.Cancel();
        await task;

        Assert.False(engine.IsRunning);
        Assert.True(stoppedRaised);
        Assert.Empty(input.Clicks); // parked before any click
    }

    [Fact]
    public async Task StartThenStop_RunsAndStops()
    {
        var input = new FakeInputSimulator();
        var clock = new FakeClock { OnDelay = (_, ct) => Task.Delay(Timeout.Infinite, ct) };
        var settings = new AppSettings { ClickLimit = null };
        var engine = NewEngine(settings, input, clock);

        engine.Start();
        Assert.True(engine.IsRunning);

        engine.Stop();
        await engine.WaitForStopAsync();

        Assert.False(engine.IsRunning);
    }
}
