// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
using Cotton.Email;
using Cotton.Server.Models;
using ServerChangePasswordRequestDto = Cotton.Server.Models.Requests.ChangePasswordRequestDto;
using System.IdentityModel.Tokens.Jwt;
using CottonLoginRequestDto = Cotton.Auth.LoginRequestDto;

namespace Cotton.Server.IntegrationTests
{
    public class AuthSmokeTests : AuthEndpointTestBase
    {

        [Test]
        public async Task Login_Returns_Auth_Session()
        {
            Assert.That(_client, Is.Not.Null);
            Assert.That(_notifications, Is.Not.Null);

            AuthSessionResponseDto payload = await LoginAsync("testuser", "testpassword");
            Assert.Multiple(() =>
            {
                Assert.That(string.IsNullOrWhiteSpace(payload.AccessToken), Is.False, "Token must be present");
                Assert.That(string.IsNullOrWhiteSpace(payload.RefreshToken), Is.False, "Refresh token must be present");
                Assert.That(payload.User.Username, Is.EqualTo("testuser"));
                Assert.That(payload.User.Id, Is.Not.EqualTo(Guid.Empty));
                Assert.That(_notifications!.Emails, Has.Count.EqualTo(1));
            });

            var (_, template, parameters, _, _) = _notifications!.Emails.Single();
            Assert.Multiple(() =>
            {
                Assert.That(template, Is.EqualTo(EmailTemplate.SecurityAlert));
                Assert.That(parameters[EmailTemplateParameterNames.SecurityTitle], Is.EqualTo("New login to your account"));
                Assert.That(parameters[EmailTemplateParameterNames.SecurityContent], Does.Contain("8.8.8.8"));
                Assert.That(parameters[EmailTemplateParameterNames.OccurredAt], Does.EndWith("+00:00 (UTC)"));
            });

            string[] parts = payload.AccessToken.Split('.');
            Assert.That(parts.Length, Is.EqualTo(3), "JWT must have3 parts");

            await TestContext.Progress.WriteLineAsync(
                $"Login OK. Token: {payload.AccessToken[..Math.Min(16, payload.AccessToken.Length)]}...");
        }

        [Test]
        public async Task Refresh_Returns_Auth_Session()
        {
            Assert.That(_client, Is.Not.Null);

            using HttpResponseMessage login = await PostLoginAsync(
                "testuser",
                "testpassword",
                "8.8.8.8");
            login.EnsureSuccessStatusCode();
            string refreshCookie = login.Headers
                .GetValues("Set-Cookie")
                .Select(value => value.Split(';', 2)[0])
                .Single(value => value.StartsWith("refresh_token=", StringComparison.Ordinal));

            using HttpRequestMessage request = new(
                HttpMethod.Post,
                "/api/v1/auth/refresh");
            request.Headers.Add("Cookie", refreshCookie);
            request.Content = JsonContent.Create(new { });

            using HttpResponseMessage response = await _client!.SendAsync(request);
            response.EnsureSuccessStatusCode();
            AuthSessionResponseDto? payload = await response.Content
                .ReadFromJsonAsync<AuthSessionResponseDto>();

            Assert.Multiple(() =>
            {
                Assert.That(payload, Is.Not.Null);
                Assert.That(payload!.AccessToken, Is.Not.Empty);
                Assert.That(payload.RefreshToken, Is.Not.Empty);
                Assert.That(payload.User.Username, Is.EqualTo("testuser"));
                Assert.That(payload.User.Id, Is.Not.EqualTo(Guid.Empty));
            });
        }

        [Test]
        public async Task Login_Succeeds_WhenSecurityEmailFails()
        {
            RecordingNotificationsProvider notifications = _notifications
                ?? throw new InvalidOperationException("Notifications provider is not configured.");
            notifications.ThrowOnEmail = true;

            TokenPairResponseDto payload = await LoginAsync("emailfailure", "testpassword");

            Assert.That(string.IsNullOrWhiteSpace(payload.AccessToken), Is.False, "Token must be present");
        }

