// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using System.Threading.RateLimiting;

namespace Cotton.Server.Auth
{
    /// <summary>
    /// Limits failed public-share token lookups without throttling valid share traffic.
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
        /// Records one failed lookup and returns its rate-limit lease.
        /// </summary>
        public RateLimitLease AttemptAcquire(HttpRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return _limiter.AttemptAcquire(request);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _limiter.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
