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

namespace Cotton.Server.Services
{
    public class PasskeyService(
        CottonDbContext _dbContext,
        IMemoryCache _cache,
        SettingsProvider _settings,
        IDatabaseIntegrityVerifier _integrity,
        INotificationsProvider _notifications,
        ILogger<PasskeyService> _logger)
    {
        private static readonly TimeSpan OptionsLifetime = TimeSpan.FromMinutes(5);

        public async Task<IReadOnlyList<PasskeyCredentialDto>> GetCredentialsAsync(
            Guid userId,
            CancellationToken ct)
        {
            List<PasskeyCredentialDto> credentials = await _dbContext.UserPasskeyCredentials
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.LastUsedAt ?? x.CreatedAt)
                .Select(x => new PasskeyCredentialDto
                {
                    Id = x.Id,
                    Label = x.Label,
                    CredentialId = WebEncoders.Base64UrlEncode(x.CredentialId),
                    Transports = x.Transports,
                    AaGuid = x.AaGuid,
                    IsBackupEligible = x.IsBackupEligible,
                    IsBackedUp = x.IsBackedUp,
                    CreatedAt = x.CreatedAt,
                    LastUsedAt = x.LastUsedAt
                })
                .ToListAsync(ct);

            foreach (PasskeyCredentialDto credential in credentials)
            {
                credential.AuthenticatorName = PasskeyAuthenticatorResolver.ResolveName(credential.AaGuid);
                credential.AuthenticatorKind = PasskeyAuthenticatorResolver.ResolveKind(credential.Transports);
            }

            return credentials;
        }

        public async Task<PasskeyRegistrationOptionsResponseDto> BeginRegistrationAsync(
            Guid userId,
            string? requestedLabel,
            CancellationToken ct)
        {
            User user = await _dbContext.Users.FindAsync([userId], ct)
                ?? throw new EntityNotFoundException<User>();
            _integrity.RequireValid(_dbContext, user, "passkey.registration-options");

            var existingCredentials = await _dbContext.UserPasskeyCredentials
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new { x.CredentialId, x.Transports })
                .ToListAsync(ct);

