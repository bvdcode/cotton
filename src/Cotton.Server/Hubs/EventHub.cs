// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Cotton.Server.Hubs
{
    [Authorize]
    public class EventHub : Hub
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

            await base.OnConnectedAsync();
        }
    }
}
