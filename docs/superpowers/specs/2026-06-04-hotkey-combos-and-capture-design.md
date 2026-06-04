# Hotkey Combos + Capture Hotkey + Position Sequences — Design

**Date:** 2026-06-04
**Status:** Approved (pending written-spec review)
**Author:** Alexandros Karayiannis (with Claude)
**Builds on:** `2026-06-03-auto-clicker-design.md`

## Problems being solved

1. **Capture button is useless** — it reads the last mouse-move position, which is the button
   itself, so it can never capture a real on-screen target.
2. **Hotkeys are bare F-keys only** — user wants modifier combos (`Ctrl+F6`, `Shift+Alt+~`).
3. **Coordinate fields truncate** — a value like `1388` shows as `13…` (fields too narrow).
4. **Capture button misaligned** — sits inline, not on its own line.
5. **Tray menu has Start** — starting from the menu is nonsensical (with current-cursor mode the
   first click lands on the menu) and redundant with the hotkey.
6. **No multi-position automation** — user wants the mouse to move through N positions, performing a
   per-position action with a per-position delay, looping.

## Solution overview

- Replace the bare `HotKey` enum with a **`Hotkey` record** (base key + modifiers), set by
  **recording** the pressed combo.
- Add a **capture hotkey** that **appends the live cursor position** as a step in a **position
  sequence**.
- Replace single `FixedCoordinate` targeting with a **`Sequence`** of steps: each step has its own
  position, button, click type, and delay. The engine moves → clicks → waits → next → loops.
- Fix the coordinate width + alignment; remove the broken capture button; drop Start/Stop from the
  tray menu.

## Decisions (locked)

- **Hotkeys set by recording**; **Esc** cancels; **no duplicate** hotkeys.
- **Modifiers:** Ctrl, Shift, Alt, Meta (Win/⌘); left/right unified.
- **Exact-match** modifiers; **fire once per physical press** (ignore OS auto-repeat, re-arm on
  release); a hotkey needs a real (non-modifier) base key.
- **Defaults:** Start/Stop = `F6`, Capture = `F8`.
- **Target modes:** `CurrentCursor` or `Sequence` (old `FixedCoordinate` removed; a single fixed
  point is a 1-step sequence).
- **Capture hotkey appends** the current cursor position to the sequence and switches to
  `Sequence` mode, then saves and refreshes the UI.
- **Per-position action** (button + single/double) and **per-position delay**.
- **Sequence loops** until stopped or the click limit is reached; each position-action = 1 click.
- **Cursor visibly moves** to each position before clicking.
- **Tray menu:** `Settings… | Quit` only.
- **Config reset on upgrade:** the schema changes, so an old `config.json` falls back to defaults
  (no crash). Acceptable for personal use.

## Core model (OS-free)

- **`KeyId` enum (new):** `None`, `F1`–`F12`, `A`–`Z`, `D0`–`D9`, `Backquote`, `Minus`, `Equals`,
  `LeftBracket`, `RightBracket`, `Semicolon`, `Quote`, `Comma`, `Period`, `Slash`, `Backslash`,
  `Space`, `Enter`, `Tab`, `Escape`, `Insert`, `Delete`, `Home`, `End`, `PageUp`, `PageDown`,
  `Up`, `Down`, `Left`, `Right`.
- **`Hotkey` record (new):** `record Hotkey(KeyId Key, bool Ctrl, bool Shift, bool Alt, bool Meta)`.
  - `IsValid` ⇒ `Key != None`; `static Hotkey None`.
  - `ToDisplayString()` ⇒ canonical `Ctrl+Shift+Alt+Win+<key>`; key symbols: `Backquote`→`~`,
    `Escape`→`Esc`, `Space`→`Space`, else the name. E.g. `F6`, `Ctrl+F6`, `Shift+Alt+~`.
  - Value equality drives exact matching + duplicate detection.
- **`SequenceStep` (new):** `{ int X; int Y; ClickButton Button = Left; ClickType ClickType =
  Single; int DelayMs = 100 }`. `DelayMs` is the wait *before* this step's click (in a loop this is
  the inter-action delay).
