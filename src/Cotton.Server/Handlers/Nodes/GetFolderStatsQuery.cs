// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Nodes;
using Cotton.Topology;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Nodes
{
    public record GetFolderStatsQuery(Guid UserId, Guid NodeId, bool Recursive) : IRequest<FolderStatsDto?>;

    public class GetFolderStatsQueryHandler(CottonDbContext _dbContext)
        : IRequestHandler<GetFolderStatsQuery, FolderStatsDto?>
    {
        public async Task<FolderStatsDto?> Handle(GetFolderStatsQuery request, CancellationToken ct)
        {
            Node? parent = await _dbContext.Nodes.AsNoTracking()
                .AccessibleTo(request.UserId)
                .SingleOrDefaultAsync(node => node.Id == request.NodeId, ct);
            if (parent is null)
            {
                return null;
            }

            NodeDirectory directory = new(_dbContext, parent);
            IReadOnlyCollection<Guid> parentIds = request.Recursive
                ? await CollectFolderIdsAsync(directory, parent.Id, ct)
                : [parent.Id];

            int folders = request.Recursive
                ? parentIds.Count - 1
                : await directory.Nodes.AsNoTracking().CountAsync(ct);
            IQueryable<NodeFile> files = directory.GetFiles(parentIds).AsNoTracking();
            int fileCount = await files.CountAsync(ct);
            long sizeBytes = await files.SumAsync(
                file => (long?)file.FileManifest.SizeBytes, ct) ?? 0L;
            int encryptedFiles = await files.CountAsync(
                file => CottonDbContext.GetHstoreValue(file.Metadata, "isClientEncrypted") == "true", ct);

            return new FolderStatsDto
            {
                Folders = folders,
                Files = fileCount,
                EncryptedFiles = encryptedFiles,
                SizeBytes = sizeBytes,
            };
        }

        private static async Task<IReadOnlyCollection<Guid>> CollectFolderIdsAsync(
            NodeDirectory directory, Guid rootId, CancellationToken ct)
        {
            HashSet<Guid> ids = [rootId];
            List<Guid> frontier = [rootId];

            while (frontier.Count > 0)
            {
                Guid[] batch = [.. frontier];
                frontier.Clear();
                List<Guid> children = await directory.GetNodes(batch).AsNoTracking()
                    .Select(node => node.Id)
                    .ToListAsync(ct);
                foreach (Guid childId in children)
                {
                    if (ids.Add(childId))
                    {
                        frontier.Add(childId);
                    }
                }
            }

            return ids;
        }
    }
}
