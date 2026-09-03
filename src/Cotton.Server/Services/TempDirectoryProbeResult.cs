// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    public class TempDirectoryProbeResult(
        string tempPath,
        bool writable,
        string? error)
    {
        public string TempPath { get; } = tempPath;

        public bool Writable { get; } = writable;

        public string? Error { get; } = error;
    }
}
