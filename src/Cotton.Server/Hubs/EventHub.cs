// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Models.Dto;
using EasyExtensions;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Cotton.Server.Hubs
{
    [Authorize]
    public class EventHub(CottonDbContext _dbContext) : Hub
    {
        public const string NotificationMethod = "OnNotificationReceived";
        public const string SessionRevokedMethod = "SessionRevoked";

        public static string GetSessionGroupName(Guid userId, string sessionId)
        {
            return $"auth-session:{userId:N}:{sessionId}";
        }

        public override async Task OnConnectedAsync()
        {
            Guid userId = Context.User.GetUserId();
            string? sessionId = Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sid)
                ?? Context.User?.FindFirstValue(ClaimTypes.Sid);
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                Context.Abort();
                return;
            }

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                GetSessionGroupName(userId, sessionId),
                Context.ConnectionAborted);

            var unread = await _dbContext.Notifications
                .Where(x => x.UserId == userId && !x.ReadAt.HasValue)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(Context.ConnectionAborted);
            if (unread.Count > 0)
            {
                var latest = unread.First();
                var dto = latest.Adapt<NotificationDto>();
                await Clients.Caller.SendAsync(NotificationMethod, dto, Context.ConnectionAborted);
            }
            await base.OnConnectedAsync();
        }
    }
}
