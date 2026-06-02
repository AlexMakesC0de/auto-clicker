namespace AutoClicker.Core;

/// <summary>Abstraction over randomness so jitter is deterministic in tests.</summary>
public interface IRandomSource
{
    /// <summary>Returns a value in the range [0.0, 1.0).</summary>
    double NextDouble();
}

public sealed class SystemRandomSource : IRandomSource
{
    private readonly Random _random = new();
    public double NextDouble() => _random.NextDouble();
}
