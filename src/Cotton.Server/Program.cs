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
using Cotton.Server.Services.KeyManagement;
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
using Microsoft.AspNetCore.HttpOverrides;
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
            string unlockSecret;
            try
            {
                (encryptionSettings, masterKeyRuntimeState, unlockSecret) = await ResolveEncryptionSettingsAsync(args);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await RunApplicationAsync(args, encryptionSettings, masterKeyRuntimeState, unlockSecret, processHardeningStatus);
        }

        private static void ConfigureProcessTimeZone()
        {
            // User timezone settings are applied per request; the process clock
            // stays UTC so platform TLS/date handling cannot drift with them.
            Environment.SetEnvironmentVariable("TZ", "UTC");
            TimeZoneInfo.ClearCachedData();
        }

        private static async Task<(CottonEncryptionSettings Settings, MasterKeyRuntimeState RuntimeState, string UnlockSecret)> ResolveEncryptionSettingsAsync(string[] args)
        {
            string? rootMasterKey = Environment.GetEnvironmentVariable(
                ConfigurationBuilderExtensions.MasterKeyEnvironmentVariable);
            if (string.IsNullOrEmpty(rootMasterKey))
            {
                ConfigurationBuilderExtensions.ClearMasterKeyEnvironmentVariable();
                MasterKeyUnlockResult unlock = await MasterKeyUnlockServer.WaitForUnlockAsync(args);
                return (
                    unlock.Settings,
                    unlock.KeyringBacked
                        ? MasterKeyRuntimeState.FromKeyringUnlock(IsMasterKeyEnvironmentVariablePresent())
                        : MasterKeyRuntimeState.FromUnlock(IsMasterKeyEnvironmentVariablePresent()),
                    unlock.UnlockSecret);
            }

            try
            {
                KeyringStartupOpenResult keyringOpen = await KeyringStartup.TryOpenLocalIfEnabledAsync(rootMasterKey);
                if (keyringOpen.Status == KeyringStartupOpenStatus.Opened && keyringOpen.Keyring is not null)
                {
                    return (
                        KeyringV1UpgradeBuilder.CreateLegacySettingsFromState(keyringOpen.Keyring.State),
                        MasterKeyRuntimeState.FromKeyringEnvironment(environmentVariablePresentAfterResolution: false),
                        rootMasterKey);
                }

                return (
                    ConfigurationBuilderExtensions.DeriveEncryptionSettings(rootMasterKey),
                    MasterKeyRuntimeState.FromEnvironment(environmentVariablePresentAfterResolution: false),
                    rootMasterKey);
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

        private static async Task BootstrapKeyringAsync(
            WebApplication app,
            CottonEncryptionSettings encryptionSettings,
            string unlockSecret)
        {
            if (!KeyringStartup.IsEnabled())
            {
                return;
            }

            using IServiceScope scope = app.Services.CreateScope();
            KeyringBootstrapResult? keyring = await KeyringStartup.BootstrapIfEnabledAsync(
                scope.ServiceProvider,
                encryptionSettings,
                unlockSecret);
            if (keyring is not null)
            {
                scope.ServiceProvider.GetRequiredService<KeyringRuntimeState>().Set(keyring);
                await scope.ServiceProvider.GetRequiredService<KeyringJournaledObjectStore>()
                    .RepairLatestAsync();
            }
        }

        private static async Task RunApplicationAsync(
            string[] args,
            CottonEncryptionSettings encryptionSettings,
            MasterKeyRuntimeState masterKeyRuntimeState,
            string unlockSecret,
            ProcessHardeningStatus processHardeningStatus)
        {
            var builder = WebApplication.CreateBuilder(args);
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
            builder.Services
                .AddExceptionHandler()
                .AddOptions<CottonEncryptionSettings>()
                .Bind(builder.Configuration);
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });
            builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<CottonEncryptionSettings>>().Value);
            builder.Services.AddSingleton<KeyringRuntimeState>();
            builder.Services.AddScoped(sp => new KeyringJournaledObjectStore(KeyringStartup.CreateReplicas(sp)));
            builder.Services.AddScoped(sp => new KeyringDiagnosticsService(
                sp.GetRequiredService<KeyringJournaledObjectStore>()));
            builder.Services.AddScoped(sp => new KeyringAdminService(
                sp.GetRequiredService<KeyringJournaledObjectStore>(),
                sp.GetRequiredService<KeyringRuntimeState>()));
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
                .AddScoped<ILayoutService, StorageLayoutService>()
                .AddScoped<ILayoutNavigator, LayoutNavigator>()
                .AddPbkdf2PasswordHashService()
                .AddControllers().Services
                .AddStreamCipher()
                .AddDatabaseIntegrity()
                .AddChunkServices()
                .AddLayoutPathServices()
                .AddWebDavServices()
                .AddWebDavAuth()
                .AddJwt();
            builder.Services.AddAuthHardening();
            builder.Services.AddHostedService<AppVersionTrackerService>();

            var app = builder.Build();
            app.UseForwardedHeaders();
            app.UseAuthHardening();
            app.UseDefaultFiles();
            app.MapStaticAssets();
            app.UseAuthentication()
                .UseAuthorization()
                .UseExceptionHandler();
            app.MapControllers();
            app.MapFallbackToFile("/index.html");
            app.ApplyMigrations<CottonDbContext>();
            await BootstrapKeyringAsync(app, encryptionSettings, unlockSecret);
            await app.ApplyDatabaseIntegrityBridgeBackfillAsync();
            using (IServiceScope scope = app.Services.CreateScope())
            {
                var autoRestore = scope.ServiceProvider.GetRequiredService<IDatabaseAutoRestoreService>();
                autoRestore.TryRestoreIfEmptyAsync().GetAwaiter().GetResult();
                scope.ServiceProvider.GetRequiredService<SettingsProvider>().GetServerSettings();
            }
            app.MapHub<EventHub>(Routes.V1.EventHub);
            await app.RunAsync();
        }
    }
}
