using Hardcodet.Wpf.TaskbarNotification;
using PeekView.App.Services.Explorer;
using PeekView.App.Services.Startup;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace PeekView.App;

public partial class App : Application
{
    private QuickLookCoordinator? _quickLookCoordinator;
    private MainWindow? _mainWindow;
    private TaskbarIcon? _trayIcon;
    private readonly WindowsStartupService _startupService = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _mainWindow = new MainWindow();
        _quickLookCoordinator = new QuickLookCoordinator(_mainWindow);

        try
        {
            _quickLookCoordinator.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"PeekView could not start its keyboard integration:\n{ex.Message}",
                "PeekView",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        try
        {
            var trayIconImage = new BitmapImage();
            trayIconImage.BeginInit();
            trayIconImage.UriSource = new Uri(
                "pack://application:,,,/Resources/Icons/peekview.ico",
                UriKind.Absolute);
            trayIconImage.CacheOption = BitmapCacheOption.OnLoad;
            trayIconImage.EndInit();
            trayIconImage.Freeze();

            _trayIcon = new TaskbarIcon
            {
                ToolTipText = "PeekView",
                IconSource = trayIconImage,
                ContextMenu = (ContextMenu)FindResource("TrayContextMenu")
            };
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"PeekView could not create its system tray icon:\n{ex.Message}",
                "PeekView",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        UpdateStartupMenuItem();
    }

    private void UpdateStartupMenuItem()
    {
        if (FindResource("TrayContextMenu") is not ContextMenu menu)
            return;

        foreach (object item in menu.Items)
        {
            if (item is MenuItem menuItem &&
                menuItem.Name == "StartupMenuItem")
            {
                try
                {
                    menuItem.IsChecked = _startupService.IsEnabled();
                }
                catch (Exception ex)
                {
                    menuItem.IsEnabled = false;
                    menuItem.ToolTip =
                        $"Unable to read startup setting: {ex.Message}";
                }

                return;
            }
        }
    }

    private void TrayStartup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
            return;

        bool requestedState = menuItem.IsChecked;

        try
        {
            _startupService.SetEnabled(requestedState);
            menuItem.IsChecked = _startupService.IsEnabled();
        }
        catch (Exception ex)
        {
            menuItem.IsChecked = !requestedState;

            MessageBox.Show(
                $"Could not update the Windows startup setting:\n{ex.Message}",
                "PeekView — Startup Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void TrayOpen_Click(object sender, RoutedEventArgs e)
    {
        if (_mainWindow is null)
            return;

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void TrayExit_Click(object sender, RoutedEventArgs e)
    {
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _quickLookCoordinator?.Dispose();
        _quickLookCoordinator = null;

        _trayIcon?.Dispose();
        _trayIcon = null;

        _mainWindow = null;

        base.OnExit(e);
    }
}