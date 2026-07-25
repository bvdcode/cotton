// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.AspNetCore.Authorization.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class SessionRevocationNotifierTests
    {
        [Test]
        public async Task NotifyRevokedAsync_RevokesEveryAccessSessionBeforeBestEffortPublishing()
        {
            Guid userId = Guid.NewGuid();
            string firstSessionId = "first-session";
            string secondSessionId = "second-session";
            using SessionAccessTokenRevocationCache cache = new();
            await using CottonDbContext dbContext = CreateDbContext();
            SessionAccessTokenRevocationStore store = new(dbContext, cache, new NoOpIntegrityVerifier());
            RecordingPublisher publisher = new(firstSessionId);
            SessionRevocationNotifier notifier = new(
                store,
                new TestTokenProvider(),
                publisher,
                NullLogger<SessionRevocationNotifier>.Instance);

            await notifier.NotifyRevokedAsync(
                userId,
                [firstSessionId, secondSessionId, firstSessionId, null, string.Empty],
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(cache.IsRevoked(userId, firstSessionId), Is.True);
                Assert.That(cache.IsRevoked(userId, secondSessionId), Is.True);
                Assert.That(publisher.Attempts, Is.EqualTo(new[] { firstSessionId, secondSessionId }));
            });
        }

        private static CottonDbContext CreateDbContext()
        {
            DbContextOptionsBuilder<CottonDbContext> optionsBuilder = new();
            optionsBuilder.UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused");
            return new CottonDbContext(optionsBuilder.Options);
        }

        private class RecordingPublisher(string failingSessionId) : ISessionRevocationPublisher
        {
            public List<string> Attempts { get; } = [];

            public Task PublishAsync(Guid userId, string sessionId, CancellationToken cancellationToken)
            {
                Attempts.Add(sessionId);
                return sessionId == failingSessionId
                    ? Task.FromException(new InvalidOperationException("Simulated realtime failure."))
                    : Task.CompletedTask;
            }
        }

        private class TestTokenProvider : ITokenProvider
        {
            public TimeSpan TokenLifetime => TimeSpan.FromHours(1);

            public string CreateToken(Func<ClaimBuilder, ClaimBuilder>? buildClaims = null)
            {
                throw new NotSupportedException();
            }

            public string CreateToken(IClaimProvider claimProvider)
            {
                throw new NotSupportedException();
            }

            public string CreateToken(TimeSpan lifetime, Func<ClaimBuilder, ClaimBuilder>? buildClaims = null)
            {
                throw new NotSupportedException();
            }

            public bool ValidateToken(string token)
            {
                throw new NotSupportedException();
            }
        }

        private class NoOpIntegrityVerifier : IDatabaseIntegrityVerifier
        {
            public void RequireValid<TEntity>(CottonDbContext dbContext, TEntity entity, string boundary)
                where TEntity : class
            {
            }
        }
    }
}
