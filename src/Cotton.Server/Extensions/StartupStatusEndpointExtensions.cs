// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services.Startup;

namespace Cotton.Server.Extensions
{
    internal static class StartupStatusEndpointExtensions
    {
        public const string StatusPath = Routes.V1.Base + "/startup/status";

        public static IEndpointRouteBuilder MapStartupStatusEndpoint(
            this IEndpointRouteBuilder endpoints,
            StartupBlocker? blocker)
        {
            endpoints.MapGet(StatusPath, (HttpContext context) =>
            {
                DisableCaching(context);
                return blocker is null
                    ? Results.Ok(StartupStatusResponse.Ready())
                    : Results.Ok(StartupStatusResponse.BlockedBy(blocker));
            });

            return endpoints;
        }

        public static void DisableCaching(HttpContext context)
        {
            context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";
        }
    }
}
