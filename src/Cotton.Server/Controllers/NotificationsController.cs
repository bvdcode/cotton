// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Handlers.Notifications;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using Gridify;
using Gridify.EntityFramework;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;

namespace Cotton.Server.Controllers
{
    /// <summary>
    /// Exposes HTTP endpoints for notifications operations.
    /// </summary>
    [ApiController]
    public class NotificationsController(
        CottonDbContext _dbContext,
        INotificationsProvider _notifications,
        IMediator _mediator) : ControllerBase
    {
        /// <summary>
        /// Sends a test notification to the current user.
        /// </summary>
        [Authorize]
        [HttpPost(Routes.V1.Notifications + "/test")]
        public async Task<IActionResult> TestNotification()
        {
            Guid userId = User.GetUserId();
            await _notifications.SendNotificationAsync(userId, "Test Notification", "This is a test notification.");
            return Ok();
        }

        /// <summary>
        /// Gets notifications.
        /// </summary>
        [Authorize]
        [HttpGet(Routes.V1.Notifications)]
        public async Task<IActionResult> GetNotifications([FromQuery] GridifyQuery query)
        {
            Guid userId = User.GetUserId();
            Paging<Notification> notifications = await _dbContext.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .GridifyAsync(query);
            Response.Headers.Append("X-Total-Count", notifications.Count.ToString());
            IEnumerable<NotificationDto> dto = notifications.Data.Adapt<IEnumerable<NotificationDto>>();
            return Ok(dto);
        }

        /// <summary>
        /// Registers or refreshes the current session push device token.
        /// </summary>
        [Authorize]
        [HttpPut(Routes.V1.Notifications + "/device-tokens/current")]
        public async Task<IActionResult> RegisterCurrentDeviceToken(
            [FromBody] PushDeviceTokenRegistrationRequestDto request,
            CancellationToken cancellationToken)
        {
            string? sessionId = GetCurrentSessionId();
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return this.ApiUnauthorized("Session id is required");
            }

            PushDeviceTokenDto token = await _mediator.Send(
                new RegisterPushDeviceTokenRequest(User.GetUserId(), sessionId, request),
                cancellationToken);
            return Ok(token);
        }

        /// <summary>
        /// Revokes push device tokens registered by the current session.
        /// </summary>
        [Authorize]
        [HttpDelete(Routes.V1.Notifications + "/device-tokens/current-session")]
        public async Task<IActionResult> RevokeCurrentSessionDeviceTokens(CancellationToken cancellationToken)
        {
            string? sessionId = GetCurrentSessionId();
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return this.ApiUnauthorized("Session id is required");
            }

            PushDeviceTokenRevocationResultDto result = await _mediator.Send(
                new RevokeCurrentSessionPushDeviceTokensRequest(User.GetUserId(), sessionId),
                cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Marks every notification for the current user as read.
        /// </summary>
        [Authorize]
        [HttpPatch(Routes.V1.Notifications + "/mark-all-read")]
        public async Task<IActionResult> MarkAllNotificationsAsRead()
        {
            Guid userId = User.GetUserId();
            List<Notification> unreadNotifications = await _dbContext.Notifications
                .Where(n => n.UserId == userId && !n.ReadAt.HasValue)
                .ToListAsync();
            if (unreadNotifications.Count == 0)
            {
                return Ok();
            }
            foreach (Notification? notification in unreadNotifications)
            {
                notification.ReadAt = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync();
            return Ok();
        }

        /// <summary>
        /// Gets unread notifications count.
        /// </summary>
        [Authorize]
        [HttpGet(Routes.V1.Notifications + "/unread/count")]
        public async Task<IActionResult> GetUnreadNotificationsCount()
        {
            Guid userId = User.GetUserId();
            int unreadCount = await _dbContext.Notifications
                .AsNoTracking()
                .CountAsync(n => n.UserId == userId && !n.ReadAt.HasValue);
            return Ok(new { UnreadCount = unreadCount });
        }

        /// <summary>
        /// Marks one notification for the current user as read.
        /// </summary>
        [Authorize]
        [HttpPatch(Routes.V1.Notifications + "/{id:guid}/read")]
        public async Task<IActionResult> MarkNotificationAsRead(Guid id)
        {
            Guid userId = User.GetUserId();
            Notification? notification = await _dbContext.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
            if (notification is null || notification.ReadAt.HasValue)
            {
                return Ok();
            }
            notification.ReadAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return Ok();
        }

        private string? GetCurrentSessionId()
        {
            return User.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Sid)?.Value;
        }
    }
}
