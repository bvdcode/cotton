// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Http;

namespace Cotton.Server.Abstractions
{
    public interface ISharedFileDownloadNotifier
    {
        Task NotifyOnceAsync(Guid ownerId, Guid tokenId, string fileName, HttpContext httpContext, CancellationToken ct);
    }
}
