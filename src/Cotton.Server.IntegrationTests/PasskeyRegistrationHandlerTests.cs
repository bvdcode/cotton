// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Auth.Passkeys;
using Cotton.Server.Services.Passkeys;
using EasyExtensions.AspNetCore.Exceptions;
using Fido2NetLib;
using Microsoft.Extensions.Caching.Memory;

namespace Cotton.Server.IntegrationTests
{
    public class PasskeyRegistrationHandlerTests
    {
        [Test]
        public void Registration_InvalidCredentialRetainsDiagnosticCause()
        {
            Fido2VerificationException cause = new("Invalid attestation");
            using MemoryCache cache = new(new MemoryCacheOptions());
            ExceptionRecordingLogger<FinishPasskeyRegistrationRequestHandler> logger = new();
            FinishPasskeyRegistrationRequest request = CreateRequest(cache);
            FinishPasskeyRegistrationRequestHandler handler = new(null!, cache, new FailingPasskeyClient(cause), null!, null!, logger);

            Assert.ThrowsAsync<BadRequestException<UserPasskeyCredential>>(() => handler.Handle(request, CancellationToken.None));
            Assert.That(logger.Exceptions, Is.EqualTo(new[] { cause }));
            Assert.That(cache.TryGetValue(PasskeyChallenges.RegistrationCacheKey(request.Credential.RequestId), out _), Is.False);
        }

        [TestCase("database")]
        [TestCase("io")]
        [TestCase("cancellation")]
        public void Registration_TechnicalFailuresAreNotInvalidCredentials(string failure)
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
            FinishPasskeyRegistrationRequestHandler handler = new(null!, cache, new FailingPasskeyClient(cause), null!, null!, logger);

            Exception? result = Assert.CatchAsync(() => handler.Handle(request, CancellationToken.None));
            Assert.That(result, Is.SameAs(cause));
            Assert.That(logger.Exceptions, Is.Empty);
        }

        private static FinishPasskeyRegistrationRequest CreateRequest(MemoryCache cache)
        {
            Guid userId = Guid.NewGuid();
            string requestId = PasskeyChallenges.CreateRequestId();
            cache.Set(PasskeyChallenges.RegistrationCacheKey(requestId),
                new PasskeyRegistrationState(userId, null, new CredentialCreateOptions
                {
                    Rp = new("cotton.example", "Cotton"),
                    User = new Fido2User { Id = userId.ToByteArray(), Name = "user", DisplayName = "User" },
                    Challenge = [1, 2, 3],
                    PubKeyCredParams = [],
                }));
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
