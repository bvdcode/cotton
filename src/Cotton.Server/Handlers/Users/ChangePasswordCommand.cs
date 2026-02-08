// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Users
{
    public class ChangePasswordCommand(Guid userId, string oldPassword, string newPassword) : IRequest
    {
        public Guid UserId { get; } = userId;
        public string OldPassword { get; } = oldPassword;
        public string NewPassword { get; } = newPassword;
    }

    public class ChangePasswordCommandHandler(CottonDbContext _dbContext, IPasswordHashService _hasher) : IRequestHandler<ChangePasswordCommand>
    {
        public async Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
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

            user.PasswordPhc = _hasher.Hash(request.NewPassword);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
