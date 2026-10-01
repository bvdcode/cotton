// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.Passkeys;
using EasyExtensions.AspNetCore.Exceptions;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth.Passkeys
{
    public record BeginPasskeyRegistrationRequest(Guid UserId, string? Label) : IRequest<PasskeyRegistrationOptionsResponseDto>;

    public class BeginPasskeyRegistrationRequestHandler(
        CottonDbContext _dbContext,
        IMemoryCache _cache,
        IPasskeyClientFactory _factory,
        IDatabaseIntegrityVerifier _integrity)
        : IRequestHandler<BeginPasskeyRegistrationRequest, PasskeyRegistrationOptionsResponseDto>
    {
        public async Task<PasskeyRegistrationOptionsResponseDto> Handle(BeginPasskeyRegistrationRequest command, CancellationToken ct)
        {
            Guid userId = command.UserId;
            string? requestedLabel = command.Label;
            User user = await _dbContext.Users.FindAsync([userId], ct)
                ?? throw new EntityNotFoundException<User>("Current user not found.");
            _integrity.RequireValid(_dbContext, user, "passkey.registration-options");

            var existingCredentials = await _dbContext.UserPasskeyCredentials
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new { x.CredentialId, x.Transports })
                .ToListAsync(ct);

            IFido2 fido = await _factory.CreateAsync(ct);
            CredentialCreateOptions options = fido.RequestNewCredential(new RequestNewCredentialParams
            {
                User = new Fido2User
                {
                    Id = user.Id.ToByteArray(),
                    Name = user.Username,
                    DisplayName = PasskeyProtocolMapper.GetDisplayName(user)
                },
                ExcludeCredentials = existingCredentials
                    .Select(x => PasskeyProtocolMapper.CreateCredentialDescriptor(x.CredentialId, x.Transports))
                    .ToArray(),
                AuthenticatorSelection = new AuthenticatorSelection
                {
                    ResidentKey = ResidentKeyRequirement.Required,
                    UserVerification = UserVerificationRequirement.Required
                },
                AttestationPreference = AttestationConveyancePreference.Direct
            });
            string requestId = PasskeyChallenges.CreateRequestId();
            _cache.Set(
                PasskeyChallenges.RegistrationCacheKey(requestId),
                new PasskeyRegistrationState(userId, PasskeyLabelNormalizer.Normalize(requestedLabel), options),
                PasskeyChallenges.OptionsLifetime);

            return new()
            {
                RequestId = requestId,
                Options = options
            };
        }
    }
}
