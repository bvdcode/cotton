// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Extensions;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Cotton.Server.Services
{
    public static class FileDownloadResultFactory
    {
        public static FileStreamResult Create(
            HttpResponse response,
            IStoragePipeline storage,
            NodeFile nodeFile,
            bool download)
        {
            string[] uids = nodeFile.FileManifest.FileManifestChunks.GetChunkHashes();
            PipelineContext context = new()
            {
                FileSizeBytes = nodeFile.FileManifest.SizeBytes,
                ChunkLengths = nodeFile.FileManifest.FileManifestChunks.GetChunkLengths()
            };
            Stream stream = storage.GetBlobStream(uids, context);
            response.Headers.ContentEncoding = "identity";
            response.Headers.CacheControl = "private, no-store, no-transform";
            bool requestedInline = !download;
            FileResponseSecurity.ApplyFileResponseHeaders(
                response,
                nodeFile.FileManifest.ContentType,
                requestedInline);

            return new FileStreamResult(
                stream,
                FileResponseSecurity.ResolveContentTypeForResponse(
                    nodeFile.FileManifest.ContentType,
                    requestedInline))
            {
                FileDownloadName = FileResponseSecurity.ResolveFileDownloadName(
                    nodeFile.Name,
                    requestedInline,
                    nodeFile.FileManifest.ContentType),
                LastModified = new DateTimeOffset(nodeFile.CreatedAt),
                EntityTag = FileETags.CreateContentEntityTag(nodeFile),
                EnableRangeProcessing = true,
            };
        }
    }
}
