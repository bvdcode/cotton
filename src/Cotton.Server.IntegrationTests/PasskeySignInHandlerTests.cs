// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Auth.Passkeys;
using Cotton.Server.Models.Results;
using Cotton.Server.Services.Passkeys;
using Fido2NetLib;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.IntegrationTests
{
    public class PasskeySignInHandlerTests : AuthEndpointTestBase
    {
        [TestCase("invalid")]
        [TestCase("database")]
        [TestCase("io")]
        [TestCase("cancellation")]
        public async Task SignIn_OnlyVerificationFailureIsReportedAsInvalidCredential(string failure)
        {
            AuthSessionResponseDto login = await LoginAsync("passkey-user", "testpassword");
            await using AsyncServiceScope scope = _customFactory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            UserPasskeyCredential credential = new()
            {
                UserId = login.User.Id,
                CredentialId = [1],
                PublicKey = [2],
                UserHandle = login.User.Id.ToByteArray(),
                SignatureCounter = 7,
            };
            db.UserPasskeyCredentials.Add(credential);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            Exception cause = failure switch
            {
                "invalid" => new Fido2VerificationException("Invalid assertion"),
                "database" => new DbUpdateException("Database unavailable"),
                "io" => new IOException("Read failed"),
                "cancellation" => new OperationCanceledException(),
                _ => throw new ArgumentOutOfRangeException(nameof(failure)),
            };
            using MemoryCache cache = new(new MemoryCacheOptions());
            FinishPasskeySignInRequest request = CreateRequest(cache, login.User.Id);
            ExceptionRecordingLogger<FinishPasskeySignInRequestHandler> logger = new();
            FinishPasskeySignInRequestHandler handler = new(
                db, cache, new FailingPasskeyClient(cause),
                scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityVerifier>(),
                scope.ServiceProvider.GetRequiredService<AuthSessionIssuer>(), logger);

            if (failure == "invalid")
            {
                ActionResult<AuthSessionResponseDto> result = await handler.Handle(request, CancellationToken.None);
                Assert.That(result.Result, Is.TypeOf<ApiProblemResult>());
                Assert.That(logger.Exceptions, Is.EqualTo(new[] { cause }));
            }
            else
            {
                Exception? result = Assert.CatchAsync(() => handler.Handle(request, CancellationToken.None));
                Assert.That(result, Is.SameAs(cause));
                Assert.That(logger.Exceptions, Is.Empty);
            }

            Assert.That(cache.TryGetValue(PasskeyChallenges.AssertionCacheKey(request.Credential.RequestId), out _), Is.False);
            db.ChangeTracker.Clear();
            UserPasskeyCredential stored = await db.UserPasskeyCredentials.SingleAsync();
            Assert.Multiple(() =>
            {
                Assert.That(stored.SignatureCounter, Is.EqualTo(7));
                Assert.That(stored.LastUsedAt, Is.Null);
            });
        }

        private static FinishPasskeySignInRequest CreateRequest(MemoryCache cache, Guid userId)
        {
            string requestId = PasskeyChallenges.CreateRequestId();
            cache.Set(PasskeyChallenges.AssertionCacheKey(requestId),
                new PasskeyAssertionState(userId, new AssertionOptions { Challenge = [1, 2, 3] }));
            return new(new FinishPasskeyAssertionRequestDto
            {
                RequestId = requestId,
                Credential = new PasskeyAssertionCredentialDto
                {
                    Id = "AQ",
                    RawId = "AQ",
                    Type = "public-key",
                    Response = new PasskeyAssertionResponseDto
                    {
                        AuthenticatorData = "AQ",
                        ClientDataJson = "AQ",
                        Signature = "AQ",
                    },
                },
            });
        }
    }
}
