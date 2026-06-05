# Hotkey Combos + Capture Hotkey + Position Sequences — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add modifier-combo hotkeys (set by recording), a capture hotkey that builds a multi-position click **sequence** (move → click → wait → loop, per-position action + delay), and the related UI/tray fixes.

**Architecture:** Extend the OS-free `AutoClicker.Core` (new `Hotkey`/`KeyId`/`SequenceStep`/`ClickAction`; `ClickPlanner.Next` yields the next action for either target mode; `ClickEngine` runs a step index). The SharpHook adapter translates raw key events ↔ the model (modifiers from the event mask, recording, auto-repeat guard). The Avalonia settings window gets a sequence editor and two recordable hotkey rows; the tray menu loses Start/Stop.

**Tech Stack:** C# / .NET 10 · Avalonia 12 · SharpHook 7 (`SharpHook.Data`) · System.Text.Json · xUnit.

> **Build note:** Core tasks (1–4) are verified with `dotnet test tests/AutoClicker.Core.Tests`, which builds **only Core + tests**. Changing the Core model/interface intentionally breaks the **App** project's compile until the App tasks (5–10) are done. The whole-solution `dotnet build` is expected to fail between Task 2 and Task 10; each Core task still keeps the Core tests green. Task 11 restores and verifies the full build.

---

## File Structure

```
src/AutoClicker.Core/
  Enums.cs            # MODIFY: remove HotKey; TargetMode -> {CurrentCursor, Sequence}; add KeyId
  Hotkey.cs           # NEW: Hotkey record (key + modifiers), IsValid, ToDisplayString
  SequenceStep.cs     # NEW: one position + action + delay
  ClickAction.cs      # NEW: ClickAction value (one engine tick)
  AppSettings.cs      # MODIFY: drop FixedX/Y + HotKey; add Sequence + two Hotkeys
  ClickPlanner.cs     # MODIFY: Next(settings,index); ComputeDelayMs(int,bool); JitterPosition
  ClickEngine.cs      # MODIFY: loop via planner.Next + step index; stop on null
  IGlobalInputService.cs # MODIFY: two hotkeys, CaptureRequested, recording
src/AutoClicker.App/
  Adapters/SharpHookMappings.cs          # MODIFY: KeyId<->KeyCode, mask->modifiers, isModifier
  Adapters/SharpHookInputSimulator.cs    # MODIFY: move cursor before a coordinate click
  Adapters/SharpHookGlobalInputService.cs# MODIFY: combos, capture, recording, auto-repeat
  Tray/TrayAppController.cs              # MODIFY: capture appends step; recording passthrough
  App.axaml.cs                          # MODIFY: tray menu = Settings + Quit
  Settings/SequenceStepViewModel.cs     # NEW: row view-model
  Settings/SettingsViewModel.cs         # MODIFY: modes, sequence list, hotkey recording
  Settings/SettingsWindow.axaml(.cs)    # MODIFY: sequence editor + hotkey rows
tests/AutoClicker.Core.Tests/
  HotkeyTests.cs        # NEW
  AppSettingsTests.cs   # MODIFY
  ClickPlannerTests.cs  # MODIFY
  ClickEngineTests.cs   # MODIFY
  ConfigStoreTests.cs   # MODIFY
README.md               # MODIFY
```

---

## Task 1: KeyId enum + Hotkey record (Core)

**Files:**
- Modify: `src/AutoClicker.Core/Enums.cs`
- Create: `src/AutoClicker.Core/Hotkey.cs`
- Test: `tests/AutoClicker.Core.Tests/HotkeyTests.cs`

This task is **additive** (does not yet remove `HotKey` or change `TargetMode`), so Core keeps compiling.

- [ ] **Step 1: Write the failing tests**

Create `tests/AutoClicker.Core.Tests/HotkeyTests.cs`:
```csharp
using AutoClicker.Core;
using Xunit;

namespace AutoClicker.Core.Tests;

public class HotkeyTests
{
    [Fact]
    public void Equality_SameComboEqual_DifferentNot()
    {
        Assert.Equal(new Hotkey(KeyId.F6), new Hotkey(KeyId.F6));
        Assert.NotEqual(new Hotkey(KeyId.F6), new Hotkey(KeyId.F7));
        Assert.NotEqual(new Hotkey(KeyId.F6, Ctrl: true), new Hotkey(KeyId.F6));
        Assert.Equal(new Hotkey(KeyId.F6, Ctrl: true), new Hotkey(KeyId.F6, Ctrl: true));
    }

    [Fact]
    public void IsValid_NoneInvalid_RealKeyValid()
    {
        Assert.False(new Hotkey(KeyId.None).IsValid);
        Assert.False(Hotkey.None.IsValid);
        Assert.True(new Hotkey(KeyId.F6).IsValid);
    }

    [Theory]
    [InlineData(false, false, false, false, KeyId.F6, "F6")]
    [InlineData(true, false, false, false, KeyId.F6, "Ctrl+F6")]
    [InlineData(false, true, true, false, KeyId.Backquote, "Shift+Alt+~")]
    [InlineData(false, false, false, false, KeyId.Escape, "Esc")]
    [InlineData(true, true, true, true, KeyId.D5, "Ctrl+Shift+Alt+Win+5")]
    [InlineData(false, false, false, false, KeyId.A, "A")]
    public void ToDisplayString_FormatsCanonically(
        bool ctrl, bool shift, bool alt, bool meta, KeyId key, string expected)
    {
        Assert.Equal(expected, new Hotkey(key, ctrl, shift, alt, meta).ToDisplayString());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AutoClicker.Core.Tests --filter HotkeyTests`
Expected: FAIL — `Hotkey`/`KeyId` do not exist (compile error).

- [ ] **Step 3: Add the KeyId enum**

In `src/AutoClicker.Core/Enums.cs`, **add** this enum (leave `HotKey`, `ClickButton`, `ClickType`, `TargetMode` untouched for now):
```csharp
/// <summary>Platform-neutral keys that can be used in a hotkey. D0–D9 must stay contiguous.</summary>
public enum KeyId
{
    None = 0,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    Backquote, Minus, Equals, LeftBracket, RightBracket, Semicolon, Quote, Comma, Period, Slash, Backslash,
    Space, Enter, Tab, Escape, Insert, Delete, Home, End, PageUp, PageDown,
    Up, Down, Left, Right
}
```

- [ ] **Step 4: Create the Hotkey record**

