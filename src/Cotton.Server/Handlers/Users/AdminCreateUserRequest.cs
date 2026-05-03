// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Validators;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Helpers;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using EasyExtensions.Models.Enums;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Users
{
    public class AdminCreateUserRequest(string username, string? email, string password, UserRole role) : IRequest<UserDto>
    {
        public string Username { get; } = username;
        public string? Email { get; } = email;
        public string? Password { get; } = password;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public UserRole Role { get; } = role;
        public DateOnly? BirthDate { get; set; }
    }

    public class AdminCreateUserRequestHandler(CottonDbContext _dbContext, IPasswordHashService _hasher) : IRequestHandler<AdminCreateUserRequest, UserDto>
    {
        public async Task<UserDto> Handle(AdminCreateUserRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Username))
            {
                throw new BadRequestException<User>("Username is required");
            }

            if (!UsernameValidator.TryNormalizeAndValidate(request.Username, out var username, out var usernameError))
            {
                throw new BadRequestException<User>(usernameError);
            }

            bool exists = await _dbContext.Users.AnyAsync(x => x.Username == username, cancellationToken);
            if (exists)
            {
                throw new BadRequestException<User>("User already exists");
            }

            string phc = _hasher.Hash(string.IsNullOrWhiteSpace(request.Password)
                ? StringHelpers.CreateRandomString(32)
                : request.Password);

            var user = new User
            {
                Username = username,
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                Role = request.Role,
                PasswordPhc = phc,
                WebDavTokenPhc = _hasher.Hash(StringHelpers.CreateRandomString(32)),
                FirstName = request.FirstName,
                LastName = request.LastName,
                BirthDate = request.BirthDate,
            };

            await _dbContext.Users.AddAsync(user, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return user.Adapt<UserDto>();
        }
    }
}
