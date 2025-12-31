// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Topology;
using Cotton.Validators;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class LayoutController(CottonDbContext _dbContext, StorageLayoutService _layouts) : ControllerBase
    {
        [Authorize]
        [HttpGet($"{Routes.Nodes}/{{nodeId:guid}}")]
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
        [HttpDelete($"{Routes.Nodes}/{{nodeId:guid}}")]
        public async Task<IActionResult> DeleteLayoutNode([FromRoute] Guid nodeId)
        {
            Guid userId = User.GetUserId();
            var node = await _dbContext.Nodes
                .Where(x => x.Id == nodeId && x.OwnerId == userId)
                .SingleOrDefaultAsync();
            if (node == null)
            {
                return CottonResult.NotFound("Node not found.");
            }
            // TODO: Change root node? Or change node type? Or something else?
            var trash = await _layouts.GetUserTrashNodeAsync(userId);
            node.ParentId = trash.Id;
            await _dbContext.SaveChangesAsync();
            return Ok();
        }

        [Authorize]
        [HttpPut(Routes.Nodes)]
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
            bool nameExists = await _dbContext.Nodes
                .AnyAsync(x =>
                    x.ParentId == parentNode.Id &&
                    x.OwnerId == userId &&
                    x.NameKey == nameKey &&
                    x.LayoutId == layout.Id &&
                    x.Type == NodeType.Default);
            if (nameExists)
            {
                return this.ApiConflict("A node with the same name key already exists in the target layout: " + nameKey);
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
        [HttpGet($"{Routes.Nodes}/{{nodeId:guid}}/ancestors")]
        public async Task<IActionResult> GetAncestorNodes([FromRoute] Guid nodeId,
            [FromQuery] NodeType nodeType = NodeType.Default)
        {
            // TODO: Optimize to a single query
            // TODO: Guard against circular references
            Guid userId = User.GetUserId();
            var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(userId);
            var currentNode = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == nodeId
                    && x.OwnerId == userId
                    && x.LayoutId == layout.Id
                    && x.Type == nodeType)
                .SingleOrDefaultAsync();
            if (currentNode == null)
            {
                return CottonResult.NotFound("Node not found.");
            }
            List<NodeDto> ancestors = [];
            while (currentNode.ParentId != null)
            {
                var parentNode = await _dbContext.Nodes
                    .AsNoTracking()
                    .Where(x => x.Id == currentNode.ParentId
                        && x.OwnerId == userId
                        && x.LayoutId == layout.Id
                        && x.Type == nodeType)
                    .SingleOrDefaultAsync();
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
        [HttpGet($"{Routes.Nodes}/{{nodeId:guid}}/children")]
        public async Task<IActionResult> GetChildNodes([FromRoute] Guid nodeId,
            [FromQuery] NodeType nodeType = NodeType.Default)
        {
            Guid userId = User.GetUserId();
            var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(userId);
            var parentNode = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == nodeId
                    && x.OwnerId == userId
                    && x.LayoutId == layout.Id
                    && x.Type == nodeType)
                .SingleOrDefaultAsync();
            if (parentNode == null)
            {
                return CottonResult.NotFound("Parent node not found.");
            }

            var nodes = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.ParentId == parentNode.Id && x.OwnerId == userId && x.LayoutId == layout.Id && x.Type == nodeType)
                .ProjectToType<NodeDto>()
                .ToListAsync();

            var files = await _dbContext.NodeFiles
                .AsNoTracking()
                .Include(x => x.FileManifest)
                .Where(x => x.NodeId == parentNode.Id)
                .ToListAsync();
            var mappedFiles = files.Select(x =>
            {
                NodeFileManifestDto dto = x.Adapt<NodeFileManifestDto>();
                dto.ReadMetadataFromManifest(x.FileManifest);
                return dto;
            });

            NodeContentDto result = new()
            {
                Id = nodeId,
                Nodes = nodes,
                Files = mappedFiles
            };
            return Ok(result);
        }

        [Authorize]
        [HttpGet($"{Routes.Layouts}/resolver")]
        [HttpGet($"{Routes.Layouts}/resolver/{{*path}}")]
        public async Task<IActionResult> ResolveLayout([FromRoute] string? path,
            [FromQuery] NodeType nodeType = NodeType.Default)
        {
            Guid userId = User.GetUserId();
            var found = await _layouts.GetOrCreateLatestUserLayoutAsync(userId);
            Node currentNode = await _layouts.GetOrCreateRootNodeAsync(found.Id, userId, nodeType);
            if (string.IsNullOrWhiteSpace(path))
            {
                var dto = currentNode.Adapt<NodeDto>();
                return Ok(dto);
            }

            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            // search for the root node of this layout and user, using node type
            foreach (var part in parts)
            {
                string normalizedPart = NameValidator.NormalizeAndGetNameKey(part);
                var nextNode = await _dbContext.Nodes
                    .AsNoTracking()
                    .Where(x => x.Layout.OwnerId == userId
                        && x.ParentId == currentNode.Id
                        && x.NameKey == normalizedPart
                        && x.Type == nodeType)
                    .SingleOrDefaultAsync();
                if (nextNode == null)
                {
                    return CottonResult.NotFound($"Layout node '{part}' was not found.");
                }
                currentNode = nextNode;
            }
            var mapped = currentNode.Adapt<NodeDto>();
            return Ok(mapped);
        }
    }
}
