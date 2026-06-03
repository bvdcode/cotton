// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Cotton.Sync.Desktop.Shell;
using Cotton.Sync.Desktop.Startup;

namespace Cotton.Sync.Desktop;

/// <summary>
/// Avalonia application entry point.
/// </summary>
public sealed partial class App : Application
{
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
            desktop.MainWindow = new MainWindow(DesktopShellController.CreateDefault(StartupOptions));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
