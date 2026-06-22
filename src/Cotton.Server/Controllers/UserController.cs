// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Handlers.Users;
using Cotton.Server.Hubs;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Services;
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
    /// <summary>
    /// Exposes HTTP endpoints for user operations.
    /// </summary>
    [ApiController]
    [Route(Routes.V1.Users)]
    public class UserController(
        IMediator _mediator,
        CottonDbContext _dbContext,
        IHubContext<EventHub> _hubContext,
        UserStorageQuotaService _quota) : ControllerBase
    {
        /// <summary>
        /// Confirms email verification.
        /// </summary>
        [HttpPost("verify-email")]
        public async Task<IActionResult> ConfirmEmailVerification(
            [FromQuery] string token,
            CancellationToken cancellationToken)
        {
            var command = new ConfirmEmailVerificationRequest(token);
            await _mediator.Send(command, cancellationToken);
            return Ok();
        }

        /// <summary>
        /// Sends email verification.
        /// </summary>
        [Authorize]
        [HttpPost("me/send-email-verification")]
        public async Task<IActionResult> SendEmailVerification(CancellationToken cancellationToken)
        {
            Guid userId = User.GetUserId();
            var request = new SendEmailVerificationRequest(userId);
            await _mediator.Send(request, cancellationToken);
            return Ok();
        }

        /// <summary>
        /// Updates preferences.
        /// </summary>
        [Authorize]
        [HttpPatch("me/preferences")]
        public async Task<IActionResult> UpdatePreferences(
            [FromBody] Dictionary<string, string> request,
            [FromQuery] string? token,
            CancellationToken cancellationToken)
        {
            Guid userId = User.GetUserId();
            User foundUser = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
                ?? throw new EntityNotFoundException<User>();
            foreach (KeyValuePair<string, string> kvp in request)
            {
                foundUser.Preferences[kvp.Key] = kvp.Value;
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _hubContext.Clients.User(userId.ToString()).SendAsync(
                "PreferencesUpdated",
                token ?? string.Empty,
                foundUser.Preferences,
                cancellationToken);
            return Ok(foundUser.Preferences);
        }

        /// <summary>
        /// Gets current user.
        /// </summary>
        [Authorize]
        [HttpGet("me")]
        public IActionResult GetCurrentUser()
        {
            Guid userId = User.GetUserId();
            User? user = _dbContext.Users.Find(userId);
            if (user is null)
            {
                return NotFound();
            }
            UserDto userDto = user.Adapt<UserDto>();
            return Ok(userDto);
        }

        /// <summary>
        /// Gets current user storage quota.
        /// </summary>
        [Authorize]
        [HttpGet("me/storage-quota")]
        public async Task<IActionResult> GetCurrentUserStorageQuota(CancellationToken cancellationToken)
        {
            Guid userId = User.GetUserId();
            UserStorageQuotaDto quota = await _quota.GetSnapshotAsync(userId, cancellationToken);
            return Ok(quota);
        }

        /// <summary>
        /// Updates current user.
        /// </summary>
        [Authorize]
        [HttpPut("me")]
        public async Task<IActionResult> UpdateCurrentUser(
            [FromBody] UpdateCurrentUserRequestDto request,
            CancellationToken cancellationToken)
        {
            Guid userId = User.GetUserId();
            var command = new UpdateCurrentUserRequest(
                userId,
                request.Email,
                request.Username,
                request.FirstName,
                request.LastName,
                request.BirthDate,
                request.AvatarHash);

            try
            {
                UserDto updated = await _mediator.Send(command, cancellationToken);
                return Ok(updated);
            }
            catch (StoragePressureException)
            {
                return StatusCode(507, "Storage is running out of free space. Profile avatar uploads are temporarily paused.");
            }
        }

        /// <summary>
        /// Gets users.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet]
        public async Task<IActionResult> GetUsers([FromQuery] bool calculateStorageUsage, CancellationToken cancellationToken)
        {
            AdminGetUsersQuery query = new() { CalculateStorageUsage = calculateStorageUsage };
            IEnumerable<AdminUserDto> users = await _mediator.Send(query, cancellationToken);
            return Ok(users);
        }

        /// <summary>
        /// Creates user.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserRequest request, CancellationToken cancellationToken)
        {
            AdminCreateUserRequest command = new(request.Username, request.Email, request.Password, request.Role);
            UserDto user = await _mediator.Send(command, cancellationToken);
            return Ok(user);
        }

        /// <summary>
        /// Updates user.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPut("{userId:guid}")]
        public async Task<IActionResult> UpdateUser(
            [FromRoute] Guid userId,
            [FromBody] AdminUpdateUserRequestDto request,
            CancellationToken cancellationToken)
        {
            AdminUpdateUserRequest command = new(
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

        /// <summary>
        /// Changes the current user password after verifying the old password.
        /// </summary>
        [Authorize]
        [HttpPut("me/password")]
        public async Task<IActionResult> ChangePassword(
            [FromBody] ChangePasswordRequestDto request,
            CancellationToken cancellationToken)
        {
            Guid userId = User.GetUserId();
            ChangePasswordRequest command = new(userId, request.OldPassword, request.NewPassword);
            await _mediator.Send(command, cancellationToken);
            return Ok();
        }
    }
}
