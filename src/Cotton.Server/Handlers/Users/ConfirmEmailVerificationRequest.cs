// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Users
{
    /// <summary>
    /// Represents the confirm email verification request payload accepted by the API.
    /// </summary>
    public class ConfirmEmailVerificationRequest(string token) : IRequest
    {
        /// <summary>
        /// Gets the opaque token.
        /// </summary>
        public string Token { get; } = token;
    }

    /// <summary>
    /// Handles confirm email verification requests in the mediator pipeline.
    /// </summary>
    public class ConfirmEmailVerificationRequestHandler(
        CottonDbContext _dbContext,
        IDatabaseIntegrityVerifier _integrity) : IRequestHandler<ConfirmEmailVerificationRequest>
    {
        private static readonly TimeSpan TokenExpiration = TimeSpan.FromHours(24);

        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task Handle(ConfirmEmailVerificationRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                throw new BadRequestException<User>("Token is required");
            }

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.EmailVerificationToken == request.Token, cancellationToken)
                ?? throw new BadRequestException<User>("Invalid or expired token");
            _integrity.RequireValid(_dbContext, user, "user.email-verification");

            if (user.EmailVerificationTokenSentAt is null ||
                DateTime.UtcNow - user.EmailVerificationTokenSentAt.Value > TokenExpiration)
            {
                user.EmailVerificationToken = null;
                user.EmailVerificationTokenSentAt = null;
                await _dbContext.SaveChangesAsync(cancellationToken);
                throw new BadRequestException<User>("Token has expired");
            }

            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenSentAt = null;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
