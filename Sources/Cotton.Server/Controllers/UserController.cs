using Mapster;
using EasyExtensions;
using Cotton.Database;
using Cotton.Server.Models.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class UserController(CottonDbContext _dbContext) : ControllerBase
    {
        [Authorize]
        [HttpGet("/api/v1/users/me")]
        public IActionResult GetCurrentUser()
        {
            var userId = User.GetUserId();
            var user = _dbContext.Users.Find(userId);
            if (user == null)
            {
                return NotFound();
            }
            UserDto userDto = user.Adapt<UserDto>();
            return Ok(userDto);
        }
    }
}