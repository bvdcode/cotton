// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Handlers.Users;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Shared;
using EasyExtensions;
using EasyExtensions.Mediator;
using EasyExtensions.Models.Enums;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Users)]
    public class UserController(CottonDbContext _dbContext, IMediator _mediator) : ControllerBase
    {
        [Authorize]
        [HttpGet("me")]
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

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet]
        public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
        {
            AdminGetUsersQuery query = new();
            IEnumerable<AdminUserDto> users = await _mediator.Send(query, cancellationToken);
            return Ok(users);
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserRequestDto request, CancellationToken cancellationToken)
        {
            AdminCreateUserCommand command = new(request.Username, request.Password, request.Role);
            UserDto user = await _mediator.Send(command, cancellationToken);
            return Ok(user);
        }

        [Authorize]
        [HttpPut("me/password")]
        public async Task<IActionResult> ChangePassword(
            [FromBody] ChangePasswordRequestDto request,
            CancellationToken cancellationToken)
        {
            Guid userId = User.GetUserId();
            ChangePasswordCommand command = new(userId, request.OldPassword, request.NewPassword);
            await _mediator.Send(command, cancellationToken);
            return Ok();
        }
    }
}
