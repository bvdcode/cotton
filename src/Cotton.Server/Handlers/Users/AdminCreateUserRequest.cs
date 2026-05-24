// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
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
    /// <summary>
    /// Represents the admin create user request request payload accepted by the API.
    /// </summary>
    public class AdminCreateUserRequest(string username, string? email, string? password, UserRole role) : IRequest<UserDto>
    {
        /// <summary>
        /// Gets the username.
        /// </summary>
        public string Username { get; } = username;
        /// <summary>
        /// Gets the user email address.
        /// </summary>
        public string? Email { get; } = email;
        /// <summary>
        /// Gets the password submitted by the client.
        /// </summary>
        public string? Password { get; } = password;
        /// <summary>
        /// Gets or sets the user first name.
        /// </summary>
        public string? FirstName { get; set; }
        /// <summary>
        /// Gets or sets the user last name.
        /// </summary>
        public string? LastName { get; set; }
        /// <summary>
        /// Gets the role.
        /// </summary>
        public UserRole Role { get; } = role;
        /// <summary>
        /// Gets or sets the birth date.
        /// </summary>
        public DateOnly? BirthDate { get; set; }
    }

    /// <summary>
    /// Handles admin create user requests in the mediator pipeline.
    /// </summary>
    public class AdminCreateUserRequestHandler(
        CottonDbContext _dbContext,
        IPasswordHashService _hasher,
        DefaultUserContentSeeder _defaultUserContentSeeder) : IRequestHandler<AdminCreateUserRequest, UserDto>
    {
        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
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
            await _defaultUserContentSeeder.SeedAsync(user.Id, cancellationToken);
            return user.Adapt<UserDto>();
        }
    }
}
