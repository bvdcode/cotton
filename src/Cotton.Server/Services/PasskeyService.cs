// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.AspNetCore.Exceptions;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Coordinates WebAuthn passkey registration and assertion flows, including credential persistence and lifecycle management.
    /// </summary>
    public class PasskeyService(
        CottonDbContext _dbContext,
        IMemoryCache _cache,
        SettingsProvider _settings,
        IDatabaseIntegrityVerifier _integrity)
    {
        private static readonly TimeSpan OptionsLifetime = TimeSpan.FromMinutes(5);
        private const int MaxPasskeyNameLength = 120;

        /// <summary>
        /// Gets credentials async.
        /// </summary>
        public async Task<IReadOnlyList<PasskeyCredentialDto>> GetCredentialsAsync(
            Guid userId,
            CancellationToken ct)
        {
            return await _dbContext.UserPasskeyCredentials
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.LastUsedAt ?? x.CreatedAt)
                .Select(x => new PasskeyCredentialDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    CredentialId = WebEncoders.Base64UrlEncode(x.CredentialId),
                    Transports = x.Transports,
                    IsBackupEligible = x.IsBackupEligible,
                    IsBackedUp = x.IsBackedUp,
                    CreatedAt = x.CreatedAt,
                    LastUsedAt = x.LastUsedAt
                })
                .ToListAsync(ct);
        }

        /// <summary>
        /// Begins registration async.
        /// </summary>
        public async Task<PasskeyRegistrationOptionsResponseDto> BeginRegistrationAsync(
            Guid userId,
            string? requestedName,
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

            var fido = await CreateFido2Async(ct);
            var options = fido.RequestNewCredential(new RequestNewCredentialParams
            {
                User = new Fido2User
                {
                    Id = CreateUserHandle(user.Id),
                    Name = user.Username,
                    DisplayName = BuildDisplayName(user)
                },
                ExcludeCredentials = existingCredentials
                    .Select(x => CreateCredentialDescriptor(x.CredentialId, x.Transports))
                    .ToArray(),
                AuthenticatorSelection = new AuthenticatorSelection
                {
                    ResidentKey = ResidentKeyRequirement.Required,
                    UserVerification = UserVerificationRequirement.Required
                },
                AttestationPreference = AttestationConveyancePreference.None
            });
            string requestId = CreateRequestId();
            _cache.Set(
                RegistrationCacheKey(requestId),
                new RegistrationState(userId, NormalizeName(requestedName), options),
                OptionsLifetime);

            return new()
            {
                RequestId = requestId,
                Options = options
            };
        }

        /// <summary>
        /// Finishes registration async.
        /// </summary>
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
            var attestation = ToAttestationResponse(request.Credential);
            var fido = await CreateFido2Async(ct);
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

            var credential = new UserPasskeyCredential
            {
                UserId = userId,
                CredentialId = result.Id,
                PublicKey = result.PublicKey,
                UserHandle = result.User.Id,
                SignatureCounter = result.SignCount,
                Name = NormalizeName(request.Name ?? state.Name),
                Transports = NormalizeTransports(result.Transports),
                AaGuid = result.AaGuid,
                IsBackupEligible = result.IsBackupEligible,
                IsBackedUp = result.IsBackedUp,
                AttestationFormat = result.AttestationFormat
            };

            await _dbContext.UserPasskeyCredentials.AddAsync(credential, ct);
            await _dbContext.SaveChangesAsync(ct);

            return ToDto(credential);
        }

        /// <summary>
        /// Begins assertion async.
        /// </summary>
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

                if (user != null)
                {
                    _integrity.RequireValid(_dbContext, user, "passkey.assertion-options");
                    scopedUserId = user.Id;
                    var userCredentials = await _dbContext.UserPasskeyCredentials
                        .AsNoTracking()
                        .Where(x => x.UserId == user.Id)
                        .Select(x => new { x.CredentialId, x.Transports })
                        .ToListAsync(ct);
                    allowedCredentials = userCredentials
                        .Select(x => CreateCredentialDescriptor(x.CredentialId, x.Transports))
                        .ToArray();
                }
            }

            var fido = await CreateFido2Async(ct);
            var options = fido.GetAssertionOptions(new GetAssertionOptionsParams
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

        /// <summary>
        /// Finishes assertion async.
        /// </summary>
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
            var assertion = ToAssertionResponse(request.Credential);
            byte[] credentialId = assertion.RawId.Length > 0
                ? assertion.RawId
                : DecodeBrowserBuffer(request.Credential.Id);

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

            var fido = await CreateFido2Async(ct);
            VerifyAssertionResult result;
            try
            {
                result = await fido.MakeAssertionAsync(
                    new MakeAssertionParams
                    {
                        AssertionResponse = assertion,
                        OriginalOptions = state.Options,
                        StoredPublicKey = credential.PublicKey,
                        StoredSignatureCounter = ToSignatureCounter(credential.SignatureCounter),
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

        /// <summary>
        /// Renames credential async.
        /// </summary>
        public async Task<PasskeyCredentialDto> RenameCredentialAsync(
            Guid userId,
            Guid credentialId,
            string name,
            CancellationToken ct)
        {
            UserPasskeyCredential credential = await _dbContext.UserPasskeyCredentials
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Id == credentialId, ct)
                ?? throw new EntityNotFoundException<UserPasskeyCredential>();
            _integrity.RequireValid(_dbContext, credential, "passkey.rename");

            credential.Name = NormalizeName(name);
            await _dbContext.SaveChangesAsync(ct);
            return ToDto(credential);
        }

        /// <summary>
        /// Deletes credential async.
        /// </summary>
        public async Task DeleteCredentialAsync(Guid userId, Guid credentialId, CancellationToken ct)
        {
            UserPasskeyCredential credential = await _dbContext.UserPasskeyCredentials
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Id == credentialId, ct)
                ?? throw new EntityNotFoundException<UserPasskeyCredential>();
            _integrity.RequireValid(_dbContext, credential, "passkey.delete");

            _dbContext.UserPasskeyCredentials.Remove(credential);
            await _dbContext.SaveChangesAsync(ct);
        }

        private static PasskeyCredentialDto ToDto(UserPasskeyCredential credential)
        {
            return new()
            {
                Id = credential.Id,
                Name = credential.Name,
                CredentialId = WebEncoders.Base64UrlEncode(credential.CredentialId),
                Transports = credential.Transports,
                IsBackupEligible = credential.IsBackupEligible,
                IsBackedUp = credential.IsBackedUp,
                CreatedAt = credential.CreatedAt,
                LastUsedAt = credential.LastUsedAt
            };
        }

        private async Task<Fido2> CreateFido2Async(CancellationToken ct)
        {
            var publicBaseUri = new Uri(await _settings.GetPublicBaseUrlAsync(ct), UriKind.Absolute);
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

        private static string NormalizeName(string? name)
        {
            string trimmed = string.IsNullOrWhiteSpace(name) ? "Passkey" : name.Trim();
            return trimmed.Length <= MaxPasskeyNameLength
                ? trimmed
                : trimmed[..MaxPasskeyNameLength];
        }

        private static string BuildDisplayName(User user)
        {
            string displayName = $"{user.FirstName} {user.LastName}".Trim();
            return string.IsNullOrWhiteSpace(displayName) ? user.Username : displayName;
        }

        private static AuthenticatorAttestationRawResponse ToAttestationResponse(
            PasskeyAttestationCredentialDto credential)
        {
            return new()
            {
                Id = credential.Id,
                RawId = DecodeBrowserBuffer(credential.RawId),
                Type = PublicKeyCredentialType.PublicKey,
                Response = new()
                {
                    AttestationObject = DecodeBrowserBuffer(credential.Response.AttestationObject),
                    ClientDataJson = DecodeBrowserBuffer(credential.Response.ClientDataJson),
                    Transports = ParseTransports(credential.Transports)
                }
            };
        }

        private static AuthenticatorAssertionRawResponse ToAssertionResponse(
            PasskeyAssertionCredentialDto credential)
        {
            return new()
            {
                Id = credential.Id,
                RawId = DecodeBrowserBuffer(credential.RawId),
                Type = PublicKeyCredentialType.PublicKey,
                Response = new()
                {
                    AuthenticatorData = DecodeBrowserBuffer(credential.Response.AuthenticatorData),
                    ClientDataJson = DecodeBrowserBuffer(credential.Response.ClientDataJson),
                    Signature = DecodeBrowserBuffer(credential.Response.Signature),
                    UserHandle = string.IsNullOrEmpty(credential.Response.UserHandle)
                        ? []
                        : DecodeBrowserBuffer(credential.Response.UserHandle)
                }
            };
        }

        private static byte[] DecodeBrowserBuffer(string value)
        {
            return WebEncoders.Base64UrlDecode(value);
        }

        private static PublicKeyCredentialDescriptor CreateCredentialDescriptor(
            byte[] credentialId,
            string[] transports)
        {
            AuthenticatorTransport[] parsedTransports = ParseTransports(transports);
            return parsedTransports.Length == 0
                ? new PublicKeyCredentialDescriptor(credentialId)
                : new PublicKeyCredentialDescriptor(
                    PublicKeyCredentialType.PublicKey,
                    credentialId,
                    parsedTransports);
        }

        private static AuthenticatorTransport[] ParseTransports(IEnumerable<string>? transports)
        {
            if (transports is null)
            {
                return [];
            }

            return transports
                .Select(x => Enum.TryParse(x, ignoreCase: true, out AuthenticatorTransport transport)
                    ? transport
                    : (AuthenticatorTransport?)null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToArray();
        }

        private static string[] NormalizeTransports(IEnumerable<AuthenticatorTransport>? transports)
        {
            return transports?
                .Select(x => x.ToString().ToLowerInvariant())
                .Distinct()
                .Order()
                .ToArray() ?? [];
        }

        private static uint ToSignatureCounter(long value)
        {
            if (value <= 0)
            {
                return 0;
            }

            return value >= uint.MaxValue ? uint.MaxValue : (uint)value;
        }

        private record RegistrationState(
            Guid UserId,
            string Name,
            CredentialCreateOptions Options);

        private record AssertionState(
            Guid? ScopedUserId,
            AssertionOptions Options);
    }
}
