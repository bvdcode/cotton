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
    public class ChangePasswordRequest(Guid userId, string oldPassword, string newPassword) : IRequest
    {
        public Guid UserId { get; } = userId;
        public string OldPassword { get; } = oldPassword;
        public string NewPassword { get; } = newPassword;
    }

    public class ChangePasswordRequestHandler(
        CottonDbContext _dbContext,
        IPasswordHashService _hasher,
        RefreshTokenRevocationService _refreshTokenRevocations) : IRequestHandler<ChangePasswordRequest>
    {
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