Create `src/AutoClicker.Core/Hotkey.cs`:
```csharp
using System.Text;

namespace AutoClicker.Core;

/// <summary>A global hotkey: one base key plus modifier flags. Value-equality drives exact matching.</summary>
public sealed record Hotkey(KeyId Key, bool Ctrl = false, bool Shift = false, bool Alt = false, bool Meta = false)
{
    public static Hotkey None { get; } = new(KeyId.None);

    public bool IsValid => Key != KeyId.None;

    public string ToDisplayString()
    {
        if (Key == KeyId.None) return "(none)";
        var sb = new StringBuilder();
        if (Ctrl) sb.Append("Ctrl+");
        if (Shift) sb.Append("Shift+");
        if (Alt) sb.Append("Alt+");
        if (Meta) sb.Append("Win+");
        sb.Append(Symbol(Key));
        return sb.ToString();
    }

    private static string Symbol(KeyId key)
    {
        if (key >= KeyId.D0 && key <= KeyId.D9)
            return ((int)(key - KeyId.D0)).ToString();

        return key switch
        {
            KeyId.Backquote => "~",
            KeyId.Escape => "Esc",
            KeyId.Minus => "-",
            KeyId.Equals => "=",
            KeyId.LeftBracket => "[",
            KeyId.RightBracket => "]",
            KeyId.Semicolon => ";",
            KeyId.Quote => "'",
            KeyId.Comma => ",",
            KeyId.Period => ".",
            KeyId.Slash => "/",
            KeyId.Backslash => "\\",
            _ => key.ToString(),
        };
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/AutoClicker.Core.Tests --filter HotkeyTests`
Expected: PASS (all cases).

- [ ] **Step 6: Commit**

```
git add src/AutoClicker.Core/Enums.cs src/AutoClicker.Core/Hotkey.cs tests/AutoClicker.Core.Tests/HotkeyTests.cs
git commit -m "feat(core): add KeyId enum and Hotkey record"
```

---

## Task 2: Sequence model, settings, planner, and engine (Core)

**Files:**
- Create: `src/AutoClicker.Core/SequenceStep.cs`, `src/AutoClicker.Core/ClickAction.cs`
- Modify: `src/AutoClicker.Core/Enums.cs`, `src/AutoClicker.Core/AppSettings.cs`,
  `src/AutoClicker.Core/ClickPlanner.cs`, `src/AutoClicker.Core/ClickEngine.cs`
- Test: `tests/AutoClicker.Core.Tests/AppSettingsTests.cs`,
  `tests/AutoClicker.Core.Tests/ClickPlannerTests.cs`,
  `tests/AutoClicker.Core.Tests/ClickEngineTests.cs`

These types are tightly coupled (the model, planner, and engine reference each other), so they change together. **After this task the App project will not compile** until Task 10 — that is expected; Core tests stay green.

- [ ] **Step 1: Replace AppSettingsTests with the new defaults test**

Replace `tests/AutoClicker.Core.Tests/AppSettingsTests.cs`:
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
        Assert.Empty(s.Sequence);
        Assert.Equal(new Hotkey(KeyId.F6), s.StartStopHotkey);
        Assert.Equal(new Hotkey(KeyId.F8), s.CaptureHotkey);
    }

    [Fact]
    public void Clone_DeepCopiesSequence()
    {
        var s = new AppSettings();
        s.Sequence.Add(new SequenceStep { X = 1, Y = 2 });

        var copy = s.Clone();
        copy.Sequence[0].X = 99;

        Assert.Equal(1, s.Sequence[0].X); // original unaffected
    }
}
```

- [ ] **Step 2: Replace ClickPlannerTests**

Replace `tests/AutoClicker.Core.Tests/ClickPlannerTests.cs`:
```csharp
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
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/AutoClicker.Core.Tests`
Expected: FAIL — compile errors (`SequenceStep`, `ClickAction`, new `AppSettings`/`ClickPlanner` members, `TargetMode.Sequence` missing).

- [ ] **Step 4: Update Enums.cs — remove HotKey, change TargetMode**

In `src/AutoClicker.Core/Enums.cs`: **delete** the `HotKey` enum entirely, and replace the `TargetMode` enum with:
```csharp
public enum TargetMode { CurrentCursor, Sequence }
```
(Keep `ClickButton`, `ClickType`, and the `KeyId` enum from Task 1.)

- [ ] **Step 5: Create SequenceStep and ClickAction**

Create `src/AutoClicker.Core/SequenceStep.cs`:
```csharp
namespace AutoClicker.Core;

/// <summary>One position in a click sequence, with its own action and the delay before its click.</summary>
public sealed class SequenceStep
{
    public int X { get; set; }
    public int Y { get; set; }
    public ClickButton Button { get; set; } = ClickButton.Left;
    public ClickType ClickType { get; set; } = ClickType.Single;

    /// <summary>Wait (ms) before this position's click. In a loop this is the inter-action delay.</summary>
    public int DelayMs { get; set; } = 100;

    public SequenceStep Clone() => (SequenceStep)MemberwiseClone();
}
```

Create `src/AutoClicker.Core/ClickAction.cs`:
```csharp
namespace AutoClicker.Core;

/// <summary>One engine tick: wait <see cref="DelayMs"/>, then click. Null X/Y = current cursor.</summary>
public readonly record struct ClickAction(int DelayMs, int? X, int? Y, ClickButton Button, ClickType Type);
```

- [ ] **Step 6: Rewrite AppSettings**

Replace `src/AutoClicker.Core/AppSettings.cs`:
```csharp
namespace AutoClicker.Core;

/// <summary>User-configurable clicking behavior. Persisted between runs.</summary>
public sealed class AppSettings
{
    /// <summary>Delay between clicks (ms) in CurrentCursor mode. 100ms ~= 10 CPS.</summary>
    public int IntervalMs { get; set; } = 100;

    /// <summary>Button used in CurrentCursor mode (sequence steps carry their own).</summary>
    public ClickButton Button { get; set; } = ClickButton.Left;

    /// <summary>Click type used in CurrentCursor mode.</summary>
    public ClickType ClickType { get; set; } = ClickType.Single;

    /// <summary>Auto-stop after this many clicks (counts every action). Null = run until stopped.</summary>
    public int? ClickLimit { get; set; }

    public bool JitterEnabled { get; set; }

    public TargetMode TargetMode { get; set; } = TargetMode.CurrentCursor;

    /// <summary>Ordered click positions used in Sequence mode.</summary>
    public List<SequenceStep> Sequence { get; set; } = new();

    public Hotkey StartStopHotkey { get; set; } = new(KeyId.F6);
    public Hotkey CaptureHotkey { get; set; } = new(KeyId.F8);

    /// <summary>Deep copy (sequence cloned) so the engine reads a stable snapshot.</summary>
    public AppSettings Clone()
    {
        var copy = (AppSettings)MemberwiseClone();
        copy.Sequence = Sequence.Select(step => step.Clone()).ToList();
        return copy; // Hotkey is an immutable record; safe to share
    }
}
```

- [ ] **Step 7: Rewrite ClickPlanner**

Replace `src/AutoClicker.Core/ClickPlanner.cs`:
```csharp
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
```

- [ ] **Step 8: Update ClickEngine to use Next + a step index**

In `src/AutoClicker.Core/ClickEngine.cs`, add a field and rewrite the `RunAsync` loop body. Add near the other fields:
```csharp
    private int _sequenceIndex;
