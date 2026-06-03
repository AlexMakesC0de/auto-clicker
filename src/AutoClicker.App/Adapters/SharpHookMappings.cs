using AutoClicker.Core;
using SharpHook.Data;

namespace AutoClicker.App.Adapters;

internal static class SharpHookMappings
{
    public static MouseButton ToMouseButton(ClickButton button) => button switch
    {
        ClickButton.Left => MouseButton.Button1,
        ClickButton.Right => MouseButton.Button2,
        ClickButton.Middle => MouseButton.Button3,
        _ => MouseButton.Button1,
    };

    public static KeyCode ToKeyCode(HotKey hotkey) => hotkey switch
    {
        HotKey.F1 => KeyCode.VcF1,
        HotKey.F2 => KeyCode.VcF2,
        HotKey.F3 => KeyCode.VcF3,
        HotKey.F4 => KeyCode.VcF4,
        HotKey.F5 => KeyCode.VcF5,
        HotKey.F6 => KeyCode.VcF6,
        HotKey.F7 => KeyCode.VcF7,
        HotKey.F8 => KeyCode.VcF8,
        HotKey.F9 => KeyCode.VcF9,
        HotKey.F10 => KeyCode.VcF10,
        HotKey.F11 => KeyCode.VcF11,
        HotKey.F12 => KeyCode.VcF12,
        _ => KeyCode.VcF6,
    };
}
