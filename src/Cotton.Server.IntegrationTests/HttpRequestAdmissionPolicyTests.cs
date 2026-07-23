// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Controllers;
using Cotton.Server.Models.Configuration;
using Cotton.Server.Services.RequestAdmission;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using NUnit.Framework;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Cotton.Server.IntegrationTests
{
    public class HttpRequestAdmissionPolicyTests
    {
        [Test]
        public void PreviewController_DisablesRateLimitingInFavorOfItsSemaphoreQueue()
        {
            DisableRateLimitingAttribute? attribute =
                typeof(PreviewController).GetCustomAttribute<DisableRateLimitingAttribute>();

            Assert.That(attribute, Is.Not.Null);
        }

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
        public async Task Create_RejectsAnonymousRequestsAbovePerClientLimitForSameRemoteAddress()
        {
            RequestAdmissionOptions options = new()
            {
                GlobalConcurrentRequestLimit = 2,
                ClientConcurrentRequestLimit = 1,
            };
            await using PartitionedRateLimiter<HttpContext> limiter = HttpRequestAdmissionPolicy.Create(options);
            DefaultHttpContext firstContext = CreateAnonymousContext("192.0.2.10");
            DefaultHttpContext secondContext = CreateAnonymousContext("192.0.2.10");
            DefaultHttpContext otherClientContext = CreateAnonymousContext("198.51.100.10");

            using RateLimitLease first = await limiter.AcquireAsync(firstContext);
            using RateLimitLease rejected = await limiter.AcquireAsync(secondContext);
            using RateLimitLease otherClient = await limiter.AcquireAsync(otherClientContext);

            Assert.Multiple(() =>
            {
                Assert.That(first.IsAcquired, Is.True);
                Assert.That(rejected.IsAcquired, Is.False);
                Assert.That(otherClient.IsAcquired, Is.True);
            });
        }

        [Test]
        public async Task Create_RejectsAnonymousRequestsAboveGlobalLimitAcrossRemoteAddresses()
        {
            RequestAdmissionOptions options = new()
            {
                GlobalConcurrentRequestLimit = 2,
                ClientConcurrentRequestLimit = 1,
            };
            await using PartitionedRateLimiter<HttpContext> limiter = HttpRequestAdmissionPolicy.Create(options);

            using RateLimitLease first = await limiter.AcquireAsync(CreateAnonymousContext("192.0.2.10"));
            using RateLimitLease second = await limiter.AcquireAsync(CreateAnonymousContext("198.51.100.10"));
            using RateLimitLease rejected = await limiter.AcquireAsync(CreateAnonymousContext("203.0.113.10"));

            Assert.Multiple(() =>
            {
                Assert.That(first.IsAcquired, Is.True);
                Assert.That(second.IsAcquired, Is.True);
                Assert.That(rejected.IsAcquired, Is.False);
            });
        }

        [Test]
        public async Task Create_NormalizesIpv4MappedAnonymousRemoteAddress()
        {
            RequestAdmissionOptions options = new()
            {
                GlobalConcurrentRequestLimit = 2,
                ClientConcurrentRequestLimit = 1,
            };
            await using PartitionedRateLimiter<HttpContext> limiter = HttpRequestAdmissionPolicy.Create(options);
            DefaultHttpContext ipv4Context = CreateAnonymousContext("192.0.2.10");
            DefaultHttpContext ipv4MappedContext = CreateAnonymousContext("::ffff:192.0.2.10");

            using RateLimitLease first = await limiter.AcquireAsync(ipv4Context);
            using RateLimitLease rejected = await limiter.AcquireAsync(ipv4MappedContext);

            Assert.Multiple(() =>
            {
                Assert.That(first.IsAcquired, Is.True);
                Assert.That(rejected.IsAcquired, Is.False);
            });
        }

        [Test]
        public async Task Create_WebDavBasicRequestsWithoutDefaultAuthenticationUseRemoteAddressLimit()
        {
            RequestAdmissionOptions options = new()
            {
                GlobalConcurrentRequestLimit = 2,
                ClientConcurrentRequestLimit = 1,
            };
            await using PartitionedRateLimiter<HttpContext> limiter = HttpRequestAdmissionPolicy.Create(options);
            DefaultHttpContext firstContext = CreateWebDavBasicContext("192.0.2.10");
            DefaultHttpContext secondContext = CreateWebDavBasicContext("192.0.2.10");

            using RateLimitLease first = await limiter.AcquireAsync(firstContext);
            using RateLimitLease rejected = await limiter.AcquireAsync(secondContext);

            Assert.Multiple(() =>
            {
                Assert.That(first.IsAcquired, Is.True);
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

        private static DefaultHttpContext CreateAnonymousContext(string remoteIpAddress)
        {
            return CreateAnonymousContext(IPAddress.Parse(remoteIpAddress));
        }

        private static DefaultHttpContext CreateAnonymousContext(IPAddress remoteIpAddress)
        {
            DefaultHttpContext context = new();
            context.Connection.RemoteIpAddress = remoteIpAddress;
            return context;
        }

        private static DefaultHttpContext CreateWebDavBasicContext(string remoteIpAddress)
        {
            DefaultHttpContext context = CreateAnonymousContext(remoteIpAddress);
            context.Request.Headers.Authorization = "Basic dXNlcjp0b2tlbg==";
            return context;
        }
    }
}
