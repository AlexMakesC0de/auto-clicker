# Auto Clicker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a cross-platform (Windows/macOS/Linux-X11) system-tray auto clicker with configurable rate, button, click type, click limit, jitter, target position, and a global start/stop hotkey.

**Architecture:** A pure, OS-free `AutoClicker.Core` library (settings, click-planning logic, click engine, config persistence) sits behind interfaces (`IInputSimulator`, `IGlobalInputService`, `IClock`, `IRandomSource`). An `AutoClicker.App` Avalonia project provides the tray UI, settings window, and SharpHook-backed adapters that implement those interfaces. Core is fully unit-tested with fakes; the App layer is verified by a documented per-OS manual smoke test.

**Tech Stack:** C# / .NET 8 · Avalonia UI 11 (tray + settings) · SharpHook 6 (global hotkey + synthetic clicks, wraps libuiohook) · System.Text.Json (config) · xUnit (tests) · MsBox.Avalonia (error dialogs).

---

## File Structure

```
auto-clicker/
  AutoClicker.sln
  .gitignore
  src/
    AutoClicker.Core/
      AutoClicker.Core.csproj
      Enums.cs                     # ClickButton, ClickType, TargetMode, HotKey
      AppSettings.cs               # settings model + defaults
      IRandomSource.cs             # NextDouble() abstraction (+ SystemRandomSource)
      IClock.cs                    # DelayAsync abstraction (+ SystemClock)
      IInputSimulator.cs           # Click(x?,y?,button,type)  -- output
      IGlobalInputService.cs       # hotkey events + GetCursorPosition  -- input
      ClickPlanner.cs              # pure delay/target computation (jitter)
      ClickEngine.cs               # async click loop, limit, Start/Stop, Stopped event
      ConfigStore.cs               # load/save JSON to user config dir
    AutoClicker.App/
      AutoClicker.App.csproj
      Program.cs                   # entry point + BuildAvaloniaApp
      App.axaml / App.axaml.cs     # TrayIcon, composition root
      Adapters/
        SharpHookInputSimulator.cs   # IInputSimulator via SharpHook EventSimulator
        SharpHookGlobalInputService.cs # IGlobalInputService via SharpHook global hook
        SharpHookMappings.cs         # ClickButton/HotKey -> SharpHook enums
      Tray/
        TrayAppController.cs       # owns engine+services; toggle/open/quit; tray label
      Settings/
        ViewModelBase.cs           # minimal INotifyPropertyChanged base
        SettingsViewModel.cs       # editable settings + validation + save
        SettingsWindow.axaml / .cs # the settings UI
      Assets/
        app-icon.ico               # tray icon (Windows .ico; reused cross-platform)
  tests/
    AutoClicker.Core.Tests/
      AutoClicker.Core.Tests.csproj
      Fakes/
        FakeInputSimulator.cs
        FakeClock.cs
        FakeRandomSource.cs
      ClickPlannerTests.cs
      ClickEngineTests.cs
      ConfigStoreTests.cs
      AppSettingsTests.cs
  docs/superpowers/
    specs/2026-06-03-auto-clicker-design.md
    plans/2026-06-03-auto-clicker.md
  README.md
```

---

## Task 0: Solution & project scaffolding

**Files:**
- Create: `AutoClicker.sln`, `.gitignore`
- Create: `src/AutoClicker.Core/AutoClicker.Core.csproj`
- Create: `src/AutoClicker.App/AutoClicker.App.csproj`
- Create: `tests/AutoClicker.Core.Tests/AutoClicker.Core.Tests.csproj`

- [ ] **Step 1: Verify toolchain and install Avalonia templates**

Run:
```
dotnet --version
dotnet new install Avalonia.Templates
```
Expected: `dotnet --version` prints `8.x` (if not, install the .NET 8 SDK first). The template install prints `Success` (or "already installed").

- [ ] **Step 2: Create the solution and three projects**

Run (from repo root):
```
dotnet new sln -n AutoClicker
dotnet new classlib -o src/AutoClicker.Core -f net8.0
dotnet new avalonia.app -o src/AutoClicker.App
dotnet new xunit -o tests/AutoClicker.Core.Tests -f net8.0
dotnet sln add src/AutoClicker.Core src/AutoClicker.App tests/AutoClicker.Core.Tests
```
Expected: four "successfully created/added" messages. Delete the default `Class1.cs` in Core if present (`AutoClicker.Core/Class1.cs`).

- [ ] **Step 3: Wire project references and packages**

Run:
```
dotnet add tests/AutoClicker.Core.Tests reference src/AutoClicker.Core
dotnet add src/AutoClicker.App reference src/AutoClicker.Core
dotnet add src/AutoClicker.App package SharpHook
dotnet add src/AutoClicker.App package MsBox.Avalonia
```
Expected: package/reference add succeeds. (SharpHook resolves to 6.x, which exposes enums under `SharpHook.Data`. If NuGet resolves 5.x, the enum namespace is `SharpHook.Native` — adjust `using`s in Task 6 accordingly.)

- [ ] **Step 4: Set Core target framework and nullable**

Replace `src/AutoClicker.Core/AutoClicker.Core.csproj` with:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

- [ ] **Step 5: Add a .NET .gitignore**

Create `.gitignore`:
```
bin/
obj/
*.user
.vs/
[Pp]ublish/
```

- [ ] **Step 6: Build to confirm scaffolding compiles**

Run: `dotnet build`
Expected: `Build succeeded` with 0 errors (warnings about the default Avalonia template are fine).

- [ ] **Step 7: Commit**

```
git add .
git commit -m "chore: scaffold Core, App (Avalonia), and Core.Tests projects"
```

---

