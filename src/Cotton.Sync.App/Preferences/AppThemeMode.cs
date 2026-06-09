// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Preferences
{

    /// <summary>
    /// Defines the desktop theme preference.
    /// </summary>
    public enum AppThemeMode
    {
        /// <summary>
        /// Follow the operating system theme.
        /// </summary>
        System = 0,

        /// <summary>
        /// Force the light theme.
        /// </summary>
        Light = 1,

        /// <summary>
        /// Force the dark theme.
        /// </summary>
        Dark = 2,
    }
}
