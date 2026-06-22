// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Describes a archive download file entry.
    /// </summary>
    public record ArchiveDownloadFileEntry : ArchiveDownloadEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArchiveDownloadFileEntry"/> type.
        /// </summary>
        public ArchiveDownloadFileEntry(
            string path,
            long sizeBytes,
            IReadOnlyList<string> chunkHashes,
            Dictionary<string, long> chunkLengths)
            : base(path, sizeBytes, false)
        {
            ChunkHashes = chunkHashes;
            ChunkLengths = chunkLengths;
        }

        /// <summary>
        /// Gets the chunk hashes.
        /// </summary>
        public IReadOnlyList<string> ChunkHashes { get; }
        /// <summary>
        /// Gets chunk plaintext lengths keyed by chunk hash for deterministic archive streaming.
        /// </summary>
        public Dictionary<string, long> ChunkLengths { get; }
    }
}
