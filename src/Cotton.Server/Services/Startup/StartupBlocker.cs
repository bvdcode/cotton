// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Startup
{
    internal class StartupBlocker
    {
        public string Kind { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;

        public string? CurrentVersion { get; init; }

        public string? RequiredVersion { get; init; }

        public string? RequiredVersionRange { get; init; }

        public string? LastRecordedVersion { get; init; }
    }
}