## Task 1: Core enums and AppSettings model

**Files:**
- Create: `src/AutoClicker.Core/Enums.cs`
- Create: `src/AutoClicker.Core/AppSettings.cs`
- Test: `tests/AutoClicker.Core.Tests/AppSettingsTests.cs`

- [ ] **Step 1: Write the failing test for defaults**

Create `tests/AutoClicker.Core.Tests/AppSettingsTests.cs`:
```csharp
using AutoClicker.Core;
using Xunit;

namespace AutoClicker.Core.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_MatchSpec()
    {
        var s = new AppSettings();

        Assert.Equal(100, s.IntervalMs);
        Assert.Equal(ClickButton.Left, s.Button);
        Assert.Equal(ClickType.Single, s.ClickType);
        Assert.Null(s.ClickLimit);
        Assert.False(s.JitterEnabled);
        Assert.Equal(TargetMode.CurrentCursor, s.TargetMode);
        Assert.Equal(0, s.FixedX);
        Assert.Equal(0, s.FixedY);
        Assert.Equal(HotKey.F6, s.Hotkey);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AutoClicker.Core.Tests --filter Defaults_MatchSpec`
Expected: FAIL — `AppSettings`/enums do not exist (compile error).

- [ ] **Step 3: Create the enums**

Create `src/AutoClicker.Core/Enums.cs`:
```csharp
namespace AutoClicker.Core;

public enum ClickButton { Left, Middle, Right }

public enum ClickType { Single, Double }

public enum TargetMode { CurrentCursor, FixedCoordinate }

/// <summary>Function keys offered as the global start/stop hotkey.</summary>
public enum HotKey { F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12 }
```

- [ ] **Step 4: Create AppSettings with defaults**

Create `src/AutoClicker.Core/AppSettings.cs`:
```csharp
namespace AutoClicker.Core;

/// <summary>User-configurable clicking behavior. Persisted between runs.</summary>
public sealed class AppSettings
{
    /// <summary>Delay between clicks in milliseconds (>= 1). 100ms ~= 10 CPS.</summary>
    public int IntervalMs { get; set; } = 100;

    public ClickButton Button { get; set; } = ClickButton.Left;

    public ClickType ClickType { get; set; } = ClickType.Single;

    /// <summary>Auto-stop after this many clicks. Null = run until stopped.</summary>
    public int? ClickLimit { get; set; }

    public bool JitterEnabled { get; set; }

    public TargetMode TargetMode { get; set; } = TargetMode.CurrentCursor;

    /// <summary>Used only when <see cref="TargetMode"/> is FixedCoordinate.</summary>
    public int FixedX { get; set; }
    public int FixedY { get; set; }

    public HotKey Hotkey { get; set; } = HotKey.F6;

    /// <summary>Returns a shallow copy so the engine can read a stable snapshot.</summary>
    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/AutoClicker.Core.Tests --filter Defaults_MatchSpec`
Expected: PASS.

- [ ] **Step 6: Commit**

```
git add src/AutoClicker.Core/Enums.cs src/AutoClicker.Core/AppSettings.cs tests/AutoClicker.Core.Tests/AppSettingsTests.cs
git commit -m "feat(core): add settings model and click enums"
```

---

## Task 2: ClickPlanner (pure delay/target logic with jitter)

**Files:**
- Create: `src/AutoClicker.Core/IRandomSource.cs`
- Create: `src/AutoClicker.Core/ClickPlanner.cs`
- Create: `tests/AutoClicker.Core.Tests/Fakes/FakeRandomSource.cs`
- Test: `tests/AutoClicker.Core.Tests/ClickPlannerTests.cs`

`ClickPlanner` is pure: given settings + a random source it computes the per-click delay and target. Jitter is ±15% on timing and ±3px on a fixed target. `IRandomSource.NextDouble()` returns a value in `[0,1)`; tests feed exact values so results are deterministic.

- [ ] **Step 1: Create IRandomSource + SystemRandomSource**

Create `src/AutoClicker.Core/IRandomSource.cs`:
```csharp
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
```

- [ ] **Step 2: Create the test fake**

Create `tests/AutoClicker.Core.Tests/Fakes/FakeRandomSource.cs`:
```csharp
using System.Collections.Generic;
using AutoClicker.Core;

namespace AutoClicker.Core.Tests.Fakes;

/// <summary>Returns queued values in order; repeats the last value once exhausted.</summary>
public sealed class FakeRandomSource : IRandomSource
{
    private readonly Queue<double> _values;
    private double _last;

    public FakeRandomSource(params double[] values)
    {
        _values = new Queue<double>(values);
        _last = values.Length > 0 ? values[^1] : 0.0;
    }

    public double NextDouble()
    {
        if (_values.Count > 0) _last = _values.Dequeue();
        return _last;
    }
}
```

- [ ] **Step 3: Write the failing tests**

Create `tests/AutoClicker.Core.Tests/ClickPlannerTests.cs`:
```csharp
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
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test tests/AutoClicker.Core.Tests --filter ClickPlannerTests`
Expected: FAIL — `ClickPlanner` does not exist (compile error).

- [ ] **Step 5: Implement ClickPlanner**

Create `src/AutoClicker.Core/ClickPlanner.cs`:
```csharp
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
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/AutoClicker.Core.Tests --filter ClickPlannerTests`
Expected: PASS (all 8 cases).

- [ ] **Step 7: Commit**

```
git add src/AutoClicker.Core/IRandomSource.cs src/AutoClicker.Core/ClickPlanner.cs tests/AutoClicker.Core.Tests/Fakes/FakeRandomSource.cs tests/AutoClicker.Core.Tests/ClickPlannerTests.cs
git commit -m "feat(core): add ClickPlanner with deterministic jitter"
```

