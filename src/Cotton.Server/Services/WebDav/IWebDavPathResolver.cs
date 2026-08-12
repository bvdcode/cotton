// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.WebDav
{
    public interface IWebDavPathResolver
    {
        Task<WebDavResolveResult> ResolvePathAsync(Guid userId, string path, CancellationToken ct = default);

        Task<WebDavResolveResult> ResolveMetadataAsync(Guid userId, string path, CancellationToken ct = default);

        Task<WebDavParentResult> GetParentNodeAsync(Guid userId, string path, CancellationToken ct = default);
    }
}
