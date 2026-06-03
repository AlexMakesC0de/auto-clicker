# Auto Clicker

A cross-platform (Windows, macOS, Linux/X11) system-tray auto clicker for personal use.
Configurable rate, mouse button, single/double click, click limit, jitter, target position
(current cursor or a captured fixed coordinate), and a global start/stop hotkey (default F6).

Built with **.NET 10**, **Avalonia 12** (tray + settings UI), and **SharpHook 7**
(global hotkey + synthetic clicks). The OS-independent core (`AutoClicker.Core`) is unit-tested;
the OS input layer is verified by the manual smoke test below.

## Run (development)

    dotnet run --project src/AutoClicker.App

The app has no main window — look for the tray/menu-bar icon. The tray menu gives
**Start/Stop**, **Settings…**, and **Quit**. Press the hotkey (default **F6**) to toggle clicking.

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

## Design & plan

- Design spec: `docs/superpowers/specs/2026-06-03-auto-clicker-design.md`
- Implementation plan: `docs/superpowers/plans/2026-06-03-auto-clicker.md`
