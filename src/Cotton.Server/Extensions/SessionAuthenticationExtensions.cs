// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Auth;
using Cotton.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Cotton.Server.Extensions
{
    public static class SessionAuthenticationExtensions
    {
        public static IServiceCollection AddSessionAuthentication(this IServiceCollection services)
        {
            services.AddSingleton<SessionAccessTokenRevocationCache>();
            services.AddScoped<SessionAccessTokenRevocationStore>();
            services.AddScoped<ISessionRevocationPublisher, SignalRSessionRevocationPublisher>();
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                JwtBearerEvents events = options.Events ?? new JwtBearerEvents();
                SessionTokenValidatedHandler handler = new(events.OnTokenValidated);
                events.OnTokenValidated = handler.HandleAsync;
                options.Events = events;
            });
            return services;
        }
    }
}
