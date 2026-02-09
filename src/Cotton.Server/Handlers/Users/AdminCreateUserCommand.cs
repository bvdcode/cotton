// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using EasyExtensions.Models.Enums;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Users
{
    public class AdminCreateUserCommand(string username, string? email, string password, UserRole role) : IRequest<UserDto>
    {
        public string Username { get; } = username;
        public string? Email { get; } = email;
        public string Password { get; } = password;
        public UserRole Role { get; } = role;
    }

    public class AdminCreateUserCommandHandler(CottonDbContext _dbContext, IPasswordHashService _hasher) : IRequestHandler<AdminCreateUserCommand, UserDto>
    {
        public async Task<UserDto> Handle(AdminCreateUserCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Username))
            {
                throw new BadRequestException<User>("Username is required");
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                throw new BadRequestException<User>("Password is required");
            }

            var username = request.Username.Trim();
            bool exists = await _dbContext.Users.AnyAsync(x => x.Username == username, cancellationToken);
            if (exists)
            {
                throw new BadRequestException<User>("User already exists");
            }

            var user = new User
            {
                Username = username,
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                Role = request.Role,
                PasswordPhc = _hasher.Hash(request.Password),
                WebDavTokenPhc = _hasher.Hash(request.Password),
            };

            await _dbContext.Users.AddAsync(user, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return user.Adapt<UserDto>();
        }
    }
}
