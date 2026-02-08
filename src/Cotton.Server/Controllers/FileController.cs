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
