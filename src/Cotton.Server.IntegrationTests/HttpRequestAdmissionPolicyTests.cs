// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Configuration;
using Cotton.Server.Services.RequestAdmission;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Cotton.Server.IntegrationTests
{
    public class HttpRequestAdmissionPolicyTests
    {
        [Test]
        public async Task Create_RejectsRequestsAbovePerClientLimitWithoutAQueue()
        {
            RequestAdmissionOptions options = new()
            {
                GlobalConcurrentRequestLimit = 4,
                ClientConcurrentRequestLimit = 1,
            };
            await using PartitionedRateLimiter<HttpContext> limiter = HttpRequestAdmissionPolicy.Create(options);
            DefaultHttpContext firstContext = CreateAuthenticatedContext("user-1");
            DefaultHttpContext secondContext = CreateAuthenticatedContext("user-1");
            DefaultHttpContext otherUserContext = CreateAuthenticatedContext("user-2");

            using RateLimitLease first = await limiter.AcquireAsync(firstContext);
            using RateLimitLease rejected = await limiter.AcquireAsync(secondContext);
            using RateLimitLease otherUser = await limiter.AcquireAsync(otherUserContext);

            Assert.That(first.IsAcquired, Is.True);
            Assert.That(rejected.IsAcquired, Is.False);
            Assert.That(otherUser.IsAcquired, Is.True);
        }

        [Test]
        public async Task Create_RejectsRequestsAboveGlobalLimitAcrossClients()
        {
            RequestAdmissionOptions options = new()
            {
                GlobalConcurrentRequestLimit = 1,
                ClientConcurrentRequestLimit = 1,
            };
            await using PartitionedRateLimiter<HttpContext> limiter = HttpRequestAdmissionPolicy.Create(options);

            using RateLimitLease first = await limiter.AcquireAsync(CreateAuthenticatedContext("user-1"));
            using RateLimitLease rejected = await limiter.AcquireAsync(CreateAuthenticatedContext("user-2"));

            Assert.That(first.IsAcquired, Is.True);
            Assert.That(rejected.IsAcquired, Is.False);
        }

        [Test]
        public async Task Create_AnonymousRequestsUseOnlyTheGlobalLimit()
        {
            RequestAdmissionOptions options = new()
            {
                GlobalConcurrentRequestLimit = 2,
                ClientConcurrentRequestLimit = 1,
            };
            await using PartitionedRateLimiter<HttpContext> limiter = HttpRequestAdmissionPolicy.Create(options);

            using RateLimitLease first = await limiter.AcquireAsync(new DefaultHttpContext());
            using RateLimitLease second = await limiter.AcquireAsync(new DefaultHttpContext());
            using RateLimitLease rejected = await limiter.AcquireAsync(new DefaultHttpContext());

            Assert.Multiple(() =>
            {
                Assert.That(first.IsAcquired, Is.True);
                Assert.That(second.IsAcquired, Is.True);
                Assert.That(rejected.IsAcquired, Is.False);
            });
        }

        private static DefaultHttpContext CreateAuthenticatedContext(string userId)
        {
            DefaultHttpContext context = new();
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)],
                "test"));
            return context;
        }
    }
}
