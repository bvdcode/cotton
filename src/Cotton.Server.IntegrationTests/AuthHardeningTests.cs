// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Extensions;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Cotton.Server.IntegrationTests
{
    public class AuthHardeningTests
    {
        private const string TestIssuer = "session-validation-tests";
        private const string TestAudience = "session-validation-client";
        private const string TestSessionId = "test-session";
        private static readonly Guid TestUserId = Guid.NewGuid();
        private static readonly SymmetricSecurityKey SigningKey = new(RandomNumberGenerator.GetBytes(32));

        [Test]
        public async Task AbortedRequestDuringSessionLookup_DoesNotAuthenticateOrLogJwtFailure()
        {
            using CancellationTokenSource requestCancellation = new();
            SessionValidationConnectionInterceptor interceptor = new(cancellationToken =>
            {
                requestCancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            });
            using NUnitLoggerProvider logger = new();
            await using ServiceProvider services = CreateServices(interceptor, logger);
            await using AsyncServiceScope scope = services.CreateAsyncScope();
            DefaultHttpContext context = CreateContext(scope.ServiceProvider, requestCancellation.Token);

            AuthenticateResult result = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            SessionAccessTokenRevocationCache cache = services.GetRequiredService<SessionAccessTokenRevocationCache>();

            Assert.Multiple(() =>
            {
                Assert.That(interceptor.Attempts, Is.EqualTo(1));
                Assert.That(result.None, Is.True);
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Principal, Is.Null);
                Assert.That(cache.IsRevoked(TestUserId, TestSessionId), Is.False);
                Assert.That(cache.TryGetActive(TestUserId, TestSessionId, out _), Is.False);
                Assert.That(HasJwtFailure(logger), Is.False);
            });
        }

        [Test]
        public async Task CancellationWithoutAbortedRequest_IsNotSuppressed()
        {
            OperationCanceledException failure = new("Independent operation was canceled.");
            SessionValidationConnectionInterceptor interceptor = new(_ => throw failure);
            using NUnitLoggerProvider logger = new();
            await using ServiceProvider services = CreateServices(interceptor, logger);
            await using AsyncServiceScope scope = services.CreateAsyncScope();
            DefaultHttpContext context = CreateContext(scope.ServiceProvider, CancellationToken.None);

            OperationCanceledException? actual = Assert.ThrowsAsync<OperationCanceledException>(
                async () => await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme));

            Assert.Multiple(() =>
            {
                Assert.That(actual, Is.SameAs(failure));
                Assert.That(interceptor.Attempts, Is.EqualTo(1));
                Assert.That(HasJwtFailure(logger), Is.True);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task DatabaseFailure_IsNotSuppressed(bool abortRequest)
        {
            using CancellationTokenSource requestCancellation = new();
            InvalidOperationException failure = new("Session lookup failed.");
            SessionValidationConnectionInterceptor interceptor = new(_ =>
            {
                if (abortRequest)
                {
                    requestCancellation.Cancel();
                }
                throw failure;
            });
            using NUnitLoggerProvider logger = new();
            await using ServiceProvider services = CreateServices(interceptor, logger);
            await using AsyncServiceScope scope = services.CreateAsyncScope();
            DefaultHttpContext context = CreateContext(scope.ServiceProvider, requestCancellation.Token);

            InvalidOperationException? actual = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme));

            Assert.Multiple(() =>
            {
                Assert.That(actual, Is.SameAs(failure));
                Assert.That(interceptor.Attempts, Is.EqualTo(1));
                Assert.That(HasJwtFailure(logger), Is.True);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task CachedSession_PreservesAuthenticationDecision(bool revoked)
        {
            SessionValidationConnectionInterceptor interceptor = new(_ =>
                throw new InvalidOperationException("A cached session must not query the database."));
            using NUnitLoggerProvider logger = new();
            await using ServiceProvider services = CreateServices(interceptor, logger);
            await using AsyncServiceScope scope = services.CreateAsyncScope();
            SessionAccessTokenRevocationCache cache = services.GetRequiredService<SessionAccessTokenRevocationCache>();
            if (revoked)
            {
                cache.MarkRevoked(TestUserId, TestSessionId, TimeSpan.FromMinutes(1));
            }
            else
            {
                cache.MarkActive(TestUserId, TestSessionId, TimeSpan.FromMinutes(1));
            }
            DefaultHttpContext context = CreateContext(scope.ServiceProvider, CancellationToken.None);

            AuthenticateResult result = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);

            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Is.EqualTo(!revoked));
                Assert.That(result.None, Is.False);
                Assert.That(interceptor.Attempts, Is.Zero);
                Assert.That(HasJwtFailure(logger), Is.False);
            });
            if (revoked)
            {
                Assert.That(result.Failure?.Message, Is.EqualTo("Session has been revoked."));
            }
        }

        private static ServiceProvider CreateServices(
            SessionValidationConnectionInterceptor interceptor,
            NUnitLoggerProvider logger)
        {
            ServiceCollection services = new();
            services.AddLogging(logging => logging.AddProvider(logger).SetMinimumLevel(LogLevel.Debug));
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuer = TestIssuer,
                        ValidAudience = TestAudience,
                        IssuerSigningKey = SigningKey,
                    };
                });
            services.AddDbContext<CottonDbContext>(options => options
                .UseNpgsql("Host=localhost;Database=unused;Username=unused")
                .AddInterceptors(interceptor));
            services.AddSingleton<IDatabaseIntegrityVerifier, UnexpectedDatabaseIntegrityVerifier>();
            services.AddAuthHardening();
            return services.BuildServiceProvider();
        }

        private static DefaultHttpContext CreateContext(IServiceProvider services, CancellationToken requestAborted)
        {
            DefaultHttpContext context = new()
            {
                RequestServices = services,
                RequestAborted = requestAborted,
            };
            JwtSecurityToken token = new(
                issuer: TestIssuer,
                audience: TestAudience,
                claims:
                [
                    new Claim(JwtRegisteredClaimNames.Sub, TestUserId.ToString()),
                    new Claim(JwtRegisteredClaimNames.Sid, TestSessionId),
                ],
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
            context.Request.Headers.Authorization = "Bearer " + new JwtSecurityTokenHandler().WriteToken(token);
            return context;
        }

        private static bool HasJwtFailure(NUnitLoggerProvider logger)
        {
            return logger.Messages.Any(message =>
                message.Contains(nameof(JwtBearerHandler), StringComparison.Ordinal)
                && message.Contains("Exception occurred while processing message.", StringComparison.Ordinal));
        }
    }
}
