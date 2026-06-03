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
        Assert.Equal(0, s.FixedX);
        Assert.Equal(0, s.FixedY);
        Assert.Equal(HotKey.F6, s.Hotkey);
    }
}
