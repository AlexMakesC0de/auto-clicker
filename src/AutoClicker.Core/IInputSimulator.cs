namespace AutoClicker.Core;

/// <summary>Injects synthetic mouse clicks. Null coordinates mean "at the current cursor position".</summary>
public interface IInputSimulator
{
    void Click(int? x, int? y, ClickButton button, ClickType type);
}
