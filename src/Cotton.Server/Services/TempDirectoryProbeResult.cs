// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    public class TempDirectoryProbeResult
    {
        public TempDirectoryProbeResult(
            string tempPath,
            bool writable,
            string? error)
        {
            TempPath = tempPath;
            Writable = writable;
            Error = error;
        }

        public string TempPath { get; }

        public bool Writable { get; }

        public string? Error { get; }
    }
}
