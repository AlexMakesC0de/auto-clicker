using AutoClicker.Core;
using SharpHook;
using SharpHook.Data;

namespace AutoClicker.App.Adapters;

/// <summary>Real mouse-click injection via SharpHook's EventSimulator.</summary>
public sealed class SharpHookInputSimulator : IInputSimulator
{
    private readonly EventSimulator _simulator = new();

    public void Click(int? x, int? y, ClickButton button, ClickType type)
    {
        MouseButton mb = SharpHookMappings.ToMouseButton(button);
        int repeats = type == ClickType.Double ? 2 : 1;

        for (int i = 0; i < repeats; i++)
        {
            if (x is null || y is null)
            {
                _simulator.SimulateMousePress(mb);
                _simulator.SimulateMouseRelease(mb);
            }
            else
            {
                _simulator.SimulateMousePress((short)x.Value, (short)y.Value, mb);
                _simulator.SimulateMouseRelease((short)x.Value, (short)y.Value, mb);
            }
        }
    }
}
