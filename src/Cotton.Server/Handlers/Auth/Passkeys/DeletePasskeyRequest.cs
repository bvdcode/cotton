// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Localization;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Enums;
using Cotton.Server.Providers;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.Passkeys;
using EasyExtensions.AspNetCore.Exceptions;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth.Passkeys
{
    public record DeletePasskeyRequest(Guid UserId, Guid CredentialId) : IRequest;

    public class DeletePasskeyRequestHandler(
        CottonDbContext _dbContext,
        IDatabaseIntegrityVerifier _integrity,
        SettingsProvider _settings,
        INotificationsProvider _notifications,
        ILogger<DeletePasskeyRequestHandler> _logger)
        : IRequestHandler<DeletePasskeyRequest>
    {
        public async Task Handle(DeletePasskeyRequest command, CancellationToken ct)
        {
            Guid userId = command.UserId;
            Guid credentialId = command.CredentialId;
            UserPasskeyCredential credential = await _dbContext.UserPasskeyCredentials
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Id == credentialId, ct)
                ?? throw new EntityNotFoundException<UserPasskeyCredential>("Passkey not found.");
            _integrity.RequireValid(_dbContext, credential, "passkey.delete");

            _dbContext.UserPasskeyCredentials.Remove(credential);
            await _dbContext.SaveChangesAsync(ct);
            await _notifications.SendSecurityEmailAsync(
                _settings,
                _logger,
                userId,
                NotificationTemplates.PasskeyRemovedTitle,
                NotificationTemplates.PasskeyRemovedContent(PasskeyProtocolMapper.GetAuditName(credential)),
                DateTime.UtcNow);
        }
    }
}
