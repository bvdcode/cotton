// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    /// <summary>
    /// Captures the moment the main ASP.NET application starts accepting its bootstrap window.
    /// </summary>
    public sealed class ApplicationStartupClock(DateTimeOffset startedAtUtc)
    {
        public DateTimeOffset StartedAtUtc { get; } = startedAtUtc;

        public TimeSpan Uptime => DateTimeOffset.UtcNow - StartedAtUtc;
    }
}
