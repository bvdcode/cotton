// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Hubs;
using EasyExtensions.AspNetCore.Extensions;

namespace Cotton.Server.Extensions
{
    public static class CottonServerPipelineExtensions
    {
        public static WebApplication UseCottonServer(this WebApplication app)
        {
            app.UseSearchEngineExclusion();
            app.UseExceptionHandler();
            app.UseDefaultFiles();
            app.MapStaticAssets();
            app.UseAuthentication()
                .UseEndpointRateLimiting()
                .UseAuthorization();
            app.MapStartupStatusEndpoint(null);
            app.MapControllers();
            app.MapFallbackToFile("/index.html");
            app.MapHub<EventHub>(Routes.V1.EventHub);
            return app;
        }
    }
}
