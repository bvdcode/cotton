// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Cotton.Shared;
using Cotton.Storage;
using Cotton.Server.Database;
using Cotton.Server.Services;
using Cotton.Server.Extensions;
using Cotton.Storage.Abstractions;
using Microsoft.Extensions.Options;
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
            builder.Services.AddOptions<CottonSettings>()
                .Bind(builder.Configuration).Services
                .AddScoped<StorageLayoutService>()
                .AddStreamCipher()
                .AddCottonCors()
                .AddSingleton(sp => sp.GetRequiredService<IOptions<CottonSettings>>().Value)
                .AddScoped<IStorage, EncryptedFileStorage>()
                .AddOpenApi()
                .AddPostgresDbContext<CottonDbContext>(x => x.UseLazyLoadingProxies = false)
                .AddControllers().Services
                .AddJwt();

            var app = builder.Build();
            app.UseDefaultFiles();
            app.MapStaticAssets();
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }
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
