// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov | bvdcode | belov.us

using Cotton.Database;
using Cotton.Server.Models.Dto;
using EasyExtensions;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
