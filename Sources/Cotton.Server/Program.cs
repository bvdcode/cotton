using Cotton.Crypto;
using Cotton.Server.Database;
using Cotton.Server.Settings;
using Cotton.Server.Services;
using Cotton.Server.Abstractions;
using Cotton.Crypto.Abstractions;
using Microsoft.Extensions.Options;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;

namespace Cotton.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Bind CottonSettings (root-level keys allowed). Provide defaults & validation.
            builder.Services.AddOptions<CottonSettings>()
                .Bind(builder.Configuration).Services
                .AddScoped<IStreamCipher, AesGcmStreamCipher>()
                .AddSingleton(sp => sp.GetRequiredService<IOptions<CottonSettings>>().Value)
                .AddSingleton<IStorage, FileStorage>()
                .AddOpenApi()
                .AddPostgresDbContext<CottonDbContext>(
                    builder.Configuration,
                    x => x.ContextLifetime = ServiceLifetime.Scoped)
                .AddControllers();

            var app = builder.Build();
            app.UseDefaultFiles();
            app.MapStaticAssets();
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }
            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();
            app.MapFallbackToFile("/index.html");
            app.Run();
        }
    }
}
