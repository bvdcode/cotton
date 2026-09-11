// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Jobs
{
    internal static class PreviewQueueLoader
    {
        public static async Task<List<FileManifest>> LoadNextAsync(
            CottonDbContext dbContext,
            IReadOnlyCollection<string> supportedContentTypes,
            IReadOnlyDictionary<string, int> generatorVersionsByContentType,
            int limit,
            ISet<Guid> knownItemIds,
            CancellationToken cancellationToken)
        {
            if (limit <= 0)
            {
                return [];
            }

            IQueryable<FileManifest> processableItemsQuery = dbContext.FileManifests
                .Where(fileManifest => supportedContentTypes.Contains(fileManifest.ContentType));
            var itemCandidates = processableItemsQuery
                .Where(fileManifest => fileManifest.SmallFilePreviewHash == null
                    || fileManifest.SmallFilePreviewHashEncrypted == null)
                .Where(fileManifest => fileManifest.PreviewGenerationError == null)
                .Select(fileManifest => new
                {
                    fileManifest.Id,
                    fileManifest.CreatedAt,
                });

            foreach (IGrouping<int, KeyValuePair<string, int>> versionGroup in generatorVersionsByContentType.GroupBy(item => item.Value))
            {
                int generatorVersion = versionGroup.Key;
                string[] contentTypes = [.. versionGroup.Select(item => item.Key)];
                itemCandidates = itemCandidates.Union(dbContext.FileManifests
                    .Where(fileManifest => contentTypes.Contains(fileManifest.ContentType))
                    .Where(fileManifest => fileManifest.PreviewGeneratorVersion != generatorVersion)
                    .Select(fileManifest => new
                    {
                        fileManifest.Id,
                        fileManifest.CreatedAt,
                    }));
            }

            List<Guid> itemIds = await itemCandidates
                .OrderByDescending(candidate => candidate.CreatedAt)
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
                .Include(fileManifest => fileManifest.NodeFiles)
                .Include(fileManifest => fileManifest.FileManifestChunks)
                .ThenInclude(manifestChunk => manifestChunk.Chunk)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);
            Dictionary<Guid, FileManifest> itemsById = items.ToDictionary(item => item.Id);
            return [.. newItemIds.Where(itemsById.ContainsKey).Select(id => itemsById[id])];
        }
    }
}
