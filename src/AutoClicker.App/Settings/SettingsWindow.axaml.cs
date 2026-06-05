using AutoClicker.App.Tray;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace AutoClicker.App.Settings;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel = null!;

    public SettingsWindow() => AvaloniaXamlLoader.Load(this);

    public SettingsWindow(TrayAppController controller) : this()
    {
        _viewModel = new SettingsViewModel(controller);
        DataContext = _viewModel;
    }

    private static SequenceStepViewModel? RowOf(object? sender) =>
        (sender as Control)?.DataContext as SequenceStepViewModel;

    private void OnAddRowClick(object? sender, RoutedEventArgs e) => _viewModel.AddBlankStep();
    private void OnClearAllClick(object? sender, RoutedEventArgs e) => _viewModel.ClearSteps();
    private void OnRemoveStepClick(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row) _viewModel.RemoveStep(row);
    }
    private void OnMoveUpClick(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row) _viewModel.MoveStepUp(row);
    }
    private void OnMoveDownClick(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row) _viewModel.MoveStepDown(row);
    }

    private void OnRecordStartStopClick(object? sender, RoutedEventArgs e) => _viewModel.RecordStartStop();
    private void OnRecordCaptureClick(object? sender, RoutedEventArgs e) => _viewModel.RecordCapture();

    private void OnSaveClick(object? sender, RoutedEventArgs e) => _viewModel.Save();
}
