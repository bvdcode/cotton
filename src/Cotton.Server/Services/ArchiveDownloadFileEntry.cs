// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    public record ArchiveDownloadFileEntry : ArchiveDownloadEntry
    {
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

        public IReadOnlyList<string> ChunkHashes { get; }

        /// <summary>
        /// Gets chunk plaintext lengths keyed by chunk hash for deterministic archive streaming.
        /// </summary>
        public Dictionary<string, long> ChunkLengths { get; }
    }
}
