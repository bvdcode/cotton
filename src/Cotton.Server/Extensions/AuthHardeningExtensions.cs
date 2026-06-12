// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Auth;
using Cotton.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Cotton.Server.Extensions
{
    /// <summary>
    /// Contains extension methods for configuring auth hardening.
    /// </summary>
    public static class AuthHardeningExtensions
    {
        /// <summary>
        /// Registers auth hardening services.
        /// </summary>
        public static IServiceCollection AddAuthHardening(this IServiceCollection services)
        {
            services.AddSingleton<SessionAccessTokenRevocationCache>();
            services.AddScoped<SessionAccessTokenRevocationStore>();
            services.AddAuthRateLimiting();
            services.AddSessionRevocationValidation();
            return services;
        }

        /// <summary>
        /// Adds auth hardening middleware to the application pipeline.
        /// </summary>
        public static IApplicationBuilder UseAuthHardening(this IApplicationBuilder app)
        {
            return app.UseRateLimiter();
        }

        private static IServiceCollection AddAuthRateLimiting(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
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
            });
            return services;
        }

        private static IServiceCollection AddSessionRevocationValidation(this IServiceCollection services)
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                JwtBearerEvents events = options.Events ?? new JwtBearerEvents();
                Func<TokenValidatedContext, Task> existingHandler = events.OnTokenValidated;
                events.OnTokenValidated = async context =>
                {
                    await existingHandler(context);
                    if (context.Result is not null)
                    {
                        return;
                    }

                    string? userIdValue = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                        ?? context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    string? sessionId = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sid)
                        ?? context.Principal?.FindFirstValue(ClaimTypes.Sid);
                    if (!Guid.TryParse(userIdValue, out Guid userId) || string.IsNullOrWhiteSpace(sessionId))
                    {
                        context.Fail("Access token is missing required session claims.");
                        return;
                    }

                    var revocations = context.HttpContext.RequestServices
                        .GetRequiredService<SessionAccessTokenRevocationStore>();
                    bool isRevoked = await revocations.IsRevokedAsync(
                        userId,
                        sessionId,
                        context.HttpContext.RequestAborted);
                    if (isRevoked)
                    {
                        context.Fail("Session has been revoked.");
                    }
                };
                options.Events = events;
            });
            return services;
        }

        private static string GetRemoteAddressPartition(HttpContext httpContext)
        {
            return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
