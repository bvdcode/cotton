// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.EntityFrameworkCore.Database;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Auth
{
    public record LogoutSessionRequest(string? RefreshToken) : IRequest;

    public class LogoutSessionRequestHandler(
        CottonDbContext dbContext,
        IDatabaseIntegrityVerifier integrity,
        IMediator mediator) : IRequestHandler<LogoutSessionRequest>
    {
        public async Task Handle(LogoutSessionRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(request.RefreshToken))
            {
                return;
            }

            string hash = AuthSessionIssuer.HashRefreshToken(request.RefreshToken);
            ExtendedRefreshToken? token = await dbContext.RefreshTokens
                .FirstOrDefaultAsync(x => x.Token == hash, cancellationToken);
            if (token is not null && token.RevokedAt is null)
            {
                integrity.RequireValid(dbContext, token, "auth.logout");
                await mediator.Send(new RevokeAuthSessionRequest(token.UserId, token.SessionId!), cancellationToken);
            }
        }
    }
}
