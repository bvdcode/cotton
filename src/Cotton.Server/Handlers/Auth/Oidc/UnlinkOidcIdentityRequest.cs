// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Localization;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Providers;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth.Oidc
{
    public record UnlinkOidcIdentityRequest(Guid UserId, Guid IdentityId) : IRequest;

    public class UnlinkOidcIdentityRequestHandler(
        CottonDbContext _dbContext,
        IDatabaseIntegrityVerifier _integrity,
        SettingsProvider _settings,
        INotificationsProvider _notifications,
        ILogger<UnlinkOidcIdentityRequestHandler> _logger)
        : IRequestHandler<UnlinkOidcIdentityRequest>
    {
        public async Task Handle(UnlinkOidcIdentityRequest command, CancellationToken ct)
        {
            Guid userId = command.UserId;
            Guid identityId = command.IdentityId;
            UserExternalIdentity identity = await _dbContext.UserExternalIdentities
                .Include(x => x.Provider)
                .FirstOrDefaultAsync(x => x.Id == identityId && x.UserId == userId, ct)
                ?? throw new EntityNotFoundException<UserExternalIdentity>("Linked sign-in account not found.");
            _integrity.RequireValid(_dbContext, identity, "oidc.unlink");
            await EnsureCanUnlinkAsync(userId, identityId, ct);
            _dbContext.UserExternalIdentities.Remove(identity);
            await _dbContext.SaveChangesAsync(ct);
            await _notifications.SendSecurityEmailAsync(
                _settings,
                _logger,
                userId,
                NotificationTemplates.ExternalIdentityUnlinkedTitle,
                NotificationTemplates.ExternalIdentityUnlinkedContent(identity.Provider.Name),
                DateTime.UtcNow);
        }

        private async Task EnsureCanUnlinkAsync(Guid userId, Guid identityId, CancellationToken ct)
        {
            bool hasAnotherExternalIdentity = await _dbContext.UserExternalIdentities
                .AnyAsync(x => x.UserId == userId && x.Id != identityId, ct);
            if (hasAnotherExternalIdentity)
            {
                return;
            }

            bool hasPasskey = await _dbContext.UserPasskeyCredentials
                .AnyAsync(x => x.UserId == userId, ct);
            if (hasPasskey)
            {
                return;
            }

            User user = await _dbContext.Users.FindAsync([userId], ct)
                ?? throw new EntityNotFoundException<User>("Current user not found.");
            _integrity.RequireValid(_dbContext, user, "oidc.unlink-user");

            bool canResetPassword = user.IsEmailVerified
                && !string.IsNullOrWhiteSpace(user.Email)
                && _settings.GetServerSettings().EmailMode != EmailMode.None;
            if (canResetPassword)
            {
                return;
            }

            throw new BadRequestException<UserExternalIdentity>(
                "Add another sign-in method before unlinking the last external account.");
        }
    }
}
