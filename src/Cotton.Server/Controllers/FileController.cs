// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Previews;
using Cotton.Previews.Http;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Hubs;
using Cotton.Server.Jobs;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using Cotton.Validators;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Helpers;
using EasyExtensions.Mediator;
using EasyExtensions.Quartz.Extensions;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Net.Http.Headers;
using Quartz;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class FileController(
        IMediator _mediator,
        IStoragePipeline _storage,
        CottonDbContext _dbContext,
        ISchedulerFactory _scheduler,
        IHubContext<EventHub> _hubContext,
        FileManifestService _fileManifestService,
        NodeFileHistoryService _history,
        UserStorageQuotaService _quota,
        VideoTranscoder _videoTranscoder,
        HlsSegmentCache _segmentCache,
        IMemoryCache _cache,
        ILogger<FileController> _logger) : ControllerBase
    {
        private const int DefaultSharedFileTokenLength = 16;

        [HttpGet("/s/{token}")]
        [HttpHead("/s/{token}")]
        public async Task<IActionResult> Share(
            [FromRoute] string token,
            [FromQuery] string? view = null)
        {
            var result = await _mediator.Send(new ShareFileQuery(token, view, Request));

            switch (result.Kind)
            {
                case "badRequest":
                    return this.ApiBadRequest(result.ErrorMessage ?? "Bad request");
                case "notFound":
                    return this.ApiNotFound(result.ErrorMessage ?? "File not found");
                case "redirect":
                    return Redirect(result.RedirectUrl ?? "/");
                case "html":
                    return Content(result.HtmlContent ?? string.Empty, "text/html; charset=utf-8");
                case "head":
                    Response.Headers.ContentEncoding = "identity";
                    Response.Headers.CacheControl = "private, no-store, no-transform";
                    Response.ContentType = result.ContentType;
                    Response.ContentLength = result.ContentLength;
                    if (!string.IsNullOrWhiteSpace(result.EntityTag))
                    {
                        Response.Headers.ETag = result.EntityTag;
                    }
                    var cd = new ContentDispositionHeaderValue(result.Inline == true ? "inline" : "attachment")
                    {
                        FileNameStar = result.FileName,
                        FileName = result.FileName,
                    };
                    Response.Headers[HeaderNames.ContentDisposition] = cd.ToString();
                    return new EmptyResult();
                case "stream":
                    Response.Headers.ContentEncoding = "identity";
                    Response.Headers.CacheControl = "private, no-store, no-transform";
                    if (result.DeleteAfterUse && result.DeleteTokenId.HasValue)
                    {
                        Response.OnCompleted(async () =>
                        {
                            var tokenEntity = await _dbContext.DownloadTokens
                                .FirstOrDefaultAsync(x => x.Id == result.DeleteTokenId.Value);
                            if (tokenEntity != null)
                            {
                                _dbContext.DownloadTokens.Remove(tokenEntity);
                                await _dbContext.SaveChangesAsync();
                            }
                        });
                    }
                    return File(
                        result.FileStream!,
                        result.ContentType!,
                        fileDownloadName: result.DownloadName,
                        lastModified: result.LastModified,
                        entityTag: result.EntityTagValue!,
                        enableRangeProcessing: true);
                default:
                    return this.ApiBadRequest("Invalid share response");
            }
        }

        [Authorize]
        [HttpDelete(Routes.V1.Files + "/{nodeFileId:guid}")]
        public async Task<IActionResult> DeleteFile(
            [FromRoute] Guid nodeFileId,
            [FromQuery] bool skipTrash = false)
        {
            Guid userId = User.GetUserId();
            Guid? parentNodeId = await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.Id == nodeFileId && x.OwnerId == userId)
                .Select(x => (Guid?)x.NodeId)
                .SingleOrDefaultAsync();
            DeleteFileQuery query = new(userId, nodeFileId, skipTrash);
            await _mediator.Send(query);
            await _hubContext.Clients.User(userId.ToString()).SendAsync(
                "FileDeleted",
                new NodeFileDeletedEventDto(nodeFileId, parentNodeId));
            return NoContent();
        }

        [Authorize]
        [HttpPost(Routes.V1.Files + "/{nodeFileId:guid}/restore")]
        public async Task<IActionResult> RestoreFile(
            [FromRoute] Guid nodeFileId,
            [FromBody] RestoreItemRequest? request)
        {
            Guid userId = User.GetUserId();
            request ??= new RestoreItemRequest();

            var outcome = await _mediator.Send(new RestoreFileQuery(
                userId,
                nodeFileId,
                request.CreateMissingParents,
                request.Overwrite));

            if (outcome.Status == RestoreStatus.Restored)
            {
                object restoredFilePayload = outcome.RestoredFile is not null
                    ? outcome.RestoredFile
                    : new { id = nodeFileId };
                await _hubContext.Clients.User(userId.ToString()).SendAsync(
                    "FileRestored",
                    restoredFilePayload);
            }

            return Ok(outcome);
        }

        [Authorize]
        [HttpPatch(Routes.V1.Files + "/{nodeFileId:guid}/move")]
        public async Task<IActionResult> MoveFile(
            [FromRoute] Guid nodeFileId,
            [FromBody] MoveFileRequest request)
        {
            MoveFileCommand command = new()
            {
                NodeFileId = nodeFileId,
                ParentId = request.ParentId,
                UserId = User.GetUserId(),
            };
            var dto = await _mediator.Send(command);
            return Ok(dto);
        }

        [Authorize]
        [HttpPatch(Routes.V1.Files + "/{nodeFileId:guid}/rename")]
        public async Task<IActionResult> RenameFile(
            [FromRoute] Guid nodeFileId,
            [FromBody] RenameFileRequest request)
        {
            bool isValidName = NameValidator.TryNormalizeAndValidate(request.Name,
                out string normalizedName,
                out string? errorMessage);
            if (!isValidName)
            {
                return CottonResult.BadRequest(errorMessage);
            }

            Guid userId = User.GetUserId();
            var layoutId = await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.Id == nodeFileId && x.OwnerId == userId)
                .Select(x => (Guid?)x.Node.LayoutId)
                .SingleOrDefaultAsync();
            if (layoutId is null)
            {
                return CottonResult.NotFound("File not found.");
            }

            // Per-layout namespace serialization for rename — same rationale as
            // CreateFile / CreateNode / MoveFile / MoveNode.
            await using var tx = await _dbContext.Database.BeginTransactionAsync();
            await LayoutLocks.AcquireForLayoutAsync(_dbContext, layoutId.Value, default);

            var nodeFile = await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .Where(x => x.Id == nodeFileId && x.OwnerId == userId)
                .SingleOrDefaultAsync();
            if (nodeFile == null || nodeFile.Node.Type != NodeType.Default)
            {
                return CottonResult.NotFound("File not found.");
            }

            string nameKey = NameValidator.NormalizeAndGetNameKey(request.Name);
            bool fileExists = await _dbContext.NodeFiles
                .AnyAsync(x =>
                    x.NodeId == nodeFile.NodeId &&
                    x.OwnerId == userId &&
                    x.NameKey == nameKey &&
                    x.Id != nodeFileId);
            if (fileExists)
            {
                return this.ApiConflict("A file with the same name key already exists in this folder: " + nameKey);
            }

            bool nodeExists = await _dbContext.Nodes
                .AnyAsync(x =>
                    x.ParentId == nodeFile.NodeId &&
                    x.OwnerId == userId &&
                    x.Type == nodeFile.Node.Type &&
                    x.NameKey == nameKey);
            if (nodeExists)
            {
                return this.ApiConflict("A folder with the same name key already exists in this folder: " + nameKey);
            }

            nodeFile.SetName(request.Name);
            await _dbContext.SaveChangesAsync();
            await tx.CommitAsync();

            var mapped = nodeFile.Adapt<NodeFileManifestDto>();
            await _hubContext.Clients.User(userId.ToString()).SendAsync("FileRenamed", mapped);
            return Ok(mapped);
        }

        [Authorize]
        [HttpPatch(Routes.V1.Files + "/{nodeFileId:guid}/metadata")]
        [ProducesResponseType<NodeFileManifestDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<CottonResult>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<CottonResult>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateFileMetadata(
            [FromRoute] Guid nodeFileId,
            [FromBody] Dictionary<string, string?>? patch)
        {
            if (patch is null)
            {
                return CottonResult.BadRequest("Metadata patch is required.");
            }

            if (patch.Any(x => string.IsNullOrWhiteSpace(x.Key)))
            {
                return CottonResult.BadRequest("Metadata keys must be non-empty strings.");
            }

            if (patch.Any(x => x.Value is null))
            {
                return CottonResult.BadRequest("Metadata values must be strings.");
            }

            Guid userId = User.GetUserId();
            var nodeFile = await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .Where(x => x.Id == nodeFileId && x.OwnerId == userId)
                .SingleOrDefaultAsync();
            if (nodeFile == null || nodeFile.Node.Type != NodeType.Default)
            {
                return CottonResult.NotFound("File not found.");
            }

            var metadata = nodeFile.Metadata is null
                ? []
                : new Dictionary<string, string>(nodeFile.Metadata);
            foreach (var (key, value) in patch)
            {
                metadata[key] = value!;
            }

            nodeFile.Metadata = metadata;
            await _dbContext.SaveChangesAsync();

            var mapped = nodeFile.Adapt<NodeFileManifestDto>();
            try
            {
                await _hubContext.Clients.User(userId.ToString()).SendAsync("FileUpdated", mapped);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to send file metadata update notification for file {NodeFileId}",
                    nodeFileId);
            }

            return Ok(mapped);
        }

        [Authorize]
        [HttpGet(Routes.V1.Files + "/{nodeFileId:guid}/download-link")]
        public async Task<IActionResult> DownloadFile(
            [FromRoute] Guid nodeFileId,
            [FromQuery] int expireAfterMinutes = 1440,
            [FromQuery] string? customToken = "",
            [FromQuery] bool deleteAfterUse = false)
        {
            const int maxExpireMinutes = 60 * 24 * 365; // 1 year
            ArgumentOutOfRangeException.ThrowIfGreaterThan(expireAfterMinutes, maxExpireMinutes, nameof(expireAfterMinutes));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expireAfterMinutes, nameof(expireAfterMinutes));

            if (!string.IsNullOrWhiteSpace(customToken))
            {
                bool exists = await _dbContext.DownloadTokens
                    .AnyAsync(x => x.Token == customToken);
                if (exists)
                {
                    return this.ApiConflict("The custom token is already in use. Please choose a different one.");
                }
            }

            var userId = User.GetUserId();
            var nodeFile = await _dbContext.NodeFiles
                .Where(x => x.Id == nodeFileId && x.OwnerId == userId)
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .SingleOrDefaultAsync();
            if (nodeFile == null)
            {
                return CottonResult.NotFound("Node file not found");
            }

            DownloadToken newToken = new()
            {
                FileName = nodeFile.Name,
                DeleteAfterUse = deleteAfterUse,
                CreatedByUserId = userId,
                NodeFileId = nodeFile.Id,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expireAfterMinutes),
                Token = !string.IsNullOrWhiteSpace(customToken)
                    ? customToken
                    : StringHelpers.CreateRandomString(DefaultSharedFileTokenLength),
            };
            await _dbContext.DownloadTokens.AddAsync(newToken);
            await _dbContext.SaveChangesAsync();
            string link = Routes.V1.Files + $"/{nodeFileId}/download?token={newToken.Token}";
            return Ok(link);
        }

        [Authorize]
        [HttpPatch(Routes.V1.Files + "/{nodeFileId:guid}/update-content")]
        public async Task<IActionResult> UpdateFileContent(
            [FromRoute] Guid nodeFileId,
            [FromBody] CreateFileRequest request)
        {
            bool isValidName = NameValidator.TryNormalizeAndValidate(request.Name,
                out string normalizedName,
                out string? errorMessage);
            if (!isValidName)
            {
                return CottonResult.BadRequest(errorMessage);
            }

            Guid userId = User.GetUserId();
            var layoutId = await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.Id == nodeFileId && x.OwnerId == userId)
                .Select(x => (Guid?)x.Node.LayoutId)
                .SingleOrDefaultAsync();
            if (layoutId is null)
            {
                return this.ApiNotFound("Node file not found.");
            }

            byte[] proposedHash = Hasher.FromHexStringHash(request.Hash);
            List<Chunk> chunks = await _fileManifestService.GetChunksAsync(request.ChunkHashes, userId);
            var newFile = await _dbContext.FileManifests
                .FirstOrDefaultAsync(x => x.ComputedContentHash == proposedHash || x.ProposedContentHash == proposedHash)
                ?? await _fileManifestService.CreateNewFileManifestAsync(chunks, request.Name, request.ContentType, proposedHash);

            await using var tx = await _dbContext.Database.BeginTransactionAsync();
            await LayoutLocks.AcquireForLayoutAsync(_dbContext, layoutId.Value, default);

            var nodeFile = await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .Where(x => x.Id == nodeFileId && x.OwnerId == userId)
                .SingleOrDefaultAsync();
            if (nodeFile == null || nodeFile.Node.Type != NodeType.Default)
            {
                return this.ApiNotFound("Node file not found.");
            }

            string nameKey = NameValidator.NormalizeAndGetNameKey(normalizedName);
            if (!string.Equals(nodeFile.NameKey, nameKey, StringComparison.Ordinal))
            {
                bool fileExists = await _dbContext.NodeFiles
                    .AnyAsync(x =>
                        x.NodeId == nodeFile.NodeId &&
                        x.OwnerId == userId &&
                        x.NameKey == nameKey &&
                        x.Id != nodeFileId);
                if (fileExists)
                {
                    return this.ApiConflict("A file with the same name key already exists in this folder: " + nameKey);
                }

                bool nodeExists = await _dbContext.Nodes
                    .AnyAsync(x =>
                        x.ParentId == nodeFile.NodeId &&
                        x.OwnerId == userId &&
                        x.Type == nodeFile.Node.Type &&
                        x.NameKey == nameKey);
                if (nodeExists)
                {
                    return this.ApiConflict("A folder with the same name key already exists in this folder: " + nameKey);
                }
            }

            await _quota.EnsureCanChangeFileManifestAsync(userId, nodeFile.Id, newFile.Id);

            if (!nodeFile.FileManifest.ProposedContentHash.SequenceEqual(proposedHash))
            {
                await _history.SaveVersionAndUpdateManifestAsync(nodeFile, newFile.Id, userId);
                nodeFile.FileManifest = newFile;
            }

            nodeFile.SetName(normalizedName);
            if (request.Metadata is not null)
            {
                nodeFile.Metadata = request.Metadata.Count > 0
                    ? new Dictionary<string, string>(request.Metadata)
                    : [];
            }

            await _dbContext.SaveChangesAsync();
            await tx.CommitAsync();

            await _scheduler.TriggerJobAsync<ComputeManifestHashesJob>();
            await _scheduler.TriggerJobAsync<GeneratePreviewJob>();

            var mapped = nodeFile.Adapt<NodeFileManifestDto>();
            await _hubContext.Clients.User(userId.ToString()).SendAsync("FileUpdated", mapped);
            return Ok(mapped);
        }

        [HttpGet(Routes.V1.Files + "/{nodeFileId:guid}/download")]
        public async Task<IActionResult> DownloadFileByToken(
            [FromRoute] Guid nodeFileId,
            [FromQuery] string token,
            [FromQuery] bool download = true,
            [FromQuery] bool preview = false)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return CottonResult.NotFound("File not found");
            }
            var nodeFile = await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .ThenInclude(x => x.Chunk)
                .SingleOrDefaultAsync(x => x.Id == nodeFileId);
            if (nodeFile == null || nodeFile.Node.Type != NodeType.Default)
            {
                return CottonResult.NotFound("File not found");
            }
            var downloadToken = await _dbContext.DownloadTokens
                .FirstOrDefaultAsync(x => x.Token == token && x.NodeFileId == nodeFile.Id);
            if (downloadToken == null || (downloadToken.ExpiresAt.HasValue && downloadToken.ExpiresAt.Value < DateTime.UtcNow))
            {
                return CottonResult.NotFound("File not found");
            }

            if (preview && nodeFile.FileManifest.LargeFilePreviewHash != null)
            {
                string previewHashHex = Hasher.ToHexStringHash(nodeFile.FileManifest.LargeFilePreviewHash);
                var previewStream = _storage.GetBlobStream([previewHashHex]);
                string etag = $"\"sha256-{previewHashHex}\"";
                var etagHeader = new EntityTagHeaderValue(etag);
                if (Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var inmValues))
                {
                    var clientEtags = EntityTagHeaderValue.ParseList([.. inmValues!]);
                    if (clientEtags.Any(x => x.Compare(etagHeader, useStrongComparison: true)))
                    {
                        Response.Headers.ETag = etagHeader.ToString();
                        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                        return StatusCode(StatusCodes.Status304NotModified);
                    }
                }
                Response.Headers.ETag = etag;
                Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                return File(previewStream, "image/webp");
            }

            string[] uids = nodeFile.FileManifest.FileManifestChunks.GetChunkHashes();
            PipelineContext context = new()
            {
                FileSizeBytes = nodeFile.FileManifest.SizeBytes,
                ChunkLengths = nodeFile.FileManifest.FileManifestChunks.GetChunkLengths()
            };
            Stream stream = _storage.GetBlobStream(uids, context);
            Response.Headers.ContentEncoding = "identity";
            Response.Headers.CacheControl = "private, no-store, no-transform";
            var entityTag = EntityTagHeaderValue.Parse($"\"sha256-{Hasher.ToHexStringHash(nodeFile.FileManifest.ProposedContentHash)}\"");

            if (downloadToken.DeleteAfterUse)
            {
                Response.OnCompleted(async () =>
                {
                    _dbContext.DownloadTokens.Remove(downloadToken);
                    await _dbContext.SaveChangesAsync();
                });
            }

            var lastModified = new DateTimeOffset(nodeFile.CreatedAt);
            return File(
                stream,
                nodeFile.FileManifest.ContentType,
                fileDownloadName: download ? nodeFile.Name : null,
                lastModified: lastModified,
                entityTag: entityTag,
                enableRangeProcessing: true);
        }

        [HttpGet(Routes.V1.Files + "/{nodeFileId:guid}/hls/master.m3u8")]
        public async Task<IActionResult> HlsMasterPlaylistByToken(
            [FromRoute] Guid nodeFileId,
            [FromQuery] string token)
        {
            var lookup = await ResolveTranscodableSourceAsync(nodeFileId, token);
            if (lookup.Failure is not null)
            {
                return lookup.Failure;
            }

            string encodedToken = Uri.EscapeDataString(token);
            string PlaylistUrl(string qualityName) =>
                Routes.V1.Files + $"/{nodeFileId}/hls/playlist.m3u8?token={encodedToken}&quality={qualityName}";

            const string variantCodecs = "avc1.640029,mp4a.40.2";
            var variants = new[]
            {
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
            };

            Response.Headers.CacheControl = "private, no-store, no-transform";
            HonourDeleteAfterUse(lookup.DownloadToken!);
            return Content(
                HlsManifestBuilder.BuildMaster(variants),
                HlsManifestBuilder.ContentType,
                System.Text.Encoding.UTF8);
        }

        [HttpGet(Routes.V1.Files + "/{nodeFileId:guid}/hls/playlist.m3u8")]
        public async Task<IActionResult> HlsVodPlaylistByToken(
            [FromRoute] Guid nodeFileId,
            [FromQuery] string token,
            [FromQuery] string? quality = null)
        {
            var lookup = await ResolveTranscodableSourceAsync(nodeFileId, token);
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
            HonourDeleteAfterUse(lookup.DownloadToken!);
            return Content(manifest, HlsManifestBuilder.ContentType, System.Text.Encoding.UTF8);
        }

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

            var lookup = await ResolveTranscodableSourceAsync(nodeFileId, token);
            if (lookup.Failure is not null)
            {
                return lookup.Failure;
            }

            MediaProbeInfo? probe = await ProbeMediaAsync(lookup.NodeFile!);
            if (probe?.DurationSeconds is null or <= 0)
            {
                return CottonResult.BadRequest("Could not determine source duration for HLS segmentation.");
            }

            var manifestPlan = HlsManifestBuilder.Plan(probe.DurationSeconds.Value);
            if (segmentIndex >= manifestPlan.SegmentCount)
            {
                return CottonResult.NotFound("Segment index out of range.");
            }

            HonourDeleteAfterUse(lookup.DownloadToken!);

            HlsRendition rendition = HlsRenditionProfile.Parse(quality);
            string qualityName = rendition.ToString().ToLowerInvariant();
            string cacheKey = HlsSegmentCache.BuildKey(
                lookup.NodeFile!.FileManifest.Id,
                qualityName,
                segmentIndex);

            if (_segmentCache.TryGet(cacheKey, out byte[]? cachedBytes))
            {
                Response.Headers.CacheControl = "private, max-age=300";
                Response.Headers.ContentEncoding = "identity";
                return File(cachedBytes, VideoTranscoder.SegmentContentType);
            }

            double startSeconds = HlsManifestBuilder.StartTimeOf(segmentIndex);
            double segmentDuration = manifestPlan.DurationOf(segmentIndex);
            var encoderPlan = HlsRenditionProfile.Plan(
                rendition,
                probe.VideoCodec,
                probe.AudioCodec);

            Response.ContentType = VideoTranscoder.SegmentContentType;
            Response.Headers.CacheControl = "private, max-age=300";
            Response.Headers.ContentEncoding = "identity";

            using var captureStream = new MemoryStream();
            var tee = new TeeStream(Response.Body, captureStream);
            bool transcodeSucceeded = false;

            await using var sourceStream = OpenSourceStream(lookup.NodeFile);
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

        private sealed record TranscodableLookup(
            NodeFile? NodeFile,
            DownloadToken? DownloadToken,
            IActionResult? Failure);

        private async Task<TranscodableLookup> ResolveTranscodableSourceAsync(Guid nodeFileId, string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return new TranscodableLookup(null, null, CottonResult.NotFound("File not found"));
            }

            var nodeFile = await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .ThenInclude(x => x.Chunk)
                .SingleOrDefaultAsync(x => x.Id == nodeFileId);
            if (nodeFile == null || nodeFile.Node.Type != NodeType.Default)
            {
                return new TranscodableLookup(null, null, CottonResult.NotFound("File not found"));
            }

            var downloadToken = await _dbContext.DownloadTokens
                .FirstOrDefaultAsync(x => x.Token == token && x.NodeFileId == nodeFile.Id);
            if (downloadToken == null || (downloadToken.ExpiresAt.HasValue && downloadToken.ExpiresAt.Value < DateTime.UtcNow))
            {
                return new TranscodableLookup(null, null, CottonResult.NotFound("File not found"));
            }

            var playbackMode = VideoPlaybackResolver.Resolve(
                nodeFile.FileManifest.ContentType,
                hasPreview: nodeFile.FileManifest.SmallFilePreviewHash != null);
            if (playbackMode != VideoPlaybackMode.Transcode)
            {
                return new TranscodableLookup(null, null, CottonResult.BadRequest(
                    "This file is not eligible for on-the-fly transcoding."));
            }

            return new TranscodableLookup(nodeFile, downloadToken, null);
        }

        private Stream OpenSourceStream(NodeFile nodeFile)
        {
            var manifest = nodeFile.FileManifest;
            string[] uids = manifest.FileManifestChunks.GetChunkHashes();
            PipelineContext context = new()
            {
                FileSizeBytes = manifest.SizeBytes,
                ChunkLengths = manifest.FileManifestChunks.GetChunkLengths(),
            };

            return _storage.GetBlobStream(uids, context);
        }

        private void HonourDeleteAfterUse(DownloadToken downloadToken)
        {
            if (!downloadToken.DeleteAfterUse)
            {
                return;
            }

            Response.OnCompleted(async () =>
            {
                _dbContext.DownloadTokens.Remove(downloadToken);
                await _dbContext.SaveChangesAsync();
            });
        }

        private async Task<MediaProbeInfo?> ProbeMediaAsync(NodeFile nodeFile)
        {
            string cacheKey = $"hls-media-probe:{nodeFile.FileManifest.Id}";
            if (_cache.TryGetValue<MediaProbeInfo>(cacheKey, out var cached))
            {
                return cached;
            }

            MediaProbeInfo? probe;
            await using (var probeStream = OpenSourceStream(nodeFile))
            await using (var probeServer = new RangeStreamServer(probeStream, _logger))
            {
                probe = await FfmpegBinary.TryGetMediaProbeAsync(
                    probeServer.Url,
                    cancellationToken: HttpContext.RequestAborted)
                    .ConfigureAwait(false);
            }

            if (probe is not null)
            {
                _cache.Set(cacheKey, probe, TimeSpan.FromHours(1));
            }

            return probe;
        }

        [Authorize]
        [HttpPost(Routes.V1.Files + "/from-chunks")]
        public async Task<IActionResult> CreateFileFromChunks([FromBody] CreateFileRequest request)
        {
            Guid userId = User.GetUserId();
            request.UserId = userId;
            NodeFileManifestDto manifest = await _mediator.Send(request);
            await _scheduler.TriggerJobAsync<ComputeManifestHashesJob>();
            await _scheduler.TriggerJobAsync<GeneratePreviewJob>();
            await _hubContext.Clients.User(userId.ToString()).SendAsync("FileCreated", manifest);
            return Ok(manifest);
        }
    }
}
