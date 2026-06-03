using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace AutoClicker.App.Dialogs;

public partial class MessageWindow : Window
{
    public MessageWindow() => AvaloniaXamlLoader.Load(this);

    public MessageWindow(string title, string message) : this()
    {
        Title = title;
        this.FindControl<TextBlock>("MessageText")!.Text = message;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close();
}
