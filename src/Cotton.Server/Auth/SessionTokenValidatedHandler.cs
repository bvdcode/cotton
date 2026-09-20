// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Cotton.Server.Auth
{
    internal class SessionTokenValidatedHandler(Func<TokenValidatedContext, Task> previousHandler)
    {
        public async Task HandleAsync(TokenValidatedContext context)
        {
            await previousHandler(context);
            if (context.Result is not null)
            {
                return;
            }

            string? userIdValue = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            string? sessionId = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sid)
                ?? context.Principal?.FindFirstValue(ClaimTypes.Sid);
            if (!Guid.TryParse(userIdValue, out Guid userId) || string.IsNullOrWhiteSpace(sessionId))
            {
                context.Fail("Access token is missing required session claims.");
                return;
            }

            SessionAccessTokenRevocationStore revocations = context.HttpContext.RequestServices
                .GetRequiredService<SessionAccessTokenRevocationStore>();
            try
            {
                bool isRevoked = await revocations.IsRevokedAsync(
                    userId,
                    sessionId,
                    context.HttpContext.RequestAborted);
                if (isRevoked)
                {
                    context.Fail("Session has been revoked.");
                }
            }
            catch (OperationCanceledException ex) when (context.HttpContext.RequestAborted.IsCancellationRequested)
            {
                ILogger<SessionTokenValidatedHandler> logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<SessionTokenValidatedHandler>>();
                logger.LogDebug(ex, "Session validation canceled because the HTTP request was aborted.");
                context.NoResult();
            }
        }
    }
}
