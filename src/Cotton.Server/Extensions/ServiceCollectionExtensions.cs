// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Database;
using Cotton.Server.Abstractions;
using Cotton.Database.Integrity;
using Cotton.Server.Auth;
using Cotton.Server.Models.Configuration;
using Cotton.Server.Handlers.WebDav;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Server.Services.Computation;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.Search;
using Cotton.Server.Services.Previews;
using Cotton.Server.Services.DatabaseIntegrity.Descriptors;
using Cotton.Server.Services.FileMetadata;
using Cotton.Server.Services.Startup;
using Cotton.Server.Services.WebDav;
using Cotton.TextExtraction;
using Microsoft.AspNetCore.Authentication;

namespace Cotton.Server.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddComputationServices(this IServiceCollection services)
        {
            services.AddOptions<TextIndexingOptions>()
                .Validate(options => options.MaxExtractedTextBytes > 0,
                    "TextIndexing:MaxExtractedTextBytes must be greater than zero.")
                .Validate(options => options.MaxStructuredFileBytes > 0,
                    "TextIndexing:MaxStructuredFileBytes must be greater than zero.")
                .ValidateOnStart();
            services.AddSingleton<EmbeddingDimensionCache>();
            services.AddScoped<ComputationService>();
            services.AddScoped<TeiClient>();
            services.AddScoped<TextEmbeddingChunker>();
            services.AddSingleton<IFileTextExtractor, PdfTextExtractor>();
            services.AddSingleton<IFileTextExtractor, TextFileExtractor>();
            services.AddSingleton<IFileTextExtractor, HtmlTextExtractor>();
            services.AddSingleton<IFileTextExtractor, WordDocumentTextExtractor>();
            services.AddSingleton<IFileTextExtractor, PresentationTextExtractor>();
            services.AddSingleton<IFileTextExtractor, SpreadsheetTextExtractor>();
            services.AddSingleton<IFileTextExtractor, EpubTextExtractor>();
            services.AddSingleton<IFileTextExtractor, EmailTextExtractor>();
            services.AddSingleton<IFileTextExtractor, OpenDocumentTextExtractor>();
            services.AddSingleton<IFileTextExtractor, JupyterNotebookTextExtractor>();
            services.AddSingleton<FileTextExtractorProvider>();
            services.AddHttpClient(TeiClient.RemoteClientName, client => client.Timeout = TeiClient.RequestTimeout)
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                    UseCookies = false,
                });
            return services;
        }

        public static IServiceCollection AddStreamCipher(this IServiceCollection services)
        {
            services.AddSingleton<ServerSettingsCache>();
            services.AddSingleton<DatabaseFieldProtector>();
#pragma warning disable CS0618 // TEMPORARY 0.5 RECOVERY: remove after the upgrade window.
            services.AddSingleton<LegacyZeroKeySettingsRecovery>();
            services.AddSingleton<IDatabaseFieldProtector>(sp =>
                sp.GetRequiredService<LegacyZeroKeySettingsRecovery>());
#pragma warning restore CS0618
            return services.AddScoped<IStreamCipher>(sp =>
            {
                CottonEncryptionSettings settings = sp.GetRequiredService<CottonEncryptionSettings>();
                ServerSettingsCache cache = sp.GetRequiredService<ServerSettingsCache>();
                return StreamCipherFactory.Create(settings, cache.GetEncryptionThreads());
            });
        }

        public static IServiceCollection AddWebDavServices(this IServiceCollection services)
        {
            services.AddScoped<IWebDavPathResolver, WebDavPathResolver>();
            services.AddSingleton<WebDavLockManager>();
            services.AddScoped<WebDavPutContentReader>();
            return services;
        }

        public static IServiceCollection AddChunkServices(this IServiceCollection services)
        {
            services.AddScoped<FilePreviewRenderer>();
            services.AddScoped<IChunkIngestService, ChunkIngestService>();
            services.AddScoped<NodeFileHistoryService>();
            services.AddScoped<FileVersionStorageService>();
            services.AddScoped<FileVersionRetentionService>();
            services.AddScoped<FileVersionService>();
            services.AddScoped<IEventNotificationService, EventNotificationService>();
            services.AddScoped<ISyncChangeRecorder, SyncChangeRecorder>();
            return services;
        }

        public static IServiceCollection AddDatabaseIntegrity(this IServiceCollection services)
        {
            services.AddSingleton<DatabaseIntegrityKeyProvider>();
            services.AddSingleton<IDatabaseIntegrityProtector, DatabaseIntegrityProtector>();
            services.AddSingleton<IDatabaseIntegrityDescriptorRegistry, DatabaseIntegrityDescriptorRegistry>();
            services.AddScoped<IDatabaseIntegrityChangeSigner, DatabaseIntegrityChangeSigner>();
            services.AddScoped<DatabaseIntegritySaveChangesInterceptor>();
            services.AddScoped<IDatabaseIntegrityVerifier, DatabaseIntegrityVerifier>();
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
            services.AddSingleton<IDatabaseIntegrityDescriptor, NodeIntegrityDescriptor>();
            services.AddSingleton<IDatabaseIntegrityDescriptor, NodeFileIntegrityDescriptor>();
            services.AddSingleton<IDatabaseIntegrityDescriptor, FileManifestIntegrityDescriptor>();
            services.AddSingleton<IDatabaseIntegrityDescriptor, FileManifestChunkIntegrityDescriptor>();
            services.AddSingleton<IDatabaseIntegrityDescriptor, ChunkIntegrityDescriptor>();

            return services;
        }

        public static IServiceCollection AddStartupValidation(this IServiceCollection services)
        {
            services.AddSingleton<TempDirectoryProbe>();
            services.AddScoped<StartupPreflightValidator>();
            services.AddScoped<IStartupCheck, TempDirectoryStartupCheck>();

            return services;
        }

        public static IServiceCollection AddLayoutSearchProviders(this IServiceCollection services)
        {
            services.AddScoped<ILayoutSearchProvider, NameLayoutSearchProvider>();
            return services;
        }

        public static IServiceCollection AddFileContentMetadataServices(this IServiceCollection services)
        {
            services.AddScoped<FileContentMetadataExtractorProvider>();
            services.AddScoped<IFileContentMetadataExtractor, ImageFileContentMetadataExtractor>();
            services.AddScoped<IFileContentMetadataExtractor, MediaFileContentMetadataExtractor>();
            return services;
        }

        public static IServiceCollection AddWebDavAuth(this IServiceCollection services)
        {
            services.AddSingleton<Cotton.Server.Services.WebDav.WebDavAuthCache>();
            services.AddSingleton<WebDavAuthenticationFailureLimiter>();

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
