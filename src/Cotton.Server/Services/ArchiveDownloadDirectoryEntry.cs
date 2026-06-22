// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Describes a archive download directory entry.
    /// </summary>
    public record ArchiveDownloadDirectoryEntry : ArchiveDownloadEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArchiveDownloadDirectoryEntry"/> type.
        /// </summary>
        public ArchiveDownloadDirectoryEntry(string path)
            : base(path, 0, true)
        {
        }
    }
}