---

## Task 3: Core interfaces (IClock, IInputSimulator, IGlobalInputService) + fakes

**Files:**
- Create: `src/AutoClicker.Core/IClock.cs`
- Create: `src/AutoClicker.Core/IInputSimulator.cs`
- Create: `src/AutoClicker.Core/IGlobalInputService.cs`
- Create: `tests/AutoClicker.Core.Tests/Fakes/FakeClock.cs`
- Create: `tests/AutoClicker.Core.Tests/Fakes/FakeInputSimulator.cs`

These are the seams between pure Core logic and OS-specific adapters. This task only defines interfaces + test fakes (no behavior to unit-test on its own); the next task exercises them through `ClickEngine`.

- [ ] **Step 1: Create IClock + SystemClock**

Create `src/AutoClicker.Core/IClock.cs`:
```csharp
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
```

- [ ] **Step 2: Create IInputSimulator (output)**

Create `src/AutoClicker.Core/IInputSimulator.cs`:
```csharp
namespace AutoClicker.Core;

/// <summary>Injects synthetic mouse clicks. Null coordinates mean "at the current cursor position".</summary>
public interface IInputSimulator
{
    void Click(int? x, int? y, ClickButton button, ClickType type);
}
```

- [ ] **Step 3: Create IGlobalInputService (input)**

Create `src/AutoClicker.Core/IGlobalInputService.cs`:
```csharp
namespace AutoClicker.Core;

/// <summary>Global hook: raises an event when the configured hotkey is pressed,
/// and reports the current cursor position (for "capture position").</summary>
public interface IGlobalInputService : IDisposable
{
    /// <summary>Raised on the global hook thread when the registered hotkey is pressed.</summary>
    event EventHandler? HotkeyPressed;

    /// <summary>Begins listening. Throws if the OS denied input access (permissions).</summary>
    void Start();

    /// <summary>Sets/changes which key toggles clicking. Safe to call after Start.</summary>
    void SetHotkey(HotKey hotkey);

    /// <summary>Last known cursor position from the global hook (0,0 until the mouse moves).</summary>
    (int X, int Y) GetCursorPosition();
}
```

- [ ] **Step 4: Create FakeClock**

Create `tests/AutoClicker.Core.Tests/Fakes/FakeClock.cs`:
```csharp
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
```

- [ ] **Step 5: Create FakeInputSimulator**

Create `tests/AutoClicker.Core.Tests/Fakes/FakeInputSimulator.cs`:
```csharp
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
```

- [ ] **Step 6: Build to confirm interfaces compile**

Run: `dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 7: Commit**

```
git add src/AutoClicker.Core/IClock.cs src/AutoClicker.Core/IInputSimulator.cs src/AutoClicker.Core/IGlobalInputService.cs tests/AutoClicker.Core.Tests/Fakes/FakeClock.cs tests/AutoClicker.Core.Tests/Fakes/FakeInputSimulator.cs
git commit -m "feat(core): add clock/input/global-input interfaces and test fakes"
```

---

## Task 4: ClickEngine (async click loop)

**Files:**
- Create: `src/AutoClicker.Core/ClickEngine.cs`
- Test: `tests/AutoClicker.Core.Tests/ClickEngineTests.cs`

`ClickEngine` owns the loop. `RunAsync(ct)` is public so tests can await it directly with a `FakeClock` — no real time passes. `Start()`/`Stop()` wrap it with an internal `CancellationTokenSource` for the app. A double-click counts as **one** click toward the limit (one `Click` call per iteration).

- [ ] **Step 1: Write the failing tests**

Create `tests/AutoClicker.Core.Tests/ClickEngineTests.cs`:
```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AutoClicker.Core.Tests --filter ClickEngineTests`
Expected: FAIL — `ClickEngine` does not exist (compile error).

- [ ] **Step 3: Implement ClickEngine**

Create `src/AutoClicker.Core/ClickEngine.cs`:
```csharp
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
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var s = _settings;
                if (s.ClickLimit is int limit && ClicksPerformed >= limit) break;

                await _clock.DelayAsync(_planner.ComputeDelayMs(s), cancellationToken)
                    .ConfigureAwait(false);

                var (x, y) = _planner.ComputeTarget(s);
                _input.Click(x, y, s.Button, s.ClickType);
                ClicksPerformed++;
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/AutoClicker.Core.Tests --filter ClickEngineTests`
Expected: PASS (all 5 cases).

- [ ] **Step 5: Run the full Core suite**

Run: `dotnet test tests/AutoClicker.Core.Tests`
Expected: PASS (all tests so far).

- [ ] **Step 6: Commit**

```
git add src/AutoClicker.Core/ClickEngine.cs tests/AutoClicker.Core.Tests/ClickEngineTests.cs
git commit -m "feat(core): add ClickEngine with limit, cancellation, and Stopped event"
```

---

## Task 5: ConfigStore (JSON persistence)

**Files:**
- Create: `src/AutoClicker.Core/ConfigStore.cs`
- Test: `tests/AutoClicker.Core.Tests/ConfigStoreTests.cs`

`ConfigStore` takes an explicit file path (testable). A static `DefaultPath()` computes the per-OS location via `SpecialFolder.ApplicationData` (Windows `%APPDATA%`; macOS/Linux `~/.config`). Missing or corrupt files fall back to defaults.

- [ ] **Step 1: Write the failing tests**

Create `tests/AutoClicker.Core.Tests/ConfigStoreTests.cs`:
```csharp
using System;
using System.IO;
using AutoClicker.Core;
using Xunit;

namespace AutoClicker.Core.Tests;

