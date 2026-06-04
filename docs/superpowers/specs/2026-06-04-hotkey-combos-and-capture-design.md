# Hotkey Combinations + Capture-Position Hotkey — Design

**Date:** 2026-06-04
**Status:** Approved (pending written-spec review)
**Author:** Alexandros Karayiannis (with Claude)
**Builds on:** `2026-06-03-auto-clicker-design.md`

## Problem

1. The in-window **Capture position** button captures the cursor *over the button* (it reads the
   last mouse-move position, which is the button itself), so it can't capture a real target.
2. Hotkeys are limited to bare F1–F12. The user wants **modifier combinations** (e.g. `Ctrl+F6`,
   `Shift+Alt+~`).

## Solution Overview

- Replace the bare-key `HotKey` enum with a **`Hotkey` record** = a base key + modifier flags.
- Add a **second configurable hotkey** to capture the cursor position from anywhere on screen.
- Set hotkeys by **recording** the physically-pressed combo.
- Pressing the capture hotkey stores the live cursor position, switches the target to
  **Fixed coordinate**, and saves.

## Decisions (locked)

- **Set via recording:** "Set…" button listens for the next combo; **Esc** cancels.
- **Capture action:** store `FixedX/Y` + switch `TargetMode = FixedCoordinate` + save + refresh UI.
- **Modifiers supported:** Ctrl, Shift, Alt, Meta (Win/⌘). Left/right variants are unified.
- **Matching is EXACT:** held modifiers must equal the configured set (bare `F6` does not fire while
  Ctrl is held; `Ctrl+F6` does not fire as bare `F6`).
- **Fire once per physical press:** ignore OS auto-repeat; re-arm on key release (prevents a held
  hotkey from rapidly toggling).
