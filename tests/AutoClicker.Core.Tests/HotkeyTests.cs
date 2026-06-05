using AutoClicker.Core;
using Xunit;

namespace AutoClicker.Core.Tests;

public class HotkeyTests
{
    [Fact]
    public void Equality_SameComboEqual_DifferentNot()
    {
        Assert.Equal(new Hotkey(KeyId.F6), new Hotkey(KeyId.F6));
        Assert.NotEqual(new Hotkey(KeyId.F6), new Hotkey(KeyId.F7));
        Assert.NotEqual(new Hotkey(KeyId.F6, Ctrl: true), new Hotkey(KeyId.F6));
        Assert.Equal(new Hotkey(KeyId.F6, Ctrl: true), new Hotkey(KeyId.F6, Ctrl: true));
    }

    [Fact]
    public void IsValid_NoneInvalid_RealKeyValid()
    {
        Assert.False(new Hotkey(KeyId.None).IsValid);
        Assert.False(Hotkey.None.IsValid);
        Assert.True(new Hotkey(KeyId.F6).IsValid);
    }

    [Theory]
    [InlineData(false, false, false, false, KeyId.F6, "F6")]
    [InlineData(true, false, false, false, KeyId.F6, "Ctrl+F6")]
    [InlineData(false, true, true, false, KeyId.Backquote, "Shift+Alt+~")]
    [InlineData(false, false, false, false, KeyId.Escape, "Esc")]
    [InlineData(true, true, true, true, KeyId.D5, "Ctrl+Shift+Alt+Win+5")]
    [InlineData(false, false, false, false, KeyId.A, "A")]
    public void ToDisplayString_FormatsCanonically(
        bool ctrl, bool shift, bool alt, bool meta, KeyId key, string expected)
    {
        Assert.Equal(expected, new Hotkey(key, ctrl, shift, alt, meta).ToDisplayString());
    }
}
