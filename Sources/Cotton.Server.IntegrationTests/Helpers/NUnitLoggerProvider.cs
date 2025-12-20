// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests.Helpers;

public sealed class NUnitLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new NUnitLogger(categoryName);
    public void Dispose() { }

    private sealed class NUnitLogger(string category) : ILogger
    {
        private static readonly Lock _lock = new();
        IDisposable ILogger.BeginScope<TState>(TState state) => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var message = formatter(state, exception);
            lock (_lock)
            {
                TestContext.Progress.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss.fff}] {logLevel,-11} {category}: {message}");
                if (exception != null)
                {
                    TestContext.Progress.WriteLine(exception);
                }
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
