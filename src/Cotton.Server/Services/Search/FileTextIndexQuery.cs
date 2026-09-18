// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;

namespace Cotton.Server.Services.Search
{
    internal static class FileTextIndexQuery
    {
        public static IQueryable<FileManifest> Pending(CottonDbContext dbContext, string[] contentTypes)
        {
            return dbContext.FileManifests
                .Where(manifest => manifest.TextIndexVersion != VectorIndexDefinition.Version)
                .Where(manifest => manifest.NodeFiles.Any(file =>
                    file.Node.Layout.IsActive && file.Node.Type == NodeType.Default
                    && (file.OriginalNodeFileId == Guid.Empty || file.Id == file.OriginalNodeFileId)
                    && contentTypes.Contains(file.ContentType)));
        }
    }
}
