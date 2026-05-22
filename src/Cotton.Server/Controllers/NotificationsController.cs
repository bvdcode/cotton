// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Abstractions;
using Cotton.Server.Models.Dto;
using EasyExtensions;
using Gridify;
using Gridify.EntityFramework;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Controllers
{
    /// <summary>
    /// Exposes HTTP endpoints for notifications operations.
    /// </summary>
    [ApiController]
    public class NotificationsController(
        CottonDbContext _dbContext,
        INotificationsProvider _notifications) : ControllerBase
    {
        /// <summary>
        /// Sends a test notification to the current user.
        /// </summary>
        [Authorize]
        [HttpPost(Routes.V1.Notifications + "/test")]
        public async Task<IActionResult> TestNotification()
        {
            Guid userId = User.GetUserId();
            await _notifications.SendNotificationAsync(userId, "Test Notification", "Such a beautiful notification!");
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
            var notifications = await _dbContext.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .GridifyAsync(query);
            Response.Headers.Append("X-Total-Count", notifications.Count.ToString());
            var dto = notifications.Data.Adapt<IEnumerable<NotificationDto>>();
            return Ok(dto);
        }

        /// <summary>
        /// Marks every notification for the current user as read.
        /// </summary>
        [Authorize]
        [HttpPatch(Routes.V1.Notifications + "/mark-all-read")]
        public async Task<IActionResult> MarkAllNotificationsAsRead()
        {
            Guid userId = User.GetUserId();
            var unreadNotifications = await _dbContext.Notifications
                .Where(n => n.UserId == userId && !n.ReadAt.HasValue)
                .ToListAsync();
            if (unreadNotifications.Count == 0)
            {
                return Ok();
            }
            foreach (var notification in unreadNotifications)
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
            var notification = await _dbContext.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
            if (notification == null || notification.ReadAt.HasValue)
            {
                return Ok();
            }
            notification.ReadAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return Ok();
        }
    }
}
