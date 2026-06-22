// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.Users
{
    /// <summary>
    /// Represents the confirm password reset request payload accepted by the API.
    /// </summary>
    public class ConfirmPasswordResetRequest(string token, string newPassword) : IRequest
    {
        /// <summary>
        /// Gets the opaque token.
        /// </summary>
        public string Token { get; } = token;

        /// <summary>
        /// Gets the new password.
        /// </summary>
        public string NewPassword { get; } = newPassword;
    }

    /// <summary>
    /// Handles confirm password reset requests in the mediator pipeline.
    /// </summary>
    public class ConfirmPasswordResetRequestHandler(
        CottonDbContext _dbContext,
        IPasswordHashService _hasher,
        RefreshTokenRevocationService _refreshTokenRevocations,
        SessionRevocationNotifier _sessionRevocationNotifier,
        IDatabaseIntegrityVerifier _integrity) : IRequestHandler<ConfirmPasswordResetRequest>
    {
        private static readonly TimeSpan TokenExpiration = TimeSpan.FromHours(1);

        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task Handle(ConfirmPasswordResetRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                throw new BadRequestException<User>("Token is required");
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                throw new BadRequestException<User>("New password is required");
            }

            User user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.PasswordResetToken == request.Token, cancellationToken)
                ?? throw new BadRequestException<User>("Invalid or expired token");
            _integrity.RequireValid(_dbContext, user, "user.password-reset");

            if (user.PasswordResetTokenSentAt is null ||
                DateTime.UtcNow - user.PasswordResetTokenSentAt.Value > TokenExpiration)
            {
                user.PasswordResetToken = null;
                user.PasswordResetTokenSentAt = null;
                await _dbContext.SaveChangesAsync(cancellationToken);
                throw new BadRequestException<User>("Token has expired");
            }

            await using IDbContextTransaction tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            user.PasswordPhc = _hasher.Hash(request.NewPassword);
            user.PasswordResetToken = null;
            user.PasswordResetTokenSentAt = null;

            user.IsTotpEnabled = false;
            user.TotpSecretEncrypted = null;
            user.TotpEnabledAt = null;
            user.TotpFailedAttempts = 0;

            await _dbContext.SaveChangesAsync(cancellationToken);

            RefreshTokenRevocationResult revocation = await _refreshTokenRevocations.RevokeUserSessionsAsync(
                user.Id,
                DateTime.UtcNow,
                cancellationToken);

            await tx.CommitAsync(cancellationToken);

            await _sessionRevocationNotifier.NotifyRevokedAsync(
                user.Id,
                revocation.SessionIds,
                CancellationToken.None);
        }
    }
}
