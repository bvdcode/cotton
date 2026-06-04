// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Abstractions;
using Cotton.Database.Integrity;
using Cotton.Server.Auth;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.Search;
using Cotton.Server.Services.DatabaseIntegrity.Descriptors;
using Cotton.Server.Services.WebDav;
using EasyExtensions.Abstractions;
using Microsoft.AspNetCore.Authentication;

namespace Cotton.Server.Extensions
{
    /// <summary>
    /// Contains extension methods for configuring service collection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers stream cipher services.
        /// </summary>
        public static IServiceCollection AddStreamCipher(this IServiceCollection services)
        {
            return services.AddScoped<IStreamCipher>(sp =>
            {
                var settings = sp.GetRequiredService<CottonEncryptionSettings>();
                return StreamCipherFactory.Create(settings, SettingsProvider.GetCachedEncryptionThreads());
            });
        }

        /// <summary>
        /// Registers web dav services services.
        /// </summary>
        public static IServiceCollection AddWebDavServices(this IServiceCollection services)
        {
            services.AddScoped<IWebDavPathResolver, WebDavPathResolver>();
            return services;
        }

        /// <summary>
        /// Registers chunk services services.
        /// </summary>
        public static IServiceCollection AddChunkServices(this IServiceCollection services)
        {
            services.AddScoped<IChunkIngestService, ChunkIngestService>();
            services.AddScoped<NodeFileHistoryService>();
            services.AddScoped<FileVersionStorageService>();
            services.AddScoped<FileVersionRetentionService>();
            services.AddScoped<FileVersionService>();
            services.AddScoped<IEventNotificationService, EventNotificationService>();
            services.AddScoped<ISyncChangeRecorder, SyncChangeRecorder>();
            services.AddScoped<SyncChangeRetentionService>();
            return services;
        }

        /// <summary>
        /// Registers database integrity services.
        /// </summary>
        public static IServiceCollection AddDatabaseIntegrity(this IServiceCollection services)
        {
            services.AddSingleton<IDatabaseIntegrityKeyProvider, DatabaseIntegrityKeyProvider>();
            services.AddSingleton<IDatabaseIntegrityProtector, DatabaseIntegrityProtector>();
            services.AddSingleton<IDatabaseIntegrityDescriptorRegistry, DatabaseIntegrityDescriptorRegistry>();
            services.AddScoped<IDatabaseIntegrityChangeSigner, DatabaseIntegrityChangeSigner>();
            services.AddScoped<IDatabaseIntegrityVerifier, DatabaseIntegrityVerifier>();
            services.AddScoped<IDatabaseIntegrityBridgeBackfillService, DatabaseIntegrityBridgeBackfillService>();
            services.AddScoped<DatabaseIntegrityDiagnosticsService>();
            services.AddScoped<FileGraphIntegrityVerifier>();
            services.AddSingleton<DatabaseIntegrityFailureReporter>();
            services.AddSingleton<IDatabaseIntegrityFailureReporter>(sp =>
                sp.GetRequiredService<DatabaseIntegrityFailureReporter>());
            services.AddHostedService(sp => sp.GetRequiredService<DatabaseIntegrityFailureReporter>());

            services.AddSingleton<IDatabaseIntegrityDescriptor, UserIntegrityDescriptor>();
            services.AddSingleton<IDatabaseIntegrityDescriptor, UserPasskeyCredentialIntegrityDescriptor>();
            services.AddSingleton<IDatabaseIntegrityDescriptor, OidcProviderIntegrityDescriptor>();
            services.AddSingleton<IDatabaseIntegrityDescriptor, UserExternalIdentityIntegrityDescriptor>();
            services.AddSingleton<IDatabaseIntegrityDescriptor, OidcLoginStateIntegrityDescriptor>();
            services.AddSingleton<IDatabaseIntegrityDescriptor, ExtendedRefreshTokenIntegrityDescriptor>();
            services.AddSingleton<IDatabaseIntegrityDescriptor, DownloadTokenIntegrityDescriptor>();
            services.AddSingleton<IDatabaseIntegrityDescriptor, NodeShareTokenIntegrityDescriptor>();
            services.AddSingleton<IDatabaseIntegrityDescriptor, CottonServerSettingsIntegrityDescriptor>();
            services.AddSingleton<IDatabaseIntegrityDescriptor, NodeIntegrityDescriptor>();
            services.AddSingleton<IDatabaseIntegrityDescriptor, NodeFileIntegrityDescriptor>();
            services.AddSingleton<IDatabaseIntegrityDescriptor, FileManifestIntegrityDescriptor>();
            services.AddSingleton<IDatabaseIntegrityDescriptor, FileManifestChunkIntegrityDescriptor>();
            services.AddSingleton<IDatabaseIntegrityDescriptor, ChunkIntegrityDescriptor>();

            return services;
        }

        /// <summary>
        /// Registers layout path services services.
        /// </summary>
        public static IServiceCollection AddLayoutPathServices(this IServiceCollection services)
        {
            services.AddScoped<ILayoutPathResolver, LayoutPathResolver>();
            return services;
        }

        /// <summary>
        /// Registers layout search services.
        /// </summary>
        public static IServiceCollection AddLayoutSearchServices(this IServiceCollection services)
        {
            services.AddScoped<ILayoutSearchService, LayoutSearchService>();
            services.AddScoped<ILayoutSearchProvider, NameLayoutSearchProvider>();
            services.AddScoped<ILayoutSearchProvider, NoOpVectorLayoutSearchProvider>();
            return services;
        }

        /// <summary>
        /// Registers web dav auth services.
        /// </summary>
        public static IServiceCollection AddWebDavAuth(this IServiceCollection services)
        {
            services.AddSingleton<Cotton.Server.Services.WebDav.WebDavAuthCache>();

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
