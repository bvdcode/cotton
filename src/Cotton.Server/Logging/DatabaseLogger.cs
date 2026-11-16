// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cotton.Server.Logging
{
    internal class DatabaseLogger(ILogger inner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (ChunkUpsertConflictScope.IsActive
                && (eventId == RelationalEventId.CommandError || eventId == CoreEventId.SaveChangesFailed)
                && ChunkUpsertConflictScope.IsConflict(exception))
            {
                return;
            }

            inner.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
