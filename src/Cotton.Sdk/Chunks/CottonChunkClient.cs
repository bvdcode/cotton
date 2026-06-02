// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sdk.Internal;

namespace Cotton.Sdk.Chunks;

/// <summary>
/// Provides chunk upload and deduplication operations.
/// </summary>
public sealed class CottonChunkClient : ICottonChunkClient
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
        using HttpResponseMessage response = await _transport.SendAsync(
            HttpMethod.Get,
            "/api/v1/chunks/" + Uri.EscapeDataString(hash) + "/exists",
            cancellationToken: cancellationToken).ConfigureAwait(false);
        await CottonHttpTransport.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return bool.TryParse(body, out bool exists) && exists;
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
            "/api/v1/chunks/raw?hash=" + Uri.EscapeDataString(hash),
            content,
            contentType,
            authorize: true,
            cancellationToken);
    }
}
