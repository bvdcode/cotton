// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Users
{
    public class ChangePasswordRequest(Guid userId, string oldPassword, string newPassword) : IRequest
    {
        public Guid UserId { get; } = userId;
        public string OldPassword { get; } = oldPassword;
        public string NewPassword { get; } = newPassword;
    }

    public class ChangePasswordRequestHandler(CottonDbContext _dbContext, IPasswordHashService _hasher) : IRequestHandler<ChangePasswordRequest>
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

            await _dbContext.RefreshTokens
                .Where(x => x.UserId == user.Id && x.RevokedAt == null)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(t => t.RevokedAt, _ => DateTime.UtcNow),
                    cancellationToken);

            await tx.CommitAsync(cancellationToken);
        }
    }
}
