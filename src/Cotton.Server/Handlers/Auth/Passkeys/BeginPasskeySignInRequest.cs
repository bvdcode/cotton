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
    public record BeginPasskeySignInRequest(string? Username) : IRequest<PasskeyAssertionOptionsResponseDto>;

    public class BeginPasskeySignInRequestHandler(
        CottonDbContext _dbContext,
        IMemoryCache _cache,
        IPasskeyClientFactory _factory,
        IDatabaseIntegrityVerifier _integrity)
        : IRequestHandler<BeginPasskeySignInRequest, PasskeyAssertionOptionsResponseDto>
    {
        public async Task<PasskeyAssertionOptionsResponseDto> Handle(BeginPasskeySignInRequest command, CancellationToken ct)
        {
            string? username = command.Username;
            Guid? scopedUserId = null;
            PublicKeyCredentialDescriptor[] allowedCredentials = [];

            string? normalizedUsername = username?.Trim();
            if (!string.IsNullOrEmpty(normalizedUsername))
            {
                User? user = await _dbContext.Users
                    .FirstOrDefaultAsync(x => x.Username == normalizedUsername || x.Email == normalizedUsername, ct);

                if (user is not null)
                {
                    _integrity.RequireValid(_dbContext, user, "passkey.assertion-options");
                    scopedUserId = user.Id;
                    var userCredentials = await _dbContext.UserPasskeyCredentials
                        .AsNoTracking()
                        .Where(x => x.UserId == user.Id)
                        .Select(x => new { x.CredentialId, x.Transports })
                        .ToListAsync(ct);
                    allowedCredentials = userCredentials
                        .Select(x => PasskeyProtocolMapper.CreateCredentialDescriptor(x.CredentialId, x.Transports))
                        .ToArray();
                }
            }

            IFido2 fido = await _factory.CreateAsync(ct);
            AssertionOptions options = fido.GetAssertionOptions(new GetAssertionOptionsParams
            {
                AllowedCredentials = allowedCredentials,
                UserVerification = UserVerificationRequirement.Required
            });

            string requestId = PasskeyChallenges.CreateRequestId();
            _cache.Set(PasskeyChallenges.AssertionCacheKey(requestId), new PasskeyAssertionState(scopedUserId, options), PasskeyChallenges.OptionsLifetime);

            return new()
            {
                RequestId = requestId,
                Options = options
            };
        }
    }
}
