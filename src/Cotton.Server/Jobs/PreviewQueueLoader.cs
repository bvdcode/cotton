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
        private const string PreviousOutOfMemoryError =
            "All matching preview generators failed. (Exception of type 'System.OutOfMemoryException' was thrown.)";

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
            IQueryable<NodeFile> availableFiles = PreviewFileQuery.AvailableFiles(dbContext);
            FileManifest? item = await dbContext.FileManifests
                .Where(manifest => availableFiles.Any(file => file.FileManifestId == manifest.Id))
                .SingleOrDefaultAsync(manifest => manifest.Id == id, cancellationToken);
            if (item is null || !NeedsPreview(item))
            {
                return null;
            }

            await dbContext.Entry(item).Collection(manifest => manifest.FileManifestChunks)
                .Query().Include(manifestChunk => manifestChunk.Chunk).LoadAsync(cancellationToken);
            await availableFiles.Where(file => file.FileManifestId == id).LoadAsync(cancellationToken);
            return item;
        }

        private static bool NeedsPreview(FileManifest manifest)
        {
            if (manifest.SizeBytes == 0)
            {
                return false;
            }

            if (manifest.PreviewGenerationError is not null)
            {
                return manifest.PreviewGeneratorVersion != PreviewGeneratorProvider.FailedAttemptVersion
                    || manifest.PreviewGenerationError == PreviousOutOfMemoryError;
            }

            return manifest.SmallFilePreviewHash is null || manifest.SmallFilePreviewHashEncrypted is null
                || (manifest.PreviewGeneratorId is not null
                    && PreviewGeneratorProvider.GetGeneratorVersions().TryGetValue(manifest.PreviewGeneratorId, out int version)
                    && manifest.PreviewGeneratorVersion != version);
        }

        private static IQueryable<FileManifest> GetCandidates(CottonDbContext dbContext)
        {
            IQueryable<NodeFile> availableFiles = PreviewFileQuery.AvailableFiles(dbContext);
            IQueryable<FileManifest> manifests = dbContext.FileManifests;
            IQueryable<Guid> candidateIds = manifests
                .Where(manifest => (manifest.PreviewGenerationError == null
                        && (manifest.SmallFilePreviewHash == null || manifest.SmallFilePreviewHashEncrypted == null))
                    || (manifest.PreviewGenerationError != null
                        && manifest.PreviewGeneratorVersion != PreviewGeneratorProvider.FailedAttemptVersion))
                .Select(manifest => manifest.Id);

            candidateIds = candidateIds.Union(manifests
                .Where(manifest => manifest.PreviewGeneratorVersion == PreviewGeneratorProvider.FailedAttemptVersion
                    && manifest.PreviewGenerationError == PreviousOutOfMemoryError)
                .Select(manifest => manifest.Id));

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

            return manifests.Where(manifest => manifest.SizeBytes > 0 && candidateIds.Contains(manifest.Id)
                && availableFiles.Any(file => file.FileManifestId == manifest.Id));
        }
    }
}
