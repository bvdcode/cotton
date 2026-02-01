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

            // TODO: Remove after testing
            app.UseWhen(
                ctx => ctx.Request.Path.StartsWithSegments("/api/v1/webdav"),
                b => b.Use(async (ctx, next) =>
                {
                    var log = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("WebDavWire");

                    string allHeaders = string.Join("; ",
                        ctx.Request.Headers.Select(h => $"{h.Key}={h.Value}"));
                    log.LogInformation("WEBDAV REQ {Method} {Path} CL={CL} Expect={Expect} UA={UA} All={allHeaders}",
                        ctx.Request.Method,
                        ctx.Request.Path + ctx.Request.QueryString,
                        ctx.Request.ContentLength,
                        ctx.Request.Headers.Expect.ToString(),
                        ctx.Request.Headers.UserAgent.ToString(),
                        allHeaders);

                    await next();

                    log.LogInformation("WEBDAV RESP {StatusCode}",
                        ctx.Response.StatusCode);
                }));

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
