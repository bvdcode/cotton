using Cotton.Server.Database;
using Cotton.Server.Services;
using Cotton.Server.Settings;
using Cotton.Server.Extensions;
using Cotton.Server.Abstractions;
using Microsoft.Extensions.Options;
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
                .AddSingleton(sp => sp.GetRequiredService<IOptions<CottonSettings>>().Value)
                .AddScoped<IStorage, FileStorage>()
                .AddOpenApi()
                .AddPostgresDbContext<CottonDbContext>()
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
