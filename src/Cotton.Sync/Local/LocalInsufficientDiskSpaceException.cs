// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Local
{
    /// <summary>
    /// Represents a planned local download that cannot start because the target drive lacks free space.
    /// </summary>
    public class LocalInsufficientDiskSpaceException : IOException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LocalInsufficientDiskSpaceException" /> class.
        /// </summary>
        public LocalInsufficientDiskSpaceException(string relativePath, long requiredBytes, long availableBytes)
            : base("Not enough disk space to download '" + relativePath + "'. Free space on this computer and retry sync.")
        {
            RelativePath = relativePath;
            RequiredBytes = requiredBytes;
            AvailableBytes = availableBytes;
        }

        /// <summary>
        /// Gets the relative path that cannot be downloaded.
        /// </summary>
        public string RelativePath { get; }

        /// <summary>
        /// Gets the required free bytes for the planned download.
        /// </summary>
        public long RequiredBytes { get; }

        /// <summary>
        /// Gets the available free bytes on the target drive when the check ran.
        /// </summary>
        public long AvailableBytes { get; }
    }
}
