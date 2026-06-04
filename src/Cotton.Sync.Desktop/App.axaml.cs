// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Cotton.Sync.Desktop.Composition;
using Cotton.Sync.Desktop.Diagnostics;
using Cotton.Sync.Desktop.Platform;
using Cotton.Sync.Desktop.Shell;
using Cotton.Sync.Desktop.Startup;

namespace Cotton.Sync.Desktop;

/// <summary>
/// Avalonia application entry point.
/// </summary>
public sealed partial class App : Application
{
    private DesktopSingleInstanceActivationServer? _singleInstanceActivationServer;
    private DesktopTrayController? _trayController;

    internal static DesktopStartupOptions StartupOptions { get; set; } = DesktopStartupOptions.Empty;

    internal static DesktopAppPaths? StartupPaths { get; set; }

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
            DesktopAppPaths paths = StartupPaths ?? DesktopAppPaths.CreateDefault();
            DesktopTraceLogging.Install(paths);
            bool useTrayLifecycle = DesktopPlatformCapabilities.IsTrayLifecycleSupported;
            if (useTrayLifecycle)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            IDesktopShellController controller = StartupOptions.VisualSmokeScenario is { } scenario
                ? VisualSmokeShellController.Create(scenario)
                : DesktopShellController.CreateDefault(paths, StartupOptions);
            var window = new MainWindow(
                controller,
                StartupOptions.StartMinimizedToTray,
                useTrayLifecycle,
                StartupOptions.VisualSmokeScenario);
            desktop.MainWindow = window;
            if (useTrayLifecycle)
            {
                _trayController = new DesktopTrayController(window, desktop);
            }

            _singleInstanceActivationServer = DesktopSingleInstanceActivation.StartServer(
                paths.SingleInstanceLockPath,
                () => Dispatcher.UIThread.Post(window.ShowShell));
            desktop.Exit += (_, _) =>
            {
                _trayController?.Dispose();
                _singleInstanceActivationServer?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
