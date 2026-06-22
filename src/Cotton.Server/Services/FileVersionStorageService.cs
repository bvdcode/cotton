// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Coordinates file version storage.
    /// </summary>
    public sealed class FileVersionStorageService(CottonDbContext _dbContext)
    {
        /// <summary>
        /// Deletes historical versions async.
        /// </summary>
        public async Task<long> DeleteHistoricalVersionsAsync(
            Guid userId,
            IReadOnlyCollection<NodeFile> versions,
            CancellationToken ct = default)
        {
            if (versions.Count == 0)
            {
                return 0;
            }

            long removedBytes = versions.Sum(x => x.FileManifest.SizeBytes);
            Guid[] versionIds = [.. versions.Select(x => x.Id)];
            Guid[] wrapperNodeIds = [.. versions.Select(x => x.NodeId).Distinct()];

            await _dbContext.DownloadTokens
                .Where(x => x.CreatedByUserId == userId && versionIds.Contains(x.NodeFileId))
                .ExecuteDeleteAsync(ct);

            _dbContext.NodeFiles.RemoveRange(versions);
            await _dbContext.SaveChangesAsync(ct);

            foreach (Guid wrapperNodeId in wrapperNodeIds)
            {
                await DeleteWrapperIfEmptyAsync(userId, wrapperNodeId, ct);
            }

            return removedBytes;
        }

        private async Task DeleteWrapperIfEmptyAsync(Guid userId, Guid wrapperNodeId, CancellationToken ct)
        {
            Node? wrapper = await _dbContext.Nodes
                .Where(x => x.Id == wrapperNodeId && x.OwnerId == userId && x.Type == NodeType.Trash)
                .SingleOrDefaultAsync(ct);
            if (wrapper is null)
            {
                return;
            }

            bool hasChildNodes = await _dbContext.Nodes
                .AnyAsync(x => x.ParentId == wrapper.Id && x.OwnerId == userId, ct);
            if (hasChildNodes)
            {
                return;
            }

            bool hasFiles = await _dbContext.NodeFiles
                .AnyAsync(x => x.NodeId == wrapper.Id && x.OwnerId == userId, ct);
            if (hasFiles)
            {
                return;
            }

            _dbContext.Nodes.Remove(wrapper);
            await _dbContext.SaveChangesAsync(ct);
        }
    }
}
