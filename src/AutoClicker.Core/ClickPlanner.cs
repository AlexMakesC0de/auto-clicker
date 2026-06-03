namespace AutoClicker.Core;

/// <summary>Pure computation of per-click delay and target position, including jitter.</summary>
public sealed class ClickPlanner
{
    private const double TimingJitterFraction = 0.15; // +/-15%
    private const int PositionJitterPixels = 3;        // +/-3px

    private readonly IRandomSource _random;

    public ClickPlanner(IRandomSource random) => _random = random;

    public int ComputeDelayMs(AppSettings s)
    {
        int baseMs = Math.Max(1, s.IntervalMs);
        if (!s.JitterEnabled) return baseMs;

        // map [0,1) -> [-1,1], then scale
        double factor = 1.0 + (_random.NextDouble() * 2.0 - 1.0) * TimingJitterFraction;
        return Math.Max(1, (int)Math.Round(baseMs * factor));
    }

    public (int? X, int? Y) ComputeTarget(AppSettings s)
    {
        if (s.TargetMode == TargetMode.CurrentCursor) return (null, null);
        if (!s.JitterEnabled) return (s.FixedX, s.FixedY);

        int dx = Offset();
        int dy = Offset();
        return (s.FixedX + dx, s.FixedY + dy);
    }

    private int Offset() =>
        (int)Math.Round((_random.NextDouble() * 2.0 - 1.0) * PositionJitterPixels);
}
