// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.State
{
    /// <summary>
    /// Represents a relative sync path that cannot be synchronized safely across supported platforms.
    /// </summary>
    public sealed class SyncPathValidationException : ArgumentException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SyncPathValidationException" /> class.
        /// </summary>
        public SyncPathValidationException(string relativePath, string? segment, string reason)
            : base($"Relative path '{relativePath}' is not portable: {reason}", nameof(relativePath))
        {
            RelativePath = relativePath;
            Segment = segment;
            Reason = reason;
        }

        /// <summary>
        /// Gets the original relative path.
        /// </summary>
        public string RelativePath { get; }

        /// <summary>
        /// Gets the invalid path segment when validation failed on a segment.
        /// </summary>
        public string? Segment { get; }

        /// <summary>
        /// Gets the validation reason.
        /// </summary>
        public string Reason { get; }
    }
}
