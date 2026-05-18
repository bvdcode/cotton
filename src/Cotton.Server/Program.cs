// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Database;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Hubs;
using Cotton.Server.Mappings;
using Cotton.Server.Providers;
using Cotton.Server.Services;
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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cotton.Server
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            ConfigureProcessTimeZone();

            CottonEncryptionSettings encryptionSettings;
            try
            {
                encryptionSettings = await ResolveEncryptionSettingsAsync(args);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await new MasterKeySentinelStore(NullLogger<MasterKeySentinelStore>.Instance)
                .EnsureValidOrThrowAsync(encryptionSettings);

            await RunApplicationAsync(args, encryptionSettings);
        }

        private static void ConfigureProcessTimeZone()
        {
            // User timezone settings are applied per request; the process clock
            // stays UTC so platform TLS/date handling cannot drift with them.
            Environment.SetEnvironmentVariable("TZ", "UTC");
            TimeZoneInfo.ClearCachedData();
        }

        private static async Task<CottonEncryptionSettings> ResolveEncryptionSettingsAsync(string[] args)
        {
            string? rootMasterKey = Environment.GetEnvironmentVariable(
                ConfigurationBuilderExtensions.MasterKeyEnvironmentVariable);
            if (string.IsNullOrEmpty(rootMasterKey))
            {
                ConfigurationBuilderExtensions.ClearMasterKeyEnvironmentVariable();
                return await MasterKeyUnlockServer.WaitForUnlockAsync(args);
            }

            try
            {
                return ConfigurationBuilderExtensions.DeriveEncryptionSettings(rootMasterKey);
            }
            finally
            {
                ConfigurationBuilderExtensions.ClearMasterKeyEnvironmentVariable();
            }
        }

        private static async Task RunApplicationAsync(string[] args, CottonEncryptionSettings encryptionSettings)
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
            builder.Services
                .AddOptions<HlsSegmentCacheOptions>()
                .Bind(builder.Configuration.GetSection("HlsSegmentCache"));
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
                .AddScoped<IPostgresDumpService, PostgresDumpService>()
                .AddScoped<IDatabaseBackupManifestService, DatabaseBackupManifestService>()
                .AddScoped<IDatabaseAutoRestoreService, DatabaseAutoRestoreService>()
                .AddScoped<FileManifestService>()
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
                .AddChunkServices()
                .AddLayoutPathServices()
                .AddWebDavServices()
                .AddWebDavAuth()
                .AddJwt();
            builder.Services.AddHostedService<AppVersionTrackerService>();

            var app = builder.Build();
            app.UseForwardedHeaders();
            app.UseDefaultFiles();
            app.MapStaticAssets();
            app.UseAuthentication()
                .UseAuthorization()
                .UseExceptionHandler();
            app.MapControllers();
            app.MapFallbackToFile("/index.html");
            app.ApplyMigrations<CottonDbContext>();
            using (IServiceScope scope = app.Services.CreateScope())
            {
                var autoRestore = scope.ServiceProvider.GetRequiredService<IDatabaseAutoRestoreService>();
                autoRestore.TryRestoreIfEmptyAsync().GetAwaiter().GetResult();
            }
            app.MapHub<EventHub>(Routes.V1.EventHub);
            await app.RunAsync();
        }
    }
}
