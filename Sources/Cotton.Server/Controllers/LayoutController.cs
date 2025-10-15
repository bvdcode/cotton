using Mapster;
using EasyExtensions;
using Cotton.Server.Models;
using Cotton.Server.Database;
using Cotton.Server.Models.Dto;
using Microsoft.AspNetCore.Mvc;
using Cotton.Server.Extensions;
using Microsoft.EntityFrameworkCore;
using Cotton.Server.Database.Models;
using Cotton.Server.Database.Models.Enums;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class LayoutController(CottonDbContext _dbContext) : ControllerBase
    {
        [HttpGet($"{Routes.Layouts}/nodes/{{nodeId:guid}}/children")]
        public async Task<CottonResult> GetChildNodes([FromRoute] Guid nodeId,
            [FromQuery] UserLayoutNodeType type = UserLayoutNodeType.Default)
        {
            Guid userId = User.GetUserId();
            var layout = await _dbContext.GetLatestUserLayoutAsync(userId);
            var parentNode = await _dbContext.UserLayoutNodes
                .AsNoTracking()
                .Include(x => x.UserLayout)
                .Where(x => x.Id == nodeId
                    && x.UserLayout.OwnerId == userId
                    && x.UserLayoutId == layout.Id
                    && x.Type == type)
                .SingleOrDefaultAsync();
            if (parentNode == null)
            {
                return CottonResult.NotFound("Parent node not found.");
            }

            var nodes = await _dbContext.UserLayoutNodes
                .AsNoTracking()
                .Include(x => x.UserLayout)
                .Where(x => x.ParentId == parentNode.Id && x.UserLayout.OwnerId == userId)
                .ProjectToType<UserLayoutNodeDto>()
                .ToListAsync();

            var files = await _dbContext.UserLayoutNodeFiles
                .AsNoTracking()
                .Include(x => x.FileManifest)
                .Where(x => x.UserLayoutNodeId == parentNode.Id)
                .ProjectToType<FileManifestDto>()
                .ToListAsync();

            NodeContentDto result = new()
            {
                Nodes = nodes,
                Files = files
            };
            return CottonResult.Ok("Child nodes retrieved successfully.", result);
        }

        [HttpGet($"{Routes.Layouts}/resolver")]
        [HttpGet($"{Routes.Layouts}/resolver/{{*path}}")]
        public async Task<CottonResult> ResolveLayout([FromRoute] string? path,
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

            UserLayoutNode currentNode = await _dbContext.GetRootNodeAsync(found.Id, userId, type);
            foreach (var part in parts)
            {
                var nextNode = await _dbContext.UserLayoutNodes
                    .AsNoTracking()
                    .Where(x => x.UserLayout.OwnerId == userId && x.ParentId == currentNode.Id && x.Name == part && x.Type == type)
                    .SingleOrDefaultAsync();
                if (nextNode == null)
                {
                    return CottonResult.NotFound($"Layout node '{part}' was not found.");
                }
                currentNode = nextNode;
            }
            var mapped = currentNode.Adapt<UserLayoutNodeDto>();
            return CottonResult.Ok("Layout node resolved successfully.", mapped);
        }
    }
}
