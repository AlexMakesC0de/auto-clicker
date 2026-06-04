using System.Text;

namespace AutoClicker.Core;

/// <summary>A global hotkey: one base key plus modifier flags. Value-equality drives exact matching.</summary>
public sealed record Hotkey(KeyId Key, bool Ctrl = false, bool Shift = false, bool Alt = false, bool Meta = false)
{
    public static Hotkey None { get; } = new(KeyId.None);

    public bool IsValid => Key != KeyId.None;

    public string ToDisplayString()
    {
        if (Key == KeyId.None) return "(none)";
        var sb = new StringBuilder();
        if (Ctrl) sb.Append("Ctrl+");
        if (Shift) sb.Append("Shift+");
        if (Alt) sb.Append("Alt+");
        if (Meta) sb.Append("Win+");
        sb.Append(Symbol(Key));
        return sb.ToString();
    }

    private static string Symbol(KeyId key)
    {
        if (key >= KeyId.D0 && key <= KeyId.D9)
            return ((int)(key - KeyId.D0)).ToString();

        return key switch
        {
            KeyId.Backquote => "~",
            KeyId.Escape => "Esc",
            KeyId.Minus => "-",
            KeyId.Equals => "=",
            KeyId.LeftBracket => "[",
            KeyId.RightBracket => "]",
            KeyId.Semicolon => ";",
            KeyId.Quote => "'",
            KeyId.Comma => ",",
            KeyId.Period => ".",
            KeyId.Slash => "/",
            KeyId.Backslash => "\\",
            _ => key.ToString(),
        };
    }
}
