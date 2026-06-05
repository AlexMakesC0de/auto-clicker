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

            _controller.HookFailed += (_, message) =>
                new MessageWindow("Auto Clicker", message).Show();

            BuildTrayIcon();
            _controller.Initialize();

            // Show the settings window on launch so it's obvious the app started.
            // On Windows 11 the tray icon otherwise hides in the overflow flyout.
            // Closing this window keeps the app running in the tray (OnExplicitShutdown).
            OpenSettings();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void BuildTrayIcon()
    {
        var settingsItem = new NativeMenuItem("Settings…");
        settingsItem.Click += (_, _) => OpenSettings();

        var quitItem = new NativeMenuItem("Quit");
        quitItem.Click += (_, _) => Quit();

        var menu = new NativeMenu();
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
