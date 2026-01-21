// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Validators;
using EasyExtensions.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Topology
{
    public class StorageLayoutService(CottonDbContext _dbContext)
    {
        private static readonly SemaphoreSlim _layoutSemaphore = new(1, 1);

        public async Task<Node> GetUserTrashRootAsync(Guid ownerId)
        {
            var layout = await GetOrCreateLatestUserLayoutAsync(ownerId);
            return await GetOrCreateRootNodeAsync(layout.Id, ownerId, NodeType.Trash);
        }

        public async Task<Node> GetOrCreateRootNodeAsync(Guid layoutId, Guid ownerId, NodeType nodeType)
        {
            await _layoutSemaphore.WaitAsync();
            try
            {
                var currentNode = await _dbContext.Nodes
                    .AsNoTracking()
                    .Include(x => x.Layout)
                    .Where(x => x.Layout.OwnerId == ownerId
                        && x.LayoutId == layoutId
                        && x.ParentId == null
                        && x.Type == nodeType)
                    .FirstOrDefaultAsync();
                if (currentNode == null)
                {
                    NameValidator.TryNormalizeAndValidate(nodeType.ToString(), out string normalized, out _);
                    Node newNode = new()
                    {
                        Type = nodeType,
                        OwnerId = ownerId,
                        LayoutId = layoutId,
                    };
                    newNode.SetName(nodeType.ToString());
                    await _dbContext.Nodes.AddAsync(newNode);
                    await _dbContext.SaveChangesAsync();
                    return newNode;
                }
                return currentNode;
            }
            finally
            {
                _layoutSemaphore.Release();
            }
        }

        public async Task<Layout> GetOrCreateLatestUserLayoutAsync(Guid ownerId)
        {
            await _layoutSemaphore.WaitAsync();
            try
            {
                var found = await _dbContext.UserLayouts
                    .Where(x => x.OwnerId == ownerId && x.IsActive)
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefaultAsync();
                if (found == null)
                {
                    Layout newLayout = new()
                    {
                        IsActive = true,
                        OwnerId = ownerId,
                    };
                    await _dbContext.UserLayouts.AddAsync(newLayout);
                    await _dbContext.SaveChangesAsync();
                    return newLayout;
                }
                return found;
            }
            finally
            {
                _layoutSemaphore.Release();
            }
        }

        public async Task<Chunk?> FindChunkAsync(byte[] hash)
        {
            return await _dbContext.Chunks.FindAsync(hash);
        }

        public async Task<Node> CreateTrashItemAsync(Guid userId)
        {
            Node trashRoot = await GetUserTrashRootAsync(userId);
            Node trashItem = new()
            {
                OwnerId = userId,
                LayoutId = trashRoot.LayoutId,
                ParentId = trashRoot.Id,
                Type = NodeType.Trash
            };
            trashItem.SetName("trash-item-" + StringHelpers.CreateRandomString(8));
            await _dbContext.Nodes.AddAsync(trashItem);
            await _dbContext.SaveChangesAsync();
            return trashItem;
        }
    }
}