public class ConfigStoreTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "autoclicker-tests-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_dir, "config.json");

    [Fact]
    public void SaveThenLoad_RoundTripsAllFields()
    {
        var store = new ConfigStore(FilePath);
        var saved = new AppSettings
        {
            IntervalMs = 42,
            Button = ClickButton.Middle,
            ClickType = ClickType.Double,
            ClickLimit = 7,
            JitterEnabled = true,
            TargetMode = TargetMode.FixedCoordinate,
            FixedX = 11,
            FixedY = 22,
            Hotkey = HotKey.F9,
        };

        store.Save(saved);
        var loaded = store.Load();

        Assert.Equal(42, loaded.IntervalMs);
        Assert.Equal(ClickButton.Middle, loaded.Button);
        Assert.Equal(ClickType.Double, loaded.ClickType);
        Assert.Equal(7, loaded.ClickLimit);
        Assert.True(loaded.JitterEnabled);
        Assert.Equal(TargetMode.FixedCoordinate, loaded.TargetMode);
        Assert.Equal(11, loaded.FixedX);
        Assert.Equal(22, loaded.FixedY);
        Assert.Equal(HotKey.F9, loaded.Hotkey);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var store = new ConfigStore(FilePath);

        var loaded = store.Load();

        Assert.Equal(new AppSettings().IntervalMs, loaded.IntervalMs);
        Assert.Equal(HotKey.F6, loaded.Hotkey);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ this is not valid json");
        var store = new ConfigStore(FilePath);

        var loaded = store.Load();

        Assert.Equal(new AppSettings().IntervalMs, loaded.IntervalMs);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AutoClicker.Core.Tests --filter ConfigStoreTests`
Expected: FAIL — `ConfigStore` does not exist (compile error).

- [ ] **Step 3: Implement ConfigStore**

Create `src/AutoClicker.Core/ConfigStore.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoClicker.Core;

/// <summary>Loads/saves <see cref="AppSettings"/> as JSON at a given file path.</summary>
public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    public ConfigStore(string filePath) => _filePath = filePath;

    /// <summary>Per-OS config file path: %APPDATA%\AutoClicker\config.json (Windows),
    /// ~/.config/AutoClicker/config.json (macOS/Linux).</summary>
    public static string DefaultPath()
    {
        string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(baseDir, "AutoClicker", "config.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new AppSettings();
            string json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        string? dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, Options));
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/AutoClicker.Core.Tests --filter ConfigStoreTests`
Expected: PASS (all 3 cases).

- [ ] **Step 5: Commit**

```
git add src/AutoClicker.Core/ConfigStore.cs tests/AutoClicker.Core.Tests/ConfigStoreTests.cs
git commit -m "feat(core): add ConfigStore JSON persistence with safe fallbacks"
```

---

## Task 6: SharpHook adapters (real input + global hook)

**Files:**
- Create: `src/AutoClicker.App/Adapters/SharpHookMappings.cs`
- Create: `src/AutoClicker.App/Adapters/SharpHookInputSimulator.cs`
- Create: `src/AutoClicker.App/Adapters/SharpHookGlobalInputService.cs`

OS glue — not unit-tested (validated by the Task 9 smoke test). Uses SharpHook 6 (`SharpHook.Data` namespace). `MouseButton.Button1`=left, `Button2`=right, `Button3`=middle. A null coordinate clicks at the current position; otherwise the coordinate overload is used.

- [ ] **Step 1: Create the enum mappings**

Create `src/AutoClicker.App/Adapters/SharpHookMappings.cs`:
```csharp
using AutoClicker.Core;
using SharpHook.Data;

namespace AutoClicker.App.Adapters;

internal static class SharpHookMappings
{
    public static MouseButton ToMouseButton(ClickButton button) => button switch
    {
        ClickButton.Left => MouseButton.Button1,
        ClickButton.Right => MouseButton.Button2,
        ClickButton.Middle => MouseButton.Button3,
        _ => MouseButton.Button1,
    };

    public static KeyCode ToKeyCode(HotKey hotkey) => hotkey switch
    {
        HotKey.F1 => KeyCode.VcF1,
        HotKey.F2 => KeyCode.VcF2,
        HotKey.F3 => KeyCode.VcF3,
        HotKey.F4 => KeyCode.VcF4,
        HotKey.F5 => KeyCode.VcF5,
        HotKey.F6 => KeyCode.VcF6,
        HotKey.F7 => KeyCode.VcF7,
        HotKey.F8 => KeyCode.VcF8,
        HotKey.F9 => KeyCode.VcF9,
        HotKey.F10 => KeyCode.VcF10,
        HotKey.F11 => KeyCode.VcF11,
        HotKey.F12 => KeyCode.VcF12,
        _ => KeyCode.VcF6,
    };
}
```

- [ ] **Step 2: Implement SharpHookInputSimulator**

Create `src/AutoClicker.App/Adapters/SharpHookInputSimulator.cs`:
```csharp
using AutoClicker.Core;
using SharpHook;
using SharpHook.Data;

namespace AutoClicker.App.Adapters;

/// <summary>Real mouse-click injection via SharpHook's EventSimulator.</summary>
public sealed class SharpHookInputSimulator : IInputSimulator
{
    private readonly EventSimulator _simulator = new();

    public void Click(int? x, int? y, ClickButton button, ClickType type)
    {
        MouseButton mb = SharpHookMappings.ToMouseButton(button);
        int repeats = type == ClickType.Double ? 2 : 1;

        for (int i = 0; i < repeats; i++)
        {
            if (x is null || y is null)
            {
                _simulator.SimulateMousePress(mb);
                _simulator.SimulateMouseRelease(mb);
            }
            else
            {
                _simulator.SimulateMousePress((short)x.Value, (short)y.Value, mb);
                _simulator.SimulateMouseRelease((short)x.Value, (short)y.Value, mb);
            }
        }
    }
}
```

- [ ] **Step 3: Implement SharpHookGlobalInputService**

Create `src/AutoClicker.App/Adapters/SharpHookGlobalInputService.cs`:
```csharp
using AutoClicker.Core;
using SharpHook;
using SharpHook.Data;

namespace AutoClicker.App.Adapters;

/// <summary>Global hook via SharpHook: detects the hotkey and tracks the cursor position.</summary>
public sealed class SharpHookGlobalInputService : IGlobalInputService
{
    private readonly TaskPoolGlobalHook _hook = new();
    private KeyCode _hotkey = KeyCode.VcF6;
    private int _cursorX;
    private int _cursorY;

    public event EventHandler? HotkeyPressed;

    public SharpHookGlobalInputService()
    {
        _hook.KeyPressed += OnKeyPressed;
        _hook.MouseMoved += OnMouseMoved;
        _hook.MouseDragged += OnMouseMoved;
    }

    public void Start()
    {
        // Throws HookException if the OS denied access (e.g. macOS permissions not granted).
        _hook.RunAsync();
    }

    public void SetHotkey(HotKey hotkey) => _hotkey = SharpHookMappings.ToKeyCode(hotkey);

    public (int X, int Y) GetCursorPosition() => (_cursorX, _cursorY);

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (e.Data.KeyCode == _hotkey)
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
    }

    private void OnMouseMoved(object? sender, MouseHookEventArgs e)
    {
        _cursorX = e.Data.X;
        _cursorY = e.Data.Y;
    }

    public void Dispose() => _hook.Dispose();
}
```

- [ ] **Step 4: Build to confirm the adapters compile against the installed SharpHook**

Run: `dotnet build src/AutoClicker.App`
Expected: `Build succeeded`. If you get "namespace SharpHook.Data not found", the resolved SharpHook is v5.x — change `using SharpHook.Data;` to `using SharpHook.Native;` in the three files above and rebuild.

- [ ] **Step 5: Commit**

```
git add src/AutoClicker.App/Adapters/
git commit -m "feat(app): add SharpHook adapters for clicks and global hotkey"
```

---

## Task 7: Avalonia app shell, tray icon, and composition root

**Files:**
- Modify/Create: `src/AutoClicker.App/Program.cs`
- Modify/Create: `src/AutoClicker.App/App.axaml`
- Modify/Create: `src/AutoClicker.App/App.axaml.cs`
- Create: `src/AutoClicker.App/Tray/TrayAppController.cs`
- Create: `src/AutoClicker.App/Assets/app-icon.ico`

The app starts with **no main window** (`ShutdownMode.OnExplicitShutdown`), shows a tray icon, and toggles clicking from the hotkey or the tray menu. `TrayAppController` is the composition root that owns the engine, services, and settings.

- [ ] **Step 1: Add a tray icon asset**

Place any small `.ico` at `src/AutoClicker.App/Assets/app-icon.ico` (16x16 or 32x32). If you don't have one, generate a solid placeholder:
```
dotnet tool install -g dotnet-script   # only if you want to script it; otherwise just drop in any .ico
```
Simplest: download/copy any `.ico` into that path. It will be referenced as an Avalonia resource. (On macOS/Linux a `.ico` is accepted by Avalonia's `WindowIcon`/`TrayIcon`.)

- [ ] **Step 2: Write TrayAppController**

Create `src/AutoClicker.App/Tray/TrayAppController.cs`:
```csharp
using System;
using AutoClicker.Core;
using Avalonia.Threading;

