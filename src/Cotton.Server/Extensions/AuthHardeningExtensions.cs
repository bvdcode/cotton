// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Auth;
using Cotton.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Cotton.Server.Extensions
{
    public static class AuthHardeningExtensions
    {
        private const string RobotsTagHeader = "X-Robots-Tag";
        private const string RobotsNoIndexValue = "noindex";

        public static IServiceCollection AddAuthHardening(this IServiceCollection services)
        {
            services.AddSingleton<SessionAccessTokenRevocationCache>();
            services.AddScoped<SessionAccessTokenRevocationStore>();
            services.AddScoped<ISessionRevocationPublisher, SignalRSessionRevocationPublisher>();
            services.AddSessionRevocationValidation();
            return services;
        }

        public static IApplicationBuilder UseAuthHardening(this IApplicationBuilder app)
        {
            return app.UseSearchEngineExclusion();
        }

        private static IApplicationBuilder UseSearchEngineExclusion(this IApplicationBuilder app)
        {
            return app.Use((context, next) =>
            {
                context.Response.OnStarting(static state =>
                {
                    HttpResponse response = (HttpResponse)state;
                    response.Headers[RobotsTagHeader] = RobotsNoIndexValue;
                    return Task.CompletedTask;
                }, context.Response);
                return next();
            });
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

                    SessionAccessTokenRevocationStore revocations = context.HttpContext.RequestServices
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
    }
}
