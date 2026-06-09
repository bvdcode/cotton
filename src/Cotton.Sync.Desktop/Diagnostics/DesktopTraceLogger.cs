// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Cotton.Sync.Desktop.Diagnostics
{
    internal class DesktopTraceLogger : ILogger
    {
        private readonly string _categoryName;

        public DesktopTraceLogger(string categoryName)
        {
            _categoryName = string.IsNullOrWhiteSpace(categoryName) ? "Cotton.Sync.Desktop" : categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string message = DesktopTraceLogFormatter.Format(_categoryName, logLevel, eventId, formatter(state, exception), exception);
            Trace.WriteLine(DesktopSecretRedactor.Redact(message));
        }
    }
}
