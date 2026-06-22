// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton;
using Cotton.Files;
using Cotton.Sdk.Internal;
using System.Net;

namespace Cotton.Sdk.Files;

/// <summary>
/// Provides Cotton file operations used by synchronization clients.
/// </summary>
public sealed class CottonFileClient : ICottonFileClient
{
    private const string IfMatchHeaderName = "If-Match";

    private readonly CottonHttpTransport _transport;

    internal CottonFileClient(CottonHttpTransport transport)
    {
        _transport = transport;
    }

    /// <summary>
    /// Creates a file entry from already uploaded chunks.
    /// </summary>
    public Task<NodeFileManifestDto> CreateFromChunksAsync(
        CreateFileFromChunksRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _transport.SendJsonAsync<NodeFileManifestDto>(
            HttpMethod.Post,
            Routes.V1.Files + "/from-chunks",
            request,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Updates file content from already uploaded chunks.
    /// </summary>
    public Task<NodeFileManifestDto> UpdateContentAsync(
        Guid nodeFileId,
        CreateFileFromChunksRequestDto request,
        string? expectedETag = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _transport.SendJsonAsync<NodeFileManifestDto>(
            HttpMethod.Patch,
            $"{Routes.V1.Files}/{nodeFileId}/update-content",
            request,
            headers: CreateIfMatchHeader(expectedETag),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Moves a file to a different parent node.
    /// </summary>
    public Task<NodeFileManifestDto> MoveAsync(
        Guid nodeFileId,
        Guid parentId,
        string? expectedETag = null,
        CancellationToken cancellationToken = default)
    {
        return _transport.SendJsonAsync<NodeFileManifestDto>(
            HttpMethod.Patch,
            $"{Routes.V1.Files}/{nodeFileId}/move",
            new MoveFileRequestDto { ParentId = parentId },
            headers: CreateIfMatchHeader(expectedETag),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Renames a file.
    /// </summary>
    public Task<NodeFileManifestDto> RenameAsync(
        Guid nodeFileId,
        string name,
        string? expectedETag = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _transport.SendJsonAsync<NodeFileManifestDto>(
            HttpMethod.Patch,
            $"{Routes.V1.Files}/{nodeFileId}/rename",
            new RenameFileRequestDto { Name = name.Trim() },
            headers: CreateIfMatchHeader(expectedETag),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Merges metadata into a file entry.
    /// </summary>
    public Task<NodeFileManifestDto> UpdateMetadataAsync(
        Guid nodeFileId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return _transport.SendJsonAsync<NodeFileManifestDto>(
            HttpMethod.Patch,
            $"{Routes.V1.Files}/{nodeFileId}/metadata",
            new Dictionary<string, string>(metadata),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Deletes a file entry.
    /// </summary>
    public Task DeleteAsync(
        Guid nodeFileId,
        bool skipTrash = false,
        string? expectedETag = null,
        CancellationToken cancellationToken = default)
    {
        return _transport.SendNoContentAsync(
            HttpMethod.Delete,
            $"{Routes.V1.Files}/{nodeFileId}?skipTrash={skipTrash.ToString().ToLowerInvariant()}",
            headers: CreateIfMatchHeader(expectedETag),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Restores a trashed file entry.
    /// </summary>
    public Task<RestoreOutcomeDto> RestoreAsync(
        Guid nodeFileId,
        RestoreItemRequestDto? request = null,
        CancellationToken cancellationToken = default)
    {
        return _transport.SendJsonAsync<RestoreOutcomeDto>(
            HttpMethod.Post,
            $"{Routes.V1.Files}/{nodeFileId}/restore",
            request ?? new RestoreItemRequestDto(),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Lists file versions.
    /// </summary>
    public Task<List<FileVersionDto>> GetVersionsAsync(Guid nodeFileId, CancellationToken cancellationToken = default)
    {
        return _transport.SendJsonAsync<List<FileVersionDto>>(
            HttpMethod.Get,
            $"{Routes.V1.Files}/{nodeFileId}/versions",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Downloads owned file content through bearer-token authentication.
    /// </summary>
    public Task DownloadContentAsync(
        Guid nodeFileId,
        Stream destination,
        bool download = false,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string path = $"{Routes.V1.Files}/{nodeFileId}/content?download={download.ToString().ToLowerInvariant()}";
        return _transport.DownloadAsync(path, destination, authorize: true, progress, cancellationToken);
    }

    /// <summary>
    /// Downloads a byte range of owned file content through bearer-token authentication.
    /// </summary>
    public Task DownloadContentRangeAsync(
        Guid nodeFileId,
        Stream destination,
        long offset,
        long length,
        string? expectedETag = null,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        if (length - 1 > long.MaxValue - offset)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "Range end exceeds Int64.MaxValue.");
        }

        string path = $"{Routes.V1.Files}/{nodeFileId}/content?download=false";
        IReadOnlyDictionary<string, string> headers = CreateRangeDownloadHeaders(offset, length, expectedETag);
        return _transport.DownloadAsync(
            path,
            destination,
            authorize: true,
            progress,
            cancellationToken,
            headers,
            expectedStatusCode: HttpStatusCode.PartialContent,
            validateResponse: response => ValidateRangeResponse(response, offset, length, expectedETag));
    }

    /// <summary>
    /// Gets the immutable content manifest and ordered chunk metadata for an owned file.
    /// </summary>
    public Task<FileContentManifestDto> GetContentManifestAsync(
        Guid nodeFileId,
        string? expectedETag = null,
        CancellationToken cancellationToken = default)
    {
        return _transport.SendJsonAsync<FileContentManifestDto>(
            HttpMethod.Get,
            $"{Routes.V1.Files}/{nodeFileId}/content-manifest",
            headers: CreateIfMatchHeader(expectedETag),
            cancellationToken: cancellationToken);
    }

    private static IReadOnlyDictionary<string, string>? CreateIfMatchHeader(string? expectedETag)
    {
        if (string.IsNullOrWhiteSpace(expectedETag))
        {
            return null;
        }

        string trimmed = expectedETag.Trim();
        string headerValue = trimmed == "*" || trimmed.StartsWith('"')
            ? trimmed
            : "\"" + trimmed.Trim('"') + "\"";
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [IfMatchHeaderName] = headerValue,
        };
    }

    private static IReadOnlyDictionary<string, string> CreateRangeDownloadHeaders(
        long offset,
        long length,
        string? expectedETag)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Range"] = $"bytes={offset}-{offset + length - 1}",
        };

        IReadOnlyDictionary<string, string>? ifMatchHeader = CreateIfMatchHeader(expectedETag);
        if (ifMatchHeader is not null)
        {
            foreach (KeyValuePair<string, string> header in ifMatchHeader)
            {
                headers[header.Key] = header.Value;
            }
        }

        return headers;
    }

    private static long ValidateRangeResponse(
        HttpResponseMessage response,
        long offset,
        long length,
        string? expectedETag)
    {
        var contentRange = response.Content.Headers.ContentRange
            ?? throw CreateInvalidRangeResponseException(response, "missing Content-Range header");
        if (!string.Equals(contentRange.Unit, "bytes", StringComparison.OrdinalIgnoreCase)
            || contentRange.From != offset
            || !contentRange.To.HasValue)
        {
            throw CreateInvalidRangeResponseException(response, $"unexpected Content-Range '{contentRange}'");
        }

        long receivedLength = contentRange.To.Value - contentRange.From!.Value + 1;
        if (receivedLength <= 0 || receivedLength > length)
        {
            throw CreateInvalidRangeResponseException(response, $"unexpected range length {receivedLength}");
        }

        if (response.Content.Headers.ContentLength.HasValue
            && response.Content.Headers.ContentLength.Value != receivedLength)
        {
            throw CreateInvalidRangeResponseException(
                response,
                $"Content-Length {response.Content.Headers.ContentLength.Value} does not match Content-Range length {receivedLength}");
        }

        if (!string.IsNullOrWhiteSpace(expectedETag) && expectedETag.Trim() != "*")
        {
            string? responseETag = response.Headers.ETag?.Tag;
            if (string.IsNullOrWhiteSpace(responseETag)
                || !string.Equals(NormalizeETag(responseETag), NormalizeETag(expectedETag), StringComparison.Ordinal))
            {
                throw CreateInvalidRangeResponseException(response, "response ETag does not match expected ETag");
            }
        }

        return receivedLength;
    }

    private static CottonApiException CreateInvalidRangeResponseException(HttpResponseMessage response, string reason)
    {
        return new CottonApiException(
            response.StatusCode,
            null,
            $"Cotton API range download returned an invalid response: {reason}.");
    }

    private static string NormalizeETag(string value)
    {
        string normalized = value.Trim();
        if (normalized.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..].Trim();
        }

        return normalized.Trim('"');
    }
}
