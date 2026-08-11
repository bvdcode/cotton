// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Auth;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Layouts;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
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
        CottonDbContext _dbContext,
        IStoragePipeline _storage,
        IDatabaseIntegrityVerifier _integrity,
        FileGraphIntegrityVerifier _fileGraphIntegrity,
        PublicShareLookupFailureLimiter _publicShareLookupFailures,
        ArchiveDownloadService _archives) : ControllerBase
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
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

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

            Guid targetNodeId = nodeId ?? nodeShareToken.NodeId;
            bool canAccessNode = await IsNodeInSharedSubtreeAsync(
                targetNodeId,
                nodeShareToken.NodeId,
                nodeShareToken.CreatedByUserId);

            if (!canAccessNode)
            {
                return this.ApiNotFound("Folder not found.");
            }

            Node? targetNode = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == targetNodeId
                    && x.OwnerId == nodeShareToken.CreatedByUserId
                    && x.Type == NodeType.Default)
                .SingleOrDefaultAsync();
            if (targetNode is null)
            {
                return this.ApiNotFound("Folder not found.");
            }

            int skip = (page - 1) * pageSize;

            IQueryable<NodeDto> nodesQuery = _dbContext.Nodes
                .AsNoTracking()
                .OrderBy(x => x.NameKey)
                .Where(x => x.ParentId == targetNodeId
                    && x.OwnerId == nodeShareToken.CreatedByUserId
                    && x.Type == NodeType.Default)
                .ProjectToType<NodeDto>();

            IQueryable<NodeFile> filesBaseQuery = _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.NodeId == targetNodeId
                    && x.OwnerId == nodeShareToken.CreatedByUserId);

            int nodesCount = await nodesQuery.CountAsync();
            int filesCount = await filesBaseQuery.CountAsync();

            int nodesToTake = Math.Max(0, Math.Min(pageSize, nodesCount - skip));
            int filesSkip = Math.Max(0, skip - nodesCount);
            int filesToTake = Math.Max(0, pageSize - nodesToTake);

            List<NodeDto> nodes = nodesToTake == 0 ? []
                : await nodesQuery.Skip(skip).Take(nodesToTake).ToListAsync();

            List<SharedNodeFileDto> files = filesToTake == 0 ? []
                : await LoadSharedFilesAsync(filesBaseQuery, filesSkip, filesToTake);

            SharedNodeContentDto response = new()
            {
                Nodes = nodes,
                Files = files,
                Id = targetNode.Id,
                CreatedAt = targetNode.CreatedAt,
                UpdatedAt = targetNode.UpdatedAt,
            };

            Response.Headers.Append("X-Total-Count", (nodesCount + filesCount).ToString());
            return Ok(response);
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

            SharedNodeAccess? nodeShareToken = await ResolveActiveNodeShareTokenAsync(token);
            if (nodeShareToken is null)
            {
                return this.ApiPublicShareNotFound(
                    _publicShareLookupFailures,
                    token,
                    "Shared folder not found.");
            }

            bool canAccessNode = await IsNodeInSharedSubtreeAsync(
                nodeId,
                nodeShareToken.NodeId,
                nodeShareToken.CreatedByUserId);
            if (!canAccessNode)
            {
                return this.ApiNotFound("Folder not found.");
            }

            Node? currentNode = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == nodeId
                    && x.OwnerId == nodeShareToken.CreatedByUserId
                    && x.Type == NodeType.Default)
                .SingleOrDefaultAsync();
            if (currentNode is null)
            {
                return this.ApiNotFound("Folder not found.");
            }

            (List<NodeDto> ancestors, string? error) = await LoadSharedAncestorsAsync(
                currentNode,
                nodeShareToken.NodeId,
                nodeShareToken.CreatedByUserId);
            if (error is not null)
            {
                return this.ApiConflict(error);
            }

            return Ok(ancestors);
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

            SharedNodeAccess? nodeShareToken = await ResolveActiveNodeShareTokenAsync(token);
            if (nodeShareToken is null)
            {
                return this.ApiPublicShareNotFound(
                    _publicShareLookupFailures,
                    token,
                    "Shared folder not found.");
            }

            Guid targetNodeId = nodeId ?? nodeShareToken.NodeId;
            bool canAccessNode = await IsNodeInSharedSubtreeAsync(
                targetNodeId,
                nodeShareToken.NodeId,
                nodeShareToken.CreatedByUserId);
            if (!canAccessNode)
            {
                return this.ApiNotFound("Folder not found.");
            }

            CreateArchiveDownloadLinkResult result = await _archives.CreateDownloadLinkAsync(
                nodeShareToken.CreatedByUserId,
                new CreateArchiveDownloadLinkRequest
                {
                    NodeIds = [targetNodeId],
                    EnforcePublicShareLimits = true,
                },
                cancellationToken);

            return result.StatusCode switch
            {
                StatusCodes.Status200OK => Ok(result.Link),
                StatusCodes.Status400BadRequest => BadRequest(result.Error),
                StatusCodes.Status404NotFound => NotFound(result.Error),
                _ => StatusCode(result.StatusCode, result.Error),
            };
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

            SharedNodeAccess? nodeShareToken = await ResolveActiveNodeShareTokenAsync(token);
            if (nodeShareToken is null)
            {
                return this.ApiPublicShareNotFound(_publicShareLookupFailures, token, "File not found.");
            }

            NodeFile? nodeFile = await LoadSharedNodeFileAsync(nodeFileId, nodeShareToken.CreatedByUserId);
            if (nodeFile is null)
            {
                return this.ApiNotFound("File not found.");
            }

            bool servesPreview = preview && nodeFile.FileManifest.LargeFilePreviewHash is not null;
            RequireSharedFileIntegrity(nodeFile, servesPreview);

            if (nodeFile.Node.Type != NodeType.Default)
            {
                return this.ApiNotFound("File not found.");
            }

            bool canAccessFile = await IsNodeInSharedSubtreeAsync(
                nodeFile.NodeId,
                nodeShareToken.NodeId,
                nodeShareToken.CreatedByUserId);
            if (!canAccessFile)
            {
                return this.ApiNotFound("File not found.");
            }

            return servesPreview
                ? ServeSharedLargePreview(nodeFile)
                : ServeSharedFileDownload(nodeFile, download);
        }

        private async Task<(List<NodeDto> Ancestors, string? Error)> LoadSharedAncestorsAsync(
            Node currentNode,
            Guid sharedRootNodeId,
            Guid ownerId)
        {
            const int maxDepth = 256;
            int depth = 0;
            HashSet<Guid> visited = [currentNode.Id];
            List<NodeDto> ancestors = [];

            while (currentNode.ParentId.HasValue)
            {
                if (depth++ >= maxDepth)
                {
                    return ([], "Maximum node hierarchy depth exceeded.");
                }

                Guid parentId = currentNode.ParentId.Value;
                if (!visited.Add(parentId))
                {
                    return ([], "Circular reference detected in node hierarchy.");
                }

                Node? parentNode = await _dbContext.Nodes
                    .AsNoTracking()
                    .Where(x => x.Id == parentId
                        && x.OwnerId == ownerId
                        && x.Type == NodeType.Default)
                    .SingleOrDefaultAsync();

                if (parentNode is null)
                {
                    break;
                }

                if (parentNode.Id == sharedRootNodeId)
                {
                    ancestors.Add(parentNode.Adapt<NodeDto>());
                    break;
                }

                ancestors.Add(parentNode.Adapt<NodeDto>());
                currentNode = parentNode;
            }

            ancestors.Reverse();
            return (ancestors, null);
        }

        private Task<NodeFile?> LoadSharedNodeFileAsync(Guid nodeFileId, Guid ownerId)
        {
            return _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .ThenInclude(x => x.Chunk)
                .SingleOrDefaultAsync(x => x.Id == nodeFileId && x.OwnerId == ownerId);
        }

        private void RequireSharedFileIntegrity(NodeFile nodeFile, bool servesPreview)
        {
            if (servesPreview)
            {
                _fileGraphIntegrity.RequireValidMetadata(_dbContext, nodeFile, "shared-folder.preview");
                return;
            }

            _fileGraphIntegrity.RequireValidContent(_dbContext, nodeFile, "shared-folder.download");
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

        private IActionResult ServeSharedFileDownload(NodeFile nodeFile, bool download)
        {
            string[] uids = nodeFile.FileManifest.FileManifestChunks.GetChunkHashes();
            PipelineContext context = new()
            {
                FileSizeBytes = nodeFile.FileManifest.SizeBytes,
                ChunkLengths = nodeFile.FileManifest.FileManifestChunks.GetChunkLengths(),
            };

            Stream stream = _storage.GetBlobStream(uids, context);
            Response.Headers.ContentEncoding = "identity";
            Response.Headers.CacheControl = "private, no-store, no-transform";
            EntityTagHeaderValue entityTag = FileETags.CreateContentEntityTag(nodeFile);
            bool requestedInline = !download;
            FileResponseSecurity.ApplyFileResponseHeaders(Response, nodeFile.FileManifest.ContentType, requestedInline);

            return File(
                stream,
                FileResponseSecurity.ResolveContentTypeForResponse(nodeFile.FileManifest.ContentType, requestedInline),
                fileDownloadName: FileResponseSecurity.ResolveFileDownloadName(
                    nodeFile.Name,
                    requestedInline,
                    nodeFile.FileManifest.ContentType),
                lastModified: new DateTimeOffset(nodeFile.CreatedAt),
                entityTag: entityTag,
                enableRangeProcessing: true);
        }

        private Task<SharedNodeAccess?> ResolveActiveNodeShareTokenAsync(string token) =>
            _mediator.Send(
                new ResolveSharedNodeAccessQuery(token),
                HttpContext.RequestAborted);

        private async Task<bool> IsNodeInSharedSubtreeAsync(
            Guid nodeId,
            Guid sharedRootNodeId,
            Guid ownerId)
        {
            const int maxDepth = 512;

            Node? currentNode = await LoadVerifiedSharedDefaultNodeAsync(
                nodeId,
                ownerId,
                "shared-folder.subtree.node");

            if (currentNode is null)
            {
                return false;
            }

            if (currentNode.Id == sharedRootNodeId)
            {
                return true;
            }

            HashSet<Guid> visited = [currentNode.Id];
            int depth = 0;

            while (currentNode.ParentId.HasValue)
            {
                if (depth++ >= maxDepth)
                {
                    return false;
                }

                Guid parentId = currentNode.ParentId.Value;
                if (!visited.Add(parentId))
                {
                    return false;
                }

                currentNode = await LoadVerifiedSharedDefaultNodeAsync(
                    parentId,
                    ownerId,
                    "shared-folder.subtree.ancestor");

                if (currentNode is null)
                {
                    return false;
                }

                if (currentNode.Id == sharedRootNodeId)
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<Node?> LoadVerifiedSharedDefaultNodeAsync(
            Guid nodeId,
            Guid ownerId,
            string boundary)
        {
            Node? node = await _dbContext.Nodes
                .Where(x => x.Id == nodeId
                    && x.OwnerId == ownerId
                    && x.Type == NodeType.Default)
                .SingleOrDefaultAsync();

            if (node is null)
            {
                return null;
            }

            try
            {
                _integrity.RequireValid(_dbContext, node, boundary);
            }
            catch (DatabaseIntegrityException)
            {
                return null;
            }

            return node;
        }

        private static async Task<List<SharedNodeFileDto>> LoadSharedFilesAsync(
            IQueryable<NodeFile> filesBaseQuery,
            int filesSkip,
            int filesToTake)
        {
            List<NodeFile> fileEntities = await filesBaseQuery
                .OrderBy(x => x.NameKey)
                .Include(x => x.FileManifest)
                .Skip(filesSkip)
                .Take(filesToTake)
                .ToListAsync();

            return [.. fileEntities.Select(x => new SharedNodeFileDto
            {
                Id = x.Id,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                NodeId = x.NodeId,
                Name = x.Name,
                ContentType = x.FileManifest.ContentType,
                SizeBytes = x.FileManifest.SizeBytes,
                PreviewHashEncryptedHex = x.FileManifest.GetPreviewHashEncryptedHex(),
            })];
        }
    }
}
