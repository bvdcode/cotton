// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;

namespace Cotton.Server.Services.Startup
{
    internal static class StartupBlockedServer
    {
        public static async Task RunAsync(
            string[] args,
            StartupBlocker blocker)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            builder.Logging.AddFilter(
                "Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager",
                LogLevel.Error);

            WebApplication app = builder.Build();
            ILogger logger = app.Services
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Cotton.Server.Startup");

            logger.LogCritical(
                "Cotton startup is blocked by {BlockerKind}: {BlockerMessage}",
                blocker.Kind,
                blocker.Message);

            app.UseDefaultFiles();
            app.Use(async (context, next) =>
            {
                if (IsStartupStatusRequest(context.Request))
                {
                    await next();
                    return;
                }

                if (IsApiRequest(context.Request))
                {
                    context.Response.ApplyNoStoreHeaders();
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    await context.Response.WriteAsJsonAsync(
                        StartupStatusResponse.BlockedBy(blocker),
                        cancellationToken: context.RequestAborted);
                    return;
                }

                await next();
            });
            app.MapStaticAssets();
            app.MapStartupStatusEndpoint(blocker);
            app.MapFallbackToFile("/index.html");

            await app.RunAsync();
        }

        private static bool IsStartupStatusRequest(HttpRequest request)
        {
            return request.Path.Equals(
                new PathString(StartupStatusEndpointExtensions.StatusPath),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsApiRequest(HttpRequest request)
        {
            return request.Path.StartsWithSegments(
                new PathString(Routes.V1.Base),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
