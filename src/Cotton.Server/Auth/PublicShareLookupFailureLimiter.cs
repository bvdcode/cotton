// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using Cotton.Server.Services;
using System.Threading.RateLimiting;

namespace Cotton.Server.Auth
{
    /// <summary>
    /// Limits short public-share token lookups after repeated failures.
    /// </summary>
    public class PublicShareLookupFailureLimiter : IDisposable
    {
        private const int PermitLimit = 60;
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

        private readonly PartitionedRateLimiter<HttpRequest> _limiter;

        /// <summary>
        /// Initializes the limiter with the application trusted-proxy resolver.
        /// </summary>
        public PublicShareLookupFailureLimiter()
            : this(request => request.GetTrustedClientIPAddress().ToString())
        {
        }

        internal PublicShareLookupFailureLimiter(Func<HttpRequest, string> getPartitionKey)
        {
            ArgumentNullException.ThrowIfNull(getPartitionKey);
            _limiter = PartitionedRateLimiter.Create<HttpRequest, string>(request =>
                RateLimitPartition.GetFixedWindowLimiter(
                    getPartitionKey(request),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = PermitLimit,
                        QueueLimit = 0,
                        Window = Window,
                    }));
        }

        /// <summary>
        /// Checks whether short-token lookups are currently allowed without consuming a permit.
        /// </summary>
        public RateLimitLease? CheckAvailability(HttpRequest request, string token)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(token);
            return RequiresProtection(token)
                ? _limiter.AttemptAcquire(request, permitCount: 0)
                : null;
        }

        /// <summary>
        /// Records one failed short-token lookup and returns its rate-limit lease.
        /// </summary>
        public RateLimitLease? RecordFailure(HttpRequest request, string token)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(token);
            return RequiresProtection(token)
                ? _limiter.AttemptAcquire(request)
                : null;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _limiter.Dispose();
            GC.SuppressFinalize(this);
        }

        private static bool RequiresProtection(string token)
        {
            return token.Length < PublicShareTokenGenerator.ExpandedTokenLength;
        }
    }
}
