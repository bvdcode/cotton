// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging;

namespace Cotton.Sdk.Tests.Fakes
{
    internal class RecordingLoggerFactory : ILoggerFactory
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
}
