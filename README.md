<div align="center">

# Auto Clicker

**A cross-platform system tray auto clicker with global hotkeys and recorded click sequences.**

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![Avalonia 12](https://img.shields.io/badge/Avalonia-12-8B44AC)
![Windows | macOS | Linux (X11)](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux%20(X11)-555)

</div>

A personal-use auto clicker that lives in the tray (or the macOS menu bar). It clicks either wherever your cursor is, or through a list of screen positions you record with a hotkey. It is built with .NET 10, [Avalonia](https://avaloniaui.net/) for the tray and settings window, and [SharpHook](https://github.com/TolikPylypchuk/SharpHook) for the global hotkeys and synthetic clicks.

## Features

- **Two modes.** *Current cursor* clicks where the pointer is. *Sequence* moves to each recorded position, clicks, waits and moves on, looping until you stop it.
- **Per-step settings.** Every sequence step has its own position, mouse button, click type and wait time.
- **Click options.** Interval in milliseconds (default 100 ms, about 10 clicks per second), left, middle or right button, single or double click, and an optional click limit that stops automatically.
- **Jitter.** When on, each delay varies by up to 15% and each sequence position by up to 3 pixels.
- **Recorded hotkeys.** Set each hotkey by pressing the combination you want, with Ctrl, Shift, Alt and Win/Cmd as modifiers. Esc cancels recording, and the two hotkeys cannot be the same.
  - Start/stop, default **F6**. Holding the combo does not make it flip on and off.
  - Capture position, default **F8**. Adds the spot under your cursor to the sequence and switches to Sequence mode.
- **Sequence editor.** Reorder, remove, add rows or clear all.
- **Saved settings.** Everything persists to a JSON file between runs.

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Run from source

```bash
git clone https://github.com/AlexMakesC0de/auto-clicker.git
cd auto-clicker
dotnet run --project src/AutoClicker.App
```

The Settings window opens on launch and the app stays in the tray. The tray menu has **Settings...** and **Quit**. Use the start/stop hotkey to toggle clicking.

### Build a standalone binary

Pick the runtime for your device:

```bash
# Windows
dotnet publish src/AutoClicker.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
# macOS (Apple Silicon: osx-arm64, Intel: osx-x64)
dotnet publish src/AutoClicker.App -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true
# Linux (X11)
dotnet publish src/AutoClicker.App -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```

The output is in `src/AutoClicker.App/bin/Release/net10.0/<runtime>/publish/`.

## Platform notes

- **Windows:** no special permissions. To click into apps running as Administrator, run the clicker as Administrator too.
- **macOS:** grant the app **Accessibility** and **Input Monitoring** in System Settings > Privacy & Security, then restart it. Without them the hotkeys and clicks do not work, and the app shows a warning if the global hook cannot start.
- **Linux:** needs an **X11** session. Wayland blocks global input hooks and synthetic input, so it is not supported (XWayland may work in some setups).

## Configuration

Settings are saved to:

- Windows: `%APPDATA%\AutoClicker\config.json`
- macOS and Linux: `~/.config/AutoClicker/config.json`

Delete the file to go back to the defaults.

## How it works

The solution is split so the clicking logic can be tested without touching the real mouse:

- `AutoClicker.Core` holds the settings, the hotkey model, the `ClickPlanner` (works out the next click and its delay, including jitter) and the `ClickEngine` that runs the loop. Input, time and randomness are behind interfaces (`IInputSimulator`, `IGlobalInputService`, `IClock`, `IRandomSource`).
- `AutoClicker.App` is the Avalonia app: tray controller, settings window and view models, plus SharpHook adapters that implement those interfaces against the real OS.

## Project structure

```text
src/
  AutoClicker.Core/          OS-independent logic
  AutoClicker.App/           Avalonia tray app and SharpHook adapters
tests/
  AutoClicker.Core.Tests/    xUnit tests for the core
docs/superpowers/            Design specs and implementation plans
```

## Tests

```bash
dotnet test
```

The unit tests cover the core: planner, engine, settings, config store and hotkeys. The OS input layer is checked by hand on each platform: launch, set a hotkey combo, capture two positions, run the sequence, edit it, and quit from the tray.

## License

[MIT](LICENSE)
