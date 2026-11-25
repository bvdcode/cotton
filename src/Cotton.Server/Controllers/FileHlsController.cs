// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Auth;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Models;
using Cotton.Server.Models.Requests;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using Mediana.Core.Memory;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class FileHlsController(
        IStoragePipeline _storage,
        CottonDbContext _dbContext,
        VideoTranscoder _videoTranscoder,
        HlsTranscodeCoordinator _hlsTranscodes,
        HlsSegmentCache _segmentCache,
        IMediator _mediator,
        PublicShareLookupFailureLimiter _publicShareLookupFailures,
        ILogger<FileHlsController> _logger) : ControllerBase
    {
        [HttpGet(Routes.V1.Files + "/{nodeFileId:guid}/hls/master.m3u8")]
        public async Task<IActionResult> HlsMasterPlaylistByToken(
            [FromRoute] Guid nodeFileId,
            [FromQuery] FileDownloadCredentials credentials)
        {
            HlsSourceLookup lookup = await ResolveTranscodableSourceAsync(nodeFileId, credentials);
            if (lookup.Failure is not null)
            {
                return lookup.Failure;
            }

            string credentialQuery = credentials.ToQueryString();
            string PlaylistUrl(string qualityName) =>
                Routes.V1.Files + $"/{nodeFileId}/hls/playlist.m3u8?{credentialQuery}&quality={qualityName}";

            HlsManifestBuilder.HlsVariant[] variants =
            [
                new("Source", 12_000_000, PlaylistUrl("source")),
                new("1080p", 7_000_000, PlaylistUrl("high")),
                new("720p", 3_000_000, PlaylistUrl("medium")),
                new("480p", 1_400_000, PlaylistUrl("low")),
            ];

            Response.Headers.CacheControl = "private, no-store, no-transform";
            Response.RegisterDeleteAfterUse(_dbContext, lookup.DownloadToken);
            return Content(
                HlsManifestBuilder.BuildMaster(variants),
                HlsManifestBuilder.ContentType,
                System.Text.Encoding.UTF8);
        }

        [HttpGet(Routes.V1.Files + "/{nodeFileId:guid}/hls/playlist.m3u8")]
        public async Task<IActionResult> HlsVodPlaylistByToken(
            [FromRoute] Guid nodeFileId,
            [FromQuery] FileDownloadCredentials credentials,
            [FromQuery] string? quality = null)
        {
            HlsSourceLookup lookup = await ResolveTranscodableSourceAsync(nodeFileId, credentials);
            if (lookup.Failure is not null)
            {
                return lookup.Failure;
            }

            HlsVideoPlan? plan = await GetVideoPlanAsync(lookup.NodeFile!);
            if (plan is null)
            {
                return UnsupportedVideo();
            }

            HlsRendition rendition = HlsRenditionProfile.Parse(quality);
            string credentialQuery = credentials.ToQueryString();
            string qualityName = rendition.ToString().ToLowerInvariant();
            string manifest = HlsManifestBuilder.Build(
                plan.Segments,
                segmentIndex => Routes.V1.Files
                    + $"/{nodeFileId}/hls/seg-{segmentIndex}.ts?{credentialQuery}&quality={qualityName}");

            Response.Headers.CacheControl = "private, no-store, no-transform";
            Response.RegisterDeleteAfterUse(_dbContext, lookup.DownloadToken);
            return Content(manifest, HlsManifestBuilder.ContentType, System.Text.Encoding.UTF8);
        }

        [HttpGet(Routes.V1.Files + "/{nodeFileId:guid}/hls/seg-{segmentIndex:int}.ts")]
        public async Task<IActionResult> HlsSegmentByToken(
            [FromRoute] Guid nodeFileId,
            [FromRoute] int segmentIndex,
            [FromQuery] FileDownloadCredentials credentials,
            [FromQuery] string? quality = null)
        {
            if (segmentIndex < 0)
            {
                return CottonResult.BadRequest("Segment index must be non-negative.");
            }

            HlsSourceLookup lookup = await ResolveTranscodableSourceAsync(nodeFileId, credentials);
            if (lookup.Failure is not null)
            {
                return lookup.Failure;
            }

            NodeFile nodeFile = lookup.NodeFile!;
            DownloadToken? downloadToken = lookup.DownloadToken;
            HlsRendition rendition = HlsRenditionProfile.Parse(quality);
            string qualityName = rendition.ToString().ToLowerInvariant();
            string cacheKey = HlsSegmentCache.BuildKey(
                nodeFile.FileManifest.Id,
                qualityName,
                segmentIndex);

            if (_segmentCache.TryGet(cacheKey, out byte[]? cachedBytes))
            {
                Response.RegisterDeleteAfterUse(_dbContext, downloadToken);
                return ServeCachedHlsSegment(cachedBytes);
            }

            HlsVideoPlan? plan = await GetVideoPlanAsync(nodeFile);
            if (plan is null)
            {
                return UnsupportedVideo();
            }

            if (segmentIndex >= plan.Segments.Count)
            {
                return CottonResult.NotFound("Segment index out of range.");
            }

            Response.RegisterDeleteAfterUse(_dbContext, downloadToken);

            return await TranscodeHlsSegmentAsync(
                nodeFile,
                nodeFileId,
                segmentIndex,
                cacheKey,
                plan,
                rendition);
        }

        private async Task<IActionResult> TranscodeHlsSegmentAsync(
            NodeFile nodeFile,
            Guid nodeFileId,
            int segmentIndex,
            string cacheKey,
            HlsVideoPlan plan,
            HlsRendition rendition)
        {
            await using IAsyncDisposable segmentLease = await _hlsTranscodes.EnterSegmentAsync(
                cacheKey,
                HttpContext.RequestAborted);
            if (_segmentCache.TryGet(cacheKey, out byte[]? cachedBytes))
            {
                return ServeCachedHlsSegment(cachedBytes);
            }

            await using IAsyncDisposable transcodeLease = await _hlsTranscodes.EnterTranscodeAsync(
                HttpContext.RequestAborted);
            await using HlsByteSource source = OpenSource(nodeFile);
            try
            {
                byte[] bytes = await _videoTranscoder.TranscodeSegmentAsync(
                    source, plan, segmentIndex, rendition, HttpContext.RequestAborted);
                _segmentCache.Set(cacheKey, bytes);
                return ServeCachedHlsSegment(bytes);
            }
            catch (Exception ex) when (IsUnsupportedMedia(ex))
            {
                _logger.LogWarning(ex, "HLS segment {SegmentIndex} is unsupported for node file {NodeFileId}",
                    segmentIndex, nodeFileId);
                return UnsupportedVideo();
            }
        }

        private IActionResult ServeCachedHlsSegment(byte[] cachedBytes)
        {
            Response.Headers.CacheControl = "private, max-age=300";
            Response.Headers.ContentEncoding = "identity";
            return File(cachedBytes, VideoTranscoder.SegmentContentType);
        }

        private async Task<HlsSourceLookup> ResolveTranscodableSourceAsync(
            Guid nodeFileId,
            FileDownloadCredentials credentials)
        {
            if (!credentials.IsValid)
            {
                return new HlsSourceLookup(
                    null,
                    null,
                    this.ApiNotFound("File not found"));
            }

            IActionResult? blocked = this.GetPublicShareLookupBlockRejection(
                _publicShareLookupFailures,
                credentials.Value);
            if (blocked is not null)
            {
                return new HlsSourceLookup(null, null, blocked);
            }

            ResolveHlsSourceResult result = await _mediator.Send(
                new ResolveHlsSourceQuery(nodeFileId, credentials),
                HttpContext.RequestAborted);
            switch (result.Status)
            {
                case ResolveHlsSourceStatus.Success:
                    return new HlsSourceLookup(
                        result.NodeFile,
                        result.DownloadToken,
                        null);
                case ResolveHlsSourceStatus.TokenNotFound:
                    return new HlsSourceLookup(
                        null,
                        null,
                        this.ApiPublicShareNotFound(
                            _publicShareLookupFailures,
                            credentials.Value,
                            "File not found"));
                case ResolveHlsSourceStatus.FileNotFound:
                    return new HlsSourceLookup(
                        null,
                        null,
                        CottonResult.NotFound("File not found"));
                case ResolveHlsSourceStatus.NotTranscodable:
                    return new HlsSourceLookup(
                        null,
                        null,
                        CottonResult.BadRequest(
                            "This file is not eligible for on-the-fly transcoding."));
                default:
                    throw new ArgumentOutOfRangeException(nameof(result.Status));
            }
        }

        private HlsByteSource OpenSource(NodeFile nodeFile)
        {
            FileManifest manifest = nodeFile.FileManifest;
            string[] uids = manifest.FileManifestChunks.GetChunkHashes();
            PipelineContext context = new()
            {
                FileSizeBytes = manifest.SizeBytes,
                ChunkLengths = manifest.FileManifestChunks.GetChunkLengths(),
            };

            return new HlsByteSource(_storage.GetBlobStream(uids, context));
        }

        private async Task<HlsVideoPlan?> GetVideoPlanAsync(NodeFile nodeFile)
        {
            Guid manifestId = nodeFile.FileManifest.Id;
            if (_segmentCache.TryGetPlan(manifestId, out HlsVideoPlan? cached))
            {
                return cached;
            }

            CancellationToken ct = HttpContext.RequestAborted;
            await using IAsyncDisposable manifestLease = await _hlsTranscodes.EnterProbeManifestAsync(manifestId, ct);
            if (_segmentCache.TryGetPlan(manifestId, out cached))
            {
                return cached;
            }

            await using IAsyncDisposable probeLease = await _hlsTranscodes.EnterProbeAsync(ct);
            await using HlsByteSource source = OpenSource(nodeFile);
            try
            {
                HlsVideoPlan plan = await _videoTranscoder.PlanAsync(source, ct);
                _segmentCache.SetPlan(manifestId, plan);
                return plan;
            }
            catch (Exception ex) when (IsUnsupportedMedia(ex))
            {
                _logger.LogWarning(ex, "Could not plan HLS segments for node file {NodeFileId}", nodeFile.Id);
                return null;
            }
        }

        private static bool IsUnsupportedMedia(Exception exception) =>
            exception is InvalidDataException or NotSupportedException or MediaMemoryLimitException;

        private static IActionResult UnsupportedVideo() =>
            CottonResult.BadRequest("This video cannot be streamed. Download the original file to play it.");
    }
}
