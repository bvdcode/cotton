// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.Passkeys;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Passkeys;
using EasyExtensions.Passkeys.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Cotton.Server.Models.Results;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Handlers.Auth.Passkeys
{
    public record FinishPasskeySignInRequest(FinishPasskeyAssertionRequestDto Credential) : IRequest<ActionResult<AuthSessionResponseDto>>;

    public class FinishPasskeySignInRequestHandler(
        CottonDbContext _dbContext,
        IMemoryCache _cache,
        IPasskeyServerFactory _factory,
        IDatabaseIntegrityVerifier _integrity,
        AuthSessionIssuer _sessionIssuer,
        ILogger<FinishPasskeySignInRequestHandler> _logger)
        : IRequestHandler<FinishPasskeySignInRequest, ActionResult<AuthSessionResponseDto>>
    {
        public async Task<ActionResult<AuthSessionResponseDto>> Handle(FinishPasskeySignInRequest command, CancellationToken ct)
        {
            FinishPasskeyAssertionRequestDto request = command.Credential;
            if (!_cache.TryGetValue(PasskeyChallenges.AssertionCacheKey(request.RequestId), out PasskeyAssertionState? state)
                || state is null)
            {
                throw new BadRequestException<UserPasskeyCredential>("Passkey sign-in request has expired");
            }

            _cache.Remove(PasskeyChallenges.AssertionCacheKey(request.RequestId));
            AuthenticationResponse assertion;
            try
            {
                assertion = PasskeyProtocolMapper.ToAssertionResponse(request.Credential);
            }
            catch (Exception ex) when (PasskeyVerificationErrors.IsInvalidResponse(ex))
            {
                _logger.LogWarning(ex, "Passkey assertion response could not be decoded.");
                return new ApiProblemResult(StatusCodes.Status401Unauthorized, "Invalid passkey", "unauthorized");
            }
            byte[] credentialId = assertion.RawId;

            UserPasskeyCredential? credential = await _dbContext.UserPasskeyCredentials
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.CredentialId == credentialId, ct);
            if (credential is null)
            {
                return new ApiProblemResult(StatusCodes.Status401Unauthorized, "Invalid passkey", "unauthorized");
            }
            _integrity.RequireValid(_dbContext, credential, "passkey.assertion-credential");
            _integrity.RequireValid(_dbContext, credential.User, "passkey.assertion-user");

            if (state.ScopedUserId.HasValue && credential.UserId != state.ScopedUserId.Value)
            {
                return new ApiProblemResult(StatusCodes.Status401Unauthorized, "Invalid passkey", "unauthorized");
            }

            IPasskeyServer server = await _factory.CreateAsync(ct);
            AuthenticationResult result;
            try
            {
                result = server.VerifyAuthentication(assertion, state.Options,
                    new StoredCredential(credential.CredentialId, credential.PublicKey, credential.UserHandle,
                        PasskeyProtocolMapper.ToSignatureCounter(credential.SignatureCounter), credential.IsBackupEligible));
            }
            catch (Exception ex) when (PasskeyVerificationErrors.IsInvalidResponse(ex))
            {
                _logger.LogWarning(ex, "Passkey assertion verification failed for credential {CredentialId}.", credential.Id);
                return new ApiProblemResult(StatusCodes.Status401Unauthorized, "Invalid passkey", "unauthorized");
            }

            credential.SignatureCounter = result.SignatureCounter;
            credential.IsBackedUp = result.IsBackedUp;
            credential.LastUsedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            return await _sessionIssuer.SignInAsync(credential.User, request.TrustDevice, AuthType.Passkey, ct);
        }
    }
}
