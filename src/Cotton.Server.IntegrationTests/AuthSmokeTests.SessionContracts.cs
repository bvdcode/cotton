// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using NUnit.Framework;
using OtpNet;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cotton.Server.IntegrationTests
{
    public partial class AuthSmokeTests
    {
        [Test]
        public async Task Login_InvalidPassword_PreservesProblemDetailsContract()
        {
            await LoginAsync("testuser", "testpassword");
            using HttpResponseMessage response = await PostLoginAsync("testuser", "wrong-password", "8.8.8.8");
            await AssertProblemAsync(response, HttpStatusCode.Unauthorized,
                "Invalid username or password", "unauthorized", "/api/v1/auth/login");
        }

        [Test]
        public async Task Refresh_MissingOrRevokedToken_PreservesProblemDetailsContract()
        {
            using HttpResponseMessage missing = await _client!.PostAsync("/api/v1/auth/refresh", null);
            await AssertProblemAsync(missing, HttpStatusCode.NotFound,
                "Refresh token was not provided.", "not_found", "/api/v1/auth/refresh");

            AuthSessionResponseDto login = await LoginAsync("testuser", "testpassword");
            using HttpResponseMessage refresh = await _client.PostAsync(
                $"/api/v1/auth/refresh?refreshToken={Uri.EscapeDataString(login.RefreshToken)}", null);
            refresh.EnsureSuccessStatusCode();
            AuthSessionResponseDto? renewed = await refresh.Content.ReadFromJsonAsync<AuthSessionResponseDto>();
            Assert.That(renewed?.RefreshToken, Is.Not.Null.And.Not.EqualTo(login.RefreshToken));

            using HttpResponseMessage replay = await _client.PostAsync(
                $"/api/v1/auth/refresh?refreshToken={Uri.EscapeDataString(login.RefreshToken)}", null);
            await AssertProblemAsync(replay, HttpStatusCode.NotFound,
                "Refresh token not found or revoked.", "not_found", "/api/v1/auth/refresh");
        }

        [Test]
        public async Task Logout_UsesCookie_RevokesAccessAndClearsBothCookies()
        {
            AuthSessionResponseDto login = await LoginAsync("testuser", "testpassword");
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/auth/logout");
            request.Headers.Add("Cookie", $"refresh_token={login.RefreshToken}");
            using HttpResponseMessage response = await _client!.SendAsync(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            string[] cookies = response.Headers.GetValues("Set-Cookie").ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(cookies.Any(cookie => cookie.StartsWith("refresh_token=;", StringComparison.Ordinal)), Is.True);
                Assert.That(cookies.Any(cookie => cookie.StartsWith("access_token=;", StringComparison.Ordinal)), Is.True);
            });

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
            using HttpResponseMessage me = await _client.GetAsync("/api/v1/auth/me");
            Assert.That(me.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Login_WithTotp_PreservesFailuresAndResetsCounterOnSuccess()
        {
            AuthSessionResponseDto login = await LoginAsync("totpuser", "testpassword");
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
            using HttpResponseMessage setupResponse = await _client.PostAsync("/api/v1/auth/totp/setup", null);
            setupResponse.EnsureSuccessStatusCode();
            TotpSetup setup = (await setupResponse.Content.ReadFromJsonAsync<TotpSetup>())!;
            Totp totp = new(Base32Encoding.ToBytes(setup.SecretBase32));
            using HttpResponseMessage confirm = await _client.PostAsJsonAsync(
                "/api/v1/auth/totp/confirm", new ConfirmTotpRequestDto { TwoFactorCode = totp.ComputeTotp() });
            confirm.EnsureSuccessStatusCode();
            _client.DefaultRequestHeaders.Authorization = null;

            using HttpResponseMessage missing = await PostLoginAsync("totpuser", "testpassword", "8.8.8.8");
            await AssertProblemAsync(missing, HttpStatusCode.Forbidden,
                "Two-factor authentication code is required", "forbidden", "/api/v1/auth/login");

            using HttpResponseMessage invalid = await PostTotpLoginAsync("invalid");
            await AssertProblemAsync(invalid, HttpStatusCode.Forbidden,
                "Invalid two-factor authentication code", "forbidden", "/api/v1/auth/login");
            Assert.That(await GetTotpFailedAttemptsAsync("totpuser"), Is.EqualTo(1));

            using HttpResponseMessage valid = await PostTotpLoginAsync(totp.ComputeTotp());
            Assert.That(valid.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await GetTotpFailedAttemptsAsync("totpuser"), Is.Zero);
        }

        private Task<HttpResponseMessage> PostTotpLoginAsync(string code)
        {
            return _client!.PostAsJsonAsync("/api/v1/auth/login", new Cotton.Auth.LoginRequestDto
            {
                Username = "totpuser",
                Password = "testpassword",
                TwoFactorCode = code,
            });
        }

        private static async Task AssertProblemAsync(
            HttpResponseMessage response, HttpStatusCode status, string detail, string code, string path)
        {
            using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            JsonElement body = json.RootElement;
            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(status));
                Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
                Assert.That(body.GetProperty("status").GetInt32(), Is.EqualTo((int)status));
                Assert.That(body.GetProperty("detail").GetString(), Is.EqualTo(detail));
                Assert.That(body.GetProperty("code").GetString(), Is.EqualTo(code));
                Assert.That(body.GetProperty("instance").GetString(), Is.EqualTo(path));
                Assert.That(body.GetProperty("traceId").GetString(), Is.Not.Null.And.Not.Empty);
            });
        }
    }
}
