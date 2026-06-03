# Auto Clicker — Design

**Date:** 2026-06-03
**Status:** Approved (pending written-spec review)
**Author:** Alexandros Karayiannis (with Claude)

## Purpose

A cross-platform (Windows, macOS, Linux) system-tray auto clicker for **personal use** on the
author's own devices. Lives in the tray/menu bar, is driven primarily by a configurable global
hotkey, and injects synthetic mouse clicks with configurable rate, button, click type, limit,
jitter, and target position.

## Goals

- Single codebase that runs on Windows, macOS, and Linux (X11).
- Self-contained, single-file executable per OS.
- Configurable clicking behavior, persisted between runs.
- Core logic decoupled from OS-specific input so it can be unit-tested without real clicks.

## Non-Goals (YAGNI)

- No click sequences, macros, or recording/playback.
- No multiple saved profiles.
- No scripting language or plugins.
- No Wayland-native support (see Platform Notes).

## Tech Stack

| Concern | Choice | Why |
|---|---|---|
| Language / runtime | **C# / .NET 8** | Author preference (C# or C++); easiest path here. |
| Tray + Settings UI | **Avalonia UI** | Cross-platform desktop UI with tray-icon support. |
| Global hotkey + synthetic input | **SharpHook** (wraps `libuiohook`) | One cross-platform library solves the two hardest problems (global hooks **and** event simulation) on all three OSes. |
| Config persistence | `System.Text.Json` → file in user config dir | No extra deps. |
| Tests | **xUnit** | Standard .NET test framework. |

