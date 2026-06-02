using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoClicker.Core;

namespace AutoClicker.Core.Tests.Fakes;

/// <summary>
/// Records requested delays. By default each delay completes immediately,
/// so a click loop with a limit runs deterministically with no real waiting.
/// Set <see cref="OnDelay"/> to override (e.g. park forever until cancelled).
/// </summary>
public sealed class FakeClock : IClock
{
    public List<int> RequestedDelays { get; } = new();
    public Func<int, CancellationToken, Task>? OnDelay { get; set; }

    public Task DelayAsync(int milliseconds, CancellationToken cancellationToken)
    {
        RequestedDelays.Add(milliseconds);
        return OnDelay is not null
            ? OnDelay(milliseconds, cancellationToken)
            : Task.CompletedTask;
    }
}
