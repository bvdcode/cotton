// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync;

namespace Cotton.Sdk.Sync
{
    /// <summary>
    /// Provides durable synchronization feed operations.
    /// </summary>
    public interface ICottonSyncClient
    {
        /// <summary>
        /// Gets ordered remote mutations after the supplied cursor.
        /// </summary>
        Task<SyncChangesResponseDto> GetChangesAsync(
            long sinceCursor = 0,
            int limit = 500,
            CancellationToken cancellationToken = default);
    }
}
