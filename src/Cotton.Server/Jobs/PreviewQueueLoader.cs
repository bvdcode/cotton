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
            FileManifest? item = await GetCandidates(dbContext, id)
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

        private static IQueryable<FileManifest> GetCandidates(CottonDbContext dbContext, Guid? id = null)
        {
            IQueryable<NodeFile> availableFiles = PreviewFileQuery.AvailableFiles(dbContext);
            IQueryable<FileManifest> manifests = dbContext.FileManifests;
            if (id.HasValue)
            {
                manifests = manifests.Where(manifest => manifest.Id == id.Value);
            }
            IQueryable<Guid> candidateIds = manifests
                .Where(manifest => (manifest.PreviewGenerationError == null
                        && (manifest.SmallFilePreviewHash == null || manifest.SmallFilePreviewHashEncrypted == null))
                    || (manifest.PreviewGenerationError != null
                        && manifest.PreviewGeneratorVersion != PreviewGeneratorProvider.FailedAttemptVersion))
                .Select(manifest => manifest.Id);

            foreach (IGrouping<int, KeyValuePair<string, int>> group in PreviewGeneratorProvider.GetGeneratorVersions().GroupBy(generator => generator.Value))
            {
                int version = group.Key;
                string[] generatorIds = [.. group.Select(generator => generator.Key)];
                IQueryable<FileManifest> generated = manifests
                    .Where(manifest => manifest.PreviewGenerationError == null
                        && manifest.PreviewGeneratorId != null && generatorIds.Contains(manifest.PreviewGeneratorId));
                candidateIds = candidateIds
                    .Union(generated.Where(manifest => manifest.PreviewGeneratorVersion < version).Select(manifest => manifest.Id))
                    .Union(generated.Where(manifest => manifest.PreviewGeneratorVersion > version).Select(manifest => manifest.Id));
            }

            return manifests.Where(manifest => candidateIds.Contains(manifest.Id)
                && availableFiles.Any(file => file.FileManifestId == manifest.Id));
        }
    }
}
