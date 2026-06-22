// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Startup
{
    internal class StartupStatusResponse
    {
        public bool Blocked { get; init; }

        public StartupBlocker? Blocker { get; init; }

        public static StartupStatusResponse Ready()
        {
            return new StartupStatusResponse
            {
                Blocked = false,
                Blocker = null,
            };
        }

        public static StartupStatusResponse BlockedBy(StartupBlocker blocker)
        {
            return new StartupStatusResponse
            {
                Blocked = true,
                Blocker = blocker,
            };
        }
    }
}
