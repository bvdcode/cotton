// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Auth;
using Cotton.Server.Models;
using Cotton.Server.Models.Configuration;
using Cotton.Server.Services.RequestAdmission;
using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;

namespace Cotton.Server.Extensions
{
    /// <summary>
    /// Configures bounded HTTP request admission and endpoint-specific rate limits.
    /// </summary>
    public static class RequestAdmissionExtensions
    {
        /// <summary>
        /// Registers HTTP request admission policies.
        /// </summary>
        public static IServiceCollection AddRequestAdmission(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            RequestAdmissionOptions admissionOptions = configuration
                .GetSection(RequestAdmissionOptions.SectionName)
                .Get<RequestAdmissionOptions>() ?? new RequestAdmissionOptions();
            admissionOptions.Validate();
            services.AddSingleton(admissionOptions);

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status503ServiceUnavailable;
                options.GlobalLimiter = HttpRequestAdmissionPolicy.Create(admissionOptions);
                options.OnRejected = WriteCapacityRejectionAsync;

                AddEndpointPolicies(options);
            });
            return services;
        }

        /// <summary>
        /// Adds request admission middleware after authentication has identified the client.
        /// </summary>
        public static IApplicationBuilder UseRequestAdmission(this IApplicationBuilder app)
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
                new FixedWindowEndpointPolicy(GetPublicShareArchivePartition, 5));
        }

        internal static async ValueTask WriteCapacityRejectionAsync(
            OnRejectedContext context,
            CancellationToken cancellationToken)
        {
            await WriteRejectionAsync(
                context,
                "The server is processing too many concurrent requests. Retry shortly.",
                "request_capacity_exhausted",
                HttpStatusCode.ServiceUnavailable,
                cancellationToken);
        }

        internal static async ValueTask WriteEndpointRateLimitRejectionAsync(
            OnRejectedContext context,
            CancellationToken cancellationToken)
        {
            await WriteRejectionAsync(
                context,
                "Too many requests. Retry later.",
                "rate_limit_exceeded",
                HttpStatusCode.TooManyRequests,
                cancellationToken);
        }

        private static async ValueTask WriteRejectionAsync(
            OnRejectedContext context,
            string message,
            string messageCode,
            HttpStatusCode statusCode,
            CancellationToken cancellationToken)
        {
            context.HttpContext.Response.StatusCode = (int)statusCode;
            context.HttpContext.Response.Headers.RetryAfter = GetRetryAfterSeconds(context.Lease);
            CottonResult result = new()
            {
                Success = false,
                Message = message,
                MessageCode = messageCode,
                StatusCode = statusCode,
            };
            await context.HttpContext.Response.WriteAsJsonAsync(result, cancellationToken);
        }

        private static string GetPublicShareArchivePartition(HttpContext httpContext)
        {
            string token = httpContext.Request.RouteValues.TryGetValue("token", out object? routeToken)
                ? routeToken?.ToString() ?? "unknown"
                : "unknown";
            return $"{GetRemoteAddressPartition(httpContext)}:{token}";
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
