// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.FileMetadata
{
    public class FileMetadataUnavailableException : Exception
    {
        public FileMetadataUnavailableException(string message)
            : base(message)
        {
        }

        public FileMetadataUnavailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
