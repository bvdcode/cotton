// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Mapster;
using EasyExtensions;
using Cotton.Server.Models;
using Cotton.Server.Database;
using Cotton.Server.Extensions;
using Cotton.Server.Models.Dto;
using Microsoft.AspNetCore.Mvc;
using Cotton.Server.Database.Models;
using Microsoft.EntityFrameworkCore;
using Cotton.Server.Models.Requests;
using Microsoft.AspNetCore.Authorization;
using Cotton.Server.Database.Models.Enums;
using Cotton.Server.Validators;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class LayoutController(CottonDbContext _dbContext) : ControllerBase
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
            var parentNode = await _dbContext.UserLayoutNodes
                .AsNoTracking()
                .Where(x => x.Id == request.ParentId && x.OwnerId == userId)
                .SingleOrDefaultAsync();
            if (parentNode == null)
            {
                return CottonResult.NotFound("Parent node not found.");
            }
            var newNode = new Node
            {
                OwnerId = userId,
                Name = normalizedName,
                ParentId = parentNode.Id,
                Type = UserLayoutNodeType.Default,
                LayoutId = parentNode.LayoutId,
            };
            await _dbContext.UserLayoutNodes.AddAsync(newNode);
            await _dbContext.SaveChangesAsync();
            var mapped = newNode.Adapt<UserLayoutNodeDto>();
            return Ok(mapped);
        }

        [Authorize]
        [HttpGet($"{Routes.Layouts}/nodes/{{nodeId:guid}}/ancestors")]
        public async Task<IActionResult> GetAncestorNodes([FromRoute] Guid nodeId,
            [FromQuery] UserLayoutNodeType type = UserLayoutNodeType.Default)
        {
            // TODO: Optimize to a single query
            // TODO: Guard against circular references
            Guid userId = User.GetUserId();
            var layout = await _dbContext.GetLatestUserLayoutAsync(userId);
            var currentNode = await _dbContext.UserLayoutNodes
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
            List<UserLayoutNodeDto> ancestors = [];
            while (currentNode.ParentId != null)
            {
                var parentNode = await _dbContext.UserLayoutNodes
                    .AsNoTracking()
                    .Where(x => x.Id == currentNode.ParentId && x.OwnerId == userId)
                    .SingleOrDefaultAsync();
                if (parentNode == null)
                {
                    break;
                }
                ancestors.Add(parentNode.Adapt<UserLayoutNodeDto>());
                currentNode = parentNode;
            }
            ancestors.Reverse();
            return Ok(ancestors);
        }

        [Authorize]
        [HttpGet($"{Routes.Layouts}/nodes/{{nodeId:guid}}/children")]
        public async Task<IActionResult> GetChildNodes([FromRoute] Guid nodeId,
            [FromQuery] UserLayoutNodeType type = UserLayoutNodeType.Default)
        {
            Guid userId = User.GetUserId();
            var layout = await _dbContext.GetLatestUserLayoutAsync(userId);
            var parentNode = await _dbContext.UserLayoutNodes
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

            var nodes = await _dbContext.UserLayoutNodes
                .AsNoTracking()
                .Where(x => x.ParentId == parentNode.Id && x.OwnerId == userId)
                .ProjectToType<UserLayoutNodeDto>()
                .ToListAsync();

            var files = await _dbContext.UserLayoutNodeFiles
                .AsNoTracking()
                .Include(x => x.FileManifest)
                .Where(x => x.NodeId == parentNode.Id)
                .Select(x => x.FileManifest)
                .Where(x => x.OwnerId == userId)
                .ProjectToType<FileManifestDto>()
                .ToListAsync();

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
            [FromQuery] UserLayoutNodeType type = UserLayoutNodeType.Default)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                path = "/";
            }
            Guid userId = User.GetUserId();
            var found = await _dbContext.GetLatestUserLayoutAsync(userId);

            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            // search for the root node of this layout and user, using node type

            Node currentNode = await _dbContext.GetRootNodeAsync(found.Id, userId, type);
            foreach (var part in parts)
            {
                var nextNode = await _dbContext.UserLayoutNodes
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
            var mapped = currentNode.Adapt<UserLayoutNodeDto>();
            return Ok(mapped);
        }
    }
}
