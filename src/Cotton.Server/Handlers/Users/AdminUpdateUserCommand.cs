// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using EasyExtensions.Models.Enums;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Users
{
    public class AdminUpdateUserCommand(
        Guid initiatorUserId,
        Guid userId,
        string username,
        string? email,
        UserRole role,
        string? firstName,
        string? lastName,
        DateOnly? birthDate) : IRequest<AdminUserDto>
    {
        public Guid UserId { get; } = userId;
        public Guid AdminUserId { get; } = initiatorUserId;
        public string Username { get; } = username;
        public string? Email { get; } = email;
        public UserRole Role { get; } = role;
        public string? FirstName { get; } = firstName;
        public string? LastName { get; } = lastName;
        public DateOnly? BirthDate { get; } = birthDate;
    }

    public class AdminUpdateUserCommandHandler(CottonDbContext _dbContext) : IRequestHandler<AdminUpdateUserCommand, AdminUserDto>
    {
        public async Task<AdminUserDto> Handle(AdminUpdateUserCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Username))
            {
                throw new BadRequestException<User>("Username is required");
            }

            var foundAdmin = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == request.AdminUserId, cancellationToken)
                ?? throw new EntityNotFoundException<User>();

            var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
                ?? throw new EntityNotFoundException<User>();

            if (user.Role != request.Role && foundAdmin.Role < user.Role)
            {
                throw new AccessDeniedException<User>("User role cannot be changed to a different role by an admin with lower role");
            }

            var newUsername = request.Username.Trim();
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
