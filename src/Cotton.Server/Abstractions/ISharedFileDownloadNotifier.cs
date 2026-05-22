// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Abstractions
{
    /// <summary>
    /// Defines the shared file download notifier contract used by the server runtime.
    /// </summary>
    public interface ISharedFileDownloadNotifier
    {
        /// <summary>
        /// Notifies connected clients that once occurred.
        /// </summary>
        Task NotifyOnceAsync(Guid ownerId, Guid tokenId, string fileName, HttpContext httpContext, CancellationToken ct);
    }
}
