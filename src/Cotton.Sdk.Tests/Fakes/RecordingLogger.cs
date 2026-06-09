// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging;

namespace Cotton.Sdk.Tests.Fakes
{
    internal class RecordingLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly List<RecordingLogEntry> _entries;

        public RecordingLogger(string categoryName, List<RecordingLogEntry> entries)
        {
            _categoryName = categoryName;
            _entries = entries;
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
            _entries.Add(new RecordingLogEntry(_categoryName, logLevel, formatter(state, exception), exception));
        }
    }
}
