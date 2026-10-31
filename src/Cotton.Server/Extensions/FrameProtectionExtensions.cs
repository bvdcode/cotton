// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Net.Http.Headers;

namespace Cotton.Server.Extensions
{
    public static class FrameProtectionExtensions
    {
        private const string FrameAncestorsPolicy = "frame-ancestors 'self'";
        private const string SameOrigin = "SAMEORIGIN";

        public static IApplicationBuilder UseFrameProtection(this IApplicationBuilder app)
        {
            return app.Use(static (context, next) =>
            {
                context.Response.OnStarting(static state =>
                {
                    HttpResponse response = (HttpResponse)state;
                    response.Headers.Append(HeaderNames.ContentSecurityPolicy, FrameAncestorsPolicy);
                    response.Headers[HeaderNames.XFrameOptions] = SameOrigin;
                    return Task.CompletedTask;
                }, context.Response);
                return next(context);
            });
        }
    }
}
