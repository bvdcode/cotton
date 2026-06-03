// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Cotton.Sync.Desktop.Platform;
using Cotton.Sync.Desktop.Shell;
using Cotton.Sync.Desktop.Startup;

namespace Cotton.Sync.Desktop;

/// <summary>
/// Avalonia application entry point.
/// </summary>
public sealed partial class App : Application
{
    private DesktopTrayController? _trayController;

    internal static DesktopStartupOptions StartupOptions { get; set; } = DesktopStartupOptions.Empty;

    /// <inheritdoc />
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            bool useTrayLifecycle = DesktopPlatformCapabilities.IsTrayLifecycleSupported;
            if (useTrayLifecycle)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            var window = new MainWindow(
                DesktopShellController.CreateDefault(StartupOptions),
                StartupOptions.StartMinimizedToTray,
                useTrayLifecycle);
            desktop.MainWindow = window;
            if (useTrayLifecycle)
            {
                _trayController = new DesktopTrayController(window, desktop);
                desktop.Exit += (_, _) => _trayController?.Dispose();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
