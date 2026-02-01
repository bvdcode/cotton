// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Server.Auth;
using Cotton.Server.Services;
using Cotton.Server.Services.WebDav;
using Cotton.Shared;
using EasyExtensions.Abstractions;
using EasyExtensions.Crypto;
using Microsoft.AspNetCore.Authentication;

namespace Cotton.Server.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddStreamCipher(this IServiceCollection services)
        {
            return services.AddScoped<IStreamCipher>(sp =>
            {
                var settings = sp.GetRequiredService<CottonEncryptionSettings>();
                if (string.IsNullOrWhiteSpace(settings.MasterEncryptionKey))
                {
                    throw new InvalidOperationException("MasterEncryptionKey is not configured.");
                }
                // Derive 32-byte key (SHA-256 of provided string)
                byte[] keyMaterial = Convert.FromBase64String(settings.MasterEncryptionKey);
                int keyId = settings.MasterEncryptionKeyId;
                int? threads = settings.EncryptionThreads > 0 ? settings.EncryptionThreads : null;
                return new AesGcmStreamCipher(keyMaterial, keyId, threads);
            });
        }

        public static IServiceCollection AddWebDavServices(this IServiceCollection services)
        {
            services.AddScoped<IWebDavPathResolver, WebDavPathResolver>();
            services.AddSingleton<IWebDavLockStore, WebDavInMemoryLockStore>();
            services.AddScoped<IWebDavLockGuard, WebDavLockGuard>();
            return services;
        }

        public static IServiceCollection AddChunkServices(this IServiceCollection services)
        {
            services.AddScoped<IChunkIngestService, ChunkIngestService>();
            services.AddScoped<NodeFileHistoryService>();
            return services;
        }

        public static IServiceCollection AddLayoutPathServices(this IServiceCollection services)
        {
            services.AddScoped<ILayoutPathResolver, LayoutPathResolver>();
            return services;
        }

        public static IServiceCollection AddWebDavAuth(this IServiceCollection services)
        {
            services
                .AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, WebDavBasicAuthenticationHandler>(
                    WebDavBasicAuthenticationHandler.SchemeName,
                    _ => { });

            services
                .AddAuthorizationBuilder()
                .AddPolicy(WebDavBasicAuthenticationHandler.PolicyName, policy =>
                {
                    policy.AddAuthenticationSchemes(WebDavBasicAuthenticationHandler.SchemeName);
                    policy.RequireAuthenticatedUser();
                });

            return services;
        }
    }
}