```
Replace the whole `RunAsync` method with:
```csharp
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
```

- [ ] **Step 9: Update ClickEngineTests for the sequence model**

In `tests/AutoClicker.Core.Tests/ClickEngineTests.cs`, replace the `RunAsync_UsesButtonTypeAndTargetFromSettings` test with two tests, and add an empty-sequence test. Replace that one method with:
```csharp
    [Fact]
    public async Task RunAsync_Sequence_CyclesPositionsAndCountsEachAction()
    {
        var input = new FakeInputSimulator();
        var settings = new AppSettings { TargetMode = TargetMode.Sequence, ClickLimit = 3 };
        settings.Sequence.Add(new SequenceStep { X = 10, Y = 20, Button = ClickButton.Left,  ClickType = ClickType.Single });
        settings.Sequence.Add(new SequenceStep { X = 30, Y = 40, Button = ClickButton.Right, ClickType = ClickType.Double });
        var engine = NewEngine(settings, input, new FakeClock());

        await engine.RunAsync(CancellationToken.None);

        Assert.Equal(3, input.Clicks.Count);
        Assert.Equal(new FakeInputSimulator.Recorded(10, 20, ClickButton.Left, ClickType.Single), input.Clicks[0]);
        Assert.Equal(new FakeInputSimulator.Recorded(30, 40, ClickButton.Right, ClickType.Double), input.Clicks[1]);
        Assert.Equal(new FakeInputSimulator.Recorded(10, 20, ClickButton.Left, ClickType.Single), input.Clicks[2]);
    }

    [Fact]
    public async Task RunAsync_SequenceMode_EmptyList_StopsImmediately()
    {
        var input = new FakeInputSimulator();
        var settings = new AppSettings { TargetMode = TargetMode.Sequence }; // empty
        var engine = NewEngine(settings, input, new FakeClock());

        await engine.RunAsync(CancellationToken.None);

        Assert.Empty(input.Clicks);
        Assert.False(engine.IsRunning);
    }
```
(The other ClickEngineTests — limit at N, current-cursor null coords, cancellation, start/stop — are unchanged and still valid.)

- [ ] **Step 10: Run the full Core suite**

Run: `dotnet test tests/AutoClicker.Core.Tests`
Expected: PASS (Hotkey, AppSettings, ClickPlanner, ClickEngine). The App project is now intentionally broken — do not build the solution yet.

- [ ] **Step 11: Commit**

```
git add src/AutoClicker.Core tests/AutoClicker.Core.Tests
git commit -m "feat(core): sequence target mode (steps, ClickAction, planner.Next, engine index)"
```

---

## Task 3: ConfigStore round-trip + legacy fallback (Core)

**Files:**
- Test: `tests/AutoClicker.Core.Tests/ConfigStoreTests.cs`
- (No `ConfigStore.cs` change — it already serializes whatever `AppSettings` holds.)

- [ ] **Step 1: Replace ConfigStoreTests**

Replace `tests/AutoClicker.Core.Tests/ConfigStoreTests.cs`:
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
    public void SaveThenLoad_RoundTripsSequenceAndHotkeys()
    {
        var store = new ConfigStore(FilePath);
        var saved = new AppSettings
        {
            TargetMode = TargetMode.Sequence,
            JitterEnabled = true,
            ClickLimit = 7,
            StartStopHotkey = new Hotkey(KeyId.F6, Ctrl: true),
            CaptureHotkey = new Hotkey(KeyId.Backquote, Shift: true, Alt: true),
        };
        saved.Sequence.Add(new SequenceStep { X = 11, Y = 22, Button = ClickButton.Right, ClickType = ClickType.Double, DelayMs = 250 });

        store.Save(saved);
        var loaded = store.Load();

        Assert.Equal(TargetMode.Sequence, loaded.TargetMode);
        Assert.True(loaded.JitterEnabled);
        Assert.Equal(7, loaded.ClickLimit);
        Assert.Equal(new Hotkey(KeyId.F6, Ctrl: true), loaded.StartStopHotkey);
        Assert.Equal(new Hotkey(KeyId.Backquote, Shift: true, Alt: true), loaded.CaptureHotkey);
        var step = Assert.Single(loaded.Sequence);
        Assert.Equal(11, step.X);
        Assert.Equal(22, step.Y);
        Assert.Equal(ClickButton.Right, step.Button);
        Assert.Equal(ClickType.Double, step.ClickType);
        Assert.Equal(250, step.DelayMs);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var loaded = new ConfigStore(FilePath).Load();
        Assert.Equal(new Hotkey(KeyId.F6), loaded.StartStopHotkey);
        Assert.Equal(TargetMode.CurrentCursor, loaded.TargetMode);
    }

    [Fact]
    public void Load_LegacyConfig_FallsBackToDefaultsWithoutThrowing()
    {
        // Old schema: a removed TargetMode value and a bare string Hotkey.
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath,
            "{ \"TargetMode\": \"FixedCoordinate\", \"FixedX\": 5, \"Hotkey\": \"F6\" }");

        var loaded = new ConfigStore(FilePath).Load();

        Assert.Equal(new AppSettings().IntervalMs, loaded.IntervalMs);
        Assert.Equal(new Hotkey(KeyId.F6), loaded.StartStopHotkey);
        Assert.Equal(TargetMode.CurrentCursor, loaded.TargetMode);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/AutoClicker.Core.Tests --filter ConfigStoreTests`
Expected: PASS. (The legacy test relies on `ConfigStore.Load` catching `JsonException` from the unknown enum value and returning defaults — already implemented.)

- [ ] **Step 3: Commit**

```
git add tests/AutoClicker.Core.Tests/ConfigStoreTests.cs
git commit -m "test(core): ConfigStore round-trips sequence/hotkeys and ignores legacy config"
```

---

## Task 4: Update IGlobalInputService interface (Core)

**Files:**
- Modify: `src/AutoClicker.Core/IGlobalInputService.cs`

No tests (no Core consumer); this only changes the contract the App implements.

- [ ] **Step 1: Replace the interface**

Replace `src/AutoClicker.Core/IGlobalInputService.cs`:
```csharp
namespace AutoClicker.Core;

/// <summary>Global hook: detects the two hotkeys, reports the cursor position, and records combos.</summary>
public interface IGlobalInputService : IDisposable
{
    /// <summary>Raised on the hook thread when the start/stop hotkey is pressed.</summary>
    event EventHandler? HotkeyPressed;

    /// <summary>Raised on the hook thread when the capture hotkey is pressed.</summary>
    event EventHandler? CaptureRequested;

    void Start();

    void SetStartStopHotkey(Hotkey hotkey);
    void SetCaptureHotkey(Hotkey hotkey);

    /// <summary>Capture the next pressed combo into <paramref name="onRecorded"/>. Esc cancels.</summary>
    void BeginRecording(Action<Hotkey> onRecorded);
    void CancelRecording();

    /// <summary>Last known cursor position from the global hook (0,0 until the mouse moves).</summary>
    (int X, int Y) GetCursorPosition();
}
```

- [ ] **Step 2: Build Core only (App still broken — expected)**

Run: `dotnet build src/AutoClicker.Core`
Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```
git add src/AutoClicker.Core/IGlobalInputService.cs
git commit -m "feat(core): two hotkeys, capture event, and recording on IGlobalInputService"
```

---

## Task 5: SharpHook key/modifier mapping (App)

**Files:**
- Modify: `src/AutoClicker.App/Adapters/SharpHookMappings.cs`

- [ ] **Step 1: Replace the mappings file**

