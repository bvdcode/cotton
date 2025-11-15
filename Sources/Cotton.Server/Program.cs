// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Cotton.Shared;
using Cotton.Database;
using Cotton.Topology;
using Cotton.Storage.Pipelines;
using Cotton.Server.Extensions;
using Cotton.Storage.Processors;
using Cotton.Storage.Abstractions;
using Cotton.Autoconfig.Extensions;
using Microsoft.Extensions.Options;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.EntityFrameworkCore.Extensions;
using EasyExtensions.AspNetCore.Authorization.Extensions;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;

namespace Cotton.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddCottonOptions();
            builder.Services
                .AddOptions<CottonSettings>()
                .Bind(builder.Configuration);

            builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<CottonSettings>>().Value);
            builder.Services.AddScoped<IStoragePipeline, FileStoragePipeline>()
                .AddScoped<IStorageProcessor, FileSystemStorageProcessor>()
                .AddScoped<IStorageProcessor, CryptoProcessor>()
                .AddPostgresDbContext<CottonDbContext>(x => x.UseLazyLoadingProxies = false)
                .AddScoped<StorageLayoutService>()
                .AddPbkdf2PasswordHashService()
                .AddControllers().Services
                .AddStreamCipher()
                .AddCottonCors()
                .AddJwt();

            var app = builder.Build();
            app.UseDefaultFiles();
            app.MapStaticAssets();
            app.UseCottonCors();
            app.UseAuthentication()
                .UseAuthorization();
            app.MapControllers();
            app.MapFallbackToFile("/index.html");
            app.ApplyMigrations<CottonDbContext>();
            app.Run();
        }
    }
}
