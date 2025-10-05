using Cotton.Server.Database;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;

namespace Cotton.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services
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
