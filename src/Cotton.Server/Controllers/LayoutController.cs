// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Database.Models.Enums;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Layouts;
using Cotton.Server.Handlers.Nodes;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Layouts)]
    public class LayoutController(IMediator _mediator) : ControllerBase
    {
        [Authorize]
        [HttpGet("{layoutId:guid}/recent")]
        public async Task<IActionResult> GetRecentNodes([FromRoute] Guid layoutId,
            [FromQuery] int count = 10,
            [FromQuery] string[]? contentType = null,
            [FromQuery] string[]? excludeContentType = null,
            [FromQuery] bool excludeClientEncrypted = false)
        {
            Guid userId = User.GetUserId();
            GetRecentNodesQuery request = new(
                userId,
                layoutId,
                count,
                contentType,
                excludeContentType,
                excludeClientEncrypted);
            IEnumerable<NodeFileManifestDto> result = await _mediator.Send(request);
            return Ok(result);
        }

        [Authorize]
        [HttpPost("nodes/resolve")]
        public async Task<IActionResult> ResolveOwnedNodes([FromBody] Guid[]? nodeIds)
        {
            if (nodeIds is null)
            {
                return CottonResult.BadRequest("Node ids are required.");
            }
            if (nodeIds.Length > ResolveOwnedNodesQuery.MaximumNodeIds)
            {
                return CottonResult.BadRequest(
                    $"A maximum of {ResolveOwnedNodesQuery.MaximumNodeIds} node ids is allowed.");
            }

            Guid userId = User.GetUserId();
            IReadOnlyList<NodeDto> nodes = await _mediator.Send(
                new ResolveOwnedNodesQuery(userId, nodeIds),
                HttpContext.RequestAborted);
            return Ok(nodes);
        }

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

        [Authorize]
        [HttpPatch("nodes/{nodeId:guid}/rename")]
        public async Task<IActionResult> RenameLayoutNode(
            [FromRoute] Guid nodeId,
            [FromBody] RenameNodeRequestDto request)
        {
            Guid userId = User.GetUserId();
            RenameNodeResult result = await _mediator.Send(
                new RenameNodeRequest(userId, nodeId, request.Name),
                HttpContext.RequestAborted);
            if (result.Status != RenameNodeStatus.Renamed)
            {
                return result.Status switch
                {
                    RenameNodeStatus.InvalidName => CottonResult.BadRequest(result.Error!),
                    RenameNodeStatus.NodeNotFound => CottonResult.NotFound(result.Error!),
                    RenameNodeStatus.NameConflict => this.ApiConflict(result.Error!),
                    _ => throw new ArgumentOutOfRangeException(nameof(result.Status)),
                };
            }

            return Ok(result.Node!);
        }

        [Authorize]
        [HttpGet("nodes/{nodeId:guid}")]
        public async Task<IActionResult> GetLayoutNode([FromRoute] Guid nodeId)
        {
            Guid userId = User.GetUserId();
            NodeDto? node = await _mediator.Send(
                new GetOwnedNodeQuery(userId, nodeId),
                HttpContext.RequestAborted);
            if (node is null)
            {
                return CottonResult.NotFound("Node not found.");
            }

            return Ok(node);
        }

        [Authorize]
        [HttpPatch("nodes/{nodeId:guid}/metadata")]
        [ProducesResponseType<NodeDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<CottonResult>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<CottonResult>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateLayoutNodeMetadata(
            [FromRoute] Guid nodeId,
            [FromBody] Dictionary<string, string?>? patch)
        {
            Guid userId = User.GetUserId();
            UpdateNodeMetadataResult result = await _mediator.Send(
                new UpdateNodeMetadataRequest(userId, nodeId, patch),
                HttpContext.RequestAborted);
            if (result.Status != UpdateNodeMetadataStatus.Updated)
            {
                return result.Status switch
                {
                    UpdateNodeMetadataStatus.InvalidPatch => CottonResult.BadRequest(result.Error!),
                    UpdateNodeMetadataStatus.NodeNotFound => CottonResult.NotFound(result.Error!),
                    _ => throw new ArgumentOutOfRangeException(nameof(result.Status)),
                };
            }

            return Ok(result.Node!);
        }

        [Authorize]
        [HttpDelete("nodes/{nodeId:guid}")]
        public async Task<IActionResult> DeleteLayoutNode(
            [FromRoute] Guid nodeId,
            [FromQuery] bool skipTrash = false)
        {
            Guid userId = User.GetUserId();
            DeleteNodeRequest request = new(userId, nodeId, skipTrash);
            await _mediator.Send(
                request,
                HttpContext.RequestAborted);
            return Ok();
        }

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

            return Ok(outcome);
        }

        [Authorize]
        [HttpPut("nodes")]
        public async Task<IActionResult> CreateLayoutNode([FromBody] CreateNodeRequestDto request)
        {
            Guid userId = User.GetUserId();
            CreateNodeResult result = await _mediator.Send(
                new CreateNodeRequest(userId, request.ParentId, request.Name),
                HttpContext.RequestAborted);
            if (result.Status != CreateNodeStatus.Created)
            {
                return result.Status switch
                {
                    CreateNodeStatus.InvalidName => CottonResult.BadRequest(result.Error!),
                    CreateNodeStatus.ParentNotFound => CottonResult.NotFound(result.Error!),
                    CreateNodeStatus.NameConflict => this.ApiConflict(result.Error!),
                    _ => throw new ArgumentOutOfRangeException(nameof(result.Status)),
                };
            }

            return Ok(result.Node!);
        }

        [Authorize]
        [HttpGet("nodes/{nodeId:guid}/ancestors")]
        public async Task<IActionResult> GetAncestorNodes(
            [FromRoute] Guid nodeId,
            [FromQuery] NodeType nodeType = NodeType.Default)
        {
            Guid userId = User.GetUserId();
            GetNodeAncestorsResult result = await _mediator.Send(
                new GetNodeAncestorsQuery(userId, nodeId, nodeType),
                HttpContext.RequestAborted);
            switch (result.Status)
            {
                case GetNodeAncestorsStatus.Success:
                    return Ok(result.Ancestors);
                case GetNodeAncestorsStatus.NodeNotFound:
                    return this.ApiNotFound("Node not found.");
                case GetNodeAncestorsStatus.InvalidHierarchy:
                    return this.ApiConflict(result.Error!);
                default:
                    throw new ArgumentOutOfRangeException(nameof(result.Status));
            }
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
            PagedResult<NodeContentDto> result = await _mediator.Send(query);
            Response.Headers.Append("X-Total-Count", result.TotalCount.ToString());
            return Ok(result.Payload);
        }

        [Authorize]
        [HttpGet("resolver")]
        [HttpGet("resolver/{*path}")]
        public async Task<IActionResult> ResolveLayout([FromRoute] string? path,
            [FromQuery] NodeType nodeType = NodeType.Default)
        {
            Guid userId = User.GetUserId();
            NodeDto? node = await _mediator.Send(
                new ResolveLayoutPathQuery(userId, path, nodeType),
                HttpContext.RequestAborted);
            if (node is null)
            {
                return CottonResult.NotFound("Layout node was not found.");
            }

            return Ok(node);
        }
    }
}
