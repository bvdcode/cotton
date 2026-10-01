// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using EasyExtensions.AspNetCore.Authorization.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Cotton.Server.IntegrationTests
{
    public class SessionAuthenticationTests
    {
        private const string TestIssuer = "session-validation-tests";
        private const string TestAudience = "session-validation-client";
        private const string TestSessionId = "test-session";
        private static readonly Guid TestUserId = Guid.NewGuid();
        private static readonly string SigningSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        private static readonly SymmetricSecurityKey SigningKey = new(Encoding.UTF8.GetBytes(SigningSecret));

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
            NUnitLoggerProvider logger,
            Action<JwtBearerOptions>? configureOptions = null)
        {
            ServiceCollection services = new();
            services.AddLogging(logging => logging.AddProvider(logger).SetMinimumLevel(LogLevel.Debug));
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JwtSettings:Key"] = SigningSecret,
                    ["JwtSettings:Issuer"] = TestIssuer,
                    ["JwtSettings:Audience"] = TestAudience,
                }).Build());
            services.AddJwt();
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.MapInboundClaims = false;
                configureOptions?.Invoke(options);
            });
            services.AddDbContext<CottonDbContext>(options => options
                .UseNpgsql("Host=localhost;Database=unused;Username=unused")
                .AddInterceptors(interceptor));
            services.AddSingleton<IDatabaseIntegrityVerifier, UnexpectedDatabaseIntegrityVerifier>();
            services.AddSessionAuthentication();
            return services.BuildServiceProvider();
        }

        private static DefaultHttpContext CreateContext(
            IServiceProvider services,
            CancellationToken requestAborted,
            Claim[]? claims = null)
        {
            DefaultHttpContext context = new()
            {
                RequestServices = services,
                RequestAborted = requestAborted,
            };
            JwtSecurityToken token = new(
                issuer: TestIssuer,
                audience: TestAudience,
                claims: claims ??
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
