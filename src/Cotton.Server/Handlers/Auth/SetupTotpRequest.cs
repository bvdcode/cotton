// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Helpers;
using Cotton.Server.Models;
using Cotton.Server.Providers;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth
{
    public record SetupTotpRequest(Guid UserId, string? Host) : IRequest<TotpOperationResult>;

    public class SetupTotpRequestHandler(
        CottonDbContext _dbContext,
        IStreamCipher _crypto,
        IDatabaseIntegrityVerifier _integrity,
        SettingsProvider _settings) : IRequestHandler<SetupTotpRequest, TotpOperationResult>
    {
        public async Task<TotpOperationResult> Handle(
            SetupTotpRequest request,
            CancellationToken cancellationToken)
        {
            User? user = await _dbContext.Users.FindAsync([request.UserId], cancellationToken);
            if (user is null)
            {
                return new TotpOperationResult(TotpOperationStatus.Unauthorized, "User not found");
            }

            _integrity.RequireValid(_dbContext, user, "auth.setup-totp");
            if (user.IsTotpEnabled)
            {
                return new TotpOperationResult(TotpOperationStatus.Conflict, "TOTP is already enabled for this user");
            }

            string account = string.IsNullOrWhiteSpace(request.Host)
                ? user.Username
                : $"{user.Username}@{request.Host}";
            string publicBaseUrl = await _settings.GetPublicBaseUrlAsync(cancellationToken);
            Uri imageUri = new(
                new Uri(publicBaseUrl + '/', UriKind.Absolute),
                "assets/icons/icon-192.png");
            TotpSetup setup = TotpHelpers.CreateSetup(
                Constants.ShortProductName,
                account,
                imageUri);
            user.TotpSecretEncrypted = await _crypto.EncryptStringAsync(
                setup.SecretBase32,
                cancellationToken: cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new TotpOperationResult(TotpOperationStatus.Success, Setup: setup);
        }
    }
}
