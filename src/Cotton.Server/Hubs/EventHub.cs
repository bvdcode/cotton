// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Cotton.Server.Hubs
{
    /// <summary>
    /// Publishes realtime events to connected clients.
    /// </summary>
    [Authorize]
    public class EventHub : Hub
    {
        /// <summary>
        /// Defines the notification method.
        /// </summary>
        public const string NotificationMethod = "OnNotificationReceived";
        /// <summary>
        /// Defines the session revoked method.
        /// </summary>
        public const string SessionRevokedMethod = "SessionRevoked";

        /// <summary>
        /// Gets session group name.
        /// </summary>
        public static string GetSessionGroupName(Guid userId, string sessionId)
        {
            return $"auth-session:{userId:N}:{sessionId}";
        }

        /// <inheritdoc />
        public override async Task OnConnectedAsync()
        {
            if (Context.User == null)
            {
                Context.Abort();
                return;
            }
            Guid userId = Context.User.GetUserId();
            string? sessionId = Context.User.FindFirstValue(JwtRegisteredClaimNames.Sid)
                ?? Context.User.FindFirstValue(ClaimTypes.Sid);
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
