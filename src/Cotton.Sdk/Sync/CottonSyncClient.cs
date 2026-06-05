// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Shared.Contracts.Sync;
using Cotton.Sdk.Internal;

namespace Cotton.Sdk.Sync;

/// <summary>
/// Provides durable synchronization feed operations.
/// </summary>
public sealed class CottonSyncClient : ICottonSyncClient
{
    private readonly CottonHttpTransport _transport;

    internal CottonSyncClient(CottonHttpTransport transport)
    {
        _transport = transport;
    }

    /// <inheritdoc />
    public Task<SyncChangesResponseDto> GetChangesAsync(
        long sinceCursor = 0,
        int limit = 500,
        CancellationToken cancellationToken = default)
    {
        if (sinceCursor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sinceCursor), sinceCursor, "Cursor must be non-negative.");
        }

        string path = $"/api/v1/sync/changes?since={sinceCursor}&limit={limit}";
        return _transport.SendJsonAsync<SyncChangesResponseDto>(
            HttpMethod.Get,
            path,
            cancellationToken: cancellationToken);
    }
}
