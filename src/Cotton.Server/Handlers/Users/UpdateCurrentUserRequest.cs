// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Validators;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Users
{
    public class UpdateCurrentUserRequest(
        Guid userId,
        string? email,
        string? username,
        string? firstName,
        string? lastName,
        DateOnly? birthDate) : IRequest<UserDto>
    {
        public Guid UserId { get; } = userId;
        public string? Email { get; } = email;
        public string? Username { get; } = username;
        public string? FirstName { get; } = firstName;
        public string? LastName { get; } = lastName;
        public DateOnly? BirthDate { get; } = birthDate;
    }

    public class UpdateCurrentUserRequestHandler(CottonDbContext _dbContext) : IRequestHandler<UpdateCurrentUserRequest, UserDto>
    {
        public async Task<UserDto> Handle(UpdateCurrentUserRequest request, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
                    ?? throw new EntityNotFoundException<User>();

            string? newEmail = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
            bool emailChanged = !string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase);

            string? newUsername = null;
            bool usernameChanged = false;

            if (!string.IsNullOrWhiteSpace(request.Username))
            {
                bool isValid = UsernameValidator.TryNormalizeAndValidate(request.Username, out string normalizedUsername, out string error);
                if (!isValid)
                {
                    throw new BadRequestException<User>(error);
                }

                usernameChanged = !string.Equals(user.Username, normalizedUsername, StringComparison.Ordinal);
                newUsername = normalizedUsername;

                if (usernameChanged)
                {
                    bool usernameTaken = await _dbContext.Users
                        .AnyAsync(x => x.Id != user.Id && x.Username == normalizedUsername, cancellationToken);
                    if (usernameTaken)
                    {
                        throw new BadRequestException<User>("Username is already taken");
                    }
                }
            }

            if (emailChanged && newEmail != null)
            {
                bool emailTaken = await _dbContext.Users
                    .AnyAsync(x => x.Id != user.Id && x.Email == newEmail, cancellationToken);
                if (emailTaken)
                {
                    throw new BadRequestException<User>("Email is already taken");
                }
            }

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

            if (usernameChanged)
            {
                user.Username = newUsername!;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return user.Adapt<UserDto>();
        }
    }
}
