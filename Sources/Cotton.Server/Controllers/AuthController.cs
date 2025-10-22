using Cotton.Server.Database;
using Microsoft.AspNetCore.Mvc;
using EasyExtensions.AspNetCore.Authorization.Abstractions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class AuthController(CottonDbContext _dbContext, ITokenProvider _tokens) : ControllerBase
    {
        [HttpPost("/api/v1/auth/login")]
        public async Task<IActionResult> Login()
        {
            var user = await _dbContext.Users.SingleOrDefaultAsync();
            if (user == null)
            {
                user = new()
                {
                    Username = "admin"
                };
                await _dbContext.Users.AddAsync(user);
                await _dbContext.SaveChangesAsync();
            }
            var token = _tokens.CreateToken(x => x.Add("sub", user.Id.ToString()));
            return Ok(new { token });
        }
    }
}
