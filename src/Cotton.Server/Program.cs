// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Database;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Hubs;
using Cotton.Server.Mappings;
using Cotton.Server.Models.Configuration;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Server.Services.Startup;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using Cotton.Storage.Processors;
using Cotton.Topology;
using Cotton.Topology.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Extensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.EntityFrameworkCore.Extensions;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using EasyExtensions.Quartz.Extensions;
using Microsoft.Extensions.Options;

namespace Cotton.Server
{
    /// <summary>
    /// Represents program.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Starts the Cotton server process.
        /// </summary>
        public static async Task Main(string[] args)
        {
            ConfigureProcessTimeZone();
            ProcessHardeningStatus processHardeningStatus = LinuxProcessHardening.ApplyFromEnvironment();

            CottonEncryptionSettings encryptionSettings;
            MasterKeyRuntimeState masterKeyRuntimeState;
            try
            {
                (encryptionSettings, masterKeyRuntimeState) = await ResolveEncryptionSettingsAsync(args);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await RunApplicationAsync(args, encryptionSettings, masterKeyRuntimeState, processHardeningStatus);
        }

        private static void ConfigureProcessTimeZone()
        {
            // User timezone settings are applied per request; the process clock
            // stays UTC so platform TLS/date handling cannot drift with them.
            Environment.SetEnvironmentVariable("TZ", "UTC");
            TimeZoneInfo.ClearCachedData();
        }

        private static async Task<(CottonEncryptionSettings Settings, MasterKeyRuntimeState RuntimeState)> ResolveEncryptionSettingsAsync(string[] args)
        {
            string? rootMasterKey = Environment.GetEnvironmentVariable(
                ConfigurationBuilderExtensions.MasterKeyEnvironmentVariable);
            if (string.IsNullOrEmpty(rootMasterKey))
            {
                ConfigurationBuilderExtensions.ClearMasterKeyEnvironmentVariable();
                CottonEncryptionSettings settings = await MasterKeyUnlockServer.WaitForUnlockAsync(args);
                return (settings, MasterKeyRuntimeState.FromUnlock(IsMasterKeyEnvironmentVariablePresent()));
            }

            try
            {
                return (
                    ConfigurationBuilderExtensions.DeriveEncryptionSettings(rootMasterKey),
                    MasterKeyRuntimeState.FromEnvironment(environmentVariablePresentAfterResolution: false));
            }
            finally
            {
                ConfigurationBuilderExtensions.ClearMasterKeyEnvironmentVariable();
            }
        }

        private static bool IsMasterKeyEnvironmentVariablePresent()
        {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(
                ConfigurationBuilderExtensions.MasterKeyEnvironmentVariable));
        }

