# Auto Clicker

A cross-platform (Windows, macOS, Linux/X11) system-tray auto clicker for personal use.
Configurable rate, mouse button, single/double click, click limit, and jitter. Two targeting
modes: **Current cursor**, or a **position sequence** (move → click → wait → next → loop) where each
step has its own button, click type, and wait interval. Two global hotkeys, each set by **recording**
a key combo (Ctrl/Shift/Alt/Win supported): a **start/stop** hotkey (default **F6**) and a
**capture-position** hotkey (default **F8**) that appends the spot under your cursor to the sequence.

Built with **.NET 10**, **Avalonia 12** (tray + settings UI), and **SharpHook 7**
(global hotkey + synthetic clicks). The OS-independent core (`AutoClicker.Core`) is unit-tested;
the OS input layer is verified by the manual smoke test below.

## Run (development)

    dotnet run --project src/AutoClicker.App

On launch the Settings window opens; the app also lives in the tray/menu-bar. The tray menu gives
only **Settings…** and **Quit**. Press the start/stop hotkey (default **F6**) to toggle clicking, and
the capture hotkey (default **F8**) to add the cursor position to a sequence.

## Build a standalone binary (per device)

    # Windows
    dotnet publish src/AutoClicker.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
    # macOS (Apple Silicon -> osx-arm64, Intel -> osx-x64)
    dotnet publish src/AutoClicker.App -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true
    # Linux (X11)
    dotnet publish src/AutoClicker.App -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true

The binary is under `src/AutoClicker.App/bin/Release/net10.0/<rid>/publish/`.

## Permissions / platform notes

- **macOS:** Grant the app **Accessibility** AND **Input Monitoring** in
  System Settings → Privacy & Security, then restart it. Without these, synthetic clicks and the
  global hotkey will not work. (If the global hook cannot start, the app shows a warning dialog.)
- **Linux:** Requires an **X11** session. Wayland sandboxes global input hooks and synthetic input,
  so it is unsupported (XWayland may work for some setups).
- **Windows:** Works without special permissions. To click into apps running as Administrator,
  run the clicker as Administrator too.

## Configuration

Settings persist to a JSON file:

- Windows: `%APPDATA%\AutoClicker\config.json`
- macOS / Linux: `~/.config/AutoClicker/config.json`

## Tests

    dotnet test

Unit tests cover the OS-independent core (planner, engine, config). The OS-specific input layer is
verified by the manual smoke test below.

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
   B → click → wait → loops. Per-row Button/Type/Wait take effect.
7. **Edit:** reorder with ↑/↓, remove with ✕, **Add row** / **Clear all** work; Save persists.
8. **Duplicate guard:** try recording the capture combo equal to start/stop — it's rejected.
9. Quit from the tray — process exits, icon disappears.

## Design & plan

- Original design spec: `docs/superpowers/specs/2026-06-03-auto-clicker-design.md`
- Hotkey-combos + sequences design: `docs/superpowers/specs/2026-06-04-hotkey-combos-and-capture-design.md`
- Hotkey-combos + sequences plan: `docs/superpowers/plans/2026-06-04-hotkeys-and-sequences.md`
