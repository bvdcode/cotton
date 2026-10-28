// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Localization;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services.Passkeys;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Passkeys;
using EasyExtensions.Passkeys.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth.Passkeys
{
    public record FinishPasskeyRegistrationRequest(Guid UserId, FinishPasskeyRegistrationRequestDto Credential) : IRequest<PasskeyCredentialDto>;

    public class FinishPasskeyRegistrationRequestHandler(
        CottonDbContext _dbContext,
        IMemoryCache _cache,
        IPasskeyServerFactory _factory,
        SettingsProvider _settings,
        INotificationsProvider _notifications,
        ILogger<FinishPasskeyRegistrationRequestHandler> _logger)
        : IRequestHandler<FinishPasskeyRegistrationRequest, PasskeyCredentialDto>
    {
        public async Task<PasskeyCredentialDto> Handle(FinishPasskeyRegistrationRequest command, CancellationToken ct)
        {
            Guid userId = command.UserId;
            FinishPasskeyRegistrationRequestDto request = command.Credential;
            if (!_cache.TryGetValue(PasskeyChallenges.RegistrationCacheKey(request.RequestId), out PasskeyRegistrationState? state)
                || state is null
                || state.UserId != userId)
            {
                throw new BadRequestException<UserPasskeyCredential>("Passkey registration request has expired");
            }

            _cache.Remove(PasskeyChallenges.RegistrationCacheKey(request.RequestId));
            IPasskeyServer server = await _factory.CreateAsync(ct);
            RegistrationResult result;
            try
            {
                RegistrationResponse attestation = PasskeyProtocolMapper.ToAttestationResponse(request.Credential);
                result = server.VerifyRegistration(attestation, state.Options);
            }
            catch (Exception ex) when (PasskeyVerificationErrors.IsInvalidResponse(ex))
            {
                _logger.LogWarning(ex, "Passkey registration verification failed for user {UserId}.", userId);
                throw new BadRequestException<UserPasskeyCredential>("Passkey registration could not be verified");
            }

            if (await _dbContext.UserPasskeyCredentials.AnyAsync(x => x.CredentialId == result.CredentialId, ct))
            {
                throw new BadRequestException<UserPasskeyCredential>("Passkey is already registered");
            }

            string[] transports = PasskeyProtocolMapper.NormalizeTransports(result.Transports);
            UserPasskeyCredential credential = new UserPasskeyCredential
            {
                UserId = userId,
                CredentialId = result.CredentialId,
                PublicKey = result.CosePublicKey,
                UserHandle = result.UserHandle,
                SignatureCounter = result.SignatureCounter,
                Label = PasskeyLabelNormalizer.Normalize(request.Label ?? state.Label),
                Transports = transports,
                AaGuid = result.AaGuid,
                IsBackupEligible = result.IsBackupEligible,
                IsBackedUp = result.IsBackedUp,
                AttestationFormat = result.AttestationFormat
            };

            await _dbContext.UserPasskeyCredentials.AddAsync(credential, ct);
            await _dbContext.SaveChangesAsync(ct);
            await _notifications.SendSecurityEmailAsync(
                _settings,
                _logger,
                userId,
                NotificationTemplates.PasskeyAddedTitle,
                NotificationTemplates.PasskeyAddedContent(PasskeyProtocolMapper.GetAuditName(credential)),
                DateTime.UtcNow);

            return PasskeyProtocolMapper.ToDto(credential);
        }
    }
}
