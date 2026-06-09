// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using Cotton.Sync.Desktop.Composition;

namespace Cotton.Sync.Desktop.Diagnostics
{
    internal static class DesktopTraceLogging
    {
        private const long MaxLogFileSizeBytes = 5L * 1024L * 1024L;

        public static void Install(DesktopAppPaths paths)
        {
            ArgumentNullException.ThrowIfNull(paths);
            Directory.CreateDirectory(paths.DataDirectory);
            if (Trace.Listeners
                .OfType<RotatingFileTraceListener>()
                .Any(listener => string.Equals(listener.Path, paths.LogFilePath, StringComparison.Ordinal)))
            {
                return;
            }

            Trace.Listeners.Add(new RotatingFileTraceListener(paths.LogFilePath, MaxLogFileSizeBytes));
            Trace.AutoFlush = true;
            Trace.TraceInformation("Cotton Sync desktop logging initialized.");
        }
    }
}
