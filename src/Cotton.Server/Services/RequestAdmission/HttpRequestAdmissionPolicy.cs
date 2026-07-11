// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Cotton.Server.Services.RequestAdmission
{
    /// <summary>
    /// Builds the chained process-wide and per-client HTTP concurrency policy.
    /// </summary>
    internal static class HttpRequestAdmissionPolicy
    {
        private const string GlobalPartitionKey = "server";

        public static PartitionedRateLimiter<HttpContext> Create(RequestAdmissionOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            options.Validate();

            PartitionedRateLimiter<HttpContext> perClientLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                context => CreateClientPartition(context, options.ClientConcurrentRequestLimit));
            PartitionedRateLimiter<HttpContext> globalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                _ => RateLimitPartition.GetConcurrencyLimiter(
                    GlobalPartitionKey,
                    _ => CreateLimiterOptions(options.GlobalConcurrentRequestLimit)));

            return PartitionedRateLimiter.CreateChained(perClientLimiter, globalLimiter);
        }

        private static RateLimitPartition<string> CreateClientPartition(
            HttpContext context,
            int permitLimit)
        {
            ArgumentNullException.ThrowIfNull(context);

            string? userId = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                string partitionKey = $"user:{userId}";
                return RateLimitPartition.GetConcurrencyLimiter(
                    partitionKey,
                    _ => CreateLimiterOptions(permitLimit));
            }

            return RateLimitPartition.GetNoLimiter("anonymous");
        }

        private static ConcurrencyLimiterOptions CreateLimiterOptions(int permitLimit)
        {
            return new ConcurrencyLimiterOptions
            {
                PermitLimit = permitLimit,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            };
        }
    }
}
