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
