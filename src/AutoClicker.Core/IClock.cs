namespace AutoClicker.Core;

/// <summary>Abstraction over waiting so the click loop can be driven deterministically in tests.</summary>
public interface IClock
{
    Task DelayAsync(int milliseconds, CancellationToken cancellationToken);
}

public sealed class SystemClock : IClock
{
    public Task DelayAsync(int milliseconds, CancellationToken cancellationToken) =>
        Task.Delay(milliseconds, cancellationToken);
}
