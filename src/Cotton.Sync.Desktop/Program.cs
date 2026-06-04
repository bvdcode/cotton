// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Avalonia;
using Cotton.Sync.Desktop.Composition;
using Cotton.Sync.Desktop.Platform;
using Cotton.Sync.Desktop.Startup;

namespace Cotton.Sync.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        DesktopStartupOptions startupOptions = DesktopStartupOptions.Parse(args);
        DesktopAppPaths paths = DesktopStartupPathResolver.Resolve(startupOptions);
        if (startupOptions.RunSelfTest)
        {
            return DesktopCommandLineRunner
                .RunSelfTestAsync(paths, startupOptions, Console.Out)
                .GetAwaiter()
                .GetResult();
        }

        if (startupOptions.ExportDiagnostics)
        {
            return DesktopCommandLineRunner
                .RunExportDiagnosticsAsync(paths, startupOptions, Console.Out)
                .GetAwaiter()
                .GetResult();
        }

        DesktopAppIdentity.ApplyToCurrentProcess();
        using DesktopSingleInstanceGuard? singleInstance = DesktopSingleInstanceGuard
            .TryAcquire(paths.SingleInstanceLockPath);
        if (singleInstance is null)
        {
            return 0;
        }

        App.StartupOptions = startupOptions;
        App.StartupPaths = paths;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }

}
