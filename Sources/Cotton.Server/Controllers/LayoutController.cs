// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Mapster;
using EasyExtensions;
using Cotton.Topology;
using Cotton.Database;
using Cotton.Validators;
using Cotton.Server.Models;
using Cotton.Database.Models;
using Microsoft.AspNetCore.Mvc;
using Cotton.Server.Models.Dto;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using EasyExtensions.AspNetCore.Extensions;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class LayoutController(CottonDbContext _dbContext, StorageLayoutService _layouts) : ControllerBase
    {
        [Authorize]
        [HttpPut($"{Routes.Layouts}/nodes")]
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
                LayoutId = parentNode.LayoutId,
            };
            newNode.SetName(request.Name);
            await _dbContext.Nodes.AddAsync(newNode);
            await _dbContext.SaveChangesAsync();
            var mapped = newNode.Adapt<NodeDto>();
            return Ok(mapped);
        }

        [Authorize]
        [HttpGet($"{Routes.Layouts}/nodes/{{nodeId:guid}}/ancestors")]
        public async Task<IActionResult> GetAncestorNodes([FromRoute] Guid nodeId,
            [FromQuery] NodeType type = NodeType.Default)
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
                    && x.Type == type)
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
                    .Where(x => x.Id == currentNode.ParentId && x.OwnerId == userId)
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
        [HttpGet($"{Routes.Layouts}/nodes/{{nodeId:guid}}/children")]
        public async Task<IActionResult> GetChildNodes([FromRoute] Guid nodeId,
            [FromQuery] NodeType type = NodeType.Default)
        {
            Guid userId = User.GetUserId();
            var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(userId);
            var parentNode = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == nodeId
                    && x.OwnerId == userId
                    && x.LayoutId == layout.Id
                    && x.Type == type)
                .SingleOrDefaultAsync();
            if (parentNode == null)
            {
                return CottonResult.NotFound("Parent node not found.");
            }

            var nodes = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.ParentId == parentNode.Id && x.OwnerId == userId)
                .ProjectToType<NodeDto>()
                .ToListAsync();

            var files = _dbContext.NodeFiles
                .AsNoTracking()
                .Include(x => x.FileManifest)
                .Where(x => x.NodeId == parentNode.Id)
                .ToList()
                .Select(x =>
                {
                    NodeFileManifestDto dto = x.Adapt<NodeFileManifestDto>();
                    dto.ReadMetadataFromManifest(x.FileManifest);
                    return dto;
                });

            NodeContentDto result = new()
            {
                Id = nodeId,
                Nodes = nodes,
                Files = files
            };
            return Ok(result);
        }

        [Authorize]
        [HttpGet($"{Routes.Layouts}/resolver")]
        [HttpGet($"{Routes.Layouts}/resolver/{{*path}}")]
        public async Task<IActionResult> ResolveLayout([FromRoute] string? path,
            [FromQuery] NodeType type = NodeType.Default)
        {
            Guid userId = User.GetUserId();
            var found = await _layouts.GetOrCreateLatestUserLayoutAsync(userId);
            Node currentNode = await _layouts.GetOrCreateRootNodeAsync(found.Id, userId, type);
            if (string.IsNullOrWhiteSpace(path))
            {
                var dto = currentNode.Adapt<NodeDto>();
                return Ok(dto);
            }

            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            // search for the root node of this layout and user, using node type
            foreach (var part in parts)
            {
                var nextNode = await _dbContext.Nodes
                    .AsNoTracking()
                    .Where(x => x.Layout.OwnerId == userId
                        && x.ParentId == currentNode.Id
                        && x.Name == part
                        && x.Type == type)
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
