// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging;

namespace Cotton.Sdk.Tests.Fakes;

internal sealed class RecordingLoggerFactory : ILoggerFactory
{
    public List<RecordingLogEntry> Entries { get; } = [];

    public ILogger CreateLogger(string categoryName)
    {
        return new RecordingLogger(categoryName, Entries);
    }

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }
}

internal sealed record RecordingLogEntry(
    string CategoryName,
    LogLevel Level,
    string Message,
    Exception? Exception);

internal sealed class RecordingLogger : ILogger
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
        return NullScope.Instance;
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

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
