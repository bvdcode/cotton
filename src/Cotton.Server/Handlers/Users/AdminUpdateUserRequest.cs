// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Validators;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using EasyExtensions.Models.Enums;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Users
{
    /// <summary>
    /// Represents the admin update user request request payload accepted by the API.
    /// </summary>
    public class AdminUpdateUserRequest(
        Guid initiatorUserId,
        Guid userId,
        string username,
        string? email,
        UserRole role,
        string? firstName,
        string? lastName,
        DateOnly? birthDate) : IRequest<AdminUserDto>
    {
        /// <summary>
        /// Gets the owning user identifier.
        /// </summary>
        public Guid UserId { get; } = userId;
        /// <summary>
        /// Gets the administrator user identifier.
        /// </summary>
        public Guid AdminUserId { get; } = initiatorUserId;
        /// <summary>
        /// Gets the username.
        /// </summary>
        public string Username { get; } = username;
        /// <summary>
        /// Gets the user email address.
        /// </summary>
        public string? Email { get; } = email;
        /// <summary>
        /// Gets the role.
        /// </summary>
        public UserRole Role { get; } = role;
        /// <summary>
        /// Gets the user first name.
        /// </summary>
        public string? FirstName { get; } = firstName;
        /// <summary>
        /// Gets the user last name.
        /// </summary>
        public string? LastName { get; } = lastName;
        /// <summary>
        /// Gets the birth date.
        /// </summary>
        public DateOnly? BirthDate { get; } = birthDate;
    }

    /// <summary>
    /// Handles admin update user requests in the mediator pipeline.
    /// </summary>
    public class AdminUpdateUserRequestHandler(CottonDbContext _dbContext) : IRequestHandler<AdminUpdateUserRequest, AdminUserDto>
    {
        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task<AdminUserDto> Handle(AdminUpdateUserRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Username))
            {
                throw new BadRequestException<User>("Username is required");
            }

            if (!UsernameValidator.TryNormalizeAndValidate(request.Username, out var newUsername, out var usernameError))
            {
                throw new BadRequestException<User>(usernameError);
            }

            var foundAdmin = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == request.AdminUserId, cancellationToken)
                ?? throw new EntityNotFoundException<User>();

            var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
                ?? throw new EntityNotFoundException<User>();

            if (user.Role != request.Role && foundAdmin.Role < user.Role)
            {
                throw new AccessDeniedException<User>("User role cannot be changed to a different role by an admin with lower role");
            }

            bool usernameTaken = await _dbContext.Users.AnyAsync(
                x => x.Id != request.UserId && x.Username == newUsername,
                cancellationToken);
            if (usernameTaken)
            {
                throw new BadRequestException<User>("Username is already taken");
            }

            string? newEmail = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
            bool emailChanged = !string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase);

            user.Username = newUsername;
            user.Role = request.Role;
            user.FirstName = string.IsNullOrWhiteSpace(request.FirstName) ? null : request.FirstName.Trim();
            user.LastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim();
            user.BirthDate = request.BirthDate;

            if (emailChanged)
            {
                user.Email = newEmail;
                user.IsEmailVerified = false;
                user.EmailVerificationToken = null;
                user.EmailVerificationTokenSentAt = null;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Reuse existing AdminUserDto shape
            return user.Adapt<AdminUserDto>();
        }
    }
}
