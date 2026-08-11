// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Layouts;
using Cotton.Server.Handlers.Nodes;
using Cotton.Server.Hubs;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Services;
using Cotton.Topology.Abstractions;
using Cotton.Validators;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Controllers
{
    /// <summary>
    /// Exposes HTTP endpoints for layout operations.
    /// </summary>
    [ApiController]
    [Route(Routes.V1.Layouts)]
    public class LayoutController(
        IMediator _mediator,
        CottonDbContext _dbContext,
        ILayoutService _layouts,
        ISyncChangeRecorder _syncChanges,
        IHubContext<EventHub> _hubContext,
        ILogger<LayoutController> _logger,
        ILayoutNavigator _navigator,
        ILayoutMutationGate _layoutGate) : ControllerBase
    {
        /// <summary>
        /// Gets recent nodes.
        /// </summary>
        [Authorize]
        [HttpGet("{layoutId:guid}/recent")]
        public async Task<IActionResult> GetRecentNodes([FromRoute] Guid layoutId,
            [FromQuery] int count = 10)
        {
            Guid userId = User.GetUserId();
            GetRecentNodesQuery request = new(userId, layoutId, count);
            IEnumerable<NodeFileManifestDto> result = await _mediator.Send(request);
            return Ok(result);
        }

        /// <summary>
        /// Searches files and folders across a layout.
        /// </summary>
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
            PagedResult<SearchResultDto> result = await _mediator.Send(request);
            Response.Headers.Append("X-Total-Count", result.TotalCount.ToString());
            return Ok(result.Payload);
        }

        /// <summary>
        /// Gets layout stats.
        /// </summary>
        [Authorize]
        [HttpGet("{layoutId:guid}/stats")]
        public async Task<IActionResult> GetLayoutStats([FromRoute] Guid layoutId)
        {
            Guid userId = User.GetUserId();
            LayoutStatsDto? stats = await _mediator.Send(
                new GetLayoutStatsQuery(userId, layoutId),
                HttpContext.RequestAborted);
            if (stats is null)
            {
                return CottonResult.NotFound("Layout not found.");
            }

            return Ok(stats);
        }

        /// <summary>
        /// Moves layout node.
        /// </summary>
        [Authorize]
        [HttpPatch("nodes/{nodeId:guid}/move")]
        public async Task<IActionResult> MoveLayoutNode(
            [FromRoute] Guid nodeId,
            [FromBody] MoveNodeRequestDto request)
        {
            MoveNodeCommand command = new()
            {
                NodeId = nodeId,
                ParentId = request.ParentId,
                UserId = User.GetUserId(),
            };
            NodeDto dto = await _mediator.Send(command);
            return Ok(dto);
        }

        /// <summary>
        /// Renames layout node.
        /// </summary>
        [Authorize]
        [HttpPatch("nodes/{nodeId:guid}/rename")]
        public async Task<IActionResult> RenameLayoutNode(
            [FromRoute] Guid nodeId,
            [FromBody] RenameNodeRequestDto request)
        {
            bool isValidName = NameValidator.TryNormalizeAndValidate(request.Name,
                out string normalizedName,
                out string? errorMessage);
            if (!isValidName)
            {
                return CottonResult.BadRequest(errorMessage);
            }

            Guid userId = User.GetUserId();
            Guid? layoutId = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == nodeId && x.OwnerId == userId)
                .Select(x => (Guid?)x.LayoutId)
                .SingleOrDefaultAsync();
            if (layoutId is null)
            {
                return CottonResult.NotFound("Node not found.");
            }
            await using IAsyncDisposable layoutGate = await _layoutGate.EnterAsync(layoutId.Value, HttpContext.RequestAborted);
            await using IDbContextTransaction tx = await _dbContext.Database.BeginTransactionAsync();

            Node? node = await _dbContext.Nodes
                .Where(x => x.Id == nodeId && x.OwnerId == userId)
                .SingleOrDefaultAsync();
            if (node is null)
            {
                return CottonResult.NotFound("Node not found.");
            }

            string nameKey = NameValidator.NormalizeAndGetNameKey(request.Name);

            bool nodeExists = await _dbContext.Nodes
                .AnyAsync(x =>
                    x.ParentId == node.ParentId &&
                    x.OwnerId == userId &&
                    x.NameKey == nameKey &&
                    x.LayoutId == node.LayoutId &&
                    x.Type == node.Type &&
                    x.Id != nodeId);
            if (nodeExists)
            {
                return this.ApiConflict("A folder with the same name key already exists in the parent folder: " + nameKey);
            }

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
            if (node.ParentId.HasValue)
            {
                _syncChanges.StageFolderChange(SyncChangeKind.FolderRenamed, node, node.ParentId.Value);
            }
            await _dbContext.SaveChangesAsync();
            await tx.CommitAsync();
            NodeDto mapped = node.Adapt<NodeDto>();
            await _hubContext.Clients.User(userId.ToString()).SendAsync("NodeRenamed", mapped);
            return Ok(mapped);
        }

        /// <summary>
        /// Gets layout node.
        /// </summary>
        [Authorize]
        [HttpGet("nodes/{nodeId:guid}")]
        public async Task<IActionResult> GetLayoutNode([FromRoute] Guid nodeId)
        {
            Guid userId = User.GetUserId();
            Node? node = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == nodeId && x.OwnerId == userId)
                .SingleOrDefaultAsync();
            if (node is null)
            {
                return CottonResult.NotFound("Node not found.");
            }
            NodeDto mapped = node.Adapt<NodeDto>();
            return Ok(mapped);
        }

        /// <summary>
        /// Updates layout node metadata.
        /// </summary>
        [Authorize]
        [HttpPatch("nodes/{nodeId:guid}/metadata")]
        [ProducesResponseType<NodeDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<CottonResult>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<CottonResult>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateLayoutNodeMetadata(
            [FromRoute] Guid nodeId,
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
            Node? node = await _dbContext.Nodes
                .Where(x => x.Id == nodeId && x.OwnerId == userId && x.Type == NodeType.Default)
                .SingleOrDefaultAsync();
            if (node is null)
            {
                return CottonResult.NotFound("Node not found.");
            }

            Dictionary<string, string> metadata = node.Metadata is null
                ? []
                : new Dictionary<string, string>(node.Metadata);
            foreach ((string? key, string? value) in patch)
            {
                metadata[key] = value!;
            }

            node.Metadata = metadata;
            await _dbContext.SaveChangesAsync();

            NodeDto mapped = node.Adapt<NodeDto>();
            try
            {
                await _hubContext.Clients.User(userId.ToString()).SendAsync("NodeMetadataUpdated", mapped);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to send node metadata update notification for node {NodeId}",
                    nodeId);
            }

            return Ok(mapped);
        }

        /// <summary>
        /// Deletes layout node.
        /// </summary>
        [Authorize]
        [HttpDelete("nodes/{nodeId:guid}")]
        public async Task<IActionResult> DeleteLayoutNode(
            [FromRoute] Guid nodeId,
            [FromQuery] bool skipTrash = false)
        {
            Guid userId = User.GetUserId();
            Guid? parentNodeId = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == nodeId && x.OwnerId == userId)
                .Select(x => x.ParentId)
                .SingleOrDefaultAsync();
            DeleteNodeQuery query = new(userId, nodeId, skipTrash);
            await _mediator.Send(query);
            await _hubContext.Clients.User(userId.ToString()).SendAsync(
                "NodeDeleted",
                new NodeDeletedEventDto(nodeId, parentNodeId));
            return Ok();
        }

        /// <summary>
        /// Restores layout node.
        /// </summary>
        [Authorize]
        [HttpPost("nodes/{nodeId:guid}/restore")]
        public async Task<IActionResult> RestoreLayoutNode(
            [FromRoute] Guid nodeId,
            [FromBody] RestoreItemRequestDto? request)
        {
            Guid userId = User.GetUserId();
            request ??= new RestoreItemRequestDto();

            RestoreOutcomeDto outcome = await _mediator.Send(new RestoreNodeQuery(
                userId,
                nodeId,
                request.CreateMissingParents,
                request.Overwrite));

            if (outcome.Status == RestoreStatus.Restored)
            {
                object restoredNodePayload = outcome.RestoredNode is not null
                    ? outcome.RestoredNode
                    : new { id = nodeId };
                await _hubContext.Clients.User(userId.ToString()).SendAsync(
                    "NodeRestored",
                    restoredNodePayload);
            }

            return Ok(outcome);
        }

        /// <summary>
        /// Creates layout node.
        /// </summary>
        [Authorize]
        [HttpPut("nodes")]
        public async Task<IActionResult> CreateLayoutNode([FromBody] CreateNodeRequestDto request)
        {
            bool isValidName = NameValidator.TryNormalizeAndValidate(request.Name,
                out string normalizedName,
                out string? errorMessage);
            if (!isValidName)
            {
                return CottonResult.BadRequest(errorMessage);
            }

            Guid userId = User.GetUserId();
            Layout layout = await _layouts.GetOrCreateLatestUserLayoutAsync(userId, HttpContext.RequestAborted);
            Node? preTransactionParentNode = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == request.ParentId
                    && x.OwnerId == userId
                    && x.LayoutId == layout.Id
                    && x.Type == NodeType.Default)
                .SingleOrDefaultAsync();
            if (preTransactionParentNode is null)
            {
                return CottonResult.NotFound("Parent node not found.");
            }

            string nameKey = NameValidator.NormalizeAndGetNameKey(request.Name);
            await using IAsyncDisposable layoutGate = await _layoutGate.EnterAsync(layout.Id, HttpContext.RequestAborted);
            await using IDbContextTransaction tx = await _dbContext.Database.BeginTransactionAsync();

            Node? parentNode = await _dbContext.Nodes
                .Where(x => x.Id == request.ParentId
                    && x.OwnerId == userId
                    && x.LayoutId == layout.Id
                    && x.Type == NodeType.Default)
                .SingleOrDefaultAsync();
            if (parentNode is null)
            {
                return CottonResult.NotFound("Parent node not found.");
            }

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
                Type = NodeType.Default,
                LayoutId = layout.Id,
            };
            newNode.SetParent(parentNode);
            newNode.SetName(request.Name);
            await _dbContext.Nodes.AddAsync(newNode);
            _syncChanges.StageFolderChange(SyncChangeKind.FolderCreated, newNode, parentNode.Id);
            await _dbContext.SaveChangesAsync();
            await tx.CommitAsync();
            NodeDto mapped = newNode.Adapt<NodeDto>();
            await _hubContext.Clients.User(userId.ToString()).SendAsync("NodeCreated", mapped);
            return Ok(mapped);
        }

        /// <summary>
        /// Gets ancestor nodes.
        /// </summary>
        [Authorize]
        [HttpGet("nodes/{nodeId:guid}/ancestors")]
        public async Task<IActionResult> GetAncestorNodes(
            [FromRoute] Guid nodeId,
            [FromQuery] NodeType nodeType = NodeType.Default)
        {
            Guid userId = User.GetUserId();
            Layout layout = await _layouts.GetOrCreateLatestUserLayoutAsync(userId, HttpContext.RequestAborted);

            IQueryable<Node> nodesQuery = _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.OwnerId == userId
                    && x.LayoutId == layout.Id
                    && x.Type == nodeType);

            Node? currentNode = await nodesQuery
                .SingleOrDefaultAsync(x => x.Id == nodeId);

            if (currentNode is null)
            {
                return this.ApiNotFound("Node not found.");
            }

            const int MaxDepth = 256;
            var visited = new HashSet<Guid> { currentNode.Id };
            int depth = 0;
            List<NodeDto> ancestors = [];
            while (currentNode.ParentId is not null)
            {
                if (depth++ >= MaxDepth)
                {
                    return this.ApiConflict("Maximum node hierarchy depth exceeded.");
                }
                Guid parentId = currentNode.ParentId.Value;
                if (!visited.Add(parentId))
                {
                    return this.ApiConflict("Circular reference detected in node hierarchy.");
                }
                Node? parentNode = await nodesQuery
                    .SingleOrDefaultAsync(x => x.Id == parentId);
                if (parentNode is null)
                {
                    break;
                }
                ancestors.Add(parentNode.Adapt<NodeDto>());
                currentNode = parentNode;
            }
            ancestors.Reverse();
            return Ok(ancestors);
        }

        /// <summary>
        /// Gets child nodes.
        /// </summary>
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
            PagedResult<NodeContentDto> result = await _mediator.Send(query);
            Response.Headers.Append("X-Total-Count", result.TotalCount.ToString());
            return Ok(result.Payload);
        }

        /// <summary>
        /// Resolves layout.
        /// </summary>
        [Authorize]
        [HttpGet("resolver")]
        [HttpGet("resolver/{*path}")]
        public async Task<IActionResult> ResolveLayout([FromRoute] string? path,
            [FromQuery] NodeType nodeType = NodeType.Default)
        {
            Guid userId = User.GetUserId();
            Node? currentNode = await _navigator.ResolveNodeByPathAsync(userId, path, nodeType);
            if (currentNode is null)
            {
                return CottonResult.NotFound("Layout node was not found.");
            }

            return Ok(currentNode.Adapt<NodeDto>());
        }
    }
}
