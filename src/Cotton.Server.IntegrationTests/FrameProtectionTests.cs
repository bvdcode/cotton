// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using Cotton.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using NUnit.Framework;
using System.Net;

namespace Cotton.Server.IntegrationTests
{
    public class FrameProtectionTests
    {
        [Test]
        public async Task CottonPipelineAppliesFrameProtection()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = "Cotton.Server",
            });
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddProblemDetails();
            builder.Services.AddControllers();
            builder.Services.AddAuthentication();
            builder.Services.AddAuthorization();
            builder.Services.AddEndpointRateLimiting();
            builder.Services.AddSignalR();
            await using WebApplication app = builder.Build();
            app.UseCottonServer();
            await app.StartAsync();
            using HttpClient client = app.GetTestClient();

            using HttpResponseMessage response = await client.GetAsync("/api/v1/startup/status");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            AssertFrameProtection(response);
        }

        [TestCase("/")]
        [TestCase("/index.html")]
        [TestCase("/login")]
        [TestCase("/login?returnUrl=%2Ffiles")]
        [TestCase("/reset-password")]
        [TestCase("/unlock")]
        [TestCase("/share/example")]
        public async Task StaticAndFallbackPagesIncludeFrameProtection(string path)
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            const string body = "<!doctype html><title>Cotton</title>";
            await File.WriteAllTextAsync(Path.Combine(directory, "index.html"), body);
            try
            {
                WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    WebRootPath = directory,
                });
                builder.WebHost.UseTestServer();
                builder.Logging.ClearProviders();
                await using WebApplication app = builder.Build();
                app.UseFrameProtection();
                app.UseDefaultFiles();
                app.UseStaticFiles();
                app.MapFallbackToFile("index.html");
                await app.StartAsync();
                using HttpClient client = app.GetTestClient();

                using HttpResponseMessage response = await client.GetAsync(path);
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo(body));
                AssertFrameProtection(response);

                using HttpRequestMessage headRequest = new(HttpMethod.Head, path);
                using HttpResponseMessage headResponse = await client.SendAsync(headRequest);
                Assert.That(headResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                AssertFrameProtection(headResponse);

                using HttpRequestMessage cachedRequest = new(HttpMethod.Get, path);
                cachedRequest.Headers.IfNoneMatch.Add(response.Headers.ETag!);
                using HttpResponseMessage cachedResponse = await client.SendAsync(cachedRequest);
                Assert.That(cachedResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotModified));
                AssertFrameProtection(cachedResponse);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [TestCase(HttpStatusCode.Unauthorized)]
        [TestCase(HttpStatusCode.Forbidden)]
        [TestCase(HttpStatusCode.NotFound)]
        [TestCase(HttpStatusCode.InternalServerError)]
        public async Task FrameProtectionPreservesStatusAndBody(HttpStatusCode statusCode)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            await using WebApplication app = builder.Build();
            app.UseFrameProtection();
            app.Run(async context =>
            {
                context.Response.StatusCode = (int)statusCode;
                await context.Response.WriteAsync("Response body");
            });
            await app.StartAsync();
            using HttpClient client = app.GetTestClient();

            using HttpResponseMessage response = await client.GetAsync("/api/v1/example");

            Assert.That(response.StatusCode, Is.EqualTo(statusCode));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("Response body"));
            AssertFrameProtection(response);
        }

        [Test]
        public async Task FileContentSandboxIsPreservedAlongsideFrameProtection()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            await using WebApplication app = builder.Build();
            app.UseFrameProtection();
            app.Run(async context =>
            {
                FileResponseSecurity.ApplyFileResponseHeaders(context.Response, "text/html", requestedInline: true);
                await context.Response.WriteAsync("File content");
            });
            await app.StartAsync();
            using HttpClient client = app.GetTestClient();

            using HttpResponseMessage response = await client.GetAsync("/api/v1/files/example");

            Assert.That(response.Headers.GetValues(HeaderNames.ContentSecurityPolicy), Is.EquivalentTo(new[]
            {
                "sandbox; default-src 'none'; script-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'",
                "frame-ancestors 'self'",
            }));
            Assert.That(response.Headers.GetValues(HeaderNames.XFrameOptions), Is.EqualTo(new[] { "SAMEORIGIN" }));
            Assert.That(response.Headers.GetValues(HeaderNames.XContentTypeOptions), Is.EqualTo(new[] { "nosniff" }));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("File content"));
        }

        private static void AssertFrameProtection(HttpResponseMessage response)
        {
            Assert.Multiple(() =>
            {
                Assert.That(response.Headers.GetValues(HeaderNames.ContentSecurityPolicy),
                    Is.EqualTo(new[] { "frame-ancestors 'self'" }));
                Assert.That(response.Headers.GetValues(HeaderNames.XFrameOptions),
                    Is.EqualTo(new[] { "SAMEORIGIN" }));
            });
        }
    }
}
