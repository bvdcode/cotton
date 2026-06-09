// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Preferences;

namespace Cotton.Sync.Desktop.Platform
{

    internal interface IDesktopThemeService
    {
        void Apply(AppThemeMode themeMode);
    }
}