Replace `src/AutoClicker.App/Adapters/SharpHookMappings.cs`:
```csharp
using AutoClicker.Core;
using SharpHook.Data;

namespace AutoClicker.App.Adapters;

internal static class SharpHookMappings
{
    // Single source of truth; both directions derive from this.
    private static readonly (KeyId Id, KeyCode Code)[] Pairs =
    {
        (KeyId.F1, KeyCode.VcF1), (KeyId.F2, KeyCode.VcF2), (KeyId.F3, KeyCode.VcF3),
        (KeyId.F4, KeyCode.VcF4), (KeyId.F5, KeyCode.VcF5), (KeyId.F6, KeyCode.VcF6),
        (KeyId.F7, KeyCode.VcF7), (KeyId.F8, KeyCode.VcF8), (KeyId.F9, KeyCode.VcF9),
        (KeyId.F10, KeyCode.VcF10), (KeyId.F11, KeyCode.VcF11), (KeyId.F12, KeyCode.VcF12),
        (KeyId.A, KeyCode.VcA), (KeyId.B, KeyCode.VcB), (KeyId.C, KeyCode.VcC), (KeyId.D, KeyCode.VcD),
        (KeyId.E, KeyCode.VcE), (KeyId.F, KeyCode.VcF), (KeyId.G, KeyCode.VcG), (KeyId.H, KeyCode.VcH),
        (KeyId.I, KeyCode.VcI), (KeyId.J, KeyCode.VcJ), (KeyId.K, KeyCode.VcK), (KeyId.L, KeyCode.VcL),
        (KeyId.M, KeyCode.VcM), (KeyId.N, KeyCode.VcN), (KeyId.O, KeyCode.VcO), (KeyId.P, KeyCode.VcP),
        (KeyId.Q, KeyCode.VcQ), (KeyId.R, KeyCode.VcR), (KeyId.S, KeyCode.VcS), (KeyId.T, KeyCode.VcT),
        (KeyId.U, KeyCode.VcU), (KeyId.V, KeyCode.VcV), (KeyId.W, KeyCode.VcW), (KeyId.X, KeyCode.VcX),
        (KeyId.Y, KeyCode.VcY), (KeyId.Z, KeyCode.VcZ),
        (KeyId.D0, KeyCode.Vc0), (KeyId.D1, KeyCode.Vc1), (KeyId.D2, KeyCode.Vc2), (KeyId.D3, KeyCode.Vc3),
        (KeyId.D4, KeyCode.Vc4), (KeyId.D5, KeyCode.Vc5), (KeyId.D6, KeyCode.Vc6), (KeyId.D7, KeyCode.Vc7),
        (KeyId.D8, KeyCode.Vc8), (KeyId.D9, KeyCode.Vc9),
        (KeyId.Backquote, KeyCode.VcBackQuote), (KeyId.Minus, KeyCode.VcMinus), (KeyId.Equals, KeyCode.VcEquals),
        (KeyId.LeftBracket, KeyCode.VcOpenBracket), (KeyId.RightBracket, KeyCode.VcCloseBracket),
        (KeyId.Semicolon, KeyCode.VcSemicolon), (KeyId.Quote, KeyCode.VcQuote), (KeyId.Comma, KeyCode.VcComma),
        (KeyId.Period, KeyCode.VcPeriod), (KeyId.Slash, KeyCode.VcSlash), (KeyId.Backslash, KeyCode.VcBackSlash),
        (KeyId.Space, KeyCode.VcSpace), (KeyId.Enter, KeyCode.VcEnter), (KeyId.Tab, KeyCode.VcTab),
        (KeyId.Escape, KeyCode.VcEscape), (KeyId.Insert, KeyCode.VcInsert), (KeyId.Delete, KeyCode.VcDelete),
        (KeyId.Home, KeyCode.VcHome), (KeyId.End, KeyCode.VcEnd),
        (KeyId.PageUp, KeyCode.VcPageUp), (KeyId.PageDown, KeyCode.VcPageDown),
        (KeyId.Up, KeyCode.VcUp), (KeyId.Down, KeyCode.VcDown), (KeyId.Left, KeyCode.VcLeft), (KeyId.Right, KeyCode.VcRight),
    };

    private static readonly Dictionary<KeyCode, KeyId> CodeToId = BuildCodeToId();

    private static Dictionary<KeyCode, KeyId> BuildCodeToId()
    {
        var d = new Dictionary<KeyCode, KeyId>();
        foreach (var (id, code) in Pairs) d[code] = id;
        return d;
    }

    /// <summary>Maps a raw key to our KeyId, or null if it is a key we do not support as a hotkey.</summary>
    public static KeyId? ToKeyId(KeyCode code) => CodeToId.TryGetValue(code, out var id) ? id : null;

    public static bool IsModifierKey(KeyCode code) => code is
        KeyCode.VcLeftControl or KeyCode.VcRightControl or
        KeyCode.VcLeftShift or KeyCode.VcRightShift or
        KeyCode.VcLeftAlt or KeyCode.VcRightAlt or
        KeyCode.VcLeftMeta or KeyCode.VcRightMeta;

    /// <summary>Builds a Hotkey from a base key and the event's modifier mask.</summary>
    public static Hotkey ToHotkey(KeyId key, EventMask mask) => new(
        key,
        Ctrl: mask.HasFlag(EventMask.LeftCtrl) || mask.HasFlag(EventMask.RightCtrl),
        Shift: mask.HasFlag(EventMask.LeftShift) || mask.HasFlag(EventMask.RightShift),
        Alt: mask.HasFlag(EventMask.LeftAlt) || mask.HasFlag(EventMask.RightAlt),
        Meta: mask.HasFlag(EventMask.LeftMeta) || mask.HasFlag(EventMask.RightMeta));

    public static MouseButton ToMouseButton(ClickButton button) => button switch
    {
        ClickButton.Left => MouseButton.Button1,
        ClickButton.Right => MouseButton.Button2,
        ClickButton.Middle => MouseButton.Button3,
        _ => MouseButton.Button1,
    };
}
```

- [ ] **Step 2: Verify these KeyCode names compile**

Run: `dotnet build src/AutoClicker.App 2>&1 | findstr /I "error CS0117"`
Expected: ideally no `CS0117` (unknown enum member). If any appears (SharpHook spells a key differently, e.g. `VcBackslash` vs `VcBackSlash`, `VcQuote` vs `VcApostrophe`, `VcOpenBracket` vs `VcLeftBracket`), open the `SharpHook.Data.KeyCode` enum (F12 / decompile) and correct that one entry. Other errors about the rest of the App are expected at this stage.

- [ ] **Step 3: Commit**

```
git add src/AutoClicker.App/Adapters/SharpHookMappings.cs
git commit -m "feat(app): KeyId<->KeyCode map, modifier mask, and modifier-key detection"
```

---

## Task 6: Move cursor before a coordinate click (App)

**Files:**
- Modify: `src/AutoClicker.App/Adapters/SharpHookInputSimulator.cs`

- [ ] **Step 1: Replace the simulator**