        private static async Task RunApplicationAsync(
            string[] args,
            CottonEncryptionSettings encryptionSettings,
            MasterKeyRuntimeState masterKeyRuntimeState,
            ProcessHardeningStatus processHardeningStatus)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddCottonOptions(encryptionSettings);
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
            builder.Services.AddHttpClient<OidcAvatarImportService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Cotton/1.0");
            });
            builder.Services.AddHttpClient<IPushNotificationDeliveryService, FirebaseCloudMessagingPushNotificationDeliveryService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            });
            builder.Services
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
                .AddOptions<StoragePressureOptions>()
                .Bind(builder.Configuration.GetSection("StoragePressure"));
            builder.Services
                .AddMediator()
                .AddQuartzJobs()
                .AddMemoryCache()
                .AddSignalR().Services
                .AddHttpContextAccessor()
                .AddSingleton<PerfTracker>()
                .AddSingleton<IStorageBackendTypeCache, StorageBackendTypeCache>()
                .AddSingleton<CottonPublicEmailProvider>()
                .AddScoped<SettingsProvider>()
                .AddScoped<SecurityDiagnosticsService>()
                .AddScoped<StoragePipelineProbeService>()
                .AddScoped<PasskeyService>()
                .AddScoped<AuthSessionIssuer>()
                .AddScoped<OidcProviderService>()
                .AddScoped<OidcAuthenticationService>()
                .AddScoped(sp => new OidcDiscoveryService(sp.GetRequiredService<IHttpClientFactory>().CreateClient(OidcDiscoveryService.HttpClientName)))
                .AddScoped<PushDeviceTokenRevocationService>()
                .AddScoped<RefreshTokenRevocationService>()
                .AddScoped<SessionRevocationNotifier>()
                .AddScoped<DownloadTokenExpirationService>()
                .AddScoped<IPostgresDumpService, PostgresDumpService>()
                .AddScoped<IDatabaseBackupManifestService, DatabaseBackupManifestService>()
                .AddScoped<IDatabaseAutoRestoreService, DatabaseAutoRestoreService>()
                .AddScoped<FileManifestService>()
                .AddScoped<UserStorageQuotaService>()
                .AddSingleton<ArchiveDownloadTicketStore>()
                .AddSingleton<StoredZipArchiveWriter>()
                .AddScoped<ArchiveDownloadService>()
                .AddScoped<StoragePressureGuard>()
                .AddScoped<DefaultUserContentSeeder>()
                .AddScoped<ChunkUsageService>()
                .AddScoped<StorageUsageStatsService>()
                .AddScoped<VideoTranscoder>()
                .AddSingleton<HlsSegmentCache>()
                .AddSingleton<DatabaseBackupKeyProvider>()
                .AddScoped<IS3Provider, S3Provider>()
                .AddScoped<INotificationsProvider, CottonNotifications>()
                .AddScoped<IGeoLookupService, GeoLookupService>()
                .AddScoped<ISharedFileDownloadNotifier, SharedFileDownloadNotifier>()
                .AddScoped<NodeSubtreeService>()
                .AddScoped<TrashRestoreCoordinator>()
                .AddScoped<IEncryptionChunkSizeProvider, SettingsEncryptionChunkSizeProvider>()
                .AddScoped<ICompressionLevelProvider, SettingsCompressionLevelProvider>()
                .AddScoped<IStorageProcessor, CryptoProcessor>()
                .AddScoped<IStorageProcessor, CompressionProcessor>()
                .AddScoped<IStoragePipeline, FileStoragePipeline>()
                .AddScoped<IStorageBackendProvider, StorageBackendProvider>()
                .AddPostgresDbContext<CottonDbContext>(x => x.UseLazyLoadingProxies = false)
                .AddSingleton<ILayoutMutationGate, LayoutMutationGate>()
                .AddScoped<ILayoutService, StorageLayoutService>()
                .AddScoped<ILayoutNavigator, LayoutNavigator>()
                .AddPbkdf2PasswordHashService()
                .AddControllers().Services
                .AddStreamCipher()
                .AddDatabaseIntegrity()
                .AddStartupValidation()
                .AddChunkServices()
                .AddLayoutPathServices()
                .AddLayoutSearchServices()
                .AddWebDavServices()
                .AddWebDavAuth()
                .AddJwt();
            builder.Services.AddAuthHardening();
            builder.Services.AddHostedService<AppVersionTrackerService>();

            WebApplication app = builder.Build();
            StartupBlocker? startupBlocker = await ValidateStartupAsync(app);
            if (startupBlocker is not null)
            {
                await app.DisposeAsync();
                await StartupBlockedServer.RunAsync(args, startupBlocker);
                return;
            }

            app.UseAuthHardening();
            app.UseDefaultFiles();
            app.MapStaticAssets();
            app.UseAuthentication()
                .UseAuthorization()
                .UseExceptionHandler();
            app.MapStartupStatusEndpoint(null);
            app.MapControllers();
            app.MapFallbackToFile("/index.html");
            app.ApplyMigrations<CottonDbContext>();
            using (IServiceScope scope = app.Services.CreateScope())
            {
                IDatabaseAutoRestoreService autoRestore = scope.ServiceProvider.GetRequiredService<IDatabaseAutoRestoreService>();
                autoRestore.TryRestoreIfEmptyAsync().GetAwaiter().GetResult();
                scope.ServiceProvider.GetRequiredService<SettingsProvider>().GetServerSettings();
            }
            app.MapHub<EventHub>(Routes.V1.EventHub);
            await app.RunAsync();
        }

        private static async Task<StartupBlocker?> ValidateStartupAsync(WebApplication app)
        {
            using IServiceScope scope = app.Services.CreateScope();
            IStartupPreflightValidator validator = scope.ServiceProvider.GetRequiredService<IStartupPreflightValidator>();
            return await validator.ValidateAsync(CancellationToken.None);
        }
    }
}
