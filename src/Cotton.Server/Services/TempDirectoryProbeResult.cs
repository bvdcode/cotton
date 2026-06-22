// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    /// <summary>
    /// Represents an OS temp directory writability probe result.
    /// </summary>
    public sealed class TempDirectoryProbeResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TempDirectoryProbeResult"/> class.
        /// </summary>
        public TempDirectoryProbeResult(
            string tempPath,
            bool writable,
            string? error)
        {
            TempPath = tempPath;
            Writable = writable;
            Error = error;
        }

        /// <summary>
        /// Gets the probed OS temp path.
        /// </summary>
        public string TempPath { get; }

        /// <summary>
        /// Gets a value indicating whether the temp path is writable.
        /// </summary>
        public bool Writable { get; }

        /// <summary>
        /// Gets the probe error, when temp is not writable.
        /// </summary>
        public string? Error { get; }
    }
}
