// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Jobs;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Services;
using Cotton.Shared;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Quartz;
using System.Net;
using System.Text.Json;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class FileController(
        IMediator _mediator,
        IStoragePipeline _storage,
        CottonDbContext _dbContext,
        ISchedulerFactory _scheduler,
        FileManifestService _fileManifestService,
        NodeFileHistoryService _history) : ControllerBase
    {
        private const int DefaultSharedFileTokenLength = 16;

        [HttpGet("/s/{token}")]
        [HttpHead("/s/{token}")]
        public async Task<IActionResult> Share(
            [FromRoute] string token,
            [FromQuery] string? view = null)
        {
            bool isHead = HttpMethods.IsHead(Request.Method);
            DateTime now = DateTime.UtcNow;

            string mode = (view ?? "page").Trim().ToLowerInvariant();
            if (view is not null && mode is not ("page" or "download" or "inline"))
            {
                return this.ApiBadRequest("Invalid view mode. Valid values: page, download, inline.");
            }

            bool ishtml = mode == "page";
            bool isInlineFile = mode == "inline";

            string baseAppUrl = $"{Request.Scheme}://{Request.Host}";

            var query = _dbContext.DownloadTokens
                .Where(x => x.Token == token && (!x.ExpiresAt.HasValue || x.ExpiresAt.Value > now))
                .Include(x => x.FileManifest)
                .AsQueryable();
            if (!ishtml && !isHead)
            {
                query = query
                    .Include(x => x.FileManifest.FileManifestChunks)
                    .ThenInclude(x => x.Chunk);
            }
            var downloadToken = await query.FirstOrDefaultAsync();

            if (downloadToken == null)
            {
                return ishtml
                    ? Redirect($"{baseAppUrl}/404")
                    : this.ApiNotFound("File not found");
            }

            var file = downloadToken.FileManifest;
            if (ishtml)
            {
                string canonicalUrl = $"{baseAppUrl}/s/{token}";
                string appShareUrl = $"{baseAppUrl}/share/{token}";
                string? hex = (file.EncryptedFilePreviewHash == null || file.EncryptedFilePreviewHash.Length == 0)
                    ? null : Convert.ToHexString(file.EncryptedFilePreviewHash);
                string previewTag = hex == null
                    ? string.Empty
                    : ($"<meta property=\"og:image\" content=\"{WebUtility.HtmlEncode($"{baseAppUrl}{Routes.V1.Previews}/{hex}.webp")}\" />\n" +
                      $"<meta name=\"twitter:image\" content=\"{WebUtility.HtmlEncode($"{baseAppUrl}{Routes.V1.Previews}/{hex}.webp")}\" />");
                string html = $"""
                <!doctype html>
                <html lang="en">
                <head>
                  <meta charset="utf-8">
                  <title>{WebUtility.HtmlEncode(downloadToken.FileName)} – Cotton</title>

                  <meta http-equiv="refresh" content="0;url={WebUtility.HtmlEncode(appShareUrl)}" />
                  <link rel="canonical" href="{WebUtility.HtmlEncode(canonicalUrl)}" />
                  <meta property="og:site_name" content="Cotton Cloud" />
                  <meta property="og:title" content="{WebUtility.HtmlEncode(downloadToken.FileName)}" />
                  <meta property="og:description" content="Shared via Cotton Cloud" />
                  <meta property="og:type" content="website" />
                  <meta property="og:url" content="{WebUtility.HtmlEncode(canonicalUrl)}" />
                  {previewTag}

                  <meta name="twitter:card" content="summary_large_image" />
                </head>
                <body>
                  <noscript>
                    <p><a href="{WebUtility.HtmlEncode(appShareUrl)}">Continue</a></p>
                  </noscript>
                  <script>
                    window.location.replace({JsonSerializer.Serialize(appShareUrl)});
                  </script>
                </body>
                </html>
                """;
                return Content(html, "text/html; charset=utf-8");
            }
            else
            {
                Response.Headers.ContentEncoding = "identity";
                Response.Headers.CacheControl = "private, no-store, no-transform";
                var entityTag = EntityTagHeaderValue.Parse($"\"sha256-{Hasher.ToHexStringHash(file.ProposedContentHash)}\"");

                var lastModified = new DateTimeOffset(downloadToken.CreatedAt);

                if (isHead)
                {
                    Response.ContentType = file.ContentType;
                    Response.ContentLength = file.SizeBytes;
                    Response.Headers.ETag = entityTag.ToString();

                    var cd = new ContentDispositionHeaderValue(isInlineFile ? "inline" : "attachment")
                    {
                        FileNameStar = downloadToken.FileName,
                        FileName = downloadToken.FileName
                    };
                    Response.Headers[HeaderNames.ContentDisposition] = cd.ToString();

                    return new EmptyResult();
                }

                string[] uids = file.FileManifestChunks.GetChunkHashes();
                PipelineContext context = new()
                {
                    FileSizeBytes = file.SizeBytes,
                    ChunkLengths = file.FileManifestChunks.GetChunkLengths()
                };
                Stream stream = _storage.GetBlobStream(uids, context);

                if (downloadToken.DeleteAfterUse)
                {
                    Response.OnCompleted(async () =>
                    {
                        _dbContext.DownloadTokens.Remove(downloadToken);
                        await _dbContext.SaveChangesAsync();
                    });
                }
                string? downloadName = isInlineFile ? null : downloadToken.FileName;
                return File(
                    stream,
                    file.ContentType,
                    fileDownloadName: downloadName,
                    lastModified: lastModified,
                    entityTag: entityTag,
                    enableRangeProcessing: true);
            }
        }

        [Authorize]
        [HttpDelete(Routes.V1.Files + "/{nodeFileId:guid}")]
        public async Task<IActionResult> DeleteFile(
            [FromRoute] Guid nodeFileId,
            [FromQuery] bool skipTrash = false)
        {
            Guid userId = User.GetUserId();
            DeleteFileQuery query = new(userId, nodeFileId, skipTrash);
            await _mediator.Send(query);
            return NoContent();
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
            var nodeFile = await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .Where(x => x.Id == nodeFileId && x.OwnerId == userId)
                .SingleOrDefaultAsync();
            if (nodeFile == null)
            {
                return CottonResult.NotFound("File not found.");
            }

            string nameKey = NameValidator.NormalizeAndGetNameKey(request.Name);

            // Check for duplicate files in the same folder
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

            // Check for duplicate nodes (subfolders) in the same folder
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

            var mapped = nodeFile.Adapt<FileManifestDto>();
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
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .SingleOrDefaultAsync(x => x.Id == nodeFileId && x.OwnerId == userId);
            if (nodeFile == null)
            {
                return CottonResult.NotFound("Node file not found");
            }

            DownloadToken newToken = new()
            {
                FileName = nodeFile.Name,
                DeleteAfterUse = deleteAfterUse,
                CreatedByUserId = userId,
                FileManifestId = nodeFile.FileManifestId,
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
            Guid userId = User.GetUserId();
            var nodeFile = await _dbContext.NodeFiles
                .Include(x => x.FileManifest)
                .Where(x => x.Id == nodeFileId && x.OwnerId == userId)
                .SingleOrDefaultAsync();
            if (nodeFile == null)
            {
                return this.ApiNotFound("Node file not found.");
            }
            byte[] proposedHash = Hasher.FromHexStringHash(request.Hash);
            if (nodeFile.FileManifest.ProposedContentHash.SequenceEqual(proposedHash))
            {
                return Ok();
            }
            List<Chunk> chunks = await _fileManifestService.GetChunksAsync(request.ChunkHashes, userId);
            var newFile = await _dbContext.FileManifests
                .FirstOrDefaultAsync(x => x.ComputedContentHash == proposedHash || x.ProposedContentHash == proposedHash)
                ?? await _fileManifestService.CreateNewFileManifestAsync(chunks, request.Name, request.ContentType, proposedHash);

            await _history.SaveVersionAndUpdateManifestAsync(nodeFile, newFile.Id, userId);
            await _dbContext.SaveChangesAsync();
            await _scheduler.TriggerJobAsync<ComputeManifestHashesJob>();
            await _scheduler.TriggerJobAsync<GeneratePreviewJob>();
            return Ok();
        }

        [HttpGet(Routes.V1.Files + "/{nodeFileId:guid}/download")]
        public async Task<IActionResult> DownloadFileByToken(
            [FromRoute] Guid nodeFileId,
            [FromQuery] string token,
            [FromQuery] bool download = true)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return CottonResult.NotFound("File not found");
            }
            var nodeFile = await _dbContext.NodeFiles
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .ThenInclude(x => x.Chunk)
                .SingleOrDefaultAsync(x => x.Id == nodeFileId);
            if (nodeFile == null)
            {
                return CottonResult.NotFound("File not found");
            }
            var downloadToken = await _dbContext.DownloadTokens
                .FirstOrDefaultAsync(x => x.Token == token && x.FileManifestId == nodeFile.FileManifestId);
            if (downloadToken == null || (downloadToken.ExpiresAt.HasValue && downloadToken.ExpiresAt.Value < DateTime.UtcNow))
            {
                return CottonResult.NotFound("File not found");
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

        [Authorize]
        [HttpPost(Routes.V1.Files + "/from-chunks")]
        public async Task<IActionResult> CreateFileFromChunks([FromBody] CreateFileRequest request)
        {
            Guid userId = User.GetUserId();
            request.UserId = userId;
            await _mediator.Send(request);
            await _scheduler.TriggerJobAsync<ComputeManifestHashesJob>();
            await _scheduler.TriggerJobAsync<GeneratePreviewJob>();
            return Ok();
        }
    }
}
