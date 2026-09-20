// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;

namespace Cotton.Server.Services.Previews
{
    internal static class PreviewFileQuery
    {
        public static IQueryable<NodeFile> AvailableFiles(CottonDbContext dbContext)
        {
            return dbContext.NodeFiles.Where(file => file.Node.Layout.IsActive
                && (CottonDbContext.GetHstoreValue(file.Metadata, "isClientEncrypted") ?? "false") != "true");
        }
    }
}