namespace AutoClicker.App.Tray;

/// <summary>Composition root: owns engine + services + settings and coordinates the UI.</summary>
public sealed class TrayAppController : IDisposable
{
    private readonly ConfigStore _configStore;
    private readonly IGlobalInputService _globalInput;
    private readonly ClickEngine _engine;

    public AppSettings Settings { get; private set; }

    /// <summary>Raised (on the UI thread) when running state changes, so the tray menu can update.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Raised (on the UI thread) when the global hook can't start (permissions).</summary>
    public event EventHandler<string>? HookFailed;

    public bool IsRunning => _engine.IsRunning;

    public TrayAppController(ConfigStore configStore, IGlobalInputService globalInput, IInputSimulator inputSimulator)
    {
        _configStore = configStore;
        _globalInput = globalInput;

        Settings = _configStore.Load();
        _engine = new ClickEngine(inputSimulator, new SystemClock(), new ClickPlanner(new SystemRandomSource()), Settings.Clone());
        _engine.Stopped += (_, _) => Dispatcher.UIThread.Post(() => StateChanged?.Invoke(this, EventArgs.Empty));

        _globalInput.SetHotkey(Settings.Hotkey);
        _globalInput.HotkeyPressed += (_, _) => Dispatcher.UIThread.Post(Toggle);
    }

    public void Initialize()
    {
        try
        {
            _globalInput.Start();
        }
        catch (Exception ex)
        {
            HookFailed?.Invoke(this, BuildPermissionMessage(ex));
        }
    }

