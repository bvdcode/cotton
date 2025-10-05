using System.Text;
using Cotton.Crypto;
using Cotton.Server.Database;
using Cotton.Server.Services;
using Cotton.Server.Settings;
using Cotton.Crypto.Abstractions;
using Cotton.Server.Abstractions;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
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
                .AddScoped<IStreamCipher>(sp =>
                {
                    var settings = sp.GetRequiredService<CottonSettings>();
                    if (string.IsNullOrWhiteSpace(settings.MasterEncryptionKey))
                    {
                        throw new InvalidOperationException("MasterEncryptionKey is not configured.");
                    }
                    // Derive 32-byte key (SHA-256 of provided string)
                    byte[] keyMaterial = SHA256.HashData(Encoding.UTF8.GetBytes(settings.MasterEncryptionKey));
                    int keyId = settings.MasterEncryptionKeyId;
                    int? threads = settings.EncryptionThreads.HasValue && settings.EncryptionThreads > 0 ? settings.EncryptionThreads : null;
                    return new AesGcmStreamCipher(keyMaterial, keyId, threads);
                })
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
