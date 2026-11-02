// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Auth.Oidc;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Models;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System.Security.Claims;

namespace Cotton.Server.IntegrationTests
{
    public class OidcBrowserCorrelationTests
    {
        private const string StartPath = "/api/v1/auth/oidc/start/provider/authorization-url";
        private const string LinkPath = "/api/v1/auth/oidc/link/provider/authorization-url";
        private static readonly Guid UserId = Guid.Parse("d6a4f052-26ee-4a39-9a55-c588999e833a");
        private WebApplication _app = null!;
        private HttpClient _client = null!;
        private OidcAuthorizationTestHandler _startHandler = null!;
        private OidcBrowserCompletionTestHandler _flow = null!;

        [SetUp]
        public async Task SetUp()
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
            _startHandler = new OidcAuthorizationTestHandler();
            _flow = new OidcBrowserCompletionTestHandler();
            builder.Services.AddSingleton<IRequestHandler<BeginOidcAuthenticationRequest, OidcAuthorizationResult>>(_startHandler);
            builder.Services.AddSingleton<IRequestHandler<CompleteOidcAuthenticationRequest, string>>(_flow);
            _app = builder.Build();
            _app.Use((context, next) =>
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, UserId.ToString())], "test"));
                return next(context);
            });
            _app.UseEndpointRateLimiting();
            _app.UseAuthorization();
            _app.MapControllers().DisableRateLimiting();
            await _app.StartAsync();
            _client = _app.GetTestClient();
        }

        [TearDown]
        public async Task TearDown()
        {
            _client?.Dispose();
            if (_app is not null)
            {
                await _app.DisposeAsync();
            }
        }

        [TestCase(StartPath)]
        [TestCase(LinkPath)]
        public async Task Start_SetsSecureHostCookieForTheTransaction(string path)
        {
            (string state, SetCookieHeaderValue cookie) = await StartAsync(path);

            Assert.Multiple(() =>
            {
                Assert.That(cookie.Name.ToString(), Does.StartWith("__Host-"));
                Assert.That(cookie.Name.ToString(), Does.EndWith(OidcProtocol.HashOpaqueValue(state)));
                Assert.That(cookie.Domain.HasValue, Is.False);
                Assert.That(cookie.Path.ToString(), Is.EqualTo("/"));
                Assert.That(cookie.HttpOnly, Is.True);
                Assert.That(cookie.Secure, Is.True);
                Assert.That(cookie.SameSite, Is.EqualTo(Microsoft.Net.Http.Headers.SameSiteMode.Lax));
                Assert.That(cookie.Expires, Is.InRange(DateTimeOffset.UtcNow.AddMinutes(9), DateTimeOffset.UtcNow.AddMinutes(11)));
                Assert.That(_startHandler.Starts.Single().LinkUserId, Is.EqualTo(path == LinkPath ? UserId : (Guid?)null));
            });
        }

        [TestCase(StartPath)]
        [TestCase(LinkPath)]
        public async Task Callback_InAnotherBrowserIsRejectedBeforeCompletingAuthentication(string startPath)
        {
            (string state, SetCookieHeaderValue cookie) = await StartAsync(startPath);
            using HttpClient otherBrowser = _app.GetTestClient();

            using HttpResponseMessage rejected = await otherBrowser.GetAsync(CallbackPath(state));

            Assert.That(rejected.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(_flow.CompletedStates, Is.Empty);
            Assert.That(rejected.Headers.Contains(HeaderNames.SetCookie), Is.False);

            using HttpResponseMessage accepted = await CallbackAsync(state, cookie);
            Assert.That(accepted.StatusCode, Is.EqualTo(HttpStatusCode.Found));
            Assert.That(accepted.Headers.Location?.OriginalString, Is.EqualTo("/files"));
            Assert.That(_flow.CompletedStates, Is.EqualTo(new[] { state }));
            AssertClearedCookie(accepted, cookie);
        }

        [Test]
        public async Task Callback_RejectsCookieFromAnotherTransaction()
        {
            (string firstState, SetCookieHeaderValue firstCookie) = await StartAsync();
            (_, SetCookieHeaderValue secondCookie) = await StartAsync();

            using HttpResponseMessage rejected = await CallbackAsync(firstState, secondCookie);

            Assert.That(rejected.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(_flow.CompletedStates, Is.Empty);
            using HttpResponseMessage accepted = await CallbackAsync(firstState, firstCookie);
            Assert.That(accepted.StatusCode, Is.EqualTo(HttpStatusCode.Found));
        }

        [Test]
        public async Task Callback_RejectsIncorrectCookieValue()
        {
            (string state, SetCookieHeaderValue cookie) = await StartAsync();
            cookie.Value = "invalid";

            using HttpResponseMessage response = await CallbackAsync(state, cookie);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(_flow.CompletedStates, Is.Empty);
        }

        [Test]
        public async Task Callback_LeavesAnotherTabCookieIntact()
        {
            (string firstState, SetCookieHeaderValue firstCookie) = await StartAsync();
            (string secondState, SetCookieHeaderValue secondCookie) = await StartAsync();

            using HttpResponseMessage first = await CallbackAsync(firstState, firstCookie, secondCookie);
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Found));
            AssertClearedCookie(first, firstCookie);
            using HttpResponseMessage second = await CallbackAsync(secondState, secondCookie);
            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Found));
            AssertClearedCookie(second, secondCookie);
            Assert.That(_flow.CompletedStates, Is.EqualTo(new[] { firstState, secondState }));

            using HttpResponseMessage repeated = await _client.GetAsync(CallbackPath(firstState));
            Assert.That(repeated.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(_flow.CompletedStates, Has.Count.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task Cancellation_RequiresCorrelationAndClearsOnlyItsCookie(bool includeCookie)
        {
            (string state, SetCookieHeaderValue cookie) = await StartAsync();
            using HttpRequestMessage request = new(HttpMethod.Get,
                $"/api/v1/auth/oidc/callback?state={state}&error=access_denied");
            if (includeCookie)
            {
                request.Headers.Add(HeaderNames.Cookie, $"{cookie.Name}={cookie.Value}");
            }

            using HttpResponseMessage response = await _client.SendAsync(request);

            Assert.That(_flow.CompletedStates, Is.Empty);
            if (includeCookie)
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Found));
                Assert.That(response.Headers.Location?.OriginalString, Is.EqualTo("/login?oidc=cancelled"));
                AssertClearedCookie(response, cookie);
            }
            else
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            }
        }

        private async Task<(string State, SetCookieHeaderValue Cookie)> StartAsync(string path = StartPath)
        {
            using HttpResponseMessage response = await _client.PostAsJsonAsync(path, new { ReturnUrl = "/files" });
            response.EnsureSuccessStatusCode();
            OidcAuthorizationUrlDto payload = (await response.Content.ReadFromJsonAsync<OidcAuthorizationUrlDto>())!;
            string state = QueryHelpers.ParseQuery(new Uri(payload.AuthorizationUrl).Query)["state"].ToString();
            return (state, SetCookieHeaderValue.Parse(response.Headers.GetValues(HeaderNames.SetCookie).Single()));
        }

        private async Task<HttpResponseMessage> CallbackAsync(string state, params SetCookieHeaderValue[] cookies)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, CallbackPath(state));
            request.Headers.Add(HeaderNames.Cookie, string.Join("; ", cookies.Select(cookie => $"{cookie.Name}={cookie.Value}")));
            return await _client.SendAsync(request);
        }

        private static string CallbackPath(string state) => $"/api/v1/auth/oidc/callback?state={state}&code=code";

        private static void AssertClearedCookie(HttpResponseMessage response, SetCookieHeaderValue expected)
        {
            SetCookieHeaderValue cookie = SetCookieHeaderValue.Parse(response.Headers.GetValues(HeaderNames.SetCookie).Single());
            Assert.That(cookie.Name, Is.EqualTo(expected.Name));
            Assert.That(cookie.Expires, Is.LessThan(DateTimeOffset.UtcNow));
            Assert.That(cookie.Secure, Is.True);
            Assert.That(cookie.Path.ToString(), Is.EqualTo("/"));
        }
    }
}