**Rejected alternatives:** C++/Qt (would require wiring three native input APIs —
`SendInput` / `CGEventPost` / `XTest` — plus a heavier Qt/CMake per-OS build); Python/Rust
(author wanted C#/C++).

## Architecture

Layered so the core engine has **no OS dependencies** — it talks only to interfaces, which are
implemented by SharpHook adapters at the edge.

```
┌───────────────────────────────────────────────┐
│  Avalonia Tray UI  (tray menu + Settings win)  │  thin presentation layer
├───────────────────────────────────────────────┤
│  ClickEngine  (loop, rate, jitter, limit,       │  pure logic — unit tested
│               target selection)                 │
│     depends on ↓ interfaces                      │
│     IInputSimulator      IHotkeyService         │
├───────────────────────────────────────────────┤
│  SharpHookInputSimulator   SharpHookHotkeyService│  OS glue (SharpHook)
│  ConfigStore (JSON in user config dir)          │
└───────────────────────────────────────────────┘
```

### Components

Each unit has one clear job, a well-defined interface, and explicit dependencies.

- **`ClickEngine`** — owns the background click loop. On each iteration it computes the delay
  (`interval ± jitter`), determines the click position (live cursor *or* saved fixed coordinate),
  invokes `IInputSimulator.Click`, increments the click counter, and auto-stops when the limit is
  reached. Depends only on `IInputSimulator`, an injectable clock/delay abstraction, and the current
  `AppSettings`. Contains **no** OS calls → fully unit-testable.
  - Public surface: `Start()`, `Stop()`, `IsRunning`, `ClicksPerformed`, event `Stopped`.
- **`IInputSimulator`** — abstraction over real mouse input.
  - Methods: `MoveTo(int x, int y)`, `Click(MouseButton button, ClickType type)`,
    `(int x, int y) GetCursorPosition()`.
  - Real impl: `SharpHookInputSimulator` (uses SharpHook `EventSimulator`).
  - Test impl: `FakeInputSimulator` (records calls, no real input).
- **`IHotkeyService`** — abstraction over global hotkey registration.
  - Registers the configured start/stop key; raises `HotkeyPressed` event; supports re-registration
    when the hotkey setting changes.
  - Real impl: `SharpHookHotkeyService` (global hook via SharpHook). Test impl: fake.
- **`AppSettings`** — plain settings model (see Settings below).
- **`ConfigStore`** — `Load()` / `Save(AppSettings)` to a JSON file at the cross-platform user
  config path:
  - Windows: `%APPDATA%\AutoClicker\config.json`
  - macOS: `~/Library/Application Support/AutoClicker/config.json`
  - Linux: `~/.config/AutoClicker/config.json`
  - Resolved via `Environment.SpecialFolder.ApplicationData` (works on all three).
  - Missing/corrupt file → falls back to defaults and rewrites.
- **Avalonia UI**
  - Tray icon with context menu: **Start/Stop** (reflects engine state), **Settings…**, **Quit**.
  - Settings window data-bound to `AppSettings`; validates input; on save persists via `ConfigStore`,
    pushes new settings into `ClickEngine`, and re-registers the hotkey if it changed.
- **App composition root** — wires UI + engine + adapters + config; manages lifecycle/shutdown
  (stop engine, dispose SharpHook hooks).

## Settings

Persisted between runs. All fields from the agreed capability set:

| Setting | Type | Default | Notes |
|---|---|---|---|
| `IntervalMs` | int (ms) | 100 (≈10 CPS) | UI lets the user enter CPS or ms; stored as ms. Clamped ≥ 1. |
| `Button` | enum {Left, Middle, Right} | Left | |
| `ClickType` | enum {Single, Double} | Single | |
| `ClickLimit` | int? (null = unlimited) | null | Auto-stop after N clicks. |
| `JitterEnabled` | bool | false | When on, applies timing jitter (± up to ~15% of interval) and a small position wobble (± a few px). |
| `TargetMode` | enum {CurrentCursor, FixedCoordinate} | CurrentCursor | |
| `FixedX`, `FixedY` | int | 0,0 | Used only in FixedCoordinate mode; set via **Capture current position** button. |
| `Hotkey` | key code | F6 | Global start/stop toggle; configurable. |

## Data Flow

1. **Hotkey toggle:** OS key event → `SharpHookHotkeyService` matches configured key → raises
   `HotkeyPressed` → composition root toggles `ClickEngine.Start()`/`Stop()` → tray menu updates.
2. **Click loop:** `ClickEngine` running → compute delay (`IntervalMs` ± jitter) → wait → resolve
   position (`GetCursorPosition()` or `FixedX/Y`, ± jitter wobble) → `IInputSimulator.Click(button,
   type)` → increment count → if `ClickLimit` reached → `Stop()` and raise `Stopped`.
3. **Settings change:** Settings window save → validate → `ConfigStore.Save` → update engine's
   settings → re-register hotkey if changed.

## Error Handling

- **Missing OS permissions** (macOS Accessibility/Input Monitoring; Linux input access): detect on
  startup (e.g. SharpHook init failure / no events) → show a tray notification or dialog with
  guidance on granting permission. Do not crash silently.
- **Hotkey registration failure / conflict:** surface a clear message; keep app running so the user
  can pick another key.
- **Invalid settings** (interval ≤ 0, negative limit/coords): validate and clamp in the Settings UI
  before save.
- **Engine loop exceptions:** caught inside the loop → engine stops cleanly, raises `Stopped`, and
  notifies via tray rather than tearing down the app.

## Testing Strategy

- **TDD for `ClickEngine`** using `FakeInputSimulator` + an injectable clock/delay:
  - Correct number of clicks at a given interval over simulated time.
  - Jitter stays within configured bounds (timing and position).
  - Click limit stops the engine at exactly N and raises `Stopped`.
  - Target selection: current-cursor vs fixed-coordinate produces the expected `MoveTo`/position.
  - Start/Stop idempotency and state (`IsRunning`).
- **`ConfigStore`**: save → load round-trip; missing-file and corrupt-file fall back to defaults.
- **Hotkey re-registration** logic (with fake `IHotkeyService`): changing the hotkey unregisters the
  old and registers the new.
- **Per-OS manual smoke test** (cannot be unit-tested — involves real input): documented checklist
  in the README covering real clicks, the global hotkey, capture-position, and the permission flow
  on each OS.

## Platform Notes

- **macOS:** App must be granted **Accessibility** and **Input Monitoring** in System Settings →
  Privacy & Security for synthetic input and global hooks to work. Document this in the README.
- **Linux:** Targets **X11**. Under **Wayland**, global input hooks and synthetic input are
  sandboxed by the compositor and are unsupported/unreliable; XWayland may work for some setups.
  Document the X11 requirement.
- **Windows:** Works without special permissions for ordinary apps. (Injecting into apps running as
  Administrator may require the clicker to run elevated — note in README.)

## Build & Distribution

- `dotnet publish` per runtime identifier (`win-x64`, `osx-x64`, `osx-arm64`, `linux-x64`) with
  `--self-contained` + single-file, producing one standalone binary per device. No .NET install
  required on target machines.

## Repository Layout (proposed)

```
auto-clicker/
  src/
    AutoClicker.Core/        # ClickEngine, interfaces, AppSettings, ConfigStore  (no OS deps)
    AutoClicker.App/         # Avalonia UI + SharpHook adapters + composition root
  tests/
    AutoClicker.Core.Tests/  # xUnit unit tests
  docs/superpowers/specs/    # this design doc
  README.md                  # usage + per-OS permission/build notes
```
