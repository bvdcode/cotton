// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Cotton.Server.Helpers;
using Cotton.Server.Database;
using Cotton.Server.Database.Models;
using Microsoft.EntityFrameworkCore;
using Cotton.Server.Database.Models.Enums;
using Cotton.Server.Validators;

namespace Cotton.Server.Extensions
{
    public static class CottonDbContextExtensions
    {
        public static async Task<Node> GetUserTrashNodeAsync(this CottonDbContext dbContext, Guid ownerId)
        {
            var layout = await dbContext.GetLatestUserLayoutAsync(ownerId);
            return await GetRootNodeAsync(dbContext, layout.Id, ownerId, NodeType.Trash);
        }

        public static async Task<Node> GetRootNodeAsync(this CottonDbContext dbContext, Guid layoutId, Guid ownerId, NodeType type)
        {
            var currentNode = await dbContext.Nodes
                .AsNoTracking()
                .Include(x => x.Layout)
                .Where(x => x.Layout.OwnerId == ownerId
                    && x.LayoutId == layoutId
                    && x.ParentId == null
                    && x.Type == type)
                .FirstOrDefaultAsync();
            if (currentNode == null)
            {
                NameValidator.TryNormalizeAndValidate(type.ToString(), out string normalized, out _);
                Node newNode = new()
                {
                    Type = type,
                    OwnerId = ownerId,
                    LayoutId = layoutId,
                };
                newNode.SetName(type.ToString());
                await dbContext.Nodes.AddAsync(newNode);
                await dbContext.SaveChangesAsync();
                return newNode;
            }
            return currentNode;
        }

        public static async Task<Layout> GetLatestUserLayoutAsync(this CottonDbContext dbContext, Guid ownerId)
        {
            var found = await dbContext.UserLayouts
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
                await dbContext.UserLayouts.AddAsync(newLayout);
                await dbContext.SaveChangesAsync();
                return newLayout;
            }
            return found;
        }

        public static Task<Chunk?> FindChunkAsync(this CottonDbContext dbContext, string sha256hex)
        {
            if (!HashHelpers.IsValidHash(sha256hex))
            {
                throw new ArgumentException("Invalid hash format.", nameof(sha256hex));
            }
            return FindChunkAsync(dbContext, Convert.FromHexString(sha256hex));
        }

        public static async Task<Chunk?> FindChunkAsync(this CottonDbContext dbContext, byte[] sha256)
        {
            return await dbContext.Chunks.FindAsync(sha256);
        }
    }
}
