using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Cotton.Server.Extensions
{
    public static class CorsExtensions
    {
        private const string PolicyName = "CottonCors";

        public static IServiceCollection AddCottonCors(this IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy(PolicyName, policy =>
                {
                    policy.WithOrigins(
                            "http://localhost:5173",
                            "http://localhost:5174")
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                });
            });
            return services;
        }

        public static IApplicationBuilder UseCottonCors(this IApplicationBuilder app)
        {
            return app.UseCors(PolicyName);
        }
    }
}
