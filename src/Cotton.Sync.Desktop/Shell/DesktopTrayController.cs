// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Cotton.Sync.Desktop.Platform;
using Cotton.Sync.Desktop.ViewModels;

namespace Cotton.Sync.Desktop.Shell;

internal sealed class DesktopTrayController : IDisposable
{
    private static readonly Uri IconUri = new("avares://Cotton.Sync.Desktop/Assets/icon-192.png");

    private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
    private readonly MainWindow _window;
    private readonly TrayIcon _trayIcon;
    private bool _disposed;

    public DesktopTrayController(MainWindow window, IClassicDesktopStyleApplicationLifetime lifetime)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        _trayIcon = CreateTrayIcon();
    }

    public static bool IsSupportedPlatform => DesktopPlatformCapabilities.IsTrayLifecycleSupported;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _trayIcon.Dispose();
        _disposed = true;
    }

    private static WindowIcon LoadIcon()
    {
        using Stream stream = AssetLoader.Open(IconUri);
        return new WindowIcon(stream);
    }

    private static NativeMenuItem CreateMenuItem(string header, Action action)
    {
        var item = new NativeMenuItem(header);
        item.Click += (_, _) => action();
        return item;
    }

    private TrayIcon CreateTrayIcon()
    {
        var trayIcon = new TrayIcon
        {
            Icon = LoadIcon(),
            ToolTipText = "Cotton Sync",
            IsVisible = true,
            Menu = new NativeMenu
            {
                Items =
                {
                    CreateMenuItem("Show", ShowWindow),
                    CreateMenuItem("Open folder", () => Execute(commandSource => commandSource.OpenFolderCommand)),
                    CreateMenuItem("Open web", () => Execute(commandSource => commandSource.OpenWebCommand)),
                    CreateMenuItem("Sync now", () => Execute(commandSource => commandSource.SyncNowCommand)),
                    CreateMenuItem("Pause", () => Execute(commandSource => commandSource.PauseCommand)),
                    CreateMenuItem("Resume", () => Execute(commandSource => commandSource.ResumeCommand)),
                    CreateMenuItem("Settings", ShowSettings),
                    new NativeMenuItemSeparator(),
                    CreateMenuItem("Quit", Quit),
                },
            },
        };
        trayIcon.Clicked += (_, _) => ShowWindow();
        return trayIcon;
    }

    private void Execute(Func<ShellViewModel, AsyncRelayCommand> selectCommand)
    {
        if (_window.DataContext is not ShellViewModel viewModel)
        {
            return;
        }

        AsyncRelayCommand command = selectCommand(viewModel);
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void Quit()
    {
        _window.RequestQuit();
        _lifetime.Shutdown();
    }

    private void ShowWindow()
    {
        _window.ShowShell();
    }

    private void ShowSettings()
    {
        ShowWindow();
        Execute(commandSource => commandSource.ShowSettingsCommand);
    }
}
