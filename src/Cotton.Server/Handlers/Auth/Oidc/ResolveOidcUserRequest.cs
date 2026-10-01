// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Helpers;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.EntityFrameworkCore;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth.Oidc
{
    internal record ResolveOidcUserRequest(OidcProvider Provider, OidcIdentityClaims Claims) : IRequest<User>;

    internal class ResolveOidcUserRequestHandler(
        CottonDbContext _dbContext,
        IDatabaseIntegrityVerifier _integrity,
        IPasswordHashService _hasher,
        DefaultUserContentSeeder _defaultUserContentSeeder,
        IMediator _mediator)
        : IRequestHandler<ResolveOidcUserRequest, User>
    {
        public async Task<User> Handle(ResolveOidcUserRequest command, CancellationToken ct)
        {
            OidcProvider provider = command.Provider;
            OidcIdentityClaims claims = command.Claims;
            UserExternalIdentity? identity = await _dbContext.UserExternalIdentities
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.ProviderId == provider.Id && x.Subject == claims.Subject, ct);
            if (identity is not null)
            {
                _integrity.RequireValid(_dbContext, identity, "oidc.signin-link");
                _integrity.RequireValid(_dbContext, identity.User, "oidc.signin-user");
                OidcIdentityMapper.ApplyClaims(identity, claims);
                await _mediator.Send(new SyncOidcUserProfileRequest(identity.User, provider, claims), ct);
                return identity.User;
            }

            if (!provider.AllowAccountCreation)
            {
                throw new BadRequestException<OidcProvider>(
                    "This provider can only sign in accounts that are already linked.");
            }

            OidcAccountPolicy.ValidateCreation(provider, claims);
            if (claims.Email is not null)
            {
                bool emailExists = await _dbContext.Users.AnyAsync(x => x.Email == claims.Email, ct);
                if (emailExists)
                {
                    throw new BadRequestException<User>(
                        "An account with this email already exists. Sign in normally and link this provider from profile settings.");
                }
            }

            string username = await BuildUsernameAsync(claims, ct);
            string randomSecret = OidcProtocol.CreateOpaqueValue();
            User user = new User
            {
                Username = username,
                Role = provider.DefaultRole,
                Email = claims.Email,
                IsEmailVerified = claims.EmailVerified,
                FirstName = claims.GivenName,
                LastName = claims.FamilyName,
                PasswordPhc = _hasher.Hash(randomSecret),
                WebDavTokenPhc = _hasher.Hash(randomSecret),
            };
            await _dbContext.Users.AddAsync(user, ct);
            UserExternalIdentity newIdentity = OidcIdentityMapper.Create(user.Id, provider.Id, provider.Issuer, claims);
            newIdentity.User = user;
            await _dbContext.UserExternalIdentities.AddAsync(newIdentity, ct);
            await _mediator.Send(new SyncOidcUserProfileRequest(user, provider, claims), ct);
            await _dbContext.SaveChangesAsync(ct);
            await _defaultUserContentSeeder.SeedAsync(user.Id);
            return user;
        }

        private async Task<string> BuildUsernameAsync(OidcIdentityClaims claims, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(claims.Email))
            {
                return await UsernameHelpers.BuildAvailableUsernameFromEmailAsync(_dbContext, claims.Email, ct);
            }

            string fallback = claims.PreferredUsername ?? claims.Name ?? $"user-{claims.Subject[..Math.Min(8, claims.Subject.Length)]}";
            return await UsernameHelpers.BuildAvailableUsernameFromEmailAsync(
                _dbContext,
                $"{fallback}@oidc.local",
                ct);
        }
    }
}
