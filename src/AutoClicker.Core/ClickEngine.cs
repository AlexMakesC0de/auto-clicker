namespace AutoClicker.Core;

/// <summary>Runs the click loop on a background task until stopped or the click limit is reached.</summary>
public sealed class ClickEngine
{
    private readonly IInputSimulator _input;
    private readonly IClock _clock;
    private readonly ClickPlanner _planner;
    private readonly object _gate = new();

    private volatile AppSettings _settings;
    private CancellationTokenSource? _cts;
    private Task _loop = Task.CompletedTask;
    private int _sequenceIndex;

    public ClickEngine(IInputSimulator input, IClock clock, ClickPlanner planner, AppSettings settings)
    {
        _input = input;
        _clock = clock;
        _planner = planner;
        _settings = settings;
    }

    public bool IsRunning { get; private set; }
    public int ClicksPerformed { get; private set; }

    /// <summary>Raised when the loop ends (limit reached, stopped, or cancelled).</summary>
    public event EventHandler? Stopped;

    /// <summary>Swap in new settings; picked up on the next loop iteration.</summary>
    public void UpdateSettings(AppSettings settings) => _settings = settings;

    public void Start()
    {
        lock (_gate)
        {
            if (IsRunning) return;
            _cts = new CancellationTokenSource();
            _loop = RunAsync(_cts.Token);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _cts?.Cancel();
        }
    }

    /// <summary>Awaits the currently running loop (no-op if not running). Useful for shutdown/tests.</summary>
    public Task WaitForStopAsync() => _loop;

    /// <summary>The click loop. Public so it can be awaited directly in tests with a fake clock.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        IsRunning = true;
        ClicksPerformed = 0;
        _sequenceIndex = 0;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var s = _settings;
                if (s.ClickLimit is int limit && ClicksPerformed >= limit) break;

                var next = _planner.Next(s, _sequenceIndex);
                if (next is null) break; // empty sequence

                var (action, nextIndex) = next.Value;
                await _clock.DelayAsync(action.DelayMs, cancellationToken).ConfigureAwait(false);
                _input.Click(action.X, action.Y, action.Button, action.Type);
                ClicksPerformed++;
                _sequenceIndex = nextIndex;
            }
        }
        catch (OperationCanceledException)
        {
            // normal stop path
        }
        finally
        {
            IsRunning = false;
            Stopped?.Invoke(this, EventArgs.Empty);
        }
    }
}
