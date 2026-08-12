// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Abstractions
{
    public interface ISharedFileDownloadNotifier
    {
        Task NotifyOnceAsync(Guid ownerId, Guid tokenId, string fileName, HttpContext httpContext, CancellationToken ct);
    }
}
