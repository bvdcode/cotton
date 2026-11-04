// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Archives;
using Cotton.Server.Models.Results;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace Cotton.Server.Services
{
    public class ArchiveDownloadResultExecutor(
        IMediator mediator, StoredZipArchiveWriter writer, IStoragePipeline storage,
        ILogger<ArchiveDownloadResultExecutor> logger) : FileResultExecutorBase(logger)
    {
        public async Task ExecuteAsync(ActionContext context, ArchiveDownloadResult result)
        {
            CancellationToken ct = context.HttpContext.RequestAborted;
            await using IAsyncDisposable lease = await writer.AcquireAsync(ct);
            await using ArchiveDownloadPlan plan = new();
            await mediator.Send(new PrepareArchiveDownloadRequest(result.Ticket, plan), ct);
            result.FileDownloadName = result.Ticket.FileName;
            result.EntityTag = EntityTagHeaderValue.Parse(plan.EntityTag);
            result.EnableRangeProcessing = true;
            HttpResponse response = context.HttpContext.Response;
            response.Headers.ContentEncoding = "identity";
            response.Headers.CacheControl = "private, no-store, no-transform";
            response.Headers.XContentTypeOptions = "nosniff";
            var (range, rangeLength, serveBody) = SetHeadersAndLog(
                context, result, plan.SizeBytes, enableRangeProcessing: true, etag: result.EntityTag);
            if (!serveBody)
            {
                return;
            }
            await writer.WriteRangeAsync(response.Body, ReadSourcesAsync(plan, ct),
                range?.From ?? 0, range is null ? plan.SizeBytes : rangeLength,
                plan.CentralDirectoryOffset, plan.EntityTag, ct);
        }

        private async IAsyncEnumerable<StoredZipSourceEntry> ReadSourcesAsync(
            ArchiveDownloadPlan plan, [EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (ArchiveDownloadEntry entry in plan.ReadAsync(ct))
            {
                yield return entry switch
                {
                    ArchiveDownloadDirectoryEntry directory => new(directory.Path, 0, true,
                        _ => ValueTask.FromResult<Stream>(Stream.Null)),
                    ArchiveDownloadFileEntry file => new(file.Path, file.SizeBytes, false,
                        _ => ValueTask.FromResult(OpenFileStream(file))),
                    _ => throw new InvalidOperationException($"Unsupported archive entry type '{entry.GetType().Name}'."),
                };
            }
        }

        private Stream OpenFileStream(ArchiveDownloadFileEntry file)
        {
            PipelineContext context = new()
            {
                FileSizeBytes = file.SizeBytes,
                ChunkLengths = new Dictionary<string, long>(file.ChunkLengths, StringComparer.OrdinalIgnoreCase),
            };
            return storage.GetBlobStream([.. file.ChunkHashes], context);
        }
    }
}
