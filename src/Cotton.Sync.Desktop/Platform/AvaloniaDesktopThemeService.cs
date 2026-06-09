// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Avalonia;
using Avalonia.Styling;
using Cotton.Sync.App.Preferences;

namespace Cotton.Sync.Desktop.Platform
{

    internal sealed class AvaloniaDesktopThemeService : IDesktopThemeService
    {
        public void Apply(AppThemeMode themeMode)
        {
            Application? application = Application.Current;
            if (application is null)
            {
                return;
            }

            application.RequestedThemeVariant = ToThemeVariant(themeMode);
        }

        internal static ThemeVariant ToThemeVariant(AppThemeMode themeMode)
        {
            return themeMode switch
            {
                AppThemeMode.System => ThemeVariant.Default,
                AppThemeMode.Light => ThemeVariant.Light,
                AppThemeMode.Dark => ThemeVariant.Dark,
                _ => throw new ArgumentOutOfRangeException(nameof(themeMode), themeMode, "Unsupported desktop theme mode."),
            };
        }
    }
}
