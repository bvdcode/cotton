// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using Cotton.Server.Services;
using System.Threading.RateLimiting;

namespace Cotton.Server.Auth
{
    public class PublicShareLookupFailureLimiter : IDisposable
    {
        private const int PermitLimit = 60;
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

        private readonly PartitionedRateLimiter<HttpRequest> _limiter;

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

        public RateLimitLease? CheckAvailability(HttpRequest request, string token)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(token);
            return RequiresProtection(token)
                ? _limiter.AttemptAcquire(request, permitCount: 0)
                : null;
        }

        public RateLimitLease? RecordFailure(HttpRequest request, string token)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(token);
            return RequiresProtection(token)
                ? _limiter.AttemptAcquire(request)
                : null;
        }

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
