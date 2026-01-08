using Cotton.Database.Models;
using Cotton.Server.Services;

namespace Cotton.Server.Extensions
{
    public static class FileManifestExtensions
    {
        public static string[] GetChunkHashes(this IEnumerable<FileManifestChunk> chunks)
        {
            List<string> result = [];
            ArgumentNullException.ThrowIfNull(chunks);
            int? lastOrder = null;
            foreach (var chunk in chunks)
            {
                lastOrder ??= chunk.ChunkOrder;
                ArgumentNullException.ThrowIfNull(chunk.ChunkHash);
                string hashString = Hasher.ToHexStringHash(chunk.ChunkHash);
                ArgumentException.ThrowIfNullOrWhiteSpace(hashString);
                if (lastOrder + 1 != chunk.ChunkOrder)
                {
                    throw new ArgumentException("Chunks are out of order or have missing entries.");
                }
                result.Add(hashString);
                lastOrder = chunk.ChunkOrder;
            }
            return [.. result];
        }
    }
}
