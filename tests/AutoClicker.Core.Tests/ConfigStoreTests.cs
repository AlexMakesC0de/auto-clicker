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
