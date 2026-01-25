// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    public class FileManifestService(CottonDbContext _dbContext)
    {
        private static readonly FileExtensionContentTypeProvider fileExtensionContentTypeProvider = new();

        public async Task<List<Chunk>> GetChunksAsync(string[] chunkHashes, Guid userId, CancellationToken cancellationToken = default)
        {
            List<byte[]> normalizedHashes = [.. chunkHashes.Select(Hasher.FromHexStringHash)];
            List<Chunk> ownedChunks = await _dbContext.Chunks
                .Where(c => normalizedHashes.Contains(c.Hash))
                .Where(c => _dbContext.ChunkOwnerships.Any(co => co.ChunkHash == c.Hash && co.OwnerId == userId))
                .ToListAsync(cancellationToken);

            var chunkMap = ownedChunks.ToDictionary(c => Hasher.ToHexStringHash(c.Hash), StringComparer.OrdinalIgnoreCase);
            List<Chunk> result = [];
            foreach (var hash in chunkHashes)
            {
                if (!chunkMap.TryGetValue(hash, out var chunk))
                {
                    throw new EntityNotFoundException(nameof(Chunk));
                }
                result.Add(chunk);
            }
            return result;
        }

        public async Task<FileManifest> CreateNewFileManifestAsync(
            List<Chunk> chunks,
            string fileName,
            string contentType,
            byte[] proposedContentHash,
            CancellationToken cancellationToken = default)
        {
            var newFileManifest = new FileManifest()
            {
                ContentType = contentType,
                SizeBytes = chunks.Sum(x => x.SizeBytes),
                ProposedContentHash = proposedContentHash,
            };
            if (newFileManifest.ContentType == "application/octet-stream")
            {
                string? extension = Path.GetExtension(fileName);
                if (!string.IsNullOrWhiteSpace(extension))
                {
                    bool recognized = fileExtensionContentTypeProvider.TryGetContentType(fileName, out string? recognizedContentType);
                    if (recognized && !string.IsNullOrWhiteSpace(recognizedContentType))
                    {
                        newFileManifest.ContentType = recognizedContentType;
                    }
                }
            }

            await _dbContext.FileManifests.AddAsync(newFileManifest, cancellationToken);
            for (int i = 0; i < chunks.Count; i++)
            {
                var fileChunk = new FileManifestChunk
                {
                    ChunkOrder = i,
                    ChunkHash = chunks[i].Hash,
                    FileManifest = newFileManifest,
                };
                await _dbContext.FileManifestChunks.AddAsync(fileChunk, cancellationToken);
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            return newFileManifest;
        }
    }
}