Replace `src/AutoClicker.App/Adapters/SharpHookInputSimulator.cs`:
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

        // For a fixed coordinate, move the cursor there first so the motion is visible.
        if (x is not null && y is not null)
            _simulator.SimulateMouseMovement((short)x.Value, (short)y.Value);

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

- [ ] **Step 2: Commit**

```
git add src/AutoClicker.App/Adapters/SharpHookInputSimulator.cs
git commit -m "feat(app): move cursor to the target before a fixed-coordinate click"
```

---

## Task 7: Global hook — combos, capture, recording, auto-repeat (App)

**Files:**
- Modify: `src/AutoClicker.App/Adapters/SharpHookGlobalInputService.cs`

- [ ] **Step 1: Replace the service**

Replace `src/AutoClicker.App/Adapters/SharpHookGlobalInputService.cs`:
```csharp
using AutoClicker.Core;
using SharpHook;
using SharpHook.Data;

namespace AutoClicker.App.Adapters;

/// <summary>Global hook via SharpHook: matches the two hotkeys, tracks the cursor, records combos.</summary>
public sealed class SharpHookGlobalInputService : IGlobalInputService
{
    private readonly TaskPoolGlobalHook _hook = new();
    private readonly HashSet<KeyCode> _held = new();

    private Hotkey _startStop = new(KeyId.F6);
    private Hotkey _capture = new(KeyId.F8);
    private Action<Hotkey>? _recordCallback;

    private int _cursorX;
    private int _cursorY;

    public event EventHandler? HotkeyPressed;
    public event EventHandler? CaptureRequested;

    public SharpHookGlobalInputService()
    {
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
        _hook.MouseMoved += OnMouseMoved;
        _hook.MouseDragged += OnMouseMoved;
    }

    public void Start() => _hook.RunAsync();

    public void SetStartStopHotkey(Hotkey hotkey) => _startStop = hotkey;
    public void SetCaptureHotkey(Hotkey hotkey) => _capture = hotkey;

    public void BeginRecording(Action<Hotkey> onRecorded) => _recordCallback = onRecorded;
    public void CancelRecording() => _recordCallback = null;

    public (int X, int Y) GetCursorPosition() => (_cursorX, _cursorY);

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        KeyCode code = e.Data.KeyCode;
        if (SharpHookMappings.IsModifierKey(code)) return; // wait for a real base key

        // Auto-repeat guard: ignore until the key is released.
        if (!_held.Add(code)) return;

        KeyId? id = SharpHookMappings.ToKeyId(code);
        if (id is null) return; // unsupported key

        var pressed = SharpHookMappings.ToHotkey(id.Value, e.RawEvent.Mask);

        var record = _recordCallback;
        if (record is not null)
        {
            _recordCallback = null;
            if (pressed.Key != KeyId.Escape) // Esc cancels recording
                record(pressed);
            return;
        }

        if (pressed == _startStop) HotkeyPressed?.Invoke(this, EventArgs.Empty);
        else if (pressed == _capture) CaptureRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e) => _held.Remove(e.Data.KeyCode);

    private void OnMouseMoved(object? sender, MouseHookEventArgs e)
    {
        _cursorX = e.Data.X;
        _cursorY = e.Data.Y;
    }

    public void Dispose() => _hook.Dispose();
}
```

- [ ] **Step 2: Commit**

```
git add src/AutoClicker.App/Adapters/SharpHookGlobalInputService.cs
git commit -m "feat(app): hotkey combos, capture event, combo recording, auto-repeat guard"
```

---

## Task 8: Controller capture/recording + tray menu (App)

**Files:**
- Modify: `src/AutoClicker.App/Tray/TrayAppController.cs`, `src/AutoClicker.App/App.axaml.cs`

- [ ] **Step 1: Replace TrayAppController**

Replace `src/AutoClicker.App/Tray/TrayAppController.cs`:
```csharp
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

    /// <summary>Raised (UI thread) when the engine running state changes.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Raised (UI thread) when settings change outside the window (e.g. capture).</summary>
    public event EventHandler? SettingsChangedExternally;

    /// <summary>Raised (UI thread) when the global hook can't start (permissions).</summary>
    public event EventHandler<string>? HookFailed;

    public bool IsRunning => _engine.IsRunning;

    public TrayAppController(ConfigStore configStore, IGlobalInputService globalInput, IInputSimulator inputSimulator)
    {
        _configStore = configStore;
        _globalInput = globalInput;

        Settings = _configStore.Load();
        _engine = new ClickEngine(inputSimulator, new SystemClock(), new ClickPlanner(new SystemRandomSource()), Settings.Clone());
        _engine.Stopped += (_, _) => Dispatcher.UIThread.Post(() => StateChanged?.Invoke(this, EventArgs.Empty));

        _globalInput.SetStartStopHotkey(Settings.StartStopHotkey);
        _globalInput.SetCaptureHotkey(Settings.CaptureHotkey);
        _globalInput.HotkeyPressed += (_, _) => Dispatcher.UIThread.Post(Toggle);
        _globalInput.CaptureRequested += (_, _) => Dispatcher.UIThread.Post(CaptureCurrentPosition);
    }

    public void Initialize()
    {
        try { _globalInput.Start(); }
        catch (Exception ex) { HookFailed?.Invoke(this, BuildPermissionMessage(ex)); }
    }

    public void Toggle()
    {
        if (_engine.IsRunning) _engine.Stop();
        else _engine.Start();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Capture hotkey: append the cursor position as a new step and switch to Sequence mode.</summary>
    private void CaptureCurrentPosition()
    {
        var (x, y) = _globalInput.GetCursorPosition();
        var updated = Settings.Clone();
        updated.TargetMode = TargetMode.Sequence;
        updated.Sequence.Add(new SequenceStep { X = x, Y = y });
        ApplySettings(updated);
        SettingsChangedExternally?.Invoke(this, EventArgs.Empty);
    }

    public void BeginRecordHotkey(Action<Hotkey> onRecorded) => _globalInput.BeginRecording(onRecorded);
    public void CancelRecording() => _globalInput.CancelRecording();

    /// <summary>Persist edited settings, push to the engine, and re-register both hotkeys.</summary>
    public void ApplySettings(AppSettings updated)
    {
        Settings = updated;
        _configStore.Save(updated);
        _engine.UpdateSettings(updated.Clone());
        _globalInput.SetStartStopHotkey(updated.StartStopHotkey);
        _globalInput.SetCaptureHotkey(updated.CaptureHotkey);
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

- [ ] **Step 2: Update App.axaml.cs — drop Start/Stop from the tray menu**

In `src/AutoClicker.App/App.axaml.cs`: remove the `_toggleItem` field and the `UpdateToggleLabel` method and its calls, and replace `BuildTrayIcon` plus the `StateChanged` subscription. Specifically:

Remove this field line:
```csharp
    private NativeMenuItem? _toggleItem;
```
Replace the `_controller.StateChanged += ...` line (in `OnFrameworkInitializationCompleted`) and the `UpdateToggleLabel();` call after `_controller.Initialize();` — delete both lines. The block should read:
```csharp
            _controller.HookFailed += (_, message) =>
                new MessageWindow("Auto Clicker", message).Show();

            BuildTrayIcon();
            _controller.Initialize();

            OpenSettings();
