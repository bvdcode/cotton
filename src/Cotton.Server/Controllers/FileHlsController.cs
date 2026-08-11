// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Models.Enums;
using Cotton.Previews;
using Cotton.Previews.Http;
using Cotton.Server.Auth;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Models;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Cotton.Server.Controllers
{
    /// <summary>
    /// Exposes HTTP endpoints for HLS file playback.
    /// </summary>
    [ApiController]
    public class FileHlsController(
        IStoragePipeline _storage,
        CottonDbContext _dbContext,
        VideoTranscoder _videoTranscoder,
        HlsTranscodeCoordinator _hlsTranscodes,
        HlsSegmentCache _segmentCache,
        IMemoryCache _cache,
        IMediator _mediator,
        PublicShareLookupFailureLimiter _publicShareLookupFailures,
        ILogger<FileHlsController> _logger) : ControllerBase
    {
        private static readonly TimeSpan MediaProbeCacheLifetime = TimeSpan.FromHours(1);

        /// <summary>
        /// Returns an HLS master playlist for a token-authorized file.
        /// </summary>
        [HttpGet(Routes.V1.Files + "/{nodeFileId:guid}/hls/master.m3u8")]
        public async Task<IActionResult> HlsMasterPlaylistByToken(
            [FromRoute] Guid nodeFileId,
            [FromQuery] string token)
        {
            HlsSourceLookup lookup = await ResolveTranscodableSourceAsync(nodeFileId, token);
            if (lookup.Failure is not null)
            {
                return lookup.Failure;
            }

            string encodedToken = Uri.EscapeDataString(token);
            string PlaylistUrl(string qualityName) =>
                Routes.V1.Files + $"/{nodeFileId}/hls/playlist.m3u8?token={encodedToken}&quality={qualityName}";

            const string variantCodecs = "avc1.640029,mp4a.40.2";
            HlsManifestBuilder.HlsVariant[] variants =
            [
                new HlsManifestBuilder.HlsVariant(
                    Name: "Source",
                    BandwidthBitsPerSecond: 8_000_000,
                    Width: 1920,
                    Height: 1080,
                    Codecs: variantCodecs,
                    PlaylistUrl: PlaylistUrl("source")),
                new HlsManifestBuilder.HlsVariant(
                    Name: "1080p",
                    BandwidthBitsPerSecond: 3_000_000,
                    Width: 1920,
                    Height: 1080,
                    Codecs: variantCodecs,
                    PlaylistUrl: PlaylistUrl("high")),
                new HlsManifestBuilder.HlsVariant(
                    Name: "720p",
                    BandwidthBitsPerSecond: 1_500_000,
                    Width: 1280,
                    Height: 720,
                    Codecs: variantCodecs,
                    PlaylistUrl: PlaylistUrl("medium")),
                new HlsManifestBuilder.HlsVariant(
                    Name: "480p",
                    BandwidthBitsPerSecond: 700_000,
                    Width: 854,
                    Height: 480,
                    Codecs: variantCodecs,
                    PlaylistUrl: PlaylistUrl("low")),
            ];

            Response.Headers.CacheControl = "private, no-store, no-transform";
            Response.RegisterDeleteAfterUse(_dbContext, lookup.DownloadToken!);
            return Content(
                HlsManifestBuilder.BuildMaster(variants),
                HlsManifestBuilder.ContentType,
                System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// Returns an HLS media playlist for a token-authorized file.
        /// </summary>
        [HttpGet(Routes.V1.Files + "/{nodeFileId:guid}/hls/playlist.m3u8")]
        public async Task<IActionResult> HlsVodPlaylistByToken(
            [FromRoute] Guid nodeFileId,
            [FromQuery] string token,
            [FromQuery] string? quality = null)
        {
            HlsSourceLookup lookup = await ResolveTranscodableSourceAsync(nodeFileId, token);
            if (lookup.Failure is not null)
            {
                return lookup.Failure;
            }

            MediaProbeInfo? probe = await ProbeMediaAsync(lookup.NodeFile!);
            HlsRendition rendition = HlsRenditionProfile.Parse(quality);
            string encodedToken = Uri.EscapeDataString(token);
            string qualityName = rendition.ToString().ToLowerInvariant();
            string manifest = HlsManifestBuilder.Build(
                probe?.DurationSeconds ?? 0,
                segmentIndex => Routes.V1.Files
                    + $"/{nodeFileId}/hls/seg-{segmentIndex}.ts?token={encodedToken}&quality={qualityName}");

            Response.Headers.CacheControl = "private, no-store, no-transform";
            Response.RegisterDeleteAfterUse(_dbContext, lookup.DownloadToken!);
            return Content(manifest, HlsManifestBuilder.ContentType, System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// Returns one HLS transport-stream segment for a token-authorized file.
        /// </summary>
        [HttpGet(Routes.V1.Files + "/{nodeFileId:guid}/hls/seg-{segmentIndex:int}.ts")]
        public async Task<IActionResult> HlsSegmentByToken(
            [FromRoute] Guid nodeFileId,
            [FromRoute] int segmentIndex,
            [FromQuery] string token,
            [FromQuery] string? quality = null)
        {
            if (segmentIndex < 0)
            {
                return CottonResult.BadRequest("Segment index must be non-negative.");
            }

            HlsSourceLookup lookup = await ResolveTranscodableSourceAsync(nodeFileId, token);
            if (lookup.Failure is not null)
            {
                return lookup.Failure;
            }

            NodeFile nodeFile = lookup.NodeFile!;
            DownloadToken downloadToken = lookup.DownloadToken!;
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

            MediaProbeInfo? probe = await ProbeMediaAsync(nodeFile);
            if (probe?.DurationSeconds is null or <= 0)
            {
                return CottonResult.BadRequest("Could not determine source duration for HLS segmentation.");
            }

            HlsManifestBuilder.HlsManifestPlan manifestPlan = HlsManifestBuilder.Plan(probe.DurationSeconds.Value);
            if (segmentIndex >= manifestPlan.SegmentCount)
            {
                return CottonResult.NotFound("Segment index out of range.");
            }

            Response.RegisterDeleteAfterUse(_dbContext, downloadToken);

            return await TranscodeHlsSegmentAsync(
                nodeFile,
                nodeFileId,
                segmentIndex,
                cacheKey,
                manifestPlan,
                rendition,
                probe);
        }

        private async Task<IActionResult> TranscodeHlsSegmentAsync(
            NodeFile nodeFile,
            Guid nodeFileId,
            int segmentIndex,
            string cacheKey,
            HlsManifestBuilder.HlsManifestPlan manifestPlan,
            HlsRendition rendition,
            MediaProbeInfo probe)
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
            double startSeconds = HlsManifestBuilder.StartTimeOf(segmentIndex);
            double segmentDuration = manifestPlan.DurationOf(segmentIndex);
            HlsRenditionProfile.EncoderPlan encoderPlan = HlsRenditionProfile.Plan(
                rendition,
                probe.VideoCodec,
                probe.AudioCodec);

            Response.ContentType = VideoTranscoder.SegmentContentType;
            Response.Headers.CacheControl = "private, max-age=300";
            Response.Headers.ContentEncoding = "identity";

            using MemoryStream captureStream = new();
            TeeStream tee = new(Response.Body, captureStream);
            bool transcodeSucceeded = false;

            await using Stream sourceStream = OpenSourceStream(nodeFile);
            try
            {
                await _videoTranscoder.TranscodeSegmentAsync(
                    sourceStream,
                    tee,
                    startSeconds,
                    segmentDuration,
                    encoderPlan,
                    HttpContext.RequestAborted);
                transcodeSucceeded = true;
            }
            catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "HLS segment {SegmentIndex} failed for node file {NodeFileId}",
                    segmentIndex,
                    nodeFileId);
                if (!Response.HasStarted)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError);
                }
            }

            if (transcodeSucceeded && captureStream.Length > 0)
            {
                _segmentCache.Set(cacheKey, captureStream.ToArray());
            }

            return new EmptyResult();
        }

        private IActionResult ServeCachedHlsSegment(byte[] cachedBytes)
        {
            Response.Headers.CacheControl = "private, max-age=300";
            Response.Headers.ContentEncoding = "identity";
            return File(cachedBytes, VideoTranscoder.SegmentContentType);
        }

        private async Task<HlsSourceLookup> ResolveTranscodableSourceAsync(
            Guid nodeFileId,
            string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return new HlsSourceLookup(
                    null,
                    null,
                    this.ApiNotFound("File not found"));
            }

            IActionResult? blocked = this.GetPublicShareLookupBlockRejection(
                _publicShareLookupFailures,
                token);
            if (blocked is not null)
            {
                return new HlsSourceLookup(null, null, blocked);
            }

            ResolveHlsSourceResult result = await _mediator.Send(
                new ResolveHlsSourceQuery(nodeFileId, token),
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
                            token,
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

        private Stream OpenSourceStream(NodeFile nodeFile)
        {
            FileManifest manifest = nodeFile.FileManifest;
            string[] uids = manifest.FileManifestChunks.GetChunkHashes();
            PipelineContext context = new()
            {
                FileSizeBytes = manifest.SizeBytes,
                ChunkLengths = manifest.FileManifestChunks.GetChunkLengths(),
            };

            return _storage.GetBlobStream(uids, context);
        }

        private async Task<MediaProbeInfo?> ProbeMediaAsync(NodeFile nodeFile)
        {
            Guid manifestId = nodeFile.FileManifest.Id;
            string cacheKey = $"hls-media-probe:{manifestId}";
            if (_cache.TryGetValue<MediaProbeInfo>(cacheKey, out MediaProbeInfo? cached))
            {
                return cached;
            }

            await using IAsyncDisposable manifestLease = await _hlsTranscodes.EnterProbeManifestAsync(
                manifestId,
                HttpContext.RequestAborted);
            if (_cache.TryGetValue<MediaProbeInfo>(cacheKey, out cached))
            {
                return cached;
            }

            await using IAsyncDisposable probeLease = await _hlsTranscodes.EnterProbeAsync(
                HttpContext.RequestAborted);
            MediaProbeInfo? probe;
            await using (Stream probeStream = OpenSourceStream(nodeFile))
            await using (RangeStreamServer probeServer = new(probeStream, _logger))
            {
                probe = await FfmpegBinary.TryGetMediaProbeAsync(
                    probeServer.Url,
                    cancellationToken: HttpContext.RequestAborted)
                    .ConfigureAwait(false);
            }

            if (probe is not null)
            {
                _cache.Set(cacheKey, probe, MediaProbeCacheLifetime);
            }

            return probe;
        }
    }
}
