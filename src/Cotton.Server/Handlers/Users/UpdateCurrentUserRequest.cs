// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
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
        string? firstName,
        string? lastName,
        DateOnly? birthDate) : IRequest<UserDto>
    {
        public Guid UserId { get; } = userId;
        public string? Email { get; } = email;
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

            await _dbContext.SaveChangesAsync(cancellationToken);
            return user.Adapt<UserDto>();
        }
    }
}
