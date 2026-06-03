using AutoClicker.App.Tray;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace AutoClicker.App.Settings;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel = null!;

    // Parameterless ctor for the XAML designer/loader.
    public SettingsWindow() => AvaloniaXamlLoader.Load(this);

    public SettingsWindow(TrayAppController controller) : this()
    {
        _viewModel = new SettingsViewModel(controller);
        DataContext = _viewModel;
    }

    private void OnCaptureClick(object? sender, RoutedEventArgs e) => _viewModel.CapturePosition();

    private void OnSaveClick(object? sender, RoutedEventArgs e) => _viewModel.Save();
}