            Fido2 fido = await CreateFido2Async(ct);
            CredentialCreateOptions options = fido.RequestNewCredential(new RequestNewCredentialParams
            {
                User = new Fido2User
                {
                    Id = CreateUserHandle(user.Id),
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
            string requestId = CreateRequestId();
            _cache.Set(
                RegistrationCacheKey(requestId),
                new RegistrationState(userId, PasskeyLabelNormalizer.Normalize(requestedLabel), options),
                OptionsLifetime);

            return new()
            {
                RequestId = requestId,
                Options = options
            };
        }

        public async Task<PasskeyCredentialDto> FinishRegistrationAsync(
            Guid userId,
            FinishPasskeyRegistrationRequestDto request,
            CancellationToken ct)
        {
            if (!_cache.TryGetValue(RegistrationCacheKey(request.RequestId), out RegistrationState? state)
                || state is null
                || state.UserId != userId)
            {
                throw new BadRequestException<UserPasskeyCredential>("Passkey registration request has expired");
            }

            _cache.Remove(RegistrationCacheKey(request.RequestId));
            AuthenticatorAttestationRawResponse attestation = PasskeyProtocolMapper.ToAttestationResponse(request.Credential);
            Fido2 fido = await CreateFido2Async(ct);
            RegisteredPublicKeyCredential result;
            try
            {
                result = await fido.MakeNewCredentialAsync(
                    new MakeNewCredentialParams
                    {
                        AttestationResponse = attestation,
                        OriginalOptions = state.Options,
                        IsCredentialIdUniqueToUserCallback = async (args, token) =>
                        {
                            return !await _dbContext.UserPasskeyCredentials
                                .AnyAsync(x => x.CredentialId == args.CredentialId, token);
                        }
                    },
                    ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new BadRequestException<UserPasskeyCredential>("Passkey registration could not be verified");
            }

            string[] transports = PasskeyProtocolMapper.NormalizeTransports(result.Transports);
            UserPasskeyCredential credential = new UserPasskeyCredential
            {
                UserId = userId,
                CredentialId = result.Id,
                PublicKey = result.PublicKey,
                UserHandle = result.User.Id,
                SignatureCounter = result.SignCount,
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

        public async Task<PasskeyAssertionOptionsResponseDto> BeginAssertionAsync(
            string? username,
            CancellationToken ct)
        {
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

            Fido2 fido = await CreateFido2Async(ct);
            AssertionOptions options = fido.GetAssertionOptions(new GetAssertionOptionsParams
            {
                AllowedCredentials = allowedCredentials,
                UserVerification = UserVerificationRequirement.Required
            });

            string requestId = CreateRequestId();
            _cache.Set(AssertionCacheKey(requestId), new AssertionState(scopedUserId, options), OptionsLifetime);

            return new()
            {
                RequestId = requestId,
                Options = options
            };
        }

        public async Task<User> FinishAssertionAsync(
            FinishPasskeyAssertionRequestDto request,
            CancellationToken ct)
        {
            if (!_cache.TryGetValue(AssertionCacheKey(request.RequestId), out AssertionState? state)
                || state is null)
            {
                throw new BadRequestException<UserPasskeyCredential>("Passkey sign-in request has expired");
            }

            _cache.Remove(AssertionCacheKey(request.RequestId));
            AuthenticatorAssertionRawResponse assertion = PasskeyProtocolMapper.ToAssertionResponse(request.Credential);
            byte[] credentialId = assertion.RawId.Length > 0
                ? assertion.RawId
                : PasskeyProtocolMapper.DecodeBrowserBuffer(request.Credential.Id);

            UserPasskeyCredential credential = await _dbContext.UserPasskeyCredentials
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.CredentialId == credentialId, ct)
                ?? throw new UnauthorizedAccessException("Passkey credential was not found");
            _integrity.RequireValid(_dbContext, credential, "passkey.assertion-credential");
            _integrity.RequireValid(_dbContext, credential.User, "passkey.assertion-user");

            if (state.ScopedUserId.HasValue && credential.UserId != state.ScopedUserId.Value)
            {
                throw new UnauthorizedAccessException("Passkey credential does not belong to the requested user");
            }

            Fido2 fido = await CreateFido2Async(ct);
            VerifyAssertionResult result;
            try
            {
                result = await fido.MakeAssertionAsync(
                    new MakeAssertionParams
                    {
                        AssertionResponse = assertion,
                        OriginalOptions = state.Options,
                        StoredPublicKey = credential.PublicKey,
                        StoredSignatureCounter = PasskeyProtocolMapper.ToSignatureCounter(credential.SignatureCounter),
                        IsUserHandleOwnerOfCredentialIdCallback = async (args, token) =>
                        {
                            return await _dbContext.UserPasskeyCredentials.AnyAsync(
                                x => x.CredentialId == args.CredentialId
                                    && x.UserId == credential.UserId
                                    && (args.UserHandle.Length == 0 || x.UserHandle == args.UserHandle),
                                token);
                        }
                    },
                    ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new UnauthorizedAccessException("Passkey assertion could not be verified");
            }

            credential.SignatureCounter = result.SignCount;
            credential.IsBackedUp = result.IsBackedUp;
            credential.LastUsedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            return credential.User;
        }

        public async Task<PasskeyCredentialDto> SetCredentialLabelAsync(
            Guid userId,
            Guid credentialId,
            string? label,
            CancellationToken ct)
        {
            UserPasskeyCredential credential = await _dbContext.UserPasskeyCredentials
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Id == credentialId, ct)
                ?? throw new EntityNotFoundException<UserPasskeyCredential>();
            _integrity.RequireValid(_dbContext, credential, "passkey.rename");

            credential.Label = PasskeyLabelNormalizer.Normalize(label);
            await _dbContext.SaveChangesAsync(ct);
            return PasskeyProtocolMapper.ToDto(credential);
        }

        public async Task DeleteCredentialAsync(Guid userId, Guid credentialId, CancellationToken ct)
        {
            UserPasskeyCredential credential = await _dbContext.UserPasskeyCredentials
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Id == credentialId, ct)
                ?? throw new EntityNotFoundException<UserPasskeyCredential>();
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

        private async Task<Fido2> CreateFido2Async(CancellationToken ct)
        {
            Uri publicBaseUri = new Uri(await _settings.GetPublicBaseUrlAsync(ct), UriKind.Absolute);
            string origin = publicBaseUri.GetLeftPart(UriPartial.Authority);

            return new Fido2(new Fido2Configuration
            {
                ServerDomain = publicBaseUri.Host,
                ServerName = Constants.ProductName,
                Origins = new HashSet<string> { origin },
                Timeout = 60_000,
                ChallengeSize = 32
            }, metadataService: null);
        }

        private static byte[] CreateUserHandle(Guid userId)
        {
            return userId.ToByteArray();
        }

        private static string CreateRequestId()
        {
            return WebEncoders.Base64UrlEncode(Guid.NewGuid().ToByteArray());
        }

        private static string RegistrationCacheKey(string requestId)
        {
            return $"passkey:registration:{requestId}";
        }

        private static string AssertionCacheKey(string requestId)
        {
            return $"passkey:assertion:{requestId}";
        }

        private record RegistrationState(
            Guid UserId,
            string? Label,
            CredentialCreateOptions Options);

        private record AssertionState(
            Guid? ScopedUserId,
            AssertionOptions Options);
    }
}
