// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Auth.Passkeys;
using Cotton.Server.Services.Passkeys;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Passkeys;
using EasyExtensions.Passkeys.Models;
using System.Formats.Asn1;
using System.Formats.Cbor;
using Microsoft.Extensions.Caching.Memory;

namespace Cotton.Server.IntegrationTests
{
    public class PasskeyRegistrationHandlerTests
    {
        [TestCase("protocol")]
        [TestCase("base64")]
        [TestCase("json")]
        [TestCase("cbor")]
        [TestCase("asn")]
        [TestCase("signature")]
        [TestCase("cbor-type")]
        [TestCase("overflow")]
        public async Task Registration_InvalidCredentialRetainsDiagnosticCause(string failure)
        {
            Exception cause = failure switch
            {
                "protocol" => new PasskeyVerificationException("Invalid attestation"),
                "base64" => new FormatException("Invalid base64url"),
                "json" => new JsonException("Invalid client data"),
                "cbor" => new CborContentException("Invalid attestation"),
                "asn" => new AsnContentException("Invalid certificate"),
                "signature" => new CryptographicException("Invalid signature"),
                "cbor-type" => new InvalidOperationException("Invalid CBOR value type"),
                "overflow" => new OverflowException("Invalid CBOR integer"),
                _ => throw new ArgumentOutOfRangeException(nameof(failure)),
            };
            using MemoryCache cache = new(new MemoryCacheOptions());
            ExceptionRecordingLogger<FinishPasskeyRegistrationRequestHandler> logger = new();
            FinishPasskeyRegistrationRequest request = CreateRequest(cache);
            FinishPasskeyRegistrationRequestHandler handler = new(null!, cache, new FailingPasskeyServer(cause), null!, null!, logger);

            await Assert.ThrowsAsync<BadRequestException<UserPasskeyCredential>>(() => handler.Handle(request, CancellationToken.None));
            Assert.That(logger.Exceptions, Is.EqualTo(new[] { cause }));
            Assert.That(cache.TryGetValue(PasskeyChallenges.RegistrationCacheKey(request.Credential.RequestId), out _), Is.False);
        }

        [TestCase("database")]
        [TestCase("io")]
        [TestCase("cancellation")]
        public async Task Registration_TechnicalFailuresAreNotInvalidCredentials(string failure)
        {
            Exception cause = failure switch
            {
                "database" => new DbUpdateException("Database unavailable"),
                "io" => new IOException("Read failed"),
                "cancellation" => new OperationCanceledException(),
                _ => throw new ArgumentOutOfRangeException(nameof(failure)),
            };
            using MemoryCache cache = new(new MemoryCacheOptions());
            ExceptionRecordingLogger<FinishPasskeyRegistrationRequestHandler> logger = new();
            FinishPasskeyRegistrationRequest request = CreateRequest(cache);
            FinishPasskeyRegistrationRequestHandler handler = new(null!, cache, new FailingPasskeyServer(cause), null!, null!, logger);

            Exception? result = await Assert.CatchAsync(() => handler.Handle(request, CancellationToken.None));
            Assert.That(result, Is.SameAs(cause));
            Assert.That(logger.Exceptions, Is.Empty);
        }

        private static FinishPasskeyRegistrationRequest CreateRequest(MemoryCache cache)
        {
            Guid userId = Guid.NewGuid();
            string requestId = PasskeyChallenges.CreateRequestId();
            cache.Set(PasskeyChallenges.RegistrationCacheKey(requestId),
                new PasskeyRegistrationState(userId, null,
                    new PasskeyServer(new PasskeyServerOptions("cotton.example", "Cotton", ["https://cotton.example"]))
                        .CreateRegistrationOptions(new PasskeyUser(userId.ToByteArray(), "user", "User"))));
            return new(userId, new FinishPasskeyRegistrationRequestDto
            {
                RequestId = requestId,
                Credential = new PasskeyAttestationCredentialDto
                {
                    Id = "AQ", RawId = "AQ", Type = "public-key",
                    Response = new PasskeyAttestationResponseDto { AttestationObject = "AQ", ClientDataJson = "AQ" },
                },
            });
        }
    }
}
