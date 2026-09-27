// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Auth;
using Cotton.Server.Helpers;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Validators;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Helpers;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Cotton.Server.Handlers.Auth
{
    public record ResolveLoginUserRequest(LoginRequestDto Credentials) : IRequest<User?>;

    public class ResolveLoginUserRequestHandler(
        CottonDbContext _dbContext,
        IPasswordHashService _hasher,
        IDatabaseIntegrityVerifier _integrity,
        DefaultUserContentSeeder _defaultUserContentSeeder,
        ApplicationStartupClock _startupClock,
        ILogger<ResolveLoginUserRequestHandler> _logger) : IRequestHandler<ResolveLoginUserRequest, User?>
    {
        private static readonly EmailAddressAttribute EmailValidator = new();

        public async Task<User?> Handle(ResolveLoginUserRequest command, CancellationToken cancellationToken)
        {
            LoginRequestDto request = command.Credentials;
            if (string.IsNullOrWhiteSpace(request.Username))
            {
                return null;
            }

            request.Username = request.Username.Trim();
            User? user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Username == request.Username || x.Email == request.Username, cancellationToken);
            if (user is not null)
            {
                _integrity.RequireValid(_dbContext, user, "auth.login");
                return user;
            }

            return await TryGetNewUserAsync(request, cancellationToken);
        }

        private async Task<User?> TryGetNewUserAsync(LoginRequestDto request, CancellationToken cancellationToken)
        {
            string login = request.Username.Trim();
            string? email = null;
            string username;

            if (EmailValidator.IsValid(login))
            {
                email = login;
                username = await UsernameHelpers.BuildAvailableUsernameFromEmailAsync(_dbContext, login);
            }
            else if (!UsernameValidator.TryNormalizeAndValidate(login, out username, out _))
            {
                return null;
            }

            bool isPublicInstance = Constants.IsPublicInstance;
            if (isPublicInstance)
            {
                User guest = new()
                {
                    Email = email,
                    Username = username,
                    Role = UserRole.User,
                    FirstName = NormalizeOptionalName(request.FirstName),
                    LastName = NormalizeOptionalName(request.LastName),
                    PasswordPhc = _hasher.Hash(request.Password),
                    WebDavTokenPhc = _hasher.Hash(StringHelpers.CreateRandomString(AuthConstants.WebDavTokenLength)),
                };
                await _dbContext.Users.AddAsync(guest, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await _defaultUserContentSeeder.SeedAsync(guest.Id);
                _logger.LogInformation("Created guest user {Username} on public instance", guest.Username);
                return guest;
            }

            bool hasUsers = await _dbContext.Users.AnyAsync(cancellationToken);
            if (hasUsers)
            {
                return null;
            }

            if (_startupClock.Uptime.TotalMinutes > Constants.AdminAutocreateMinutesDelay)
            {
                string errorMessage = $"Initial admin user creation is disabled after " +
                    Constants.AdminAutocreateMinutesDelay + " minutes of uptime. " +
                    "Please restart the application/container to enable it.";
                _logger.LogWarning("{msg}", errorMessage);
                throw new BadRequestException<User>(errorMessage);
            }
            User user = new()
            {
                Email = email,
                Username = username,
                Role = UserRole.Admin,
                PasswordPhc = _hasher.Hash(request.Password),
                WebDavTokenPhc = _hasher.Hash(StringHelpers.CreateRandomString(AuthConstants.WebDavTokenLength)),
            };
            await _dbContext.Users.AddAsync(user, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Created initial admin user: {Username}", user.Username);
            return user;
        }

        private static string? NormalizeOptionalName(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
