// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging;

namespace Cotton.Previews.Tests.TestInfrastructure
{
    internal class CapturingLogger : ILogger
    {
        private readonly object _sync = new();
        private readonly List<(LogLevel Level, Exception? Exception, string Message)> _entries = [];

        public IReadOnlyList<(LogLevel Level, Exception? Exception, string Message)> Entries
        {
            get
            {
                lock (_sync)
                {
                    return _entries.ToArray();
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            string message = formatter(state, exception);
            lock (_sync)
            {
                _entries.Add((logLevel, exception, message));
            }
        }
    }
}
