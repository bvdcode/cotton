// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    /// <summary>
    /// Indicates that an ffprobe output stream exceeded its configured capture limit.
    /// </summary>
    public class FfprobeOutputLimitExceededException(string outputName, int maxBytes)
        : Exception($"ffprobe {outputName} exceeded the configured {maxBytes}-byte limit.")
    {
        /// <summary>
        /// Gets the process-output stream name.
        /// </summary>
        public string OutputName { get; } = outputName;

        /// <summary>
        /// Gets the exceeded byte limit.
        /// </summary>
        public int MaxBytes { get; } = maxBytes;
    }
}
