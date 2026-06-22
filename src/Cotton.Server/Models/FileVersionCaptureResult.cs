// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models
{
    /// <summary>
    /// Represents the result of file version capture.
    /// </summary>
    public readonly record struct FileVersionCaptureResult(bool Captured, long RemovedBytes)
    {
        /// <summary>
        /// Creates an empty value object.
        /// </summary>
        public static FileVersionCaptureResult Empty { get; } = new(Captured: false, RemovedBytes: 0);
    }
}
