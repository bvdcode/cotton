// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging;

namespace Cotton.Sync.Desktop.Diagnostics
{
    internal class DesktopTraceLoggerFactory : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new DesktopTraceLogger(categoryName);
        }

        public void AddProvider(ILoggerProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
        }

        public void Dispose()
        {
        }
    }
}
