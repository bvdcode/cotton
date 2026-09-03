// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Helpers;
using Cotton.Server.Providers;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using System.Net;

namespace Cotton.Server.Handlers.Auth
{
    public record ConfirmTotpRequest(
        Guid UserId,
        string? TwoFactorCode,
        IPAddress ClientIpAddress,
        string UserAgent) : IRequest<TotpOperationResult>;

    public class ConfirmTotpRequestHandler(
        CottonDbContext _dbContext,
        IStreamCipher _crypto,
        IDatabaseIntegrityVerifier _integrity,
        INotificationsProvider _notifications,
        IGeoLookupService _geoLookup,
        SettingsProvider _settings,
        ILogger<ConfirmTotpRequestHandler> _logger) : IRequestHandler<ConfirmTotpRequest, TotpOperationResult>
    {
        public async Task<TotpOperationResult> Handle(
            ConfirmTotpRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.TwoFactorCode))
            {
                return new TotpOperationResult(
                    TotpOperationStatus.BadRequest,
                    "Two-factor authentication code is required");
            }

            User? user = await _dbContext.Users.FindAsync([request.UserId], cancellationToken);
            if (user is null)
            {
                return new TotpOperationResult(TotpOperationStatus.Unauthorized, "User not found");
            }

            _integrity.RequireValid(_dbContext, user, "auth.confirm-totp");
            if (user.IsTotpEnabled)
            {
                return new TotpOperationResult(TotpOperationStatus.Conflict, "TOTP is already enabled for this user");
            }

            if (user.TotpSecretEncrypted is null)
            {
                return new TotpOperationResult(
                    TotpOperationStatus.BadRequest,
                    "TOTP setup has not been initiated for this user");
            }

            string secret = await _crypto.DecryptStringAsync(user.TotpSecretEncrypted, cancellationToken);
            if (!TotpHelpers.VerifyCode(secret, request.TwoFactorCode))
            {
                return new TotpOperationResult(
                    TotpOperationStatus.Forbidden,
                    "Invalid two-factor authentication code");
            }

            user.IsTotpEnabled = true;
            user.TotpEnabledAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _notifications.SendOtpEnabledAsync(
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
