// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Auth;
using Cotton.Server.Models;
using EasyExtensions.AspNetCore.Extensions;
using Microsoft.AspNetCore.Mvc;
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
            services.AddSingleton<PublicShareLookupFailureLimiter>();
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
                AuthRateLimitPolicies.PublicShareArchive,
                new FixedWindowEndpointPolicy(GetRemoteAddressPartition, 5));
        }

        internal static async ValueTask WriteEndpointRateLimitRejectionAsync(
            OnRejectedContext context,
            CancellationToken cancellationToken)
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            CottonResult result = CreateRateLimitRejection(context.HttpContext.Response, context.Lease);
            await context.HttpContext.Response.WriteAsJsonAsync(result, cancellationToken);
        }

        /// <summary>
        /// Returns a 429 result when the client is already blocked from short-token lookups.
        /// </summary>
        public static IActionResult? GetPublicShareLookupBlockRejection(
            this ControllerBase controller,
            PublicShareLookupFailureLimiter limiter,
            string token)
        {
            ArgumentNullException.ThrowIfNull(controller);
            ArgumentNullException.ThrowIfNull(limiter);
            ArgumentNullException.ThrowIfNull(token);

            return GetPublicShareLookupRejection(
                controller,
                limiter.CheckAvailability(controller.Request, token));
        }

        /// <summary>
        /// Records a failed short-token lookup and returns a 429 result when its limit is exceeded.
        /// </summary>
        public static IActionResult? GetPublicShareLookupFailureRejection(
            this ControllerBase controller,
            PublicShareLookupFailureLimiter limiter,
            string token)
        {
            ArgumentNullException.ThrowIfNull(controller);
            ArgumentNullException.ThrowIfNull(limiter);
            ArgumentNullException.ThrowIfNull(token);

            return GetPublicShareLookupRejection(
                controller,
                limiter.RecordFailure(controller.Request, token));
        }

        /// <summary>
        /// Returns a public-share not-found response unless the failed lookup limit was exceeded.
        /// </summary>
        public static IActionResult ApiPublicShareNotFound(
            this ControllerBase controller,
            PublicShareLookupFailureLimiter limiter,
            string token,
            string message)
        {
            return controller.GetPublicShareLookupFailureRejection(limiter, token)
                ?? controller.ApiNotFound(message);
        }

        internal static string GetRemoteAddressPartition(HttpContext httpContext)
        {
            return httpContext.Request.GetTrustedClientIPAddress().ToString();
        }

        private static IActionResult? GetPublicShareLookupRejection(
            ControllerBase controller,
            RateLimitLease? lease)
        {
            using (lease)
            {
                return lease is null || lease.IsAcquired
                    ? null
                    : CreateRateLimitRejection(controller.Response, lease);
            }
        }

        private static CottonResult CreateRateLimitRejection(HttpResponse response, RateLimitLease lease)
        {
            response.Headers.RetryAfter = GetRetryAfterSeconds(lease);
            return new CottonResult
            {
                Success = false,
                Message = "Too many requests. Retry later.",
                MessageCode = "rate_limit_exceeded",
                StatusCode = HttpStatusCode.TooManyRequests,
            };
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

        private class FixedWindowEndpointPolicy(
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
