// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Auth;
using Cotton.Server.Services;
using Microsoft.AspNetCore.Authentication;

namespace Cotton.Server.Extensions
{
    public static class DatabaseBackupAuthenticationExtensions
    {
        public static IServiceCollection AddDatabaseBackupAuthentication(this IServiceCollection services)
        {
            services.AddSingleton<DatabaseBackupTokenService>();
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, DatabaseBackupAuthenticationHandler>(
                    DatabaseBackupAuthenticationHandler.SchemeName, _ => { });
            return services;
        }
    }
}
