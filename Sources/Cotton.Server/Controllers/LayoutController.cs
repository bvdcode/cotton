using EasyExtensions;
using Cotton.Server.Models;
using Cotton.Server.Database;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class LayoutController(CottonDbContext _dbContext) : ControllerBase
    {
        [Obsolete]
        [HttpGet(Routes.Layouts)]
        public Task<CottonResult> GetUserLayout()
        {
            Guid userId = User.GetUserId();
            var found = _dbContext.UserLayouts.Find(userId);
            return Task.FromResult(CottonResult.Ok("Layout retrieved successfully.", found));
        }
    }
}
