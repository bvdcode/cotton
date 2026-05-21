// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Requests;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Cotton.Server.Controllers;

[ApiController]
[Route(Routes.V1.Archives)]
public sealed class ArchiveController(
    ArchiveDownloadService _archives,
    ArchiveDownloadTicketStore _tickets,
    StoredZipArchiveWriter _zipWriter,
    IStoragePipeline _storage) : ControllerBase
{
    [Authorize]
    [HttpPost("download-link")]
    public async Task<IActionResult> CreateDownloadLink(
        [FromBody] CreateArchiveDownloadLinkRequest request,
        CancellationToken cancellationToken)
    {
        CreateArchiveDownloadLinkResult result = await _archives.CreateDownloadLinkAsync(
            User.GetUserId(),
            request,
            cancellationToken);

        return result.StatusCode switch
        {
            StatusCodes.Status200OK => Ok(result.Link),
            StatusCodes.Status400BadRequest => BadRequest(result.Error),
            StatusCodes.Status404NotFound => NotFound(result.Error),
            _ => StatusCode(result.StatusCode, result.Error),
        };
    }

    [AllowAnonymous]
    [HttpGet("{token}")]
    public async Task<IActionResult> Download([FromRoute] string token, CancellationToken cancellationToken)
    {
        if (!_tickets.TryGet(token, out ArchiveDownloadTicket? ticket))
        {
            return NotFound("Archive download link not found.");
        }

        IReadOnlyList<StoredZipSourceEntry> entries = [.. ticket.Entries.Select(ToSourceEntry)];
        Response.ContentType = "application/zip";
        Response.ContentLength = ticket.SizeBytes;
        Response.Headers.ContentEncoding = "identity";
        Response.Headers.CacheControl = "private, no-store, no-transform";
        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileNameStar = ticket.FileName,
        }.ToString();

        await _zipWriter.WriteAsync(Response.Body, entries, cancellationToken);
        return new EmptyResult();
    }

    private StoredZipSourceEntry ToSourceEntry(ArchiveDownloadEntry entry)
    {
        return entry switch
        {
            ArchiveDownloadDirectoryEntry directory => new StoredZipSourceEntry(
                directory.Path,
                0,
                true,
                _ => ValueTask.FromResult<Stream>(Stream.Null)),
            ArchiveDownloadFileEntry file => new StoredZipSourceEntry(
                file.Path,
                file.SizeBytes,
                false,
                _ => ValueTask.FromResult(OpenFileStream(file))),
            _ => throw new InvalidOperationException($"Unsupported archive entry type '{entry.GetType().Name}'."),
        };
    }

    private Stream OpenFileStream(ArchiveDownloadFileEntry file)
    {
        PipelineContext context = new()
        {
            FileSizeBytes = file.SizeBytes,
            ChunkLengths = new Dictionary<string, long>(file.ChunkLengths, StringComparer.OrdinalIgnoreCase),
        };
        return _storage.GetBlobStream([.. file.ChunkHashes], context);
    }
}