- **`ClickAction` (new):** `readonly record struct ClickAction(int DelayMs, int? X, int? Y,
  ClickButton Button, ClickType Type)` — what the engine performs in one tick (null X/Y = current
  cursor).
- **`AppSettings` changes:**
  - Keep `IntervalMs`, `Button`, `ClickType`, `JitterEnabled`, `ClickLimit` (Button/ClickType/
    IntervalMs apply to **CurrentCursor** mode).
  - `TargetMode` now `{ CurrentCursor, Sequence }`.
  - Remove `FixedX`, `FixedY`; add `List<SequenceStep> Sequence` (default empty).
  - Remove `HotKey Hotkey`; add `Hotkey StartStopHotkey` (default `F6`), `Hotkey CaptureHotkey`
    (default `F8`).
  - `Clone()` deep-copies the sequence list.
- **`ClickPlanner` refactor:** pure, holds no state.
  - `int ComputeDelayMs(int baseMs, bool jitter)` and `(int x,int y) JitterPosition(int x,int y)`
    (helpers).
  - `(ClickAction Action, int NextIndex)? Next(AppSettings s, int index)`:
    - `CurrentCursor` ⇒ `(new ClickAction(delay(IntervalMs), null, null, s.Button, s.ClickType), 0)`.
    - `Sequence` ⇒ if empty return `null`; else step = `s.Sequence[index % count]`,
      `delay = ComputeDelayMs(step.DelayMs, jitter)`, pos = step.X/Y (± jitter), action uses step's
      button/type; `NextIndex = (i+1) % count`.
- **`ClickEngine` change:** keep a `_sequenceIndex`; each loop iteration calls
  `planner.Next(settings, _sequenceIndex)`; `null` ⇒ stop (empty sequence); else
  `await clock.Delay(action.DelayMs)`, `input.Click(action.X, action.Y, action.Button,
  action.Type)`, `count++`, advance index. Click-limit logic unchanged.

## Global input service

```csharp
public interface IGlobalInputService : IDisposable
{
    event EventHandler? HotkeyPressed;       // start/stop combo matched
    event EventHandler? CaptureRequested;    // capture combo matched
    void Start();
    void SetStartStopHotkey(Hotkey hotkey);
    void SetCaptureHotkey(Hotkey hotkey);
    void BeginRecording(Action<Hotkey> onRecorded);
    void CancelRecording();
    (int X, int Y) GetCursorPosition();
}
```

**SharpHook adapter:** subscribe to `KeyPressed` + `KeyReleased`. On `KeyPressed`: map
`KeyCode → KeyId`; ignore pure modifier keys; build `Hotkey` from the event mask (Ctrl/Shift/Alt/
Meta, L/R OR-ed) + base key. Then (1) auto-repeat guard via a held-keys set, (2) if recording →
callback (bare `Escape` cancels) and return, (3) exact-match start/stop → `HotkeyPressed`, else
capture → `CaptureRequested`. On `KeyReleased`: clear held flag. `SharpHookMappings` holds the
`KeyId ↔ KeyCode` table, `EventMask → modifiers`, and an is-modifier check.

**SharpHook input simulator:** for a coordinate click, `SimulateMouseMovement(x, y)` first (visible
move), then press/release; current-cursor clicks press/release in place. Double = twice.

## Tray + controller

- **Tray menu:** `Settings… | Quit` (no Start/Stop). Remove the toggle item and its label updates.
- **`TrayAppController`:**
  - Register both hotkeys on startup and on every settings apply.
  - On `CaptureRequested` (marshalled to UI thread): read `GetCursorPosition()`, append a
    `SequenceStep` (default action/delay), set `TargetMode = Sequence`, save, push to engine, raise
    a `SettingsChanged` event so an open Settings window refreshes.
  - `BeginRecordHotkey(Action<Hotkey>)` / `CancelRecording()` passthroughs for the VM.

## Settings UI

- **Mode** selector: Current cursor / Sequence (toggles which section is visible).
- **Current-cursor section:** Interval (ms), Button, Click type.
- **Sequence section:** an `ItemsControl`/`ListBox` over an `ObservableCollection` of step rows;
  each row: `X`, `Y`, `Button`, `Type`, `Delay(ms)` with **adequately wide** numeric fields
  (no grouping separators, MinWidth sized for ≥5 digits), plus per-row **↑ / ↓ / ✕**. Below the
  list, on **their own aligned line**: **Add row** (blank step) and **Clear all**. A hint:
  “Press the capture hotkey to add the position under your cursor.”
