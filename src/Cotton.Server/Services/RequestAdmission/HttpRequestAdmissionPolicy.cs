// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
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
        private const string UnknownAnonymousPartitionKey = "anonymous:unknown";

        public static PartitionedRateLimiter<HttpContext> Create(RequestAdmissionOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            options.Validate();

            PartitionedRateLimiter<HttpContext> perClientEnvelopeLimiter =
                PartitionedRateLimiter.Create<HttpContext, string>(
                    context => CreateClientPartition(
                        context,
                        checked(options.ClientConcurrentRequestLimit + options.ClientQueueLimit),
                        queueLimit: 0));
            PartitionedRateLimiter<HttpContext> globalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                _ => RateLimitPartition.GetConcurrencyLimiter(
                    GlobalPartitionKey,
                    _ => CreateLimiterOptions(
                        options.GlobalConcurrentRequestLimit,
                        options.GlobalQueueLimit)));
            PartitionedRateLimiter<HttpContext> perClientExecutionLimiter =
                PartitionedRateLimiter.Create<HttpContext, string>(
                    context => CreateClientPartition(
                        context,
                        options.ClientConcurrentRequestLimit,
                        options.ClientQueueLimit));

            // The envelope keeps one client from occupying the whole global queue. The global
            // limiter then provides a strict process-wide bound, while the final limiter queues
            // ordinary bursts above the per-client execution limit.
            return PartitionedRateLimiter.CreateChained(
                perClientEnvelopeLimiter,
                globalLimiter,
                perClientExecutionLimiter);
        }

        private static RateLimitPartition<string> CreateClientPartition(
            HttpContext context,
            int permitLimit,
            int queueLimit)
        {
            ArgumentNullException.ThrowIfNull(context);

            string? userId = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                string partitionKey = $"user:{userId}";
                return RateLimitPartition.GetConcurrencyLimiter(
                    partitionKey,
                    _ => CreateLimiterOptions(permitLimit, queueLimit));
            }

            return RateLimitPartition.GetConcurrencyLimiter(
                CreateAnonymousPartitionKey(context),
                _ => CreateLimiterOptions(permitLimit, queueLimit));
        }

        private static string CreateAnonymousPartitionKey(HttpContext context)
        {
            IPAddress? remoteIpAddress = context.Connection.RemoteIpAddress;
            if (remoteIpAddress is null)
            {
                return UnknownAnonymousPartitionKey;
            }

            IPAddress normalizedRemoteIpAddress = remoteIpAddress.IsIPv4MappedToIPv6
                ? remoteIpAddress.MapToIPv4()
                : remoteIpAddress;
            return $"anonymous:{normalizedRemoteIpAddress}";
        }

        private static ConcurrencyLimiterOptions CreateLimiterOptions(int permitLimit, int queueLimit)
        {
            return new ConcurrencyLimiterOptions
            {
                PermitLimit = permitLimit,
                QueueLimit = queueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            };
        }
    }
}
