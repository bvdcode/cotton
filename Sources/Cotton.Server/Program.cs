using Cotton.Server.Database;
using Cotton.Server.Services;
using Cotton.Server.Settings;
using Cotton.Server.Extensions;
using Cotton.Server.Abstractions;
using Microsoft.Extensions.Options;
using EasyExtensions.EntityFrameworkCore.Extensions;
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
                .AddStreamCipher()
                .AddCottonCors()
                .AddSingleton(sp => sp.GetRequiredService<IOptions<CottonSettings>>().Value)
                .AddScoped<IStorage, FileStorage>()
                .AddOpenApi()
                .AddPostgresDbContext<CottonDbContext>(x => x.UseLazyLoadingProxies = false)
                .AddControllers();

            var app = builder.Build();
            app.UseDefaultFiles();
            app.MapStaticAssets();
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }
            app.UseCottonCors();
            app.UseAuthorization();
            app.MapControllers();
            app.MapFallbackToFile("/index.html");
            app.ApplyMigrations<CottonDbContext>();
            app.Run();
        }
    }
}
