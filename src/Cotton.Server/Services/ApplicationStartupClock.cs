// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    public class ApplicationStartupClock(DateTimeOffset startedAtUtc)
    {
        public DateTimeOffset StartedAtUtc { get; } = startedAtUtc;

        public TimeSpan Uptime => DateTimeOffset.UtcNow - StartedAtUtc;
    }
}
