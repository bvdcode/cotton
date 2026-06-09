// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Composition;

namespace Cotton.Sync.Desktop.Startup
{
    internal static class DesktopStartupPathResolver
    {
        public static DesktopAppPaths Resolve(DesktopStartupOptions startupOptions)
        {
            ArgumentNullException.ThrowIfNull(startupOptions);
            return startupOptions.DataDirectory is null
                ? DesktopAppPaths.CreateDefault()
                : DesktopAppPaths.CreateForDataDirectory(startupOptions.DataDirectory);
        }
    }
}
