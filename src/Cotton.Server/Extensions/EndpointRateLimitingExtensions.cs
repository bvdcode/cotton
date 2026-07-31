// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Auth;
using Cotton.Server.Models;
using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;

namespace Cotton.Server.Extensions
{
    /// <summary>
    /// Configures rate limits for endpoints exposed to credential abuse.
    /// </summary>
    public static class EndpointRateLimitingExtensions
    {
        /// <summary>
        /// Registers endpoint-specific abuse rate limits.
        /// </summary>
        public static IServiceCollection AddEndpointRateLimiting(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                AddEndpointPolicies(options);
            });
            return services;
        }

        /// <summary>
        /// Adds endpoint rate-limit middleware.
        /// </summary>
        public static IApplicationBuilder UseEndpointRateLimiting(this IApplicationBuilder app)
        {
            return app.UseRateLimiter();
        }

        private static void AddEndpointPolicies(RateLimiterOptions options)
        {
            options.AddPolicy(
                AuthRateLimitPolicies.Interactive,
                new FixedWindowEndpointPolicy(GetRemoteAddressPartition, 10));
            options.AddPolicy(
                AuthRateLimitPolicies.Refresh,
                new FixedWindowEndpointPolicy(GetRemoteAddressPartition, 60));
            options.AddPolicy(
                AuthRateLimitPolicies.PublicShareLookup,
                new FixedWindowEndpointPolicy(GetRemoteAddressPartition, 60));
            options.AddPolicy(
                AuthRateLimitPolicies.PublicShareArchive,
                new FixedWindowEndpointPolicy(GetRemoteAddressPartition, 5));
        }

        internal static async ValueTask WriteEndpointRateLimitRejectionAsync(
            OnRejectedContext context,
            CancellationToken cancellationToken)
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.Headers.RetryAfter = GetRetryAfterSeconds(context.Lease);
            CottonResult result = new()
            {
                Success = false,
                Message = "Too many requests. Retry later.",
                MessageCode = "rate_limit_exceeded",
                StatusCode = HttpStatusCode.TooManyRequests,
            };
            await context.HttpContext.Response.WriteAsJsonAsync(result, cancellationToken);
        }

        private static string GetRemoteAddressPartition(HttpContext httpContext)
        {
            return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private static string GetRetryAfterSeconds(RateLimitLease lease)
        {
            if (lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter)
                && retryAfter > TimeSpan.Zero)
            {
                int seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                return seconds.ToString(CultureInfo.InvariantCulture);
            }

            return "1";
        }

        private sealed class FixedWindowEndpointPolicy(
            Func<HttpContext, string> getPartitionKey,
            int permitLimit) : IRateLimiterPolicy<string>
        {
            public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected =>
                WriteEndpointRateLimitRejectionAsync;

            public RateLimitPartition<string> GetPartition(HttpContext httpContext)
            {
                return RateLimitPartition.GetFixedWindowLimiter(
                    getPartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = permitLimit,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1),
                    });
            }
        }
    }
}
