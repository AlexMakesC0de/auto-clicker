namespace AutoClicker.Core;

/// <summary>Pure, stateless computation of the next click action for either target mode.</summary>
public sealed class ClickPlanner
{
    private const double TimingJitterFraction = 0.15; // +/-15%
    private const int PositionJitterPixels = 3;        // +/-3px

    private readonly IRandomSource _random;

    public ClickPlanner(IRandomSource random) => _random = random;

    public int ComputeDelayMs(int baseMs, bool jitter)
    {
        int b = Math.Max(1, baseMs);
        if (!jitter) return b;
        double factor = 1.0 + (_random.NextDouble() * 2.0 - 1.0) * TimingJitterFraction;
        return Math.Max(1, (int)Math.Round(b * factor));
    }

    public (int X, int Y) JitterPosition(int x, int y) => (x + Offset(), y + Offset());

    private int Offset() => (int)Math.Round((_random.NextDouble() * 2.0 - 1.0) * PositionJitterPixels);

    /// <summary>Next action + the index to use on the following call. Null = nothing to do (empty sequence).</summary>
    public (ClickAction Action, int NextIndex)? Next(AppSettings s, int index)
    {
        if (s.TargetMode == TargetMode.CurrentCursor)
        {
            int delay = ComputeDelayMs(s.IntervalMs, s.JitterEnabled);
            return (new ClickAction(delay, null, null, s.Button, s.ClickType), 0);
        }

        var steps = s.Sequence;
        if (steps.Count == 0) return null;

        int i = ((index % steps.Count) + steps.Count) % steps.Count;
        var step = steps[i];
        int d = ComputeDelayMs(step.DelayMs, s.JitterEnabled);
        var (x, y) = s.JitterEnabled ? JitterPosition(step.X, step.Y) : (step.X, step.Y);
        return (new ClickAction(d, x, y, step.Button, step.ClickType), (i + 1) % steps.Count);
    }
}
