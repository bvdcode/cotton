// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Layouts;
using Cotton.Server.Handlers.Nodes;
using Cotton.Server.Hubs;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using Cotton.Topology.Abstractions;
using Cotton.Validators;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Helpers;
using EasyExtensions.Mediator;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Layouts)]
    public class LayoutController(
        IMediator _mediator,
        CottonDbContext _dbContext,
        ILayoutService _layouts,
        IHubContext<EventHub> _hubContext,
        ILayoutNavigator _navigator,
        IStoragePipeline _storage) : ControllerBase
    {
        private const int DefaultSharedFolderTokenLength = 16;

        [Authorize]
        [HttpGet("{layoutId:guid}/search")]
        public async Task<IActionResult> SearchLayouts(
            [FromRoute] Guid layoutId,
            [FromQuery] string query,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            Guid userId = User.GetUserId();
            SearchLayoutsQuery request = new(userId, layoutId, query, page, pageSize);
            var result = await _mediator.Send(request);
            Response.Headers.Append("X-Total-Count", result.TotalCount.ToString());
            return Ok(result);
        }

        [Authorize]
        [HttpGet("{layoutId:guid}/stats")]
        public async Task<IActionResult> GetLayoutStats([FromRoute] Guid layoutId)
        {
            Guid userId = User.GetUserId();
            var layout = await _dbContext.UserLayouts
                .AsNoTracking()
                .Where(x => x.Id == layoutId && x.OwnerId == userId)
                .SingleOrDefaultAsync();
            if (layout == null)
            {
                return CottonResult.NotFound("Layout not found.");
            }
            int nodeCount = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.LayoutId == layout.Id && x.OwnerId == userId)
                .CountAsync();
            int fileCount = await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.Node.LayoutId == layout.Id && x.Node.OwnerId == userId)
                .CountAsync();
            long sizeBytes = await _dbContext.NodeFiles
                .Include(x => x.FileManifest)
                .AsNoTracking()
                .Where(x => x.Node.LayoutId == layout.Id && x.Node.OwnerId == userId)
                .SumAsync(x => (long?)x.FileManifest.SizeBytes) ?? 0L;
            LayoutStatsDto stats = new()
            {
                SizeBytes = sizeBytes,
                LayoutId = layout.Id,
                NodeCount = nodeCount,
                FileCount = fileCount
            };
            return Ok(stats);
        }

        [Authorize]
        [HttpPatch("nodes/{nodeId:guid}/rename")]
        public async Task<IActionResult> RenameLayoutNode(
            [FromRoute] Guid nodeId,
            [FromBody] RenameNodeRequest request)
        {
            bool isValidName = NameValidator.TryNormalizeAndValidate(request.Name,
                out string normalizedName,
                out string? errorMessage);
            if (!isValidName)
            {
                return CottonResult.BadRequest(errorMessage);
            }

            Guid userId = User.GetUserId();
            var node = await _dbContext.Nodes
                .Where(x => x.Id == nodeId && x.OwnerId == userId)
                .SingleOrDefaultAsync();
            if (node == null)
            {
                return CottonResult.NotFound("Node not found.");
            }

            var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(userId);

            string nameKey = NameValidator.NormalizeAndGetNameKey(request.Name);

            // Check for duplicate nodes in the same parent
            bool nodeExists = await _dbContext.Nodes
                .AnyAsync(x =>
                    x.ParentId == node.ParentId &&
                    x.OwnerId == userId &&
                    x.NameKey == nameKey &&
                    x.LayoutId == layout.Id &&
                    x.Type == node.Type &&
                    x.Id != nodeId);
            if (nodeExists)
            {
                return this.ApiConflict("A folder with the same name key already exists in the parent folder: " + nameKey);
            }

            // Check for duplicate files in the same parent
            if (node.ParentId.HasValue)
            {
                bool fileExists = await _dbContext.NodeFiles
                    .AnyAsync(x =>
                        x.NodeId == node.ParentId.Value &&
                        x.OwnerId == userId &&
                        x.NameKey == nameKey);
                if (fileExists)
                {
                    return this.ApiConflict("A file with the same name key already exists in the parent folder: " + nameKey);
                }
            }

            node.SetName(request.Name);
            await _dbContext.SaveChangesAsync();
            await _hubContext.Clients.User(userId.ToString()).SendAsync("NodeRenamed", node.Id, node.Name, node.NameKey);
            var mapped = node.Adapt<NodeDto>();
            return Ok(mapped);
        }

        [Authorize]
        [HttpGet("nodes/{nodeId:guid}")]
        public async Task<IActionResult> GetLayoutNode([FromRoute] Guid nodeId)
        {
            Guid userId = User.GetUserId();
            var node = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == nodeId && x.OwnerId == userId)
                .SingleOrDefaultAsync();
            if (node == null)
            {
                return CottonResult.NotFound("Node not found.");
            }
            var mapped = node.Adapt<NodeDto>();
            return Ok(mapped);
        }

        [Authorize]
        [HttpDelete("nodes/{nodeId:guid}")]
        public async Task<IActionResult> DeleteLayoutNode(
            [FromRoute] Guid nodeId,
            [FromQuery] bool skipTrash = false)
        {
            Guid userId = User.GetUserId();
            DeleteNodeQuery query = new(userId, nodeId, skipTrash);
            await _mediator.Send(query);
            await _hubContext.Clients.User(userId.ToString()).SendAsync("NodeDeleted", nodeId);
            return Ok();
        }

        [Authorize]
        [HttpPut("nodes")]
        public async Task<IActionResult> CreateLayoutNode([FromBody] CreateNodeRequest request)
        {
            bool isValidName = NameValidator.TryNormalizeAndValidate(request.Name,
                out string normalizedName,
                out string? errorMessage);
            if (!isValidName)
            {
                return CottonResult.BadRequest(errorMessage);
            }

            Guid userId = User.GetUserId();
            var parentNode = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == request.ParentId && x.OwnerId == userId)
                .SingleOrDefaultAsync();
            if (parentNode == null)
            {
                return CottonResult.NotFound("Parent node not found.");
            }

            var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(userId);

            string nameKey = NameValidator.NormalizeAndGetNameKey(request.Name);

            // Check for duplicate nodes in the parent
            bool nodeExists = await _dbContext.Nodes
                .AnyAsync(x =>
                    x.ParentId == parentNode.Id &&
                    x.OwnerId == userId &&
                    x.NameKey == nameKey &&
                    x.LayoutId == layout.Id &&
                    x.Type == NodeType.Default);
            if (nodeExists)
            {
                return this.ApiConflict("A folder with the same name key already exists in the target layout: " + nameKey);
            }

            // Check for duplicate files in the parent
            bool fileExists = await _dbContext.NodeFiles
                .AnyAsync(x =>
                    x.NodeId == parentNode.Id &&
                    x.OwnerId == userId &&
                    x.NameKey == nameKey);
            if (fileExists)
            {
                return this.ApiConflict("A file with the same name key already exists in the target layout: " + nameKey);
            }

            var newNode = new Node
            {
                OwnerId = userId,
                ParentId = parentNode.Id,
                Type = NodeType.Default,
                LayoutId = layout.Id,
            };
            newNode.SetName(request.Name);
            await _dbContext.Nodes.AddAsync(newNode);
            await _dbContext.SaveChangesAsync();
            var mapped = newNode.Adapt<NodeDto>();
            await _hubContext.Clients.User(userId.ToString()).SendAsync("NodeCreated", mapped);
            return Ok(mapped);
        }

        [Authorize]
        [HttpGet("nodes/{nodeId:guid}/ancestors")]
        public async Task<IActionResult> GetAncestorNodes(
            [FromRoute] Guid nodeId,
            [FromQuery] NodeType nodeType = NodeType.Default)
        {
            Guid userId = User.GetUserId();
            var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(userId);

            var nodesQuery = _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.OwnerId == userId
                    && x.LayoutId == layout.Id
                    && x.Type == nodeType);

            var currentNode = await nodesQuery
                .SingleOrDefaultAsync(x => x.Id == nodeId);

            if (currentNode == null)
            {
                return this.ApiNotFound("Node not found.");
            }

            const int MaxDepth = 256;
            var visited = new HashSet<Guid> { currentNode.Id };
            int depth = 0;
            List<NodeDto> ancestors = [];
            while (currentNode.ParentId != null)
            {
                if (depth++ >= MaxDepth)
                {
                    return this.ApiConflict("Maximum node hierarchy depth exceeded.");
                }
                var parentId = currentNode.ParentId.Value;
                if (!visited.Add(parentId))
                {
                    return this.ApiConflict("Circular reference detected in node hierarchy.");
                }
                var parentNode = await nodesQuery
                    .SingleOrDefaultAsync(x => x.Id == parentId);
                if (parentNode == null)
                {
                    break;
                }
                ancestors.Add(parentNode.Adapt<NodeDto>());
                currentNode = parentNode;
            }
            ancestors.Reverse();
            return Ok(ancestors);
        }

        [Authorize]
        [HttpGet("nodes/{nodeId:guid}/children")]
        public async Task<IActionResult> GetChildNodes(
            [FromRoute] Guid nodeId,
            [FromQuery] NodeType nodeType = NodeType.Default,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100,
            [FromQuery] int depth = 0)
        {
            Guid userId = User.GetUserId();
            GetChildrenQuery query = new(userId, nodeId, nodeType, page, pageSize, depth);
            var result = await _mediator.Send(query);
            Response.Headers.Append("X-Total-Count", result.TotalCount.ToString());
            return Ok(result);
        }

        [Authorize]
        [HttpGet("nodes/{nodeId:guid}/share-link")]
        public async Task<IActionResult> GetNodeShareLink(
            [FromRoute] Guid nodeId,
            [FromQuery] int expireAfterMinutes = 1440,
            [FromQuery] string? customToken = "")
        {
            const int maxExpireMinutes = 60 * 24 * 365; // 1 year
            ArgumentOutOfRangeException.ThrowIfGreaterThan(expireAfterMinutes, maxExpireMinutes, nameof(expireAfterMinutes));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expireAfterMinutes, nameof(expireAfterMinutes));

            Guid userId = User.GetUserId();
            var node = await _dbContext.Nodes
                .Where(x => x.Id == nodeId && x.OwnerId == userId && x.Type == NodeType.Default)
                .SingleOrDefaultAsync();
            if (node == null)
            {
                return CottonResult.NotFound("Node not found.");
            }

            string token;
            if (!string.IsNullOrWhiteSpace(customToken))
            {
                bool exists = await _dbContext.DownloadTokens.AnyAsync(x => x.Token == customToken)
                    || await _dbContext.NodeShareTokens.AnyAsync(x => x.Token == customToken);
                if (exists)
                {
                    return this.ApiConflict("The custom token is already in use. Please choose a different one.");
                }

                token = customToken;
            }
            else
            {
                token = await CreateUniqueShareTokenAsync(DefaultSharedFolderTokenLength);
            }

            NodeShareToken newToken = new()
            {
                Name = node.Name,
                NodeId = node.Id,
                CreatedByUserId = userId,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expireAfterMinutes),
                Token = token,
            };

            await _dbContext.NodeShareTokens.AddAsync(newToken);
            await _dbContext.SaveChangesAsync();
            return Ok($"/s/{newToken.Token}");
        }

        [AllowAnonymous]
        [HttpGet("shared/{token}")]
        public async Task<IActionResult> GetSharedNodeInfo([FromRoute] string token)
        {
            var nodeShareToken = await ResolveActiveNodeShareTokenAsync(token);
            if (nodeShareToken == null)
            {
                return this.ApiNotFound("Shared folder not found.");
            }

            return Ok(new SharedNodeInfoDto
            {
                Token = nodeShareToken.Token,
                NodeId = nodeShareToken.NodeId,
                Name = nodeShareToken.Name,
                ExpiresAt = nodeShareToken.ExpiresAt,
            });
        }

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

            var nodeShareToken = await ResolveActiveNodeShareTokenAsync(token);
            if (nodeShareToken == null)
            {
                return this.ApiNotFound("Shared folder not found.");
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

            var targetNode = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == targetNodeId
                    && x.OwnerId == nodeShareToken.CreatedByUserId
                    && x.Type == NodeType.Default)
                .SingleOrDefaultAsync();
            if (targetNode == null)
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

            var filesBaseQuery = _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.NodeId == targetNodeId
                    && x.OwnerId == nodeShareToken.CreatedByUserId);

            int nodesCount = await nodesQuery.CountAsync();
            int filesCount = await filesBaseQuery.CountAsync();

            int nodesToTake = Math.Max(0, Math.Min(pageSize, nodesCount - skip));
            int filesSkip = Math.Max(0, skip - nodesCount);
            int filesToTake = Math.Max(0, pageSize - nodesToTake);

            var nodes = nodesToTake == 0 ? []
                : await nodesQuery.Skip(skip).Take(nodesToTake).ToListAsync();

            var files = filesToTake == 0 ? []
                : await filesBaseQuery
                    .OrderBy(x => x.NameKey)
                    .Include(x => x.FileManifest)
                    .Skip(filesSkip)
                    .Take(filesToTake)
                    .ProjectToType<NodeFileManifestDto>()
                    .ToListAsync();

            NodeContentDto response = new()
            {
                Nodes = nodes,
                Files = files,
                Id = targetNode.Id,
                CreatedAt = targetNode.CreatedAt,
                UpdatedAt = targetNode.UpdatedAt,
                TotalCount = nodesCount + filesCount,
            };

            Response.Headers.Append("X-Total-Count", response.TotalCount.ToString());
            return Ok(response);
        }

        [AllowAnonymous]
        [HttpGet("shared/{token}/ancestors/{nodeId:guid}")]
        public async Task<IActionResult> GetSharedNodeAncestors(
            [FromRoute] string token,
            [FromRoute] Guid nodeId)
        {
            var nodeShareToken = await ResolveActiveNodeShareTokenAsync(token);
            if (nodeShareToken == null)
            {
                return this.ApiNotFound("Shared folder not found.");
            }

            bool canAccessNode = await IsNodeInSharedSubtreeAsync(
                nodeId,
                nodeShareToken.NodeId,
                nodeShareToken.CreatedByUserId);
            if (!canAccessNode)
            {
                return this.ApiNotFound("Folder not found.");
            }

            var currentNode = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == nodeId
                    && x.OwnerId == nodeShareToken.CreatedByUserId
                    && x.Type == NodeType.Default)
                .SingleOrDefaultAsync();
            if (currentNode == null)
            {
                return this.ApiNotFound("Folder not found.");
            }

            const int maxDepth = 256;
            int depth = 0;
            var visited = new HashSet<Guid> { currentNode.Id };
            List<NodeDto> ancestors = [];

            while (currentNode.ParentId.HasValue)
            {
                if (depth++ >= maxDepth)
                {
                    return this.ApiConflict("Maximum node hierarchy depth exceeded.");
                }

                Guid parentId = currentNode.ParentId.Value;
                if (!visited.Add(parentId))
                {
                    return this.ApiConflict("Circular reference detected in node hierarchy.");
                }

                var parentNode = await _dbContext.Nodes
                    .AsNoTracking()
                    .Where(x => x.Id == parentId
                        && x.OwnerId == nodeShareToken.CreatedByUserId
                        && x.Type == NodeType.Default)
                    .SingleOrDefaultAsync();

                if (parentNode == null)
                {
                    break;
                }

                if (parentNode.Id == nodeShareToken.NodeId)
                {
                    ancestors.Add(parentNode.Adapt<NodeDto>());
                    break;
                }

                ancestors.Add(parentNode.Adapt<NodeDto>());
                currentNode = parentNode;
            }

            ancestors.Reverse();
            return Ok(ancestors);
        }

        [AllowAnonymous]
        [HttpGet("shared/{token}/files/{nodeFileId:guid}/content")]
        public async Task<IActionResult> DownloadSharedNodeFile(
            [FromRoute] string token,
            [FromRoute] Guid nodeFileId,
            [FromQuery] bool download = true,
            [FromQuery] bool preview = false)
        {
            var nodeShareToken = await ResolveActiveNodeShareTokenAsync(token);
            if (nodeShareToken == null)
            {
                return this.ApiNotFound("File not found.");
            }

            var nodeFile = await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .ThenInclude(x => x.Chunk)
                .SingleOrDefaultAsync(x => x.Id == nodeFileId
                    && x.OwnerId == nodeShareToken.CreatedByUserId);

            if (nodeFile == null || nodeFile.Node.Type != NodeType.Default)
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
                ChunkLengths = nodeFile.FileManifest.FileManifestChunks.GetChunkLengths(),
            };

            Stream stream = _storage.GetBlobStream(uids, context);
            Response.Headers.ContentEncoding = "identity";
            Response.Headers.CacheControl = "private, no-store, no-transform";
            var entityTag = EntityTagHeaderValue.Parse($"\"sha256-{Hasher.ToHexStringHash(nodeFile.FileManifest.ProposedContentHash)}\"");

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
        [HttpGet("resolver")]
        [HttpGet("resolver/{*path}")]
        public async Task<IActionResult> ResolveLayout([FromRoute] string? path,
            [FromQuery] NodeType nodeType = NodeType.Default)
        {
            Guid userId = User.GetUserId();
            var currentNode = await _navigator.ResolveNodeByPathAsync(userId, path, nodeType);
            if (currentNode is null)
            {
                return CottonResult.NotFound("Layout node was not found.");
            }

            return Ok(currentNode.Adapt<NodeDto>());
        }

        private async Task<NodeShareToken?> ResolveActiveNodeShareTokenAsync(string token)
        {
            DateTime now = DateTime.UtcNow;
            var nodeShareToken = await _dbContext.NodeShareTokens
                .AsNoTracking()
                .Include(x => x.Node)
                .Where(x => x.Token == token
                    && (!x.ExpiresAt.HasValue || x.ExpiresAt.Value > now))
                .SingleOrDefaultAsync();

            if (nodeShareToken == null || nodeShareToken.Node.Type != NodeType.Default)
            {
                return null;
            }

            return nodeShareToken;
        }

        private async Task<bool> IsNodeInSharedSubtreeAsync(
            Guid nodeId,
            Guid sharedRootNodeId,
            Guid ownerId)
        {
            const int maxDepth = 512;

            var currentNode = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == nodeId
                    && x.OwnerId == ownerId
                    && x.Type == NodeType.Default)
                .Select(x => new { x.Id, x.ParentId })
                .SingleOrDefaultAsync();

            if (currentNode == null)
            {
                return false;
            }

            if (currentNode.Id == sharedRootNodeId)
            {
                return true;
            }

            var visited = new HashSet<Guid> { currentNode.Id };
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

                if (parentId == sharedRootNodeId)
                {
                    return true;
                }

                currentNode = await _dbContext.Nodes
                    .AsNoTracking()
                    .Where(x => x.Id == parentId
                        && x.OwnerId == ownerId
                        && x.Type == NodeType.Default)
                    .Select(x => new { x.Id, x.ParentId })
                    .SingleOrDefaultAsync();

                if (currentNode == null)
                {
                    return false;
                }
            }

            return false;
        }

        private async Task<string> CreateUniqueShareTokenAsync(int length)
        {
            const int maxAttempts = 8;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                string candidate = StringHelpers.CreateRandomString(length);
                bool existsInFileTokens = await _dbContext.DownloadTokens.AnyAsync(x => x.Token == candidate);
                if (existsInFileTokens)
                {
                    continue;
                }

                bool existsInNodeTokens = await _dbContext.NodeShareTokens.AnyAsync(x => x.Token == candidate);
                if (!existsInNodeTokens)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Unable to generate a unique share token.");
        }
    }
}
