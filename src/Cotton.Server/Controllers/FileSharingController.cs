// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Files;
using Cotton.Models.Enums;
using Cotton.Server.Auth;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Models;
using Cotton.Server.Models.Requests;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Validators;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using System.ComponentModel.DataAnnotations;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class FileSharingController(
        IMediator _mediator,
        IStoragePipeline _storage,
        CottonDbContext _dbContext,
        FileGraphIntegrityVerifier _fileGraphIntegrity,
        PublicShareLookupFailureLimiter _publicShareLookupFailures) : ControllerBase
    {
        [HttpGet("/s/{token}")]
        [HttpHead("/s/{token}")]
        public Task<IActionResult> Share(
            [FromRoute] string token,
            [FromQuery] string? view = null,
            [FromQuery] bool preview = false)
        {
            return ShareAsync(token, view, preview, SharedFilePreviewFormat.Webp);
        }

        [HttpGet("/s/{token}/preview.jpg")]
        [HttpHead("/s/{token}/preview.jpg")]
        public Task<IActionResult> SocialPreview([FromRoute] string token)
        {
            return ShareAsync(token, "inline", true, SharedFilePreviewFormat.Jpeg);
        }

        private async Task<IActionResult> ShareAsync(
            string token, string? view, bool preview, SharedFilePreviewFormat format)
        {
            IActionResult? blocked = this.GetPublicShareLookupBlockRejection(
                _publicShareLookupFailures,
                token);
            if (blocked is not null)
            {
                return blocked;
            }

            ShareFileResult result = await _mediator.Send(
                new ShareFileQuery(token, view, preview, Request, format), HttpContext.RequestAborted);

            if (result.IsTokenLookupFailure)
            {
                IActionResult? rejection = this.GetPublicShareLookupFailureRejection(
                    _publicShareLookupFailures,
                    token);
                if (rejection is not null)
                {
                    return rejection;
                }
            }

            return result.Response;
        }

        [Authorize]
        [HttpGet(Routes.V1.Files + "/{nodeFileId:guid}/download-link")]
        public async Task<IActionResult> DownloadFile(
            [FromRoute] Guid nodeFileId,
            [FromQuery] ShareLinkExpirationOptions expiration,
            [FromQuery] string? customToken = "",
            [FromQuery] bool deleteAfterUse = false)
        {
            CreateFileShareLinkRequest request = new(
                User.GetUserId(),
                nodeFileId,
                expiration.ExpireAfterMinutes,
                customToken,
                deleteAfterUse);
            CreateFileShareLinkResult result = await _mediator.Send(request, HttpContext.RequestAborted);
            return result.Status switch
            {
                CreateFileShareLinkStatus.Created => Ok(result.Link),
                CreateFileShareLinkStatus.FileNotFound => CottonResult.NotFound("Node file not found"),
                CreateFileShareLinkStatus.TokenConflict => this.ApiConflict(
                    "The custom token is already in use. Please choose a different one."),
                _ => throw new ArgumentOutOfRangeException(nameof(result.Status)),
            };
        }

        [Authorize]
        [HttpPost(Routes.V1.Files + "/{nodeFileId:guid}/download-token")]
        public async Task<IActionResult> CreateDownloadToken(
            [FromRoute] Guid nodeFileId,
            [FromQuery, Range(1, ShareLinkExpiration.MaximumMinutes)]
            int expireAfterMinutes = ShareLinkExpiration.DefaultMinutes)
        {
            string? link = await _mediator.Send(
                new CreateFileDownloadTokenRequest(User.GetUserId(), nodeFileId, expireAfterMinutes),
                HttpContext.RequestAborted);
            return link is null ? CottonResult.NotFound("Node file not found") : Ok(link);
        }

        [HttpGet(Routes.V1.Files + "/{nodeFileId:guid}/download")]
        [HttpHead(Routes.V1.Files + "/{nodeFileId:guid}/download")]
        public async Task<IActionResult> DownloadFileByToken(
            [FromRoute] Guid nodeFileId,
            [FromQuery] FileDownloadCredentials credentials,
            [FromQuery] bool download = true,
            [FromQuery] bool preview = false)
        {
            if (!credentials.IsValid)
            {
                return this.ApiNotFound("File not found");
            }

            IActionResult? blocked = this.GetPublicShareLookupBlockRejection(
                _publicShareLookupFailures,
                credentials.Value);
            if (blocked is not null)
            {
                return blocked;
            }

            FileDownloadAccess? access = await _mediator.Send(
                new ResolveFileDownloadAccessQuery(nodeFileId, credentials), HttpContext.RequestAborted);
            if (access is null)
            {
                return this.ApiPublicShareNotFound(_publicShareLookupFailures, credentials.Value, "File not found");
            }

            NodeFile? nodeFile = await LoadDownloadNodeFileAsync(nodeFileId, access.UserId);
            if (nodeFile is null || !CanServeTokenDownload(nodeFile))
            {
                return CottonResult.NotFound("File not found");
            }

            bool servesPreview = CanServeLargePreview(nodeFile, preview);
            RequireDownloadGraphIntegrity(nodeFile, servesPreview);

            return servesPreview
                ? ServeLargePreview(nodeFile)
                : ServeTokenFileDownload(nodeFile, access.ShareToken, download);
        }

        private Task<NodeFile?> LoadDownloadNodeFileAsync(Guid nodeFileId, Guid userId)
        {
            return _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .ThenInclude(x => x.Chunk)
                .SingleOrDefaultAsync(x => x.Id == nodeFileId && x.OwnerId == userId, HttpContext.RequestAborted);
        }

        private static bool CanServeTokenDownload(NodeFile nodeFile) =>
            nodeFile.Node.Type == NodeType.Default || FileVersionService.IsHistoricalVersion(nodeFile);

        private static bool CanServeLargePreview(NodeFile nodeFile, bool preview) =>
            preview && nodeFile.FileManifest.LargeFilePreviewHash is not null;

        private void RequireDownloadGraphIntegrity(
            NodeFile nodeFile,
            bool servesPreview)
        {
            if (servesPreview)
            {
                _fileGraphIntegrity.RequireValidMetadata(_dbContext, nodeFile, "file.preview");
                return;
            }

            _fileGraphIntegrity.RequireValidContent(_dbContext, nodeFile, "file.download");
        }

        private IActionResult ServeLargePreview(NodeFile nodeFile)
        {
            string previewHashHex = Hasher.ToHexStringHash(nodeFile.FileManifest.LargeFilePreviewHash!);
            EntityTagHeaderValue etagHeader = new($"\"sha256-{previewHashHex}\"");
            Response.Headers.ETag = etagHeader.ToString();
            Response.Headers.CacheControl = "public, max-age=31536000, immutable";

            if (FileETags.MatchesIfNoneMatchHeader(Request, etagHeader))
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }

            Stream previewStream = _storage.GetBlobStream([previewHashHex]);
            return File(previewStream, "image/webp");
        }

        private IActionResult ServeTokenFileDownload(
            NodeFile nodeFile,
            DownloadToken? downloadToken,
            bool download)
        {
            if (!HttpMethods.IsHead(Request.Method))
            {
                Response.RegisterDeleteAfterUse(_dbContext, downloadToken);
            }
            return FileDownloadResultFactory.Create(Response, _storage, nodeFile, download);
        }
    }
}