```
Replace `BuildTrayIcon` with (Settings + Quit only):
```csharp
    private void BuildTrayIcon()
    {
        var settingsItem = new NativeMenuItem("Settings…");
        settingsItem.Click += (_, _) => OpenSettings();

        var quitItem = new NativeMenuItem("Quit");
        quitItem.Click += (_, _) => Quit();

        var menu = new NativeMenu();
        menu.Items.Add(settingsItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(quitItem);

        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://AutoClicker.App/Assets/app-icon.ico"))),
            ToolTipText = "Auto Clicker",
            Menu = menu,
            IsVisible = true,
        };

        TrayIcon.SetIcons(this, new TrayIcons { _trayIcon });
    }
```
Delete the now-unused `UpdateToggleLabel` method entirely.

- [ ] **Step 3: Commit**

```
git add src/AutoClicker.App/Tray/TrayAppController.cs src/AutoClicker.App/App.axaml.cs
git commit -m "feat(app): capture appends a sequence step; tray menu is Settings + Quit"
```

---

## Task 9: Settings view-models (App)

**Files:**
- Create: `src/AutoClicker.App/Settings/SequenceStepViewModel.cs`
- Modify: `src/AutoClicker.App/Settings/SettingsViewModel.cs`

- [ ] **Step 1: Create the row view-model**

Create `src/AutoClicker.App/Settings/SequenceStepViewModel.cs`:
```csharp
using System.Collections.Generic;
using AutoClicker.Core;

namespace AutoClicker.App.Settings;

/// <summary>Editable row for one sequence step.</summary>
public sealed class SequenceStepViewModel : ViewModelBase
{
    private int _x, _y, _delayMs;
    private ClickButton _button;
    private ClickType _clickType;

    public SequenceStepViewModel(SequenceStep step)
    {
        _x = step.X;
        _y = step.Y;
        _button = step.Button;
        _clickType = step.ClickType;
        _delayMs = step.DelayMs;
    }

    public IReadOnlyList<ClickButton> Buttons { get; } =
        new[] { ClickButton.Left, ClickButton.Middle, ClickButton.Right };
    public IReadOnlyList<ClickType> ClickTypes { get; } =
        new[] { ClickType.Single, ClickType.Double };

    public int X { get => _x; set => Set(ref _x, value); }
    public int Y { get => _y; set => Set(ref _y, value); }
    public ClickButton Button { get => _button; set => Set(ref _button, value); }
    public ClickType ClickType { get => _clickType; set => Set(ref _clickType, value); }
    public int DelayMs { get => _delayMs; set => Set(ref _delayMs, value); }

    public SequenceStep ToModel() => new()
    {
        X = X, Y = Y, Button = Button, ClickType = ClickType, DelayMs = DelayMs,
    };
}
```

- [ ] **Step 2: Replace SettingsViewModel**

Replace `src/AutoClicker.App/Settings/SettingsViewModel.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AutoClicker.App.Tray;
using AutoClicker.Core;
using Avalonia.Threading;

