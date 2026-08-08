// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Auth;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;
using System.Net;

namespace Cotton.Server.IntegrationTests
{
    public class WebDavAuthenticationFailureLimiterTests
    {
        [Test]
        public async Task RecordFailure_AccountsForConcurrentAttemptsExactly()
        {
            using MemoryCache cache = new(new MemoryCacheOptions());
            WebDavAuthenticationFailureLimiter limiter = new(cache);
            using ManualResetEventSlim start = new(initialState: false);
            Task<bool>[] attempts = Enumerable.Range(0, 100)
                .Select(_ => Task.Run(() =>
                {
                    start.Wait();
                    return limiter.RecordFailure(IPAddress.Loopback, "testuser");
                }))
                .ToArray();

            start.Set();
            bool[] results = await Task.WhenAll(attempts);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(results.Count(limited => !limited), Is.EqualTo(10));
                Assert.That(results.Count(limited => limited), Is.EqualTo(90));
                Assert.That(limiter.IsLimited(IPAddress.Loopback, "testuser"), Is.True);
            }
        }

        [Test]
        public void Clear_ResetsOnlyMatchingPartition()
        {
            using MemoryCache cache = new(new MemoryCacheOptions());
            WebDavAuthenticationFailureLimiter limiter = new(cache);
            IPAddress firstAddress = IPAddress.Parse("192.0.2.1");
            IPAddress secondAddress = IPAddress.Parse("192.0.2.2");

            for (int attempt = 0; attempt < WebDavAuthenticationFailureLimiter.FailedAttemptLimit; attempt++)
            {
                limiter.RecordFailure(firstAddress, "TestUser");
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(limiter.IsLimited(firstAddress, "testuser"), Is.True);
                Assert.That(limiter.IsLimited(secondAddress, "testuser"), Is.False);
            }

            limiter.Clear(firstAddress, "TESTUSER");

            Assert.That(limiter.IsLimited(firstAddress, "testuser"), Is.False);
        }
    }
}
