// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Net;

namespace Cotton.Server.IntegrationTests
{
    public class SearchEngineExclusionTests
    {
        [TestCase(HttpStatusCode.OK)]
        [TestCase(HttpStatusCode.Unauthorized)]
        [TestCase(HttpStatusCode.NotFound)]
        [TestCase(HttpStatusCode.InternalServerError)]
        public async Task ResponseIncludesNoIndexWithoutChangingStatusOrBody(HttpStatusCode statusCode)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            await using WebApplication app = builder.Build();
            app.UseSearchEngineExclusion();
            app.Run(async context =>
            {
                context.Response.StatusCode = (int)statusCode;
                context.Response.Headers["X-Robots-Tag"] = "index";
                await context.Response.WriteAsync("Response body");
            });
            await app.StartAsync();
            using HttpClient client = app.GetTestClient();

            using HttpResponseMessage response = await client.GetAsync("/");
            string body = await response.Content.ReadAsStringAsync();

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(statusCode));
                Assert.That(response.Headers.GetValues("X-Robots-Tag"), Is.EqualTo(new[] { "noindex" }));
                Assert.That(body, Is.EqualTo("Response body"));
            });
        }
    }
}
