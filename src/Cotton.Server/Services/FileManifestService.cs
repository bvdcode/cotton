// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Previews;
using Cotton.Server.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Npgsql;

namespace Cotton.Server.Services
{
    public class FileManifestService(
        CottonDbContext _dbContext,
        IChunkIngestService _chunkIngest,
        ILogger<FileManifestService> _logger)
    {
        private const string ProposedContentHashConstraintName = "IX_file_manifests_proposed_content_hash";

        public async Task<List<Chunk>> GetChunksAsync(
            string[] chunkHashes,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            List<byte[]> normalizedHashes = [.. chunkHashes.Select(Hasher.FromHexStringHash)];
            List<Chunk> ownedChunks = await _dbContext.Chunks
                .Where(chunk => normalizedHashes.Contains(chunk.Hash))
                .Where(chunk => _dbContext.ChunkOwnerships.Any(ownership =>
                    ownership.ChunkHash == chunk.Hash && ownership.OwnerId == userId))
                .ToListAsync(cancellationToken);

            Dictionary<string, Chunk> chunkMap = ownedChunks.ToDictionary(
                chunk => Hasher.ToHexStringHash(chunk.Hash),
                StringComparer.OrdinalIgnoreCase);

            List<Chunk> result = [];
            for (int i = 0; i < chunkHashes.Length; i++)
            {
                string normalizedHash = Hasher.ToHexStringHash(normalizedHashes[i]);
                if (!chunkMap.TryGetValue(normalizedHash, out Chunk? chunk))
                {
                    chunk = await _chunkIngest.ReuseExistingChunkAsync(userId, normalizedHashes[i], cancellationToken);
                    chunkMap[normalizedHash] = chunk;
                }

                result.Add(chunk);
            }
            return result;
        }

        public async Task<FileManifest?> GetReusableOwnedManifestAsync(
            byte[] proposedContentHash,
            Guid userId,
            bool includeChunks = false,
            CancellationToken cancellationToken = default)
        {
            IQueryable<FileManifest> query = _dbContext.FileManifests.AsQueryable();
            if (includeChunks)
            {
                query = query.Include(fileManifest => fileManifest.FileManifestChunks);
            }

            FileManifest? fileManifest = await query
                .FirstOrDefaultAsync(
                    candidate => candidate.ComputedContentHash == proposedContentHash
                        || candidate.ProposedContentHash == proposedContentHash,
                    cancellationToken);
            if (fileManifest is null)
            {
                return null;
            }

            bool ownsAllChunks = await UserOwnsManifestChunksAsync(fileManifest.Id, userId, cancellationToken);
            if (!ownsAllChunks)
            {
                throw new BadRequestException("File content must be uploaded before it can be referenced.");
            }

            return fileManifest;
        }

        public async Task<FileManifest> CreateNewFileManifestAsync(
            List<Chunk> chunks,
            string fileName,
            string? contentType,
            byte[] proposedContentHash,
            Guid userId,
            bool includeChunks = false,
            CancellationToken cancellationToken = default)
        {
            FileManifest newFileManifest = new()
            {
                ContentType = FileContentTypeResolver.Resolve(fileName, contentType),
                SizeBytes = chunks.Sum(chunk => chunk.PlainSizeBytes),
                ProposedContentHash = proposedContentHash,
                PreviewGeneratorVersion = PreviewGeneratorProvider.DefaultGeneratorVersion,
            };

            await _dbContext.FileManifests.AddAsync(newFileManifest, cancellationToken);
            for (int i = 0; i < chunks.Count; i++)
            {
                if (chunks[i].GCScheduledAfter.HasValue)
                {
                    chunks[i].GCScheduledAfter = null;
                }

                FileManifestChunk fileChunk = new()
                {
                    ChunkOrder = i,
                    ChunkHash = chunks[i].Hash,
                    FileManifest = newFileManifest,
                };
                await _dbContext.FileManifestChunks.AddAsync(fileChunk, cancellationToken);
            }
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return newFileManifest;
            }
            catch (DbUpdateException ex) when (IsConcurrentManifestInsertConflict(ex))
            {
                _logger.LogDebug(ex, "File manifest was created concurrently, reloading the existing row");
                DetachPendingManifest(newFileManifest);
            }

            return await GetReusableOwnedManifestAsync(
                proposedContentHash,
                userId,
                includeChunks,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    "A file manifest was inserted concurrently but could not be reloaded.");
        }

        public async Task<int> ClearGcSchedulesForManifestReferencesAsync(
            Guid fileManifestId,
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.Chunks
                .Where(chunk => chunk.GCScheduledAfter != null
                    && (_dbContext.FileManifestChunks.Any(manifestChunk =>
                            manifestChunk.FileManifestId == fileManifestId
                            && manifestChunk.ChunkHash == chunk.Hash)
                        || _dbContext.FileManifests.Any(fileManifest =>
                            fileManifest.Id == fileManifestId
                            && (fileManifest.SmallFilePreviewHash == chunk.Hash
                                || fileManifest.LargeFilePreviewHash == chunk.Hash))))
                .ExecuteUpdateAsync(
                    update => update.SetProperty(chunk => chunk.GCScheduledAfter, (DateTime?)null),
                    cancellationToken);
        }

        private async Task<bool> UserOwnsManifestChunksAsync(
            Guid fileManifestId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            bool hasForeignChunk = await _dbContext.FileManifestChunks
                .Where(manifestChunk => manifestChunk.FileManifestId == fileManifestId)
                .AnyAsync(manifestChunk => !_dbContext.ChunkOwnerships.Any(ownership =>
                    ownership.OwnerId == userId && ownership.ChunkHash == manifestChunk.ChunkHash), cancellationToken);

            return !hasForeignChunk;
        }

        private void DetachPendingManifest(FileManifest manifest)
        {
            foreach (EntityEntry<FileManifestChunk> entry in _dbContext.ChangeTracker
                .Entries<FileManifestChunk>()
                .Where(candidate => candidate.State == EntityState.Added
                    && ReferenceEquals(candidate.Entity.FileManifest, manifest))
                .ToArray())
            {
                entry.State = EntityState.Detached;
            }

            EntityEntry<FileManifest> manifestEntry = _dbContext.Entry(manifest);
            if (manifestEntry.State == EntityState.Added)
            {
                manifestEntry.State = EntityState.Detached;
            }
        }

        private static bool IsConcurrentManifestInsertConflict(DbUpdateException ex)
        {
            return ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: ProposedContentHashConstraintName,
            };
        }
    }
}
