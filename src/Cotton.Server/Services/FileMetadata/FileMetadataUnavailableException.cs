// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.FileMetadata
{
    /// <summary>
    /// Indicates that a valid extraction attempt found no readable metadata for the supplied content.
    /// </summary>
    public class FileMetadataUnavailableException(string message) : Exception(message)
    {
    }
}
