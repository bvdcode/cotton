// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Local
{
    /// <summary>
    /// Represents a local file or directory that cannot be read because permission was denied.
    /// </summary>
    public sealed class LocalFilePermissionDeniedException : UnauthorizedAccessException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LocalFilePermissionDeniedException" /> class.
        /// </summary>
        public LocalFilePermissionDeniedException(string relativePath, string fullPath, string reason)
            : base($"Local file '{relativePath}' cannot be read because permission was denied: {reason}")
        {
            RelativePath = relativePath;
            FullPath = fullPath;
            Reason = reason;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalFilePermissionDeniedException" /> class.
        /// </summary>
        public LocalFilePermissionDeniedException(string relativePath, string fullPath, Exception innerException)
            : base($"Local file '{relativePath}' cannot be read because permission was denied.", innerException)
        {
            RelativePath = relativePath;
            FullPath = fullPath;
            Reason = innerException.Message;
        }

        /// <summary>
        /// Gets the relative path that could not be read.
        /// </summary>
        public string RelativePath { get; }

        /// <summary>
        /// Gets the absolute file path that could not be read.
        /// </summary>
        public string FullPath { get; }

        /// <summary>
        /// Gets the reason the file could not be read.
        /// </summary>
        public string Reason { get; }
    }
}
