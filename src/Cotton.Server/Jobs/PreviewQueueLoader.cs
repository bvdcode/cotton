// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Previews;
using Cotton.Server.Services.Previews;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Jobs
{
    internal static class PreviewQueueLoader
    {
        public static async Task<List<Guid>> LoadNextIdsAsync(
            CottonDbContext dbContext,
            int limit,
            ISet<Guid> processedItemIds,
            CancellationToken cancellationToken)
        {
            if (limit <= 0)
            {
                return [];
            }

            return await GetCandidates(dbContext)
                .Where(manifest => !processedItemIds.Contains(manifest.Id))
                .OrderByDescending(candidate => candidate.CreatedAt)
                .ThenBy(candidate => candidate.Id)
                .Select(candidate => candidate.Id)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }

        public static async Task<FileManifest?> LoadItemAsync(
            CottonDbContext dbContext, Guid id, CancellationToken cancellationToken)
        {
            FileManifest? item = await GetCandidates(dbContext)
                .Include(fileManifest => fileManifest.FileManifestChunks)
                .ThenInclude(manifestChunk => manifestChunk.Chunk)
                .AsSplitQuery()
                .SingleOrDefaultAsync(manifest => manifest.Id == id, cancellationToken);
            if (item is not null)
            {
                await PreviewFileQuery.AvailableFiles(dbContext)
                    .Where(file => file.FileManifestId == id).LoadAsync(cancellationToken);
            }
            return item;
        }

        private static IQueryable<FileManifest> GetCandidates(CottonDbContext dbContext)
        {
            IQueryable<NodeFile> availableFiles = PreviewFileQuery.AvailableFiles(dbContext);
            return dbContext.FileManifests
                .Where(manifest => availableFiles.Any(file => file.FileManifestId == manifest.Id))
                .Where(manifest => manifest.PreviewGeneratorVersion != PreviewGeneratorProvider.GenerationVersion
                    || ((manifest.SmallFilePreviewHash == null || manifest.SmallFilePreviewHashEncrypted == null)
                        && manifest.PreviewGenerationError == null));
        }
    }
}
