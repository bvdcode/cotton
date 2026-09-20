// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Cotton.Server.IntegrationTests
{
    public partial class SessionAuthenticationTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task SessionClaims_AreAcceptedWithEitherClaimMapping(bool mapInboundClaims)
        {
            SessionValidationConnectionInterceptor interceptor = RejectDatabaseAccess();
            using NUnitLoggerProvider logger = new();
            await using ServiceProvider services = CreateServices(interceptor, logger,
                options => options.MapInboundClaims = mapInboundClaims);
            await using AsyncServiceScope scope = services.CreateAsyncScope();
            MarkSessionActive(services);
            DefaultHttpContext context = CreateContext(scope.ServiceProvider, CancellationToken.None);

            AuthenticateResult result = await context.AuthenticateAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Is.True);
                Assert.That(interceptor.Attempts, Is.Zero);
            });
        }

        [TestCase(null, TestSessionId)]
        [TestCase("invalid-user-id", TestSessionId)]
        [TestCase("00000000-0000-0000-0000-000000000001", null)]
        [TestCase("00000000-0000-0000-0000-000000000001", " ")]
        public async Task InvalidSessionClaims_FailBeforeDatabaseAccess(string? userId, string? sessionId)
        {
            SessionValidationConnectionInterceptor interceptor = RejectDatabaseAccess();
            using NUnitLoggerProvider logger = new();
            await using ServiceProvider services = CreateServices(interceptor, logger);
            await using AsyncServiceScope scope = services.CreateAsyncScope();
            List<Claim> claims = [];
            if (userId is not null)
            {
                claims.Add(new Claim(JwtRegisteredClaimNames.Sub, userId));
            }
            if (sessionId is not null)
            {
                claims.Add(new Claim(JwtRegisteredClaimNames.Sid, sessionId));
            }
            DefaultHttpContext context = CreateContext(scope.ServiceProvider, CancellationToken.None, [.. claims]);

            AuthenticateResult result = await context.AuthenticateAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Failure?.Message, Is.EqualTo("Access token is missing required session claims."));
                Assert.That(interceptor.Attempts, Is.Zero);
            });
        }

        [TestCase("success")]
        [TestCase("failure")]
        [TestCase("none")]
        public async Task ExistingTokenValidationResult_IsPreserved(string outcome)
        {
            int callbackCount = 0;
            AuthenticateResult? expected = null;
            SessionValidationConnectionInterceptor interceptor = RejectDatabaseAccess();
            using NUnitLoggerProvider logger = new();
            await using ServiceProvider services = CreateServices(interceptor, logger, options =>
                options.Events.OnTokenValidated = context =>
                {
                    callbackCount++;
                    switch (outcome)
                    {
                        case "success":
                            context.Success();
                            break;
                        case "failure":
                            context.Fail("Earlier validation rejected the token.");
                            break;
                        case "none":
                            context.NoResult();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(outcome));
                    }
                    expected = context.Result;
                    return Task.CompletedTask;
                });
            await using AsyncServiceScope scope = services.CreateAsyncScope();
            DefaultHttpContext context = CreateContext(scope.ServiceProvider, CancellationToken.None);

            AuthenticateResult result = await context.AuthenticateAsync();

            Assert.Multiple(() =>
            {
                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(result.Succeeded, Is.EqualTo(expected!.Succeeded));
                Assert.That(result.None, Is.EqualTo(expected.None));
                Assert.That(result.Failure, Is.SameAs(expected.Failure));
                Assert.That(interceptor.Attempts, Is.Zero);
            });
        }

        [Test]
        public async Task ExistingTokenValidation_RunsBeforeSessionClaimsAreRead()
        {
            int callbackCount = 0;
            SessionValidationConnectionInterceptor interceptor = RejectDatabaseAccess();
            using NUnitLoggerProvider logger = new();
            await using ServiceProvider services = CreateServices(interceptor, logger, options =>
                options.Events.OnTokenValidated = context =>
                {
                    callbackCount++;
                    context.Principal!.AddIdentity(new ClaimsIdentity(
                        [new Claim(JwtRegisteredClaimNames.Sid, TestSessionId)]));
                    return Task.CompletedTask;
                });
            await using AsyncServiceScope scope = services.CreateAsyncScope();
            MarkSessionActive(services);
            DefaultHttpContext context = CreateContext(scope.ServiceProvider, CancellationToken.None,
                [new Claim(JwtRegisteredClaimNames.Sub, TestUserId.ToString())]);

            AuthenticateResult result = await context.AuthenticateAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Is.True);
                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(interceptor.Attempts, Is.Zero);
            });
        }

        [Test]
        public async Task QueryStringToken_PreservesExistingMessageReceivedHandler()
        {
            SessionValidationConnectionInterceptor interceptor = RejectDatabaseAccess();
            using NUnitLoggerProvider logger = new();
            await using ServiceProvider services = CreateServices(interceptor, logger);
            await using AsyncServiceScope scope = services.CreateAsyncScope();
            MarkSessionActive(services);
            DefaultHttpContext context = CreateContext(scope.ServiceProvider, CancellationToken.None);
            string token = context.Request.Headers.Authorization.ToString()["Bearer ".Length..];
            context.Request.Headers.Remove("Authorization");
            context.Request.QueryString = QueryString.Create("access_token", token);

            AuthenticateResult result = await context.AuthenticateAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Is.True);
                Assert.That(interceptor.Attempts, Is.Zero);
            });
        }

        [Test]
        public async Task MissingBearerToken_DoesNotRunSessionValidation()
        {
            int callbackCount = 0;
            SessionValidationConnectionInterceptor interceptor = RejectDatabaseAccess();
            using NUnitLoggerProvider logger = new();
            await using ServiceProvider services = CreateServices(interceptor, logger, options =>
                options.Events.OnTokenValidated = _ =>
                {
                    callbackCount++;
                    return Task.CompletedTask;
                });
            await using AsyncServiceScope scope = services.CreateAsyncScope();
            DefaultHttpContext context = CreateContext(scope.ServiceProvider, CancellationToken.None);
            context.Request.Headers.Remove("Authorization");

            AuthenticateResult result = await context.AuthenticateAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result.None, Is.True);
                Assert.That(callbackCount, Is.Zero);
                Assert.That(interceptor.Attempts, Is.Zero);
            });
        }

        private static SessionValidationConnectionInterceptor RejectDatabaseAccess()
        {
            return new SessionValidationConnectionInterceptor(_ =>
                throw new InvalidOperationException("Session validation must not query the database in this test."));
        }

        private static void MarkSessionActive(IServiceProvider services)
        {
            services.GetRequiredService<SessionAccessTokenRevocationCache>()
                .MarkActive(TestUserId, TestSessionId, TimeSpan.FromMinutes(1));
        }
    }
}
