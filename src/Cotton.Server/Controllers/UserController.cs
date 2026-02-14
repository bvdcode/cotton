// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Handlers.Users;
using Cotton.Server.Hubs;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Shared;
using EasyExtensions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Models.Enums;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Users)]
    public class UserController(
        IMediator _mediator,
        CottonDbContext _dbContext,
        IHubContext<EventHub> _hubContext) : ControllerBase
    {
        [Authorize]
        [HttpPatch("me/preferences")]
        public async Task<IActionResult> UpdatePreferences(
            [FromBody] Dictionary<string, string> request,
            [FromQuery]
            CancellationToken cancellationToken)
        {
            Guid userId = User.GetUserId();
            var foundUser = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
                ?? throw new EntityNotFoundException<User>();
            foreach (var kvp in request)
            {
                foundUser.Preferences[kvp.Key] = kvp.Value;
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _hubContext.Clients.User(userId.ToString()).SendAsync("PreferencesUpdated", foundUser.Preferences, cancellationToken);
            return Ok(foundUser.Preferences);
        }

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
            AdminCreateUserCommand command = new(request.Username, request.Email, request.Password, request.Role);
            UserDto user = await _mediator.Send(command, cancellationToken);
            return Ok(user);
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPut("/{userId:guid}")]
        public async Task<IActionResult> UpdateUser(
            [FromRoute] Guid userId,
            [FromBody] AdminUpdateUserRequestDto request,
            CancellationToken cancellationToken)
        {
            AdminUpdateUserCommand command = new(
                User.GetUserId(),
                userId,
                request.Username,
                request.Email,
                request.Role,
                request.FirstName,
                request.LastName,
                request.BirthDate);

            AdminUserDto updated = await _mediator.Send(command, cancellationToken);
            return Ok(updated);
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