    public void Toggle()
    {
        if (_engine.IsRunning) _engine.Stop();
        else _engine.Start();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public (int X, int Y) GetCursorPosition() => _globalInput.GetCursorPosition();

    /// <summary>Applies edited settings: persist, push to engine, re-register hotkey.</summary>
    public void ApplySettings(AppSettings updated)
    {
        Settings = updated;
        _configStore.Save(updated);
        _engine.UpdateSettings(updated.Clone());
        _globalInput.SetHotkey(updated.Hotkey);
    }

    private static string BuildPermissionMessage(Exception ex) =>
        "Could not start the global input hook.\n\n" +
        "macOS: grant Accessibility AND Input Monitoring to this app in " +
        "System Settings > Privacy & Security, then restart it.\n" +
        "Linux: this requires an X11 session (Wayland is not supported).\n\n" +
        $"Details: {ex.Message}";

    public void Dispose()
    {
        _engine.Stop();
        _globalInput.Dispose();
    }
}
```

- [ ] **Step 3: Write App.axaml with the tray icon**

Replace `src/AutoClicker.App/App.axaml`:
```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="AutoClicker.App.App">
  <Application.Styles>
    <FluentTheme />
  </Application.Styles>

  <TrayIcon.Icons>
    <TrayIcons>
      <TrayIcon x:Name="AppTrayIcon"
                Icon="/Assets/app-icon.ico"
                ToolTipText="Auto Clicker">
        <TrayIcon.Menu>
          <NativeMenu>
            <NativeMenuItem x:Name="ToggleMenuItem" Header="Start" Click="OnToggleClick" />
            <NativeMenuItem Header="Settings…" Click="OnSettingsClick" />
            <NativeMenuItemSeparator />
            <NativeMenuItem Header="Quit" Click="OnQuitClick" />
          </NativeMenu>
        </TrayIcon.Menu>
      </TrayIcon>
    </TrayIcons>
  </TrayIcon.Icons>
</Application>
```

- [ ] **Step 4: Write App.axaml.cs (composition + menu handlers)**

Replace `src/AutoClicker.App/App.axaml.cs`:
```csharp
using AutoClicker.App.Adapters;
using AutoClicker.App.Settings;
using AutoClicker.App.Tray;
using AutoClicker.Core;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace AutoClicker.App;

public partial class App : Application
{
    private TrayAppController? _controller;
    private NativeMenuItem? _toggleItem;
    private SettingsWindow? _settingsWindow;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _controller = new TrayAppController(
                new ConfigStore(ConfigStore.DefaultPath()),
                new SharpHookGlobalInputService(),
                new SharpHookInputSimulator());

            _toggleItem = this.FindNameScope()?.Find<NativeMenuItem>("ToggleMenuItem");
            _controller.StateChanged += (_, _) => UpdateToggleLabel();
            _controller.HookFailed += async (_, message) =>
                await MessageBoxManager
                    .GetMessageBoxStandard("Auto Clicker", message, ButtonEnum.Ok, Icon.Warning)
                    .ShowAsync();

            _controller.Initialize();
            UpdateToggleLabel();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void UpdateToggleLabel()
    {
        if (_toggleItem is not null && _controller is not null)
            _toggleItem.Header = _controller.IsRunning ? "Stop" : "Start";
    }

    private void OnToggleClick(object? sender, System.EventArgs e) => _controller?.Toggle();

    private void OnSettingsClick(object? sender, System.EventArgs e)
    {
        if (_controller is null) return;

        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(_controller);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        else
        {
            _settingsWindow.Activate();
        }
    }

