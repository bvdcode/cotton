// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.FileMetadata
{
    /// <summary>
    /// Indicates that a valid extraction attempt found no readable metadata for the supplied content.
    /// </summary>
    public class FileMetadataUnavailableException : Exception
    {
        /// <summary>
        /// Creates an exception with a metadata unavailability reason.
        /// </summary>
        public FileMetadataUnavailableException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Creates an exception with a metadata unavailability reason and the underlying failure.
        /// </summary>
        public FileMetadataUnavailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
