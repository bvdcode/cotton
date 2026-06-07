// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Preferences;

/// <summary>
/// Stores durable desktop sync-client preferences that are not sync-pair specific.
/// </summary>
public sealed class AppPreferences
{
    /// <summary>
    /// Gets or sets the last server URL selected by the user.
    /// </summary>
    public Uri? RememberedServerUrl { get; set; }

    /// <summary>
    /// Gets or sets the last username selected by the user.
    /// </summary>
    public string? RememberedUsername { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the app should start with the operating system.
    /// </summary>
    public bool StartWithOperatingSystem { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the app should start minimized to the tray.
    /// </summary>
    public bool StartMinimizedToTray { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether desktop notifications are enabled.
    /// </summary>
    public bool EnableNotifications { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether synchronization is globally paused.
    /// </summary>
    public bool IsSyncPaused { get; set; }

    /// <summary>
    /// Gets or sets the preferred desktop theme.
    /// </summary>
    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.Dark;

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC update timestamp.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }
}
