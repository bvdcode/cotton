// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Handlers.Nodes;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Services;
using Cotton.Shared;
using Cotton.Topology.Abstractions;
using Cotton.Validators;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Layouts)]
    public class LayoutController(
        IMediator _mediator,
        CottonDbContext _dbContext,
        ILayoutService _layouts,
        ILayoutPathResolver _layoutPathResolver) : ControllerBase
    {
        [Authorize]
        [HttpGet("{layoutId:guid}/search")]
        public async Task<IActionResult> SearchLayouts(
            [FromRoute] Guid layoutId,
            [FromQuery] string query,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return CottonResult.BadRequest("Query cannot be empty.");
            }
            Guid userId = User.GetUserId();
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
            string searchKey = NameValidator.NormalizeAndGetNameKey(query);
            var nodesQuery = _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.OwnerId == userId
                    && x.LayoutId == layoutId
                    && x.NameKey.Contains(searchKey))
                .OrderBy(x => x.NameKey);

            var filesQuery = _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.OwnerId == userId
                    && x.Node.LayoutId == layoutId
                    && x.NameKey.Contains(searchKey))
                .OrderBy(x => x.NameKey);

            int nodesCount = await nodesQuery.CountAsync();
            int filesCount = await filesQuery.CountAsync();
            int totalCount = nodesCount + filesCount;

            int skip = (page - 1) * pageSize;
            int nodesToTake = Math.Max(0, Math.Min(pageSize, nodesCount - skip));
            int filesSkip = Math.Max(0, skip - nodesCount);
            int filesToTake = Math.Max(0, pageSize - nodesToTake);

            var nodes = nodesToTake == 0 ? []
                : await nodesQuery.Skip(skip).Take(nodesToTake)
                    .ProjectToType<NodeDto>()
                    .ToListAsync();

            var files = filesToTake == 0 ? []
                : await filesQuery.Skip(filesSkip).Take(filesToTake)
                    .ProjectToType<FileManifestDto>()
                    .ToListAsync();

            Response.Headers.Append("X-Total-Count", totalCount.ToString());
            SearchResultDto result = new()
            {
                Nodes = nodes,
                Files = files,
            };

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
            [FromQuery] int pageSize = 100)
        {
            Guid userId = User.GetUserId();
            GetChildrenQuery query = new(userId, nodeId, nodeType, page, pageSize);
            var result = await _mediator.Send(query);
            Response.Headers.Append("X-Total-Count", result.TotalCount.ToString());
            return Ok(result);
        }

        [Authorize]
        [HttpGet("resolver")]
        [HttpGet("resolver/{*path}")]
        public async Task<IActionResult> ResolveLayout([FromRoute] string? path,
            [FromQuery] NodeType nodeType = NodeType.Default)
        {
            Guid userId = User.GetUserId();
            var currentNode = await _layoutPathResolver.ResolveNodeByPathAsync(userId, path, nodeType);
            if (currentNode is null)
            {
                return CottonResult.NotFound("Layout node was not found.");
            }

            return Ok(currentNode.Adapt<NodeDto>());
        }
    }
}
