// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Extensions
{
    public static class SearchEngineExclusionExtensions
    {
        private const string RobotsTagHeader = "X-Robots-Tag";
        private const string RobotsNoIndexValue = "noindex";

        public static IApplicationBuilder UseSearchEngineExclusion(this IApplicationBuilder app)
        {
            return app.Use(static (context, next) =>
            {
                context.Response.OnStarting(static state =>
                {
                    HttpResponse response = (HttpResponse)state;
                    response.Headers[RobotsTagHeader] = RobotsNoIndexValue;
                    return Task.CompletedTask;
                }, context.Response);
                return next(context);
            });
        }
    }
}
