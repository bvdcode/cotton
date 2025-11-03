// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Cotton.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.AspNetCore.Authorization.Abstractions;

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
