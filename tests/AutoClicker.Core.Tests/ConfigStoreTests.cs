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
