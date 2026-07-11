// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Collections.Concurrent;

namespace Cotton.Server.IntegrationTests.Helpers
{
    public class NUnitLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IReadOnlyCollection<string> Messages => _messages.ToArray();

        public ILogger CreateLogger(string categoryName) => new NUnitLogger(categoryName, _messages);

        public void Dispose()
        {
        }

        private class NUnitLogger(
            string category,
            ConcurrentQueue<string> messages) : ILogger
        {
            private static readonly Lock _lock = new();

            IDisposable ILogger.BeginScope<TState>(TState state) => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                string message = formatter(state, exception);
                messages.Enqueue(exception is null
                    ? $"{category}: {message}"
                    : $"{category}: {message}{Environment.NewLine}{exception}");
                lock (_lock)
                {
                    TestContext.Progress.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss.fff}] {logLevel,-11} {category}: {message}");
                    if (exception is not null)
                    {
                        TestContext.Progress.WriteLine(exception);
                    }
                }
            }

            private class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();

                public void Dispose()
                {
                }
            }
        }
    }
}