namespace AutoClicker.App.Settings;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly TrayAppController _controller;

    private TargetMode _mode;
    private int _intervalMs;
    private ClickButton _button;
    private ClickType _clickType;
    private bool _jitterEnabled;
    private bool _hasClickLimit;
    private int _clickLimit;
    private string _startStopText = "";
    private string _captureText = "";
    private bool _recordingStartStop;
    private bool _recordingCapture;
    private string _status = "";

    private Hotkey _startStopHotkey;
    private Hotkey _captureHotkey;

    public SettingsViewModel(TrayAppController controller)
    {
        _controller = controller;
        LoadFrom(controller.Settings);
        controller.SettingsChangedExternally += (_, _) =>
            Dispatcher.UIThread.Post(() => LoadFrom(controller.Settings));
    }

    private void LoadFrom(AppSettings s)
    {
        Mode = s.TargetMode;
        _intervalMs = s.IntervalMs; OnChanged(nameof(IntervalMs));
        _button = s.Button; OnChanged(nameof(Button));
        _clickType = s.ClickType; OnChanged(nameof(ClickType));
        _jitterEnabled = s.JitterEnabled; OnChanged(nameof(JitterEnabled));
        _hasClickLimit = s.ClickLimit is not null; OnChanged(nameof(HasClickLimit));
        _clickLimit = s.ClickLimit ?? 100; OnChanged(nameof(ClickLimit));
        _startStopHotkey = s.StartStopHotkey;
        _captureHotkey = s.CaptureHotkey;
        StartStopText = s.StartStopHotkey.ToDisplayString();
        CaptureText = s.CaptureHotkey.ToDisplayString();

        Steps.Clear();
        foreach (var step in s.Sequence) Steps.Add(new SequenceStepViewModel(step));
    }

    public IReadOnlyList<TargetMode> Modes { get; } =
        new[] { TargetMode.CurrentCursor, TargetMode.Sequence };
    public IReadOnlyList<ClickButton> Buttons { get; } =
        new[] { ClickButton.Left, ClickButton.Middle, ClickButton.Right };
    public IReadOnlyList<ClickType> ClickTypes { get; } =
        new[] { ClickType.Single, ClickType.Double };

    public ObservableCollection<SequenceStepViewModel> Steps { get; } = new();

    public TargetMode Mode
    {
        get => _mode;
        set
        {
            if (Set(ref _mode, value))
            {
                OnChanged(nameof(IsCursorMode));
                OnChanged(nameof(IsSequenceMode));
            }
        }
    }

    public bool IsCursorMode => Mode == TargetMode.CurrentCursor;
    public bool IsSequenceMode => Mode == TargetMode.Sequence;

    public int IntervalMs { get => _intervalMs; set => Set(ref _intervalMs, value); }
    public ClickButton Button { get => _button; set => Set(ref _button, value); }
    public ClickType ClickType { get => _clickType; set => Set(ref _clickType, value); }
    public bool JitterEnabled { get => _jitterEnabled; set => Set(ref _jitterEnabled, value); }
    public bool HasClickLimit { get => _hasClickLimit; set => Set(ref _hasClickLimit, value); }
    public int ClickLimit { get => _clickLimit; set => Set(ref _clickLimit, value); }

    public string StartStopText { get => _startStopText; set => Set(ref _startStopText, value); }
    public string CaptureText { get => _captureText; set => Set(ref _captureText, value); }
    public bool RecordingStartStop { get => _recordingStartStop; set => Set(ref _recordingStartStop, value); }
    public bool RecordingCapture { get => _recordingCapture; set => Set(ref _recordingCapture, value); }
    public string Status { get => _status; set => Set(ref _status, value); }

    public void AddBlankStep() => Steps.Add(new SequenceStepViewModel(new SequenceStep()));
    public void ClearSteps() => Steps.Clear();
    public void RemoveStep(SequenceStepViewModel row) => Steps.Remove(row);

    public void MoveStepUp(SequenceStepViewModel row)
    {
        int i = Steps.IndexOf(row);
        if (i > 0) Steps.Move(i, i - 1);
    }

    public void MoveStepDown(SequenceStepViewModel row)
    {
        int i = Steps.IndexOf(row);
        if (i >= 0 && i < Steps.Count - 1) Steps.Move(i, i + 1);
    }

    public void RecordStartStop()
    {
        RecordingStartStop = true;
        StartStopText = "Press keys… (Esc to cancel)";
        _controller.BeginRecordHotkey(hk => Dispatcher.UIThread.Post(() => OnRecorded(hk, isStartStop: true)));
    }

    public void RecordCapture()
    {
        RecordingCapture = true;
        CaptureText = "Press keys… (Esc to cancel)";
        _controller.BeginRecordHotkey(hk => Dispatcher.UIThread.Post(() => OnRecorded(hk, isStartStop: false)));
    }

    private void OnRecorded(Hotkey hk, bool isStartStop)
    {
        RecordingStartStop = false;
        RecordingCapture = false;

        var other = isStartStop ? _captureHotkey : _startStopHotkey;
        if (hk == other)
        {
            Status = $"{hk.ToDisplayString()} is already used by the other hotkey.";
            StartStopText = _startStopHotkey.ToDisplayString();
            CaptureText = _captureHotkey.ToDisplayString();
            return;
        }

        if (isStartStop) { _startStopHotkey = hk; StartStopText = hk.ToDisplayString(); }
        else { _captureHotkey = hk; CaptureText = hk.ToDisplayString(); }
        Status = "Hotkey set (remember to Save).";
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
            TargetMode = Mode,
            Sequence = Steps.Select(r => r.ToModel()).ToList(),
            StartStopHotkey = _startStopHotkey,
            CaptureHotkey = _captureHotkey,
        };
        _controller.ApplySettings(updated);
        Status = "Saved.";
    }
}
```

> Note: `ViewModelBase.Set` must return `bool` (true when the value changed) and expose an
> `OnChanged(name)` helper — added in the next step.

- [ ] **Step 3: Upgrade ViewModelBase (Set returns bool + OnChanged)**

Replace `src/AutoClicker.App/Settings/ViewModelBase.cs`:
```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AutoClicker.App.Settings;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnChanged(name);
        return true;
    }

    protected void OnChanged(string? name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

- [ ] **Step 4: Commit**

```
git add src/AutoClicker.App/Settings/SequenceStepViewModel.cs src/AutoClicker.App/Settings/SettingsViewModel.cs src/AutoClicker.App/Settings/ViewModelBase.cs
git commit -m "feat(app): settings view-models for modes, sequence editing, and hotkey recording"
```

---

## Task 10: Settings window UI (App) — restores the full build

**Files:**
- Modify: `src/AutoClicker.App/Settings/SettingsWindow.axaml`,
  `src/AutoClicker.App/Settings/SettingsWindow.axaml.cs`

- [ ] **Step 1: Replace the window XAML**

Replace `src/AutoClicker.App/Settings/SettingsWindow.axaml`:
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:AutoClicker.App.Settings"
        x:Class="AutoClicker.App.Settings.SettingsWindow"
        x:DataType="vm:SettingsViewModel"
        Width="460" Height="640"
        CanResize="True"
        Title="Auto Clicker — Settings"
        WindowStartupLocation="CenterScreen">
  <ScrollViewer>
    <StackPanel Margin="16" Spacing="10">

      <TextBlock Text="Mode" FontWeight="Bold" />
      <ComboBox ItemsSource="{Binding Modes}" SelectedItem="{Binding Mode}" HorizontalAlignment="Stretch" />

      <!-- Current-cursor section -->
      <StackPanel Spacing="8" IsVisible="{Binding IsCursorMode}">
        <TextBlock Text="Interval (ms)" />
        <NumericUpDown Minimum="1" Maximum="600000" Increment="10" FormatString="0" Value="{Binding IntervalMs}" />
        <TextBlock Text="Button" />
        <ComboBox ItemsSource="{Binding Buttons}" SelectedItem="{Binding Button}" HorizontalAlignment="Stretch" />
        <TextBlock Text="Click type" />
        <ComboBox ItemsSource="{Binding ClickTypes}" SelectedItem="{Binding ClickType}" HorizontalAlignment="Stretch" />
      </StackPanel>

      <!-- Sequence section -->
      <StackPanel Spacing="8" IsVisible="{Binding IsSequenceMode}">
        <TextBlock Text="Positions (move → click → wait → next → loop)" FontWeight="Bold" />
        <TextBlock Text="Tip: press the capture hotkey while hovering a target to add its position."
                   TextWrapping="Wrap" Foreground="Gray" FontSize="12" />
        <Grid ColumnDefinitions="40,90,90,90,110,Auto">
          <TextBlock Grid.Column="0" Text="#" />
          <TextBlock Grid.Column="1" Text="X" />
          <TextBlock Grid.Column="2" Text="Y" />
          <TextBlock Grid.Column="3" Text="Delay" />
          <TextBlock Grid.Column="4" Text="Action" />
        </Grid>
        <ItemsControl ItemsSource="{Binding Steps}">
          <ItemsControl.ItemTemplate>
            <DataTemplate x:DataType="vm:SequenceStepViewModel">
              <Grid ColumnDefinitions="40,90,90,90,110,Auto" Margin="0,2">
                <NumericUpDown Grid.Column="1" Width="86" FormatString="0" Minimum="-100000" Maximum="100000" Value="{Binding X}" />
                <NumericUpDown Grid.Column="2" Width="86" FormatString="0" Minimum="-100000" Maximum="100000" Value="{Binding Y}" />
                <NumericUpDown Grid.Column="3" Width="86" FormatString="0" Minimum="1" Maximum="600000" Value="{Binding DelayMs}" />
                <StackPanel Grid.Column="4" Orientation="Horizontal" Spacing="2">
                  <ComboBox Width="62" ItemsSource="{Binding Buttons}" SelectedItem="{Binding Button}" />
                  <ComboBox Width="44" ItemsSource="{Binding ClickTypes}" SelectedItem="{Binding ClickType}" />
                </StackPanel>
                <StackPanel Grid.Column="5" Orientation="Horizontal" Spacing="2">
                  <Button Content="↑" Click="OnMoveUpClick" />
                  <Button Content="↓" Click="OnMoveDownClick" />
                  <Button Content="✕" Click="OnRemoveStepClick" />
                </StackPanel>
              </Grid>
            </DataTemplate>
          </ItemsControl.ItemTemplate>
        </ItemsControl>
        <StackPanel Orientation="Horizontal" Spacing="8">
          <Button Content="Add row" Click="OnAddRowClick" />
          <Button Content="Clear all" Click="OnClearAllClick" />
        </StackPanel>
      </StackPanel>

      <!-- Global -->
      <CheckBox Content="Randomized jitter (timing + position)" IsChecked="{Binding JitterEnabled}" />
      <CheckBox Content="Stop after N clicks" IsChecked="{Binding HasClickLimit}" />
      <NumericUpDown Minimum="1" Maximum="1000000" Increment="1" FormatString="0"
                     Value="{Binding ClickLimit}" IsEnabled="{Binding HasClickLimit}" />

      <!-- Hotkeys -->
      <TextBlock Text="Start/Stop hotkey" FontWeight="Bold" />
      <StackPanel Orientation="Horizontal" Spacing="8">
        <TextBlock Text="{Binding StartStopText}" VerticalAlignment="Center" MinWidth="160" />
        <Button Content="Set…" Click="OnRecordStartStopClick" />
      </StackPanel>
      <TextBlock Text="Capture-position hotkey" FontWeight="Bold" />
      <StackPanel Orientation="Horizontal" Spacing="8">
        <TextBlock Text="{Binding CaptureText}" VerticalAlignment="Center" MinWidth="160" />
        <Button Content="Set…" Click="OnRecordCaptureClick" />
      </StackPanel>

      <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <TextBlock Text="{Binding Status}" VerticalAlignment="Center" />
        <Button Content="Save" Click="OnSaveClick" />
      </StackPanel>
    </StackPanel>
  </ScrollViewer>
</Window>
```

- [ ] **Step 2: Replace the window code-behind**

Replace `src/AutoClicker.App/Settings/SettingsWindow.axaml.cs`:
```csharp
using AutoClicker.App.Tray;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace AutoClicker.App.Settings;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel = null!;

    public SettingsWindow() => AvaloniaXamlLoader.Load(this);

    public SettingsWindow(TrayAppController controller) : this()
    {
        _viewModel = new SettingsViewModel(controller);
        DataContext = _viewModel;
    }

    private static SequenceStepViewModel? RowOf(object? sender) =>
        (sender as Control)?.DataContext as SequenceStepViewModel;

    private void OnAddRowClick(object? sender, RoutedEventArgs e) => _viewModel.AddBlankStep();
    private void OnClearAllClick(object? sender, RoutedEventArgs e) => _viewModel.ClearSteps();
    private void OnRemoveStepClick(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row) _viewModel.RemoveStep(row);
    }
    private void OnMoveUpClick(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row) _viewModel.MoveStepUp(row);
    }
    private void OnMoveDownClick(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row) _viewModel.MoveStepDown(row);
    }

    private void OnRecordStartStopClick(object? sender, RoutedEventArgs e) => _viewModel.RecordStartStop();
    private void OnRecordCaptureClick(object? sender, RoutedEventArgs e) => _viewModel.RecordCapture();

    private void OnSaveClick(object? sender, RoutedEventArgs e) => _viewModel.Save();
}
```

- [ ] **Step 3: Build the whole solution (now restored)**

Run: `dotnet build`
Expected: `Build succeeded`, 0 errors. (Fix any remaining `KeyCode` name mismatches from Task 5 Step 2 if they surface.)

- [ ] **Step 4: Run the Core suite (regression)**

Run: `dotnet test tests/AutoClicker.Core.Tests`
Expected: PASS (all Core tests).

- [ ] **Step 5: Launch smoke (process + window appears)**

Run (PowerShell):
```
$exe = "src/AutoClicker.App/bin/Debug/net10.0/AutoClicker.App.exe"
$p = Start-Process -FilePath $exe -PassThru; Start-Sleep -Seconds 5
$p.Refresh(); if ($p.HasExited) { "EXITED $($p.ExitCode)" } else { "RUNNING title=$($p.MainWindowTitle)"; Stop-Process -Id $p.Id -Force }
```
Expected: `RUNNING title=Auto Clicker — Settings` (no early exit).

- [ ] **Step 6: Commit**

```
git add src/AutoClicker.App/Settings/SettingsWindow.axaml src/AutoClicker.App/Settings/SettingsWindow.axaml.cs
git commit -m "feat(app): settings window with sequence editor and recordable hotkey rows"
```

---

## Task 11: README + final verification

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Update the README feature + smoke-test sections**

In `README.md`, under the feature description, add hotkey-combo + sequence notes, and replace the
manual smoke-test list with:
```markdown
## Manual smoke test (run on each OS)

