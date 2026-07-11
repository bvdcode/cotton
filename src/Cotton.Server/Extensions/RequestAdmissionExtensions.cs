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
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = HttpRequestAdmissionPolicy.Create(admissionOptions);
                options.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.Headers.RetryAfter = GetRetryAfterSeconds(context.Lease);
                    CottonResult result = new()
                    {
                        Success = false,
                        Message = "The server is processing too many concurrent requests. Retry shortly.",
                        MessageCode = "request_capacity_exhausted",
                        StatusCode = HttpStatusCode.TooManyRequests,
                    };
                    await context.HttpContext.Response.WriteAsJsonAsync(result, cancellationToken);
                };

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
            options.AddPolicy(AuthRateLimitPolicies.Interactive, httpContext =>
            {
                string partitionKey = GetRemoteAddressPartition(httpContext);
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 10,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1),
                    });
            });
            options.AddPolicy(AuthRateLimitPolicies.Refresh, httpContext =>
            {
                string partitionKey = GetRemoteAddressPartition(httpContext);
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 60,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1),
                    });
            });
            options.AddPolicy(AuthRateLimitPolicies.PublicShareArchive, httpContext =>
            {
                string token = httpContext.Request.RouteValues.TryGetValue("token", out object? routeToken)
                    ? routeToken?.ToString() ?? "unknown"
                    : "unknown";
                string partitionKey = $"{GetRemoteAddressPartition(httpContext)}:{token}";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 5,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1),
                    });
            });
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
    }
}
