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
        public static async Task<List<FileManifest>> LoadNextAsync(
            CottonDbContext dbContext,
            int limit,
            ISet<Guid> knownItemIds,
            CancellationToken cancellationToken)
        {
            if (limit <= 0)
            {
                return [];
            }

            IQueryable<NodeFile> availableFiles = PreviewFileQuery.AvailableFiles(dbContext);
            IQueryable<FileManifest> itemCandidates = dbContext.FileManifests
                .Where(manifest => availableFiles.Any(file => file.FileManifestId == manifest.Id))
                .Where(manifest => !knownItemIds.Contains(manifest.Id))
                .Where(manifest => manifest.PreviewGeneratorVersion != PreviewGeneratorProvider.GenerationVersion
                    || ((manifest.SmallFilePreviewHash == null || manifest.SmallFilePreviewHashEncrypted == null)
                        && manifest.PreviewGenerationError == null));

            List<Guid> itemIds = await itemCandidates
                .OrderByDescending(candidate => candidate.CreatedAt)
                .ThenBy(candidate => candidate.Id)
                .Select(candidate => candidate.Id)
                .Take(limit)
                .ToListAsync(cancellationToken);
            List<Guid> newItemIds = [.. itemIds.Where(knownItemIds.Add)];
            if (newItemIds.Count == 0)
            {
                return [];
            }

            List<FileManifest> items = await dbContext.FileManifests
                .Where(fileManifest => newItemIds.Contains(fileManifest.Id))
                .Include(fileManifest => fileManifest.FileManifestChunks)
                .ThenInclude(manifestChunk => manifestChunk.Chunk)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);
            await availableFiles.Where(file => newItemIds.Contains(file.FileManifestId)).LoadAsync(cancellationToken);
            Dictionary<Guid, FileManifest> itemsById = items.ToDictionary(item => item.Id);
            return [.. newItemIds.Where(itemsById.ContainsKey).Select(id => itemsById[id])];
        }
    }
}
