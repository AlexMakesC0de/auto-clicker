using AutoClicker.App.Adapters;
using AutoClicker.App.Dialogs;
using AutoClicker.App.Settings;
using AutoClicker.App.Tray;
using AutoClicker.Core;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;

namespace AutoClicker.App;

public partial class App : Application
{
    private TrayAppController? _controller;
    private TrayIcon? _trayIcon;
    private NativeMenuItem? _toggleItem;
    private SettingsWindow? _settingsWindow;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _controller = new TrayAppController(
                new ConfigStore(ConfigStore.DefaultPath()),
                new SharpHookGlobalInputService(),
                new SharpHookInputSimulator());

            _controller.StateChanged += (_, _) => UpdateToggleLabel();
            _controller.HookFailed += (_, message) =>
                new MessageWindow("Auto Clicker", message).Show();

            BuildTrayIcon();
            _controller.Initialize();
            UpdateToggleLabel();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void BuildTrayIcon()
    {
        _toggleItem = new NativeMenuItem("Start");
        _toggleItem.Click += (_, _) => _controller?.Toggle();

        var settingsItem = new NativeMenuItem("Settings…");
        settingsItem.Click += (_, _) => OpenSettings();

        var quitItem = new NativeMenuItem("Quit");
        quitItem.Click += (_, _) => Quit();

        var menu = new NativeMenu();
        menu.Items.Add(_toggleItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(quitItem);

        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://AutoClicker.App/Assets/app-icon.ico"))),
            ToolTipText = "Auto Clicker",
            Menu = menu,
            IsVisible = true,
        };

        TrayIcon.SetIcons(this, new TrayIcons { _trayIcon });
    }

    private void UpdateToggleLabel()
    {
        if (_toggleItem is not null && _controller is not null)
            _toggleItem.Header = _controller.IsRunning ? "Stop" : "Start";
    }

    private void OpenSettings()
    {
        if (_controller is null) return;

        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(_controller);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        else
        {
            _settingsWindow.Activate();
        }
    }

    private void Quit()
    {
        _controller?.Dispose();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
