// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Services;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Users
{
    /// <summary>
    /// Represents the change password request request payload accepted by the API.
    /// </summary>
    public class ChangePasswordRequest(Guid userId, string oldPassword, string newPassword) : IRequest
    {
        /// <summary>
        /// Gets the owning user identifier.
        /// </summary>
        public Guid UserId { get; } = userId;
        /// <summary>
        /// Gets the old password.
        /// </summary>
        public string OldPassword { get; } = oldPassword;
        /// <summary>
        /// Gets the new password.
        /// </summary>
        public string NewPassword { get; } = newPassword;
    }

    /// <summary>
    /// Handles change password requests in the mediator pipeline.
    /// </summary>
    public class ChangePasswordRequestHandler(
        CottonDbContext _dbContext,
        IPasswordHashService _hasher,
        RefreshTokenRevocationService _refreshTokenRevocations) : IRequestHandler<ChangePasswordRequest>
    {
        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task Handle(ChangePasswordRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.OldPassword))
            {
                throw new BadRequestException<User>("Old password is required");
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                throw new BadRequestException<User>("New password is required");
            }

            var user = await _dbContext.Users.FindAsync([request.UserId], cancellationToken)
                ?? throw new EntityNotFoundException<User>();

            if (!_hasher.Verify(request.OldPassword, user.PasswordPhc))
            {
                throw new BadRequestException<User>("Old password is incorrect");
            }

            await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            user.PasswordPhc = _hasher.Hash(request.NewPassword);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _refreshTokenRevocations.RevokeUserSessionsAsync(
                user.Id,
                DateTime.UtcNow,
                cancellationToken);

            await tx.CommitAsync(cancellationToken);
        }
    }
}
