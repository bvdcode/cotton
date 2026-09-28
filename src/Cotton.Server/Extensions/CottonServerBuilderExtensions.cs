// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Database;
using Cotton.Database.Integrity;
using Cotton.Server.Abstractions;
using Cotton.Server.Mappings;
using Cotton.Server.Models.Configuration;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Server.Services.Passkeys;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using Cotton.Storage.Processors;
using Cotton.Topology;
using Cotton.Topology.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Extensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using EasyExtensions.Quartz.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cotton.Server.Extensions
{
    public static class CottonServerBuilderExtensions
    {
        public static WebApplicationBuilder AddCottonServer(
            this WebApplicationBuilder builder,
            CottonEncryptionSettings encryptionSettings,
            MasterKeyRuntimeState masterKeyRuntimeState,
            ProcessHardeningStatus processHardeningStatus)
        {
            builder.Configuration.AddCottonOptions(encryptionSettings);
            builder.ConfigureHttp2Transport();
            if (OperatingSystem.IsWindows() && !builder.Environment.IsProduction())
            {
                builder.Logging.ClearProviders();
                builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
                builder.Logging.AddConsole();
                builder.Logging.AddDebug();
            }
            builder.Logging.AddFilter(
                "Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager",
                LogLevel.Error);
            MapsterConfig.Register();
            builder.Services.AddHttpClient(AppVersionTrackerService.GitHubHttpClientName, client =>
            {
                client.BaseAddress = new Uri("https://api.github.com/");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Cotton/1.0");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            });
            builder.Services.AddHttpClient(OidcDiscoveryService.HttpClientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Cotton/1.0");
            });
            builder.Services
                .AddHttpClient<OidcAvatarImportService>(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Cotton/1.0");
                })
                .ConfigurePrimaryHttpMessageHandler(OidcAvatarConnectionPolicy.CreateHandler);
            builder.Services.AddCottonBridgeClients();
            builder.Services.AddHttpClient<IGeoLookupService, GeoLookupService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
            });
            builder.Services
                .AddHttpClient<IProxyTopologyProbeService, ProxyTopologyProbeService>(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(3);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Cotton-Proxy-Topology-Probe/1.0");
                })
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                    UseCookies = false,
                });
            builder.Services
                .AddExceptionHandler<UntrustedProxyConnectionExceptionHandler>()
                .AddExceptionHandler()
                .AddOptions<CottonEncryptionSettings>()
                .Bind(builder.Configuration);
            builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<CottonEncryptionSettings>>().Value);
            builder.Services.AddSingleton(masterKeyRuntimeState);
            builder.Services.AddSingleton(processHardeningStatus);
            builder.Services.AddSingleton(new ApplicationStartupClock(DateTimeOffset.UtcNow));
            builder.Services
                .AddOptions<HlsSegmentCacheOptions>()
                .Bind(builder.Configuration.GetSection("HlsSegmentCache"));
            builder.Services
                .AddOptions<ResourceConcurrencyOptions>()
                .Bind(builder.Configuration.GetSection(ResourceConcurrencyOptions.SectionName))
                .Validate(
                    options => options.HlsTranscodes > 0,
                    "ResourceConcurrency:HlsTranscodes must be greater than zero.")
                .Validate(
                    options => options.HlsProbes > 0,
                    "ResourceConcurrency:HlsProbes must be greater than zero.")
                .Validate(
                    options => options.ArchiveStreams > 0,
                    "ResourceConcurrency:ArchiveStreams must be greater than zero.")
                .Validate(
                    options => options.StorageWrites > 0,
                    "ResourceConcurrency:StorageWrites must be greater than zero.")
                .ValidateOnStart();
            builder.Services
                .AddOptions<StoragePressureOptions>()
                .Bind(builder.Configuration.GetSection("StoragePressure"));
            builder.Services
                .AddOptions<TextIndexingOptions>()
                .Bind(builder.Configuration.GetSection(TextIndexingOptions.SectionName));
            builder.Services
                .AddMediator()
                .AddQuartzJobs()
                .AddMemoryCache()
                .AddSignalR().Services
                .AddHttpContextAccessor()
                .AddSingleton<PerfTracker>()
                .AddSingleton<IStorageBackendTypeCache, StorageBackendTypeCache>()
                .AddSingleton<S3ConfigurationValidator>()
                .AddScoped<SettingsProvider>()
                .AddScoped<ServerSettingsValidator>()
                .AddScoped<SecurityDiagnosticsService>()
                .AddScoped<StoragePipelineProbeService>()
                .AddScoped<IPasskeyClientFactory, PasskeyClientFactory>()
                .AddScoped<AuthSessionIssuer>()
                .AddScoped(sp => new OidcDiscoveryService(sp.GetRequiredService<IHttpClientFactory>().CreateClient(OidcDiscoveryService.HttpClientName)))
                .AddScoped<RefreshTokenRevocationService>()
                .AddScoped<SessionRevocationNotifier>()
                .AddScoped<DownloadTokenExpirationService>()
                .AddScoped<IPostgresDumpService, PostgresDumpService>()
                .AddScoped<IDatabaseBackupManifestService, DatabaseBackupManifestService>()
                .AddScoped<DatabaseAutoRestoreService>()
                .AddScoped<FileManifestService>()
                .AddSingleton<UserStorageQuotaCache>()
                .AddSingleton<UserStorageQuotaMutationGate>()
                .AddSingleton<AppCodeRequestStore>()
                .AddScoped<UserStorageQuotaService>()
                .AddScoped<PublicShareTokenGenerator>()
                .AddSingleton<ArchiveDownloadTicketStore>()
                .AddSingleton<StoredZipArchiveWriter>()
                .AddScoped<StoragePressureGuard>()
                .AddScoped<DefaultUserContentSeeder>()
                .AddScoped<ChunkUsageService>()
                .AddScoped<StorageUsageStatsService>()
                .AddScoped<VideoTranscoder>()
                .AddSingleton<HlsTranscodeCoordinator>()
                .AddSingleton<HlsSegmentCache>()
                .AddSingleton<DatabaseBackupKeyProvider>()
                .AddScoped<IS3Provider, S3Provider>()
                .AddScoped<INotificationsProvider, CottonNotifications>()
                .AddScoped<ISharedFileDownloadNotifier, SharedFileDownloadNotifier>()
                .AddScoped<NodeSubtreeService>()
                .AddScoped<TrashRestoreCoordinator>()
                .AddScoped<IEncryptionChunkSizeProvider, SettingsEncryptionChunkSizeProvider>()
                .AddScoped<ICompressionLevelProvider, SettingsCompressionLevelProvider>()
                .AddScoped<IStorageProcessor, CryptoProcessor>()
                .AddScoped<IStorageProcessor, CompressionProcessor>()
                .AddSingleton(sp =>
                {
                    ResourceConcurrencyOptions options = sp
                        .GetRequiredService<IOptions<ResourceConcurrencyOptions>>()
                        .Value;
                    return new StorageWriteAdmissionGate(options.StorageWrites);
                })
                .AddScoped<IStoragePipeline, FileStoragePipeline>()
                .AddSingleton<StorageBackendFactory>()
                .AddScoped<IStorageBackendProvider, StorageBackendProvider>()
                .AddScoped<MasterKeyValidator>()
                .AddScoped<MasterKeyStartupValidator>()
                .AddPostgresDbContext<CottonDbContext>(
                    x => x.UseLazyLoadingProxies = false,
                    (sp, options) => options.AddInterceptors(
                        sp.GetRequiredService<DatabaseIntegritySaveChangesInterceptor>()))
                .AddSingleton<ILayoutMutationGate, LayoutMutationGate>()
                .AddScoped<ILayoutService, StorageLayoutService>()
                .AddScoped<ILayoutNavigator, LayoutNavigator>()
                .AddPbkdf2PasswordHashService()
                .AddControllers().Services
                .AddStreamCipher()
                .AddDatabaseIntegrity()
                .AddStartupValidation()
                .AddChunkServices()
                .AddFileContentMetadataServices()
                .AddLayoutSearchProviders()
                .AddComputationServices()
                .AddWebDavServices()
                .AddWebDavAuth()
                .AddJwt();
            builder.Services.AddSessionAuthentication();
            builder.Services.AddEndpointRateLimiting();
            builder.Services.AddHostedService<AppVersionTrackerService>();

            return builder;
        }
    }
}