- **Global section:** Jitter, Click limit (checkbox + count).
- **Hotkeys section:** two rows (Start/Stop, Capture), each = label + current combo text + **Set…**
  button. While recording, that button reads **“Press keys… (Esc to cancel)”** and is the only
  active one; a duplicate shows an inline status message.
- **Save** persists everything and re-registers both hotkeys. Capture-triggered changes update the
  bound collection live.

## Data flow

1. **Record hotkey:** Set… → `controller.BeginRecordHotkey(cb)` → adapter records next combo →
   callback (UI thread) → row updates → Save persists + re-registers.
2. **Capture/build sequence:** hover target → press capture hotkey → adapter `CaptureRequested` →
   controller appends step + mode=Sequence + save + engine update + UI refresh.
3. **Run:** engine loops via `ClickPlanner.Next`; CurrentCursor clicks in place; Sequence moves →
   clicks → waits → advances → wraps.

## Error handling

- Unsupported recorded key (no `KeyId`) ⇒ ignored, keep listening; **Esc** cancels (restore prior).
- Duplicate combo ⇒ rejected, inline message, prior kept.
- Invalid hotkey (`Key == None`) ⇒ never matches.
- `Sequence` mode with empty list ⇒ engine stops immediately (and Save is allowed; nothing to run).
- Capture pressed while running ⇒ allowed; new step picked up next loop.
- Auto-repeat ⇒ suppressed by the held-keys guard.

## Testing

- **TDD (Core unit):**
  - `Hotkey`: equality (key/modifier differences), `ToDisplayString()` (`F6`, `Ctrl+F6`,
    `Shift+Alt+~`, `Esc`), `IsValid`.
  - `ClickPlanner.Next`: CurrentCursor action (null coords, global button/type, delay/jitter);
    Sequence cycling (index wrap), per-step button/type/delay, position jitter bounds; empty
    sequence ⇒ `null`.
  - `ClickEngine`: sequence of N steps loops and counts; click-limit stops across steps; empty
    sequence stops; current-cursor unchanged.
  - `AppSettings` defaults: `TargetMode == CurrentCursor`, empty `Sequence`,
    `StartStopHotkey == F6`, `CaptureHotkey == F8`; `Clone()` deep-copies the sequence.
  - `ConfigStore`: round-trip settings incl. sequence + hotkeys; legacy/unknown config ⇒ defaults
    without throwing.
- **Manual smoke test (README):** record combos incl. `~`/modifiers; auto-repeat doesn’t flicker;
  capture builds a multi-step sequence at real targets; sequence runs (move→click→wait→loop) with
  per-step actions/delays; reorder/remove/clear; duplicate rejection; Esc cancels; tray menu has no
  Start/Stop.

## Files touched

- **Core:** `Enums.cs` (remove `HotKey`; `TargetMode` → {CurrentCursor, Sequence}; add `KeyId`),
  `Hotkey.cs` (new), `SequenceStep.cs` (new), `ClickAction.cs` (new), `AppSettings.cs`,
  `ClickPlanner.cs`, `ClickEngine.cs`, `IGlobalInputService.cs`.
- **App:** `Adapters/SharpHookMappings.cs`, `Adapters/SharpHookGlobalInputService.cs`,
  `Adapters/SharpHookInputSimulator.cs`, `Tray/TrayAppController.cs`, `App.axaml.cs`,
  `Settings/SettingsViewModel.cs`, `Settings/SequenceStepViewModel.cs` (new),
  `Settings/SettingsWindow.axaml(.cs)`.
- **Tests:** `HotkeyTests.cs` (new), `ClickPlannerTests.cs`, `ClickEngineTests.cs`,
  `AppSettingsTests.cs`, `ConfigStoreTests.cs`.
- `README.md`: smoke-test + feature notes.

## Out of scope (YAGNI)

Saved/named profiles; importing/exporting sequences; keyboard keystrokes within a sequence (mouse
only); arbitrary multi-key chords beyond modifiers; on-screen recording overlay.
