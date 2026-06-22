// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Services;

namespace Cotton.Server.Extensions
{
    /// <summary>
    /// Contains extension methods for configuring file manifest.
    /// </summary>
    public static class FileManifestExtensions
    {
        /// <summary>
        /// Gets chunk lengths.
        /// </summary>
        public static Dictionary<string, long> GetChunkLengths(this IEnumerable<FileManifestChunk> fileManifestChunks)
        {
            ArgumentNullException.ThrowIfNull(fileManifestChunks);

            Dictionary<string, long> result = new(StringComparer.OrdinalIgnoreCase);
            foreach (FileManifestChunk fileManifestChunk in fileManifestChunks)
            {
                ArgumentNullException.ThrowIfNull(fileManifestChunk.ChunkHash);
                ArgumentNullException.ThrowIfNull(fileManifestChunk.Chunk);

                string hash = Hasher.ToHexStringHash(fileManifestChunk.ChunkHash);
                long length = fileManifestChunk.Chunk.PlainSizeBytes;

                if (result.TryGetValue(hash, out long existingLength) && existingLength != length)
                {
                    throw new InvalidOperationException($"Chunk '{hash}' has conflicting lengths ({existingLength} and {length}).");
                }

                result[hash] = length;
            }

            return result;
        }

        /// <summary>
        /// Gets chunk hashes.
        /// </summary>
        public static string[] GetChunkHashes(this IEnumerable<FileManifestChunk> chunks)
        {
            List<string> result = [];
            ArgumentNullException.ThrowIfNull(chunks);
            int? lastOrder = null;
            foreach (FileManifestChunk? chunk in chunks.OrderBy(x => x.ChunkOrder))
            {
                lastOrder ??= chunk.ChunkOrder - 1;
                ArgumentNullException.ThrowIfNull(chunk.ChunkHash);
                string hashString = Hasher.ToHexStringHash(chunk.ChunkHash);
                ArgumentException.ThrowIfNullOrWhiteSpace(hashString);
                if (lastOrder + 1 != chunk.ChunkOrder)
                {
                    string orders = string.Join(", ", chunks.Select(c => c.ChunkOrder));
                    throw new ArgumentException("Chunks are out of order or have missing entries, order: " + orders);
                }
                result.Add(hashString);
                lastOrder = chunk.ChunkOrder;
            }
            return [.. result];
        }
    }
}
