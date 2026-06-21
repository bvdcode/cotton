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
                context.Response.ApplyNoStoreHeaders();
                return blocker is null
                    ? Results.Ok(StartupStatusResponse.Ready())
                    : Results.Ok(StartupStatusResponse.BlockedBy(blocker));
            });

            return endpoints;
        }
    }
}
