// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Topology.Abstractions;
using EasyExtensions.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Topology
{
    /// <summary>
    /// EF-backed service that creates and retrieves Cotton layout roots.
    /// </summary>
    public class StorageLayoutService(CottonDbContext _dbContext) : ILayoutService
    {
        private static readonly SemaphoreSlim _layoutSemaphore = new(1, 1);

        /// <inheritdoc />
        public async Task<Node> GetUserTrashRootAsync(Guid ownerId, CancellationToken ct = default)
        {
            Layout layout = await GetOrCreateLatestUserLayoutAsync(ownerId, ct);
            return await GetOrCreateRootNodeAsync(layout.Id, ownerId, NodeType.Trash, ct);
        }

        /// <inheritdoc />
        public async Task<Node> GetOrCreateRootNodeAsync(Guid layoutId, Guid ownerId, NodeType nodeType, CancellationToken ct = default)
        {
            await _layoutSemaphore.WaitAsync(ct);
            try
            {
                Node? currentNode = await _dbContext.Nodes
                    .AsNoTracking()
                    .Include(x => x.Layout)
                    .Where(x => x.Layout.OwnerId == ownerId
                        && x.LayoutId == layoutId
                        && x.ParentId == null
                        && x.Type == nodeType)
                    .FirstOrDefaultAsync(ct);
                if (currentNode is null)
                {
                    Node newNode = new()
                    {
                        Type = nodeType,
                        OwnerId = ownerId,
                        LayoutId = layoutId,
                    };
                    newNode.SetName(nodeType.ToString());
                    await _dbContext.Nodes.AddAsync(newNode, ct);
                    await _dbContext.SaveChangesAsync(ct);
                    return newNode;
                }
                return currentNode;
            }
            finally
            {
                _layoutSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task<Layout> GetOrCreateLatestUserLayoutAsync(Guid ownerId, CancellationToken ct = default)
        {
            await _layoutSemaphore.WaitAsync(ct);
            try
            {
                Layout? found = await _dbContext.UserLayouts
                    .Where(x => x.OwnerId == ownerId && x.IsActive)
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefaultAsync(ct);
                if (found is null)
                {
                    Layout newLayout = new()
                    {
                        IsActive = true,
                        OwnerId = ownerId,
                    };
                    await _dbContext.UserLayouts.AddAsync(newLayout, ct);
                    await _dbContext.SaveChangesAsync(ct);
                    return newLayout;
                }
                return found;
            }
            finally
            {
                _layoutSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task<Chunk?> FindChunkAsync(byte[] hash, CancellationToken ct = default)
        {
            return await _dbContext.Chunks.FindAsync([hash], ct);
        }

        /// <inheritdoc />
        public async Task<Node> CreateTrashItemAsync(Guid userId, CancellationToken ct = default)
        {
            Node trashRoot = await GetUserTrashRootAsync(userId, ct);
            Node trashItem = new()
            {
                OwnerId = userId,
                LayoutId = trashRoot.LayoutId,
                Type = NodeType.Trash
            };
            trashItem.SetParent(trashRoot);
            trashItem.SetName("trash-item-" + StringHelpers.CreateRandomString(8));
            await _dbContext.Nodes.AddAsync(trashItem, ct);
            await _dbContext.SaveChangesAsync(ct);
            return trashItem;
        }
    }
}
