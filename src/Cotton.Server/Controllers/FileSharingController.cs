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

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class FileSharingController(
        IMediator _mediator,
        IStoragePipeline _storage,
        CottonDbContext _dbContext,
        IDatabaseIntegrityVerifier _integrity,
        FileGraphIntegrityVerifier _fileGraphIntegrity,
        PublicShareTokenGenerator _publicShareTokens,
        PublicShareLookupFailureLimiter _publicShareLookupFailures) : ControllerBase
    {
        [HttpGet("/s/{token}")]
        [HttpHead("/s/{token}")]
        public async Task<IActionResult> Share(
            [FromRoute] string token,
            [FromQuery] string? view = null,
            [FromQuery] bool preview = false)
        {
            IActionResult? blocked = this.GetPublicShareLookupBlockRejection(
                _publicShareLookupFailures,
                token);
            if (blocked is not null)
            {
                return blocked;
            }

            ShareFileResult result = await _mediator.Send(new ShareFileQuery(token, view, preview, Request));

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

            return result.Kind switch
            {
                "badRequest" => this.ApiBadRequest(result.ErrorMessage ?? "Bad request"),
                "notFound" => this.ApiNotFound(result.ErrorMessage ?? "File not found"),
                "redirect" => Redirect(result.RedirectUrl ?? "/"),
                "html" => Content(result.HtmlContent ?? string.Empty, "text/html; charset=utf-8"),
                "head" => CreateShareHeadResponse(result),
                "stream" => CreateShareStreamResponse(result),
                _ => this.ApiBadRequest("Invalid share response")
            };
        }

        [Authorize]
        [HttpGet(Routes.V1.Files + "/{nodeFileId:guid}/download-link")]
        public async Task<IActionResult> DownloadFile(
            [FromRoute] Guid nodeFileId,
            [FromQuery] int expireAfterMinutes = 1440,
            [FromQuery] string? customToken = "",
            [FromQuery] bool deleteAfterUse = false)
        {
            const int maxExpireMinutes = 60 * 24 * 365;
            ArgumentOutOfRangeException.ThrowIfGreaterThan(expireAfterMinutes, maxExpireMinutes, nameof(expireAfterMinutes));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expireAfterMinutes, nameof(expireAfterMinutes));

            if (!string.IsNullOrWhiteSpace(customToken))
            {
                bool exists = await ShareTokenExistsAsync(customToken);
                if (exists)
                {
                    return this.ApiConflict("The custom token is already in use. Please choose a different one.");
                }
            }

            Guid userId = User.GetUserId();
            NodeFile? nodeFile = await _dbContext.NodeFiles
                .Where(x => x.Id == nodeFileId && x.OwnerId == userId)
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .SingleOrDefaultAsync();
            if (nodeFile is null || nodeFile.Node.Type != NodeType.Default)
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
                    : await _publicShareTokens.CreateUniqueAsync(HttpContext.RequestAborted),
            };
            await _dbContext.DownloadTokens.AddAsync(newToken);
            await _dbContext.SaveChangesAsync();
            string link = Routes.V1.Files + $"/{nodeFileId}/download?token={newToken.Token}";
            return Ok(link);
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
                return this.ApiNotFound("File not found");
            }

            IActionResult? blocked = this.GetPublicShareLookupBlockRejection(
                _publicShareLookupFailures,
                token);
            if (blocked is not null)
            {
                return blocked;
            }

            DownloadToken? downloadToken = await _dbContext.DownloadTokens.FindActiveAsync(token, nodeFileId);
            if (downloadToken is null)
            {
                return this.ApiPublicShareNotFound(_publicShareLookupFailures, token, "File not found");
            }

            _integrity.RequireValid(_dbContext, downloadToken, "file.download-token");

            NodeFile? nodeFile = await LoadDownloadNodeFileAsync(nodeFileId);
            if (nodeFile is null || !CanServeTokenDownload(nodeFile))
            {
                return CottonResult.NotFound("File not found");
            }

            bool servesPreview = CanServeLargePreview(nodeFile, preview);
            RequireDownloadGraphIntegrity(nodeFile, servesPreview);

            return servesPreview
                ? ServeLargePreview(nodeFile)
                : ServeTokenFileDownload(nodeFile, downloadToken, download);
        }

        private IActionResult CreateShareHeadResponse(ShareFileResult result)
        {
            bool requestedInline = result.Inline == true;
            FileResponseSecurity.ApplyFileResponseHeaders(Response, result.ContentType, requestedInline);
            Response.Headers.ContentEncoding = "identity";
            Response.Headers.CacheControl = "private, no-store, no-transform";
            Response.ContentType = FileResponseSecurity.ResolveContentTypeForResponse(result.ContentType, requestedInline);
            Response.ContentLength = result.ContentLength;
            if (!string.IsNullOrWhiteSpace(result.EntityTag))
            {
                Response.Headers.ETag = result.EntityTag;
            }

            ContentDispositionHeaderValue contentDisposition = new(
                FileResponseSecurity.ResolveContentDispositionType(result.ContentType, requestedInline))
            {
                FileNameStar = result.FileName,
                FileName = result.FileName,
            };
            Response.Headers[HeaderNames.ContentDisposition] = contentDisposition.ToString();
            return new EmptyResult();
        }

        private IActionResult CreateShareStreamResponse(ShareFileResult result)
        {
            bool requestedInline = string.IsNullOrWhiteSpace(result.DownloadName);
            FileResponseSecurity.ApplyFileResponseHeaders(Response, result.ContentType, requestedInline);
            Response.Headers.ContentEncoding = "identity";
            Response.Headers.CacheControl = "private, no-store, no-transform";
            RegisterDeleteAfterUse(result);

            string streamFileName = result.FileName ?? result.DownloadName ?? "download";
            string? streamDownloadName = requestedInline
                ? FileResponseSecurity.ResolveFileDownloadName(streamFileName, requestedInline: true, result.ContentType)
                : result.DownloadName;
            return File(
                result.FileStream!,
                FileResponseSecurity.ResolveContentTypeForResponse(result.ContentType, requestedInline),
                fileDownloadName: streamDownloadName,
                lastModified: result.LastModified,
                entityTag: result.EntityTagValue!,
                enableRangeProcessing: true);
        }

        private void RegisterDeleteAfterUse(ShareFileResult result)
        {
            if (!result.DeleteAfterUse || !result.DeleteTokenId.HasValue)
            {
                return;
            }

            Guid tokenId = result.DeleteTokenId.Value;
            Response.OnCompleted(async () =>
            {
                DownloadToken? tokenEntity = await _dbContext.DownloadTokens
                    .FirstOrDefaultAsync(x => x.Id == tokenId);
                if (tokenEntity is not null)
                {
                    _dbContext.DownloadTokens.Remove(tokenEntity);
                    await _dbContext.SaveChangesAsync();
                }
            });
        }

        private async Task<bool> ShareTokenExistsAsync(string token)
        {
            return await _dbContext.DownloadTokens.AnyAsync(x => x.Token == token)
                || await _dbContext.NodeShareTokens.AnyAsync(x => x.Token == token);
        }

        private Task<NodeFile?> LoadDownloadNodeFileAsync(Guid nodeFileId)
        {
            return _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .ThenInclude(x => x.Chunk)
                .SingleOrDefaultAsync(x => x.Id == nodeFileId);
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
            DownloadToken downloadToken,
            bool download)
        {
            Response.RegisterDeleteAfterUse(_dbContext, downloadToken);
            return FileDownloadResultFactory.Create(Response, _storage, nodeFile, download);
        }
    }
}
