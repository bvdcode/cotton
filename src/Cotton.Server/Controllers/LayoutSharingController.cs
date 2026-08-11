// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Database.Models;
using Cotton.Server.Auth;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Layouts;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace Cotton.Server.Controllers
{
    /// <summary>
    /// Exposes HTTP endpoints for shared layout operations.
    /// </summary>
    [ApiController]
    [Route(Routes.V1.Layouts)]
    public class LayoutSharingController(
        IMediator _mediator,
        IStoragePipeline _storage,
        PublicShareLookupFailureLimiter _publicShareLookupFailures) : ControllerBase
    {
        /// <summary>
        /// Gets node share link.
        /// </summary>
        [Authorize]
        [HttpGet("nodes/{nodeId:guid}/share-link")]
        public async Task<IActionResult> GetNodeShareLink(
            [FromRoute] Guid nodeId,
            [FromQuery] int expireAfterMinutes = 1440,
            [FromQuery] string? customToken = "")
        {
            Guid userId = User.GetUserId();
            CreateNodeShareLinkRequest request = new(
                userId,
                nodeId,
                expireAfterMinutes,
                customToken);
            CreateNodeShareLinkResult result = await _mediator.Send(
                request,
                HttpContext.RequestAborted);

            return result.Status switch
            {
                CreateNodeShareLinkStatus.Created => Ok(result.Link),
                CreateNodeShareLinkStatus.NodeNotFound => CottonResult.NotFound("Node not found."),
                CreateNodeShareLinkStatus.TokenConflict => this.ApiConflict(
                    "The custom token is already in use. Please choose a different one."),
                _ => throw new ArgumentOutOfRangeException(nameof(result.Status)),
            };
        }

        /// <summary>
        /// Gets shared node info.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("shared/{token}")]
        public async Task<IActionResult> GetSharedNodeInfo([FromRoute] string token)
        {
            IActionResult? blocked = this.GetPublicShareLookupBlockRejection(
                _publicShareLookupFailures,
                token);
            if (blocked is not null)
            {
                return blocked;
            }

            SharedNodeAccess? nodeShareToken = await ResolveActiveNodeShareTokenAsync(token);
            if (nodeShareToken is null)
            {
                return this.ApiPublicShareNotFound(
                    _publicShareLookupFailures,
                    token,
                    "Shared folder not found.");
            }

            return Ok(new SharedNodeInfoDto
            {
                Token = nodeShareToken.Token,
                NodeId = nodeShareToken.NodeId,
                Name = nodeShareToken.Name,
                ExpiresAt = nodeShareToken.ExpiresAt,
            });
        }

        /// <summary>
        /// Gets shared node children.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("shared/{token}/children")]
        public async Task<IActionResult> GetSharedNodeChildren(
            [FromRoute] string token,
            [FromQuery] Guid? nodeId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            IActionResult? blocked = this.GetPublicShareLookupBlockRejection(
                _publicShareLookupFailures,
                token);
            if (blocked is not null)
            {
                return blocked;
            }

            GetSharedNodeChildrenQuery query = new(token, nodeId, page, pageSize);
            GetSharedNodeChildrenResult result = await _mediator.Send(
                query,
                HttpContext.RequestAborted);
            switch (result.Status)
            {
                case GetSharedNodeChildrenStatus.Success:
                    Response.Headers.Append(
                        "X-Total-Count",
                        result.TotalCount.ToString());
                    return Ok(result.Content);
                case GetSharedNodeChildrenStatus.SharedFolderNotFound:
                    return this.ApiPublicShareNotFound(
                        _publicShareLookupFailures,
                        token,
                        "Shared folder not found.");
                case GetSharedNodeChildrenStatus.FolderNotFound:
                    return this.ApiNotFound("Folder not found.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(result.Status));
            }
        }

        /// <summary>
        /// Gets shared node ancestors.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("shared/{token}/ancestors/{nodeId:guid}")]
        public async Task<IActionResult> GetSharedNodeAncestors(
            [FromRoute] string token,
            [FromRoute] Guid nodeId)
        {
            IActionResult? blocked = this.GetPublicShareLookupBlockRejection(
                _publicShareLookupFailures,
                token);
            if (blocked is not null)
            {
                return blocked;
            }

            GetSharedNodeAncestorsQuery query = new(token, nodeId);
            GetSharedNodeAncestorsResult result = await _mediator.Send(
                query,
                HttpContext.RequestAborted);
            switch (result.Status)
            {
                case GetSharedNodeAncestorsStatus.Success:
                    return Ok(result.Ancestors);
                case GetSharedNodeAncestorsStatus.SharedFolderNotFound:
                    return this.ApiPublicShareNotFound(
                        _publicShareLookupFailures,
                        token,
                        "Shared folder not found.");
                case GetSharedNodeAncestorsStatus.FolderNotFound:
                    return this.ApiNotFound("Folder not found.");
                case GetSharedNodeAncestorsStatus.InvalidHierarchy:
                    return this.ApiConflict(result.Error!);
                default:
                    throw new ArgumentOutOfRangeException(nameof(result.Status));
            }
        }

        /// <summary>
        /// Creates shared folder archive download link.
        /// </summary>
        [AllowAnonymous]
        [EnableRateLimiting(AuthRateLimitPolicies.PublicShareArchive)]
        [HttpPost("shared/{token}/archives/download-link")]
        public async Task<IActionResult> CreateSharedArchiveDownloadLink(
            [FromRoute] string token,
            [FromQuery] Guid? nodeId,
            CancellationToken cancellationToken)
        {
            IActionResult? blocked = this.GetPublicShareLookupBlockRejection(
                _publicShareLookupFailures,
                token);
            if (blocked is not null)
            {
                return blocked;
            }

            CreateSharedArchiveDownloadLinkRequest request = new(token, nodeId);
            CreateSharedArchiveDownloadLinkResult result = await _mediator.Send(
                request,
                cancellationToken);
            switch (result.Status)
            {
                case CreateSharedArchiveDownloadLinkStatus.ArchiveResult:
                    CreateArchiveDownloadLinkResult archive = result.Archive!;
                    return archive.StatusCode switch
                    {
                        StatusCodes.Status200OK => Ok(archive.Link),
                        StatusCodes.Status400BadRequest => BadRequest(archive.Error),
                        StatusCodes.Status404NotFound => NotFound(archive.Error),
                        _ => StatusCode(archive.StatusCode, archive.Error),
                    };
                case CreateSharedArchiveDownloadLinkStatus.SharedFolderNotFound:
                    return this.ApiPublicShareNotFound(
                        _publicShareLookupFailures,
                        token,
                        "Shared folder not found.");
                case CreateSharedArchiveDownloadLinkStatus.FolderNotFound:
                    return this.ApiNotFound("Folder not found.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(result.Status));
            }
        }

        /// <summary>
        /// Downloads shared node file.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("shared/{token}/files/{nodeFileId:guid}/content")]
        public async Task<IActionResult> DownloadSharedNodeFile(
            [FromRoute] string token,
            [FromRoute] Guid nodeFileId,
            [FromQuery] bool download = true,
            [FromQuery] bool preview = false)
        {
            IActionResult? blocked = this.GetPublicShareLookupBlockRejection(
                _publicShareLookupFailures,
                token);
            if (blocked is not null)
            {
                return blocked;
            }

            GetSharedNodeFileContentQuery query = new(token, nodeFileId, preview);
            GetSharedNodeFileContentResult result = await _mediator.Send(
                query,
                HttpContext.RequestAborted);
            switch (result.Status)
            {
                case GetSharedNodeFileContentStatus.Success:
                    NodeFile nodeFile = result.NodeFile!;
                    return result.ServesPreview
                        ? ServeSharedLargePreview(nodeFile)
                        : FileDownloadResultFactory.Create(Response, _storage, nodeFile, download);
                case GetSharedNodeFileContentStatus.SharedFolderNotFound:
                    return this.ApiPublicShareNotFound(
                        _publicShareLookupFailures,
                        token,
                        "File not found.");
                case GetSharedNodeFileContentStatus.FileNotFound:
                    return this.ApiNotFound("File not found.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(result.Status));
            }
        }

        private IActionResult ServeSharedLargePreview(NodeFile nodeFile)
        {
            string previewHashHex = Hasher.ToHexStringHash(nodeFile.FileManifest.LargeFilePreviewHash!);
            EntityTagHeaderValue entityTag = new($"\"sha256-{previewHashHex}\"");
            Response.Headers.ETag = entityTag.ToString();
            Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            if (FileETags.MatchesIfNoneMatchHeader(Request, entityTag))
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }

            Stream previewStream = _storage.GetBlobStream([previewHashHex]);
            return File(previewStream, "image/webp");
        }

        private Task<SharedNodeAccess?> ResolveActiveNodeShareTokenAsync(string token) =>
            _mediator.Send(
                new ResolveSharedNodeAccessQuery(token),
                HttpContext.RequestAborted);

    }
}
