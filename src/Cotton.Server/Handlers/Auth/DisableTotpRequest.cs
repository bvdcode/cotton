// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Providers;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.Abstractions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using System.Net;

namespace Cotton.Server.Handlers.Auth
{
    public record DisableTotpRequest(
        Guid UserId,
        string? Password,
        IPAddress ClientIpAddress,
        string UserAgent) : IRequest<TotpOperationResult>;

    public class DisableTotpRequestHandler(
        CottonDbContext _dbContext,
        IPasswordHashService _hasher,
        IDatabaseIntegrityVerifier _integrity,
        INotificationsProvider _notifications,
        IGeoLookupService _geoLookup,
        SettingsProvider _settings,
        ILogger<DisableTotpRequestHandler> _logger) : IRequestHandler<DisableTotpRequest, TotpOperationResult>
    {
        public async Task<TotpOperationResult> Handle(
            DisableTotpRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return new TotpOperationResult(TotpOperationStatus.BadRequest, "Password is required");
            }

            User? user = await _dbContext.Users.FindAsync([request.UserId], cancellationToken);
            if (user is null)
            {
                return new TotpOperationResult(TotpOperationStatus.Unauthorized, "User not found");
            }

            _integrity.RequireValid(_dbContext, user, "auth.disable-totp");
            if (string.IsNullOrEmpty(user.PasswordPhc) || !_hasher.Verify(request.Password, user.PasswordPhc))
            {
                return new TotpOperationResult(TotpOperationStatus.Forbidden, "Invalid password");
            }

            if (!user.IsTotpEnabled)
            {
                return new TotpOperationResult(TotpOperationStatus.Conflict, "TOTP is not enabled for this user");
            }

            user.IsTotpEnabled = false;
            user.TotpSecretEncrypted = null;
            user.TotpEnabledAt = null;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _notifications.SendOtpDisabledAsync(
                _geoLookup,
                _settings,
                _logger,
                user.Id,
                request.ClientIpAddress,
                request.UserAgent);
            return new TotpOperationResult(TotpOperationStatus.Success);
        }
    }
}
