// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Avalonia;
using Cotton.Sync.Desktop.Startup;

namespace Cotton.Sync.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.StartupOptions = DesktopStartupOptions.Parse(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
