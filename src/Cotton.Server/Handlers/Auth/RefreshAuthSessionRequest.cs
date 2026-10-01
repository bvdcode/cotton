// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Results;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.EntityFrameworkCore.Database;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Auth
{
    public record RefreshAuthSessionRequest(string? RefreshToken) : IRequest<ActionResult<AuthSessionResponseDto>>;

    public class RefreshAuthSessionRequestHandler(
        CottonDbContext _dbContext,
        AuthSessionIssuer _sessionIssuer,
        IDatabaseIntegrityVerifier _integrity)
        : IRequestHandler<RefreshAuthSessionRequest, ActionResult<AuthSessionResponseDto>>
    {
        public async Task<ActionResult<AuthSessionResponseDto>> Handle(
            RefreshAuthSessionRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(request.RefreshToken))
            {
                return new ApiProblemResult(StatusCodes.Status404NotFound,
                    "Refresh token was not provided.", "not_found");
            }

            string refreshTokenHash = AuthSessionIssuer.HashRefreshToken(request.RefreshToken);
            ExtendedRefreshToken? dbToken = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(x => x.Token == refreshTokenHash, cancellationToken);
            if (dbToken is null || dbToken.RevokedAt is not null)
            {
                return new ApiProblemResult(StatusCodes.Status404NotFound, "Refresh token not found or revoked.", "not_found");
            }
            _integrity.RequireValid(_dbContext, dbToken, "auth.refresh-token");
            User? user = await _dbContext.Users.FindAsync([dbToken.UserId], cancellationToken);
            if (user is null)
            {
                return new ApiProblemResult(StatusCodes.Status404NotFound, "Refresh token user not found.", "not_found");
            }
            _integrity.RequireValid(_dbContext, user, "auth.refresh-user");
            string accessToken = _sessionIssuer.CreateAccessToken(user, dbToken.SessionId!);
            dbToken.RevokedAt = DateTime.UtcNow;
            var (newDbToken, newRefreshToken) = await _sessionIssuer.CreateRefreshTokenAsync(
                user,
                dbToken.IsTrusted,
                dbToken.AuthType,
                dbToken.SessionId);
            await _dbContext.RefreshTokens.AddAsync(newDbToken, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _sessionIssuer.AddRefreshTokenToCookies(newRefreshToken, dbToken.IsTrusted);
            return new AuthSessionResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                User = user.Adapt<UserDto>()
            };
        }
    }
}