- **A hotkey needs a real base key** (can't bind "just Ctrl"); recording ignores modifier-only keys.
- **No duplicate hotkeys:** recording a combo equal to the other hotkey is rejected with a message.
- **Bare keys allowed:** a bare non-F key (e.g. "A") is permitted even though it fires while typing —
  the user's responsibility for a personal tool.
- **Defaults:** Start/Stop = `F6`, Capture = `F8`.

## Architecture

Same layering as the base app: pure OS-free `AutoClicker.Core` (model + matching value-equality)
behind interfaces; `AutoClicker.App` SharpHook adapter translates raw events ↔ the model.

### Core changes

- **`KeyId` enum (new):** curated key set — `None`, `F1`–`F12`, `A`–`Z`, `D0`–`D9`, `Backquote`
  (`` ` ``/`~`), `Minus`, `Equals`, `LeftBracket`, `RightBracket`, `Semicolon`, `Quote`, `Comma`,
  `Period`, `Slash`, `Backslash`, `Space`, `Enter`, `Tab`, `Escape`, `Insert`, `Delete`, `Home`,
  `End`, `PageUp`, `PageDown`, `Up`, `Down`, `Left`, `Right`.
- **`Hotkey` record (new):** `record Hotkey(KeyId Key, bool Ctrl, bool Shift, bool Alt, bool Meta)`.
  - `IsValid` ⇒ `Key != KeyId.None`.
  - `ToDisplayString()` ⇒ modifiers in canonical order `Ctrl+Shift+Alt+Win+` then the key symbol
    (`Backquote`→`~`, `Escape`→`Esc`, `Space`→`Space`, letters/digits/F-keys as-is). Examples:
    `F6` → `"F6"`, `Ctrl+F6` → `"Ctrl+F6"`, `Shift+Alt+Backquote` → `"Shift+Alt+~"`.
  - Equality is the record's value equality (used for exact matching and duplicate detection).
  - `static Hotkey None { get; }` ⇒ `(KeyId.None, false, false, false, false)`.
- **`AppSettings`:** remove `HotKey Hotkey`; add `Hotkey StartStopHotkey` (default `F6`) and
  `Hotkey CaptureHotkey` (default `F8`). Remove the old `HotKey` enum.
- **Back-compat:** an old `config.json` containing `"Hotkey":"F6"` deserializes with the new
  hotkey properties absent ⇒ they take their defaults. No crash, no corruption.

### `IGlobalInputService` (interface) changes

```csharp
public interface IGlobalInputService : IDisposable
{
    event EventHandler? HotkeyPressed;      // start/stop combo matched
    event EventHandler? CaptureRequested;   // capture combo matched   (NEW)

    void Start();
    void SetStartStopHotkey(Hotkey hotkey); // replaces SetHotkey(HotKey)
    void SetCaptureHotkey(Hotkey hotkey);   // NEW

    void BeginRecording(Action<Hotkey> onRecorded); // NEW — capture next combo
    void CancelRecording();                          // NEW

    (int X, int Y) GetCursorPosition();
}
```

### SharpHook adapter (`SharpHookGlobalInputService`) changes

- Subscribe to `KeyPressed` **and** `KeyReleased`.
- On `KeyPressed`: map `KeyCode → KeyId`. If the key is a pure modifier (Ctrl/Shift/Alt/Meta), ignore.
  Otherwise build a `Hotkey` from the event mask (`EventMask` → Ctrl/Shift/Alt/Meta, L/R OR-ed) plus
  the base `KeyId`, then:
  1. **Auto-repeat guard:** if this same base key is already marked held, ignore. Mark held.
  2. **Recording:** if recording, invoke the callback with the built `Hotkey` (unless it's bare
     `Escape` ⇒ cancel) and stop; do not match.
  3. **Match:** if it equals the start/stop hotkey ⇒ raise `HotkeyPressed`; else if it equals the
     capture hotkey ⇒ raise `CaptureRequested`.
- On `KeyReleased`: clear the held flag for that base key (re-arm).
- `SharpHookMappings`: a single `KeyId ↔ KeyCode` table (used both directions) + an
  `EventMask → (Ctrl,Shift,Alt,Meta)` helper + a `KeyCode`→is-modifier check.

### `TrayAppController` changes

- Subscribe to `CaptureRequested` → on the UI thread: read `GetCursorPosition()`, update
  `Settings` (`FixedX/Y`, `TargetMode = FixedCoordinate`), `ConfigStore.Save`, push to engine,
  and raise an event so an open Settings window refreshes.
- Register **both** hotkeys on startup and whenever settings are applied.
- Expose `BeginRecordHotkey(Action<Hotkey>)` / `CancelRecording()` passthrough for the Settings VM.

### Settings UI changes

- Remove the single hotkey dropdown **and** the broken **Capture position** button.
- Add **two hotkey rows**, each: a label, the current combo text (`ToDisplayString()`), and a
  **Set…** button. While recording, the button shows **"Press keys… (Esc to cancel)"** and is the
  only one active. Recorded combo updates the field; a duplicate shows an inline status message.
- Keep the manual `FixedX`/`FixedY` numeric fields.
- Saving persists both hotkeys (plus existing settings) and re-registers them.

## Data Flow

1. **Record:** Settings "Set…" → `controller.BeginRecordHotkey(cb)` → adapter listens → user presses
   combo → adapter builds `Hotkey` → callback (marshalled to UI thread) → VM updates that row;
   on Save → persisted + re-registered.
2. **Capture:** user hovers target, presses capture hotkey → adapter raises `CaptureRequested` →
   controller reads cursor pos, sets `FixedX/Y` + `FixedCoordinate`, saves, pushes to engine,
   refreshes open Settings window.
3. **Start/Stop:** as before, but matching now includes exact modifiers.

## Error Handling

- **Unsupported recorded key** (KeyCode not in the map) ⇒ ignored; keep listening.
- **Esc during recording** ⇒ cancel, restore the previous combo.
- **Duplicate combo** ⇒ rejected with an inline message; previous value kept.
- **Invalid hotkey** (`Key == None`) ⇒ never matches (guards against an empty/legacy value).
- **Capture pressed while running** ⇒ allowed; engine picks up new target next iteration.

## Testing

- **TDD (Core, unit):**
  - `Hotkey` equality: same combo equal; differing key or any differing modifier not equal.
  - `Hotkey.ToDisplayString()`: `F6`, `Ctrl+F6`, `Shift+Alt+~`, `Esc`, bare letter.
  - `Hotkey.IsValid`: `None` invalid; a real key valid.
  - `AppSettings` defaults: `StartStopHotkey == F6`, `CaptureHotkey == F8`.
  - `ConfigStore`: round-trip both hotkeys; an old-style config (no hotkey fields / legacy `Hotkey`)
    loads to defaults without throwing.
- **Manual smoke test (App, documented in README):** record a combo for each hotkey; `~` and a
  modifier combo work; auto-repeat doesn't flicker; capture hotkey grabs the real target position
  and switches to Fixed; duplicate rejection; Esc cancels.

## Files Touched

- Core: `Enums.cs` (remove `HotKey`, add `KeyId`), `Hotkey.cs` (new), `AppSettings.cs`,
  `IGlobalInputService.cs`.
- App: `Adapters/SharpHookMappings.cs`, `Adapters/SharpHookGlobalInputService.cs`,
  `Tray/TrayAppController.cs`, `Settings/SettingsViewModel.cs`, `Settings/SettingsWindow.axaml(.cs)`.
- Tests: `HotkeyTests.cs` (new), update `AppSettingsTests.cs`, `ConfigStoreTests.cs`.
- `README.md`: smoke-test additions.

## Out of Scope (YAGNI)

Multi-key chords beyond modifiers; per-profile hotkeys; remappable click-action hotkeys other than
start/stop and capture; on-screen overlay while recording.