    private void OnQuitClick(object? sender, System.EventArgs e)
    {
        _controller?.Dispose();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
```

- [ ] **Step 5: Ensure Program.cs builds the app correctly**

Replace `src/AutoClicker.App/Program.cs`:
```csharp
using Avalonia;

namespace AutoClicker.App;

internal static class Program
{
    [System.STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
```

- [ ] **Step 6: Confirm the App.csproj includes the asset and Avalonia resources**

Ensure `src/AutoClicker.App/AutoClicker.App.csproj` has `<AvaloniaResource Include="Assets/**" />` (the Avalonia template adds this by default). If missing, add inside an `<ItemGroup>`:
```xml
<ItemGroup>
  <AvaloniaResource Include="Assets/**" />
</ItemGroup>
```

- [ ] **Step 7: Build (SettingsWindow not yet present — expect a known error)**

Run: `dotnet build src/AutoClicker.App`
Expected: FAIL with "The type or namespace name 'SettingsWindow' could not be found" — that is implemented in Task 8. (If any *other* error appears, fix it now.)

- [ ] **Step 8: Commit**

```
git add src/AutoClicker.App/Program.cs src/AutoClicker.App/App.axaml src/AutoClicker.App/App.axaml.cs src/AutoClicker.App/Tray/ src/AutoClicker.App/Assets/ src/AutoClicker.App/AutoClicker.App.csproj
git commit -m "feat(app): tray icon shell and composition root (settings window pending)"
```

---

## Task 8: Settings window and view model

**Files:**
- Create: `src/AutoClicker.App/Settings/ViewModelBase.cs`
- Create: `src/AutoClicker.App/Settings/SettingsViewModel.cs`
- Create: `src/AutoClicker.App/Settings/SettingsWindow.axaml`
- Create: `src/AutoClicker.App/Settings/SettingsWindow.axaml.cs`

A simple bound form. The view model exposes editable fields, validates (interval clamped to ≥ 1), supports **Capture current position**, and saves via the controller.

- [ ] **Step 1: Minimal INotifyPropertyChanged base**

Create `src/AutoClicker.App/Settings/ViewModelBase.cs`:
```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AutoClicker.App.Settings;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
```

- [ ] **Step 2: SettingsViewModel**

Create `src/AutoClicker.App/Settings/SettingsViewModel.cs`:
```csharp
using System;
using System.Collections.Generic;
using AutoClicker.App.Tray;
using AutoClicker.Core;

namespace AutoClicker.App.Settings;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly TrayAppController _controller;

    private int _intervalMs;
    private ClickButton _button;
    private ClickType _clickType;
    private bool _hasClickLimit;
    private int _clickLimit;
    private bool _jitterEnabled;
    private TargetMode _targetMode;
    private int _fixedX;
    private int _fixedY;
    private HotKey _hotkey;
    private string _status = "";

    public SettingsViewModel(TrayAppController controller)
    {
        _controller = controller;
        var s = controller.Settings;
        _intervalMs = s.IntervalMs;
        _button = s.Button;
        _clickType = s.ClickType;
        _hasClickLimit = s.ClickLimit is not null;
        _clickLimit = s.ClickLimit ?? 100;
        _jitterEnabled = s.JitterEnabled;
        _targetMode = s.TargetMode;
        _fixedX = s.FixedX;
        _fixedY = s.FixedY;
        _hotkey = s.Hotkey;
    }

    public IReadOnlyList<ClickButton> Buttons { get; } =
        new[] { ClickButton.Left, ClickButton.Middle, ClickButton.Right };
    public IReadOnlyList<ClickType> ClickTypes { get; } =
        new[] { ClickType.Single, ClickType.Double };
    public IReadOnlyList<TargetMode> TargetModes { get; } =
        new[] { TargetMode.CurrentCursor, TargetMode.FixedCoordinate };
    public IReadOnlyList<HotKey> Hotkeys { get; } = (HotKey[])Enum.GetValues(typeof(HotKey));

    public int IntervalMs { get => _intervalMs; set => Set(ref _intervalMs, value); }
    public ClickButton Button { get => _button; set => Set(ref _button, value); }
    public ClickType ClickType { get => _clickType; set => Set(ref _clickType, value); }
    public bool HasClickLimit { get => _hasClickLimit; set => Set(ref _hasClickLimit, value); }
    public int ClickLimit { get => _clickLimit; set => Set(ref _clickLimit, value); }
    public bool JitterEnabled { get => _jitterEnabled; set => Set(ref _jitterEnabled, value); }
    public TargetMode TargetMode { get => _targetMode; set => Set(ref _targetMode, value); }
    public int FixedX { get => _fixedX; set => Set(ref _fixedX, value); }
    public int FixedY { get => _fixedY; set => Set(ref _fixedY, value); }
    public HotKey Hotkey { get => _hotkey; set => Set(ref _hotkey, value); }
    public string Status { get => _status; set => Set(ref _status, value); }

    public void CapturePosition()
    {
        var (x, y) = _controller.GetCursorPosition();
        FixedX = x;
        FixedY = y;
        Status = $"Captured ({x}, {y}).";
    }

    public void Save()
    {
        var updated = new AppSettings
        {
            IntervalMs = Math.Max(1, IntervalMs),
            Button = Button,
            ClickType = ClickType,
            ClickLimit = HasClickLimit ? Math.Max(1, ClickLimit) : null,
            JitterEnabled = JitterEnabled,
            TargetMode = TargetMode,
            FixedX = FixedX,
            FixedY = FixedY,
            Hotkey = Hotkey,
        };
        _controller.ApplySettings(updated);
        Status = "Saved.";
    }
}
```

- [ ] **Step 3: SettingsWindow XAML**

Create `src/AutoClicker.App/Settings/SettingsWindow.axaml`:
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="AutoClicker.App.Settings.SettingsWindow"
        Width="360" SizeToContent="Height"
        Title="Auto Clicker — Settings"
        WindowStartupLocation="CenterScreen">
  <StackPanel Margin="16" Spacing="10">

    <TextBlock Text="Interval (ms)" />
    <NumericUpDown Minimum="1" Maximum="600000" Increment="10" Value="{Binding IntervalMs}" />

    <TextBlock Text="Button" />
    <ComboBox ItemsSource="{Binding Buttons}" SelectedItem="{Binding Button}" HorizontalAlignment="Stretch" />

    <TextBlock Text="Click type" />
    <ComboBox ItemsSource="{Binding ClickTypes}" SelectedItem="{Binding ClickType}" HorizontalAlignment="Stretch" />

    <CheckBox Content="Stop after N clicks" IsChecked="{Binding HasClickLimit}" />
    <NumericUpDown Minimum="1" Maximum="1000000" Increment="1"
                   Value="{Binding ClickLimit}" IsEnabled="{Binding HasClickLimit}" />

    <CheckBox Content="Randomized jitter (timing + position)" IsChecked="{Binding JitterEnabled}" />

    <TextBlock Text="Target" />
    <ComboBox ItemsSource="{Binding TargetModes}" SelectedItem="{Binding TargetMode}" HorizontalAlignment="Stretch" />
    <StackPanel Orientation="Horizontal" Spacing="8">
      <NumericUpDown Width="100" Value="{Binding FixedX}" />
      <NumericUpDown Width="100" Value="{Binding FixedY}" />
      <Button Content="Capture position" Click="OnCaptureClick" />
    </StackPanel>

    <TextBlock Text="Start/Stop hotkey" />
    <ComboBox ItemsSource="{Binding Hotkeys}" SelectedItem="{Binding Hotkey}" HorizontalAlignment="Stretch" />

    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
      <TextBlock Text="{Binding Status}" VerticalAlignment="Center" />
      <Button Content="Save" Click="OnSaveClick" />
    </StackPanel>
  </StackPanel>
</Window>
```

- [ ] **Step 4: SettingsWindow code-behind**

Create `src/AutoClicker.App/Settings/SettingsWindow.axaml.cs`:
```csharp
using AutoClicker.App.Tray;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AutoClicker.App.Settings;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel = null!;

    // Parameterless ctor for the XAML designer/loader.
    public SettingsWindow() => AvaloniaXamlLoader.Load(this);

    public SettingsWindow(TrayAppController controller) : this()
    {
        _viewModel = new SettingsViewModel(controller);
        DataContext = _viewModel;
    }

    private void OnCaptureClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        _viewModel.CapturePosition();

    private void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        _viewModel.Save();
}
```

- [ ] **Step 5: Build the full solution**

Run: `dotnet build`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 6: Run the Core test suite (regression check)**

Run: `dotnet test tests/AutoClicker.Core.Tests`
Expected: PASS (all Core tests).

- [ ] **Step 7: Commit**

```
git add src/AutoClicker.App/Settings/
git commit -m "feat(app): add settings window with capture-position and hotkey selection"
```

---

## Task 9: README — build, per-OS permissions, and manual smoke test

**Files:**
- Modify: `README.md`

Synthetic input and global hooks cannot be unit-tested, so this documents the manual verification each OS needs.

- [ ] **Step 1: Write the README**

Replace `README.md`:
```markdown
# Auto Clicker

A cross-platform (Windows, macOS, Linux/X11) system-tray auto clicker for personal use.
Configurable rate, mouse button, single/double click, click limit, jitter, target position
(current cursor or a captured fixed coordinate), and a global start/stop hotkey (default F6).

## Run (development)

    dotnet run --project src/AutoClicker.App

The app has no main window — look for the tray/menu-bar icon. Left-tray-menu gives
**Start/Stop**, **Settings…**, and **Quit**. Press the hotkey (default **F6**) to toggle clicking.

## Build a standalone binary (per device)

    # Windows
    dotnet publish src/AutoClicker.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
    # macOS (Apple Silicon -> osx-arm64, Intel -> osx-x64)
    dotnet publish src/AutoClicker.App -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true
    # Linux (X11)
    dotnet publish src/AutoClicker.App -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true

The binary is under `src/AutoClicker.App/bin/Release/net8.0/<rid>/publish/`.

## Permissions / platform notes

- **macOS:** Grant the app **Accessibility** AND **Input Monitoring** in
  System Settings → Privacy & Security, then restart it. Without these, synthetic clicks and the
  global hotkey will not work (the app shows a warning dialog on startup).
- **Linux:** Requires an **X11** session. Wayland sandboxes global input hooks and synthetic input,
  so it is unsupported (XWayland may work for some setups).
- **Windows:** Works without special permissions. To click into apps running as Administrator,
  run the clicker as Administrator too.

## Tests

    dotnet test

Unit tests cover the OS-independent core (planner, engine, config). The OS-specific input layer is
verified by the manual smoke test below.

## Manual smoke test (run on each OS before trusting it)

1. Launch the app; confirm the tray icon appears and the menu opens.
2. Open a text editor / harmless target. Set interval to ~500 ms, button Left, target Current cursor.
3. Place the cursor over the target, press **F6** — confirm clicks happen at ~2/sec. Press **F6** again — confirm they stop.
4. Set a **click limit** of 5, start — confirm it stops after exactly 5 clicks.
5. Set target **Fixed coordinate**, move the mouse somewhere, click **Capture position**, save.
   Start from a different cursor location — confirm clicks land at the captured spot.
6. Enable **jitter**, start — confirm timing/position vary slightly and it still clicks.
7. Change the hotkey to F7 in Settings, save, and confirm F7 now toggles (and F6 no longer does).
8. Quit from the tray menu — confirm the process exits and the icon disappears.
```

- [ ] **Step 2: Commit**

```
git add README.md
git commit -m "docs: add build, permissions, and manual smoke-test guide"
```

---

## Task 10: Final verification

**Files:** none (verification only)

- [ ] **Step 1: Clean build of everything**

Run: `dotnet build -c Release`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 2: Full test suite**

Run: `dotnet test -c Release`
Expected: all tests PASS (AppSettings, ClickPlanner, ClickEngine, ConfigStore).

- [ ] **Step 3: Publish for the current OS and launch it**

Run (substitute your RID): `dotnet publish src/AutoClicker.App -c Release -r <your-rid> --self-contained -p:PublishSingleFile=true`
Then launch the published binary and walk the README **Manual smoke test** steps 1–8 for this OS.
Expected: all smoke-test steps pass (grant macOS permissions / use X11 on Linux as noted).

- [ ] **Step 4: Final commit (if any local doc tweaks were made during verification)**

```
git add -A
git commit -m "chore: finalize auto clicker MVP"
```

---

## Self-Review notes (already reconciled against the spec)

- **Spec coverage:** interface=tray (Task 7) · current-cursor + fixed-coordinate w/ capture (Tasks 2,4,8) · adjustable rate (Tasks 1,2,8) · button + single/double (Tasks 1,4,6,8) · click limit (Tasks 1,4,8) · jitter (Task 2) · configurable global hotkey default F6 (Tasks 1,6,7,8) · persistence (Task 5) · error handling for permissions/hotkey/invalid input (Tasks 7,8) · TDD core + manual smoke (Tasks 2,4,5,9) · per-OS build (Task 9). All spec sections map to a task.
- **Double-click vs limit:** one `Click` call per loop iteration, so a double-click counts as **one** toward the limit (explicit in Task 4) — matches the decision recorded after spec approval.
- **Config path deviation:** `SpecialFolder.ApplicationData` resolves to `~/.config` on macOS under .NET (not `~/Library/Application Support`). This is acceptable for a personal tool and documented in Task 5 / ConfigStore.
- **Type consistency:** `IInputSimulator.Click(int?, int?, ClickButton, ClickType)`, `ClickPlanner.ComputeDelayMs/ComputeTarget`, `ClickEngine.RunAsync/Start/Stop/UpdateSettings/Stopped`, and `IGlobalInputService.Start/SetHotkey/GetCursorPosition/HotkeyPressed` are used identically across all tasks.
```
