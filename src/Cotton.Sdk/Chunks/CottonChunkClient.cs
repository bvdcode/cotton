// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton;
using Cotton.Sdk.Internal;

namespace Cotton.Sdk.Chunks;

/// <summary>
/// Provides chunk upload and deduplication operations.
/// </summary>
public class CottonChunkClient : ICottonChunkClient
{
    private readonly CottonHttpTransport _transport;

    internal CottonChunkClient(CottonHttpTransport transport)
    {
        _transport = transport;
    }

    /// <summary>
    /// Checks whether a chunk with the specified lowercase hexadecimal hash already exists for the user.
    /// </summary>
    public async Task<bool> ExistsAsync(string hash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        string path = Routes.V1.Chunks + "/" + Uri.EscapeDataString(hash) + "/exists";
        return await _transport.SendJsonAsync<bool>(
            HttpMethod.Get,
            path,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Uploads one raw chunk body using the server raw chunk endpoint.
    /// </summary>
    public Task UploadRawAsync(
        string hash,
        Stream content,
        string contentType = "application/octet-stream",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        return _transport.UploadRawAsync(
            Routes.V1.Chunks + "/raw?hash=" + Uri.EscapeDataString(hash),
            content,
            contentType,
            authorize: true,
            cancellationToken);
    }
}
