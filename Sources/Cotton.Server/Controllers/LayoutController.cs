using Mapster;
using EasyExtensions;
using Cotton.Server.Models;
using Cotton.Server.Database;
using Cotton.Server.Models.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cotton.Server.Database.Models.Enums;
using Cotton.Server.Extensions;
using Cotton.Server.Database.Models;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class LayoutController(CottonDbContext _dbContext) : ControllerBase
    {
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
