// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    /// <summary>
    /// Captures the moment the main ASP.NET application starts accepting its bootstrap window.
    /// </summary>
    public sealed class ApplicationStartupClock(DateTimeOffset startedAtUtc)
    {
        /// <summary>
        /// Gets the started at utc.
        /// </summary>
        public DateTimeOffset StartedAtUtc { get; } = startedAtUtc;

        /// <summary>
        /// Gets the uptime.
        /// </summary>
        public TimeSpan Uptime => DateTimeOffset.UtcNow - StartedAtUtc;
    }
}
