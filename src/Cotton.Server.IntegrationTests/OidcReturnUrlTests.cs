// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Auth.Oidc;
using Cotton.Server.IntegrationTests.Helpers;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;

namespace Cotton.Server.IntegrationTests
{
    public class OidcReturnUrlTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t ")]
        [TestCase("https://example.org")]
        [TestCase("http://example.org")]
        [TestCase("//example.org")]
        [TestCase("///example.org")]
        [TestCase("/\\example.org")]
        [TestCase("/\\\\example.org/path")]
        [TestCase("\\\\example.org")]
        [TestCase(" /\\example.org ")]
        [TestCase("/\t/example.org")]
        [TestCase("/\n/example.org")]
        [TestCase("/\r/example.org")]
        [TestCase("/\0/example.org")]
        [TestCase("/files\n/path")]
        [TestCase("javascript:alert(1)")]
        [TestCase("files")]
        [TestCase("~/files")]
        public void NormalizeReturnUrl_RejectsNonLocalAndInvalidPaths(string? returnUrl)
        {
            Assert.That(OidcProtocol.NormalizeReturnUrl(returnUrl), Is.EqualTo("/"));
        }

        [TestCase("/", "/")]
        [TestCase("/files", "/files")]
        [TestCase(" /files ", "/files")]
        [TestCase("/files/folder?view=list#preview", "/files/folder?view=list#preview")]
        [TestCase("/profile?returnUrl=https://example.org", "/profile?returnUrl=https://example.org")]
        [TestCase("/files/name%20with%20spaces", "/files/name%20with%20spaces")]
        [TestCase("/%2f%2fexample.org", "/%2f%2fexample.org")]
        [TestCase("/%5cexample.org", "/%5cexample.org")]
        public void NormalizeReturnUrl_PreservesLocalPaths(string returnUrl, string expected)
        {
            Assert.That(OidcProtocol.NormalizeReturnUrl(returnUrl), Is.EqualTo(expected));
        }

        [TestCase("/\\example.org", "/")]
        [TestCase("//example.org", "/")]
        [TestCase("https://example.org", "/")]
        [TestCase("/\t/example.org", "/")]
        [TestCase("/", "/")]
        [TestCase("/files/folder?view=list#preview", "/files/folder?view=list#preview")]
        public async Task Callback_RevalidatesStoredReturnUrlBeforeRedirecting(string returnUrl, string expected)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = "Cotton.Server",
            });
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddControllers();
            builder.Services.AddAuthorization();
            builder.Services.AddEndpointRateLimiting();
            builder.Services.AddMediator();
            builder.Services.AddSingleton<IRequestHandler<CompleteOidcAuthenticationRequest, string>>(
                new OidcReturnUrlTestHandler(returnUrl));
            await using WebApplication app = builder.Build();
            app.UseEndpointRateLimiting();
            app.UseAuthorization();
            app.MapControllers().DisableRateLimiting();
            await app.StartAsync();
            using HttpClient client = app.GetTestClient();

            using HttpResponseMessage response = await client.GetAsync("/api/v1/auth/oidc/callback?state=state&code=code");

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Found));
                Assert.That(response.Headers.Location?.OriginalString, Is.EqualTo(expected));
            });
        }
    }
}
