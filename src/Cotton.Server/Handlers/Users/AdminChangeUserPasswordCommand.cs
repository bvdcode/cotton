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
    public class AdminChangeUserPasswordCommand(Guid userId, string password) : IRequest
    {
        public Guid UserId { get; } = userId;
        public string Password { get; } = password;
    }

    public class AdminChangeUserPasswordCommandHandler(CottonDbContext _dbContext, IPasswordHashService _hasher) : IRequestHandler<AdminChangeUserPasswordCommand>
    {
        public async Task Handle(AdminChangeUserPasswordCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                throw new BadRequestException("Password is required");
            }

            var user = await _dbContext.Users.FindAsync([request.UserId], cancellationToken)
                ?? throw new EntityNotFoundException<User>();

            user.PasswordPhc = _hasher.Hash(request.Password);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