        [Test]
        public async Task Login_CannotBypassRateLimitByChangingForwardedAddress()
        {
            Assert.That(_client, Is.Not.Null);

            const string ipAddress = "9.9.9.9";
            using HttpResponseMessage firstLogin = await PostLoginAsync(
                "limiteduser",
                "testpassword",
                ipAddress);
            Assert.That(firstLogin.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            for (int i = 0; i < 9; i++)
            {
                using HttpResponseMessage failedLogin = await PostLoginAsync(
                    "limiteduser",
                    "wrong-password",
                    ipAddress);
                Assert.That(failedLogin.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            }

            using HttpResponseMessage limitedLogin = await PostLoginAsync(
                "limiteduser",
                "wrong-password",
                "8.8.4.4");
            CottonResult? result = await limitedLogin.Content.ReadFromJsonAsync<CottonResult>();
            Assert.Multiple(() =>
            {
                Assert.That(limitedLogin.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
                Assert.That(limitedLogin.Headers.RetryAfter?.Delta?.TotalSeconds, Is.GreaterThan(1));
                Assert.That(result?.MessageCode, Is.EqualTo("rate_limit_exceeded"));
                Assert.That(result?.Message, Is.EqualTo("Too many requests. Retry later."));
            });
        }

        [Test]
        public async Task Login_FromUntrustedProxy_ReturnsForbidden()
        {
            Assert.That(_client, Is.Not.Null);
            Assert.That(_customFactory, Is.Not.Null);

            using IServiceScope scope = _customFactory!.Services.CreateScope();
            SettingsProvider settingsProvider = scope.ServiceProvider.GetRequiredService<SettingsProvider>();
            ServerSettingsSnapshot settings = settingsProvider.GetServerSettings();
            IPAddress? previousTrustedProxyIpAddress = settings.TrustedProxyIpAddress;
            byte? previousTrustedProxyPrefixLength = settings.TrustedProxyPrefixLength;
            await settingsProvider.UpdateSettingsAsync(
                current =>
                {
                    current.TrustedProxyIpAddress = IPAddress.Parse("192.0.2.10");
                    current.TrustedProxyPrefixLength = null;
                },
                fallbackPublicBaseUrl: null);

            try
            {
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
                {
                    Content = JsonContent.Create(new CottonLoginRequestDto
                    {
                        Username = "untrusted-proxy",
                        Password = "testpassword"
                    })
                };
                request.Headers.Add(TestAppFactory.RemoteIpAddressHeader, "192.0.2.11");
                request.Headers.Add("CF-Connecting-IP", "203.0.113.40");

                using HttpResponseMessage response = await _client!.SendAsync(request);
                CottonResult? result = await response.Content.ReadFromJsonAsync<CottonResult>();

                Assert.Multiple(() =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
                    Assert.That(result?.MessageCode, Is.EqualTo("untrusted_proxy_connection"));
                    Assert.That(result?.Message, Does.Not.Contain("192.0.2.10"));
                    Assert.That(result?.Message, Does.Not.Contain("192.0.2.11"));
                });
            }
            finally
            {
                await settingsProvider.UpdateSettingsAsync(
                    current =>
                    {
                        current.TrustedProxyIpAddress = previousTrustedProxyIpAddress;
                        current.TrustedProxyPrefixLength = previousTrustedProxyPrefixLength;
                    },
                    fallbackPublicBaseUrl: null);
            }
        }

        [Test]
        public async Task Login_StoresClientMetadataInSession()
        {
            Assert.That(_client, Is.Not.Null);

            using HttpResponseMessage login = await PostLoginAsync(
                "deviceuser",
                "testpassword",
                "8.8.4.4",
                "Cotton Sync Desktop (CI workstation)");
            login.EnsureSuccessStatusCode();

            TokenPairResponseDto? payload = await login.Content.ReadFromJsonAsync<TokenPairResponseDto>();
            Assert.That(payload, Is.Not.Null);

            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload!.AccessToken);
            List<SessionDto>? sessions = await _client.GetFromJsonAsync<List<SessionDto>>("/api/v1/auth/sessions");

            SessionDto? session = sessions?.Single(session => session.IsCurrentSession);
            Assert.Multiple(() =>
            {
                Assert.That(session?.Device, Is.EqualTo("Cotton Sync Desktop (CI workstation)"));
                Assert.That(session?.IpAddress, Is.EqualTo("8.8.4.4"));
            });
        }

        [Test]
        public async Task RevokeSession_Invalidates_Current_AccessToken()
        {
            Assert.That(_client, Is.Not.Null);

            TokenPairResponseDto login = await LoginAsync("testuser", "testpassword");
            string sessionId = new JwtSecurityTokenHandler()
                .ReadJwtToken(login.AccessToken)
                .Claims
                .First(c => c.Type == JwtRegisteredClaimNames.Sid)
                .Value;

            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
            using HttpResponseMessage beforeRevoke = await _client.GetAsync("/api/v1/auth/me");
            Assert.That(beforeRevoke.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            using HttpResponseMessage revoke = await _client.DeleteAsync($"/api/v1/auth/sessions/{sessionId}");
            Assert.That(revoke.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            using HttpResponseMessage afterRevoke = await _client.GetAsync("/api/v1/auth/me");
            Assert.That(afterRevoke.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task ChangePassword_Invalidates_Current_AccessToken()
        {
            Assert.That(_client, Is.Not.Null);

            TokenPairResponseDto login = await LoginAsync("testuser", "testpassword");
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

            using HttpResponseMessage beforeChange = await _client.GetAsync("/api/v1/auth/me");
            Assert.That(beforeChange.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            using HttpResponseMessage change = await _client.PutAsJsonAsync(
                "/api/v1/users/me/password",
                new ServerChangePasswordRequestDto
                {
                    OldPassword = "testpassword",
                    NewPassword = "changed-testpassword"
                });
            Assert.That(change.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            using HttpResponseMessage afterChange = await _client.GetAsync("/api/v1/auth/me");
            Assert.That(afterChange.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }
    }
}
