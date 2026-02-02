// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Database;
using Cotton.Server.Extensions;
using Cotton.Server.Mappings;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Shared;
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
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddCottonOptions();
            MapsterConfig.Register();
            builder.Services
                .AddExceptionHandler()
                .AddOptions<CottonEncryptionSettings>()
                .Bind(builder.Configuration);

            builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<CottonEncryptionSettings>>().Value);
            builder.Services
                .AddMediator()
                .AddQuartzJobs()
                .AddMemoryCache()
                .AddSingleton<PerfTracker>()
                .AddScoped<SettingsProvider>()
                .AddScoped<FileManifestService>()
                .AddScoped<IS3Provider, S3Provider>()
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

            var app = builder.Build();
            app.UseDefaultFiles();
            app.MapStaticAssets();
            app.UseAuthentication()
                .UseAuthorization()
                .UseExceptionHandler();
            app.MapControllers();
            app.MapFallbackToFile("/index.html");
            app.ApplyMigrations<CottonDbContext>();
            app.Run();
        }
    }
}
