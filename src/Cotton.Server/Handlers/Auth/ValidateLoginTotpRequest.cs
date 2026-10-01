// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Helpers;
using Cotton.Server.Providers;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using System.Net;

namespace Cotton.Server.Handlers.Auth
{
    public record ValidateLoginTotpRequest(
        User User, string? TwoFactorCode, IPAddress ClientIpAddress, string UserAgent) : IRequest<string?>;

    public class ValidateLoginTotpRequestHandler(
        CottonDbContext _dbContext,
        IStreamCipher _crypto,
        SettingsProvider _settings,
        INotificationsProvider _notifications,
        IGeoLookupService _geoLookup) : IRequestHandler<ValidateLoginTotpRequest, string?>
    {
        public async Task<string?> Handle(ValidateLoginTotpRequest request, CancellationToken cancellationToken)
        {
            User user = request.User;
            if (!user.IsTotpEnabled)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(request.TwoFactorCode))
            {
                return "Two-factor authentication code is required";
            }

            if (user.TotpSecretEncrypted is null)
            {
                throw new InvalidOperationException("TOTP is enabled but secret is missing");
            }

            int maxFailedAttempts = _settings.GetServerSettings().TotpMaxFailedAttempts;
            if (user.TotpFailedAttempts >= maxFailedAttempts)
            {
                await _notifications.SendTotpLockoutAsync(
                    _geoLookup,
                    user.Id,
                    maxFailedAttempts,
                    request.ClientIpAddress,
                    request.UserAgent);
                return "Maximum number of TOTP verification attempts exceeded";
            }

            string secret = await _crypto.DecryptStringAsync(
                user.TotpSecretEncrypted,
                cancellationToken);
            bool isValid = TotpHelpers.VerifyCode(secret, request.TwoFactorCode);
            if (!isValid)
            {
                user.TotpFailedAttempts += 1;
                await _dbContext.SaveChangesAsync(cancellationToken);
                await _notifications.SendTotpFailedAttemptAsync(
                    _geoLookup,
                    user.Id,
                    user.TotpFailedAttempts,
                    request.ClientIpAddress,
                    request.UserAgent);
                return "Invalid two-factor authentication code";
            }

            user.TotpFailedAttempts = 0;
            return null;
        }
    }
}