1. Launch the app; the Settings window appears and the tray icon shows (Win11: overflow `^`).
2. Tray right-click menu shows only **Settings… / Quit** (no Start/Stop).
3. **Hotkey combo:** Settings → Start/Stop → **Set…**, press **Ctrl+F6**; the row shows `Ctrl+F6`.
   Save. Confirm Ctrl+F6 toggles clicking and bare F6 no longer does. Hold the combo — it must not
   rapidly flip on/off (auto-repeat guard).
4. **Esc cancels:** click **Set…**, press **Esc** — the previous combo is kept.
5. **Capture sequence:** set a capture hotkey (default **F8**). Hover target A, press F8; hover
   target B, press F8. Settings switches to **Sequence** mode with two rows showing real X/Y
   (numbers fully visible, e.g. `1388`).
6. **Run sequence:** start (your start/stop hotkey). The cursor visibly moves A → click → wait →
   B → click → wait → loops. Per-row Button/Type/Delay take effect.
7. **Edit:** reorder with ↑/↓, remove with ✕, **Add row** / **Clear all** work; Save persists.
8. **Duplicate guard:** try recording the capture combo equal to start/stop — it's rejected.
9. Quit from the tray — process exits, icon disappears.
```

- [ ] **Step 2: Commit**

```
git add README.md
git commit -m "docs: hotkey-combo and sequence smoke-test guide"
```

- [ ] **Step 3: Final verification — clean build + tests + publish**

Run:
```
dotnet build -c Release
dotnet test -c Release
dotnet publish src/AutoClicker.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```
Expected: build succeeds; all Core tests pass; publish produces
`src/AutoClicker.App/bin/Release/net10.0/win-x64/publish/AutoClicker.App.exe`. Launch it and walk
the README smoke test for this OS.

- [ ] **Step 4: Commit any final tweaks (if needed)**

```
git add -A
git commit -m "chore: finalize hotkeys + sequences"
```

---

## Self-Review notes (reconciled against the spec)

- **Spec coverage:** hotkey record + recording (Tasks 1,7,9,10) · exact-match + L/R unified +
  auto-repeat + no-duplicates (Tasks 5,7,9) · capture appends step + switch to Sequence (Task 8) ·
  Sequence mode per-position action/delay, loop, cursor moves (Tasks 2,6) · two hotkeys default
  F6/F8 (Tasks 2,8) · UI: wide coord fields, removed capture button, Add/Clear aligned, mode
  sections, hotkey rows (Task 10) · tray menu Settings+Quit (Task 8) · config back-compat (Task 3).
- **Placeholder scan:** none — every code step has full content; the only judgment call is the
  Task 5 `KeyCode` spellings, with an explicit build-check step to correct any that differ.
- **Type consistency:** `Hotkey(KeyId, bool…)`, `SequenceStep{X,Y,Button,ClickType,DelayMs}`,
  `ClickAction(DelayMs,X,Y,Button,Type)`, `ClickPlanner.Next(AppSettings,int) → (ClickAction,int)?`,
  `IGlobalInputService` (HotkeyPressed/CaptureRequested/SetStartStopHotkey/SetCaptureHotkey/
  BeginRecording/CancelRecording/GetCursorPosition), `ViewModelBase.Set → bool` + `OnChanged` are
  used identically across tasks.
```
