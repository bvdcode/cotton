// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public class AuthAppCodeTests : AuthEndpointTestBase
    {
        [Test]
        public async Task AppCode_ApprovalCreatesSessionForRequestingDevice()
        {
            Assert.That(_client, Is.Not.Null);
            TokenPairResponseDto browserSession = await LoginAsync("app-code-user", "testpassword");
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", browserSession.AccessToken);

            using HttpRequestMessage startRequest = new(HttpMethod.Post, "/api/v1/oauth/app-code/start")
            {
                Content = JsonContent.Create(new AppCodeStartRequestDto
                {
                    ApplicationName = " Desktop ",
                    ApplicationVersion = " 1.0 ",
                    DeviceName = " Laptop ",
                }),
            };
            startRequest.Headers.Add("X-Forwarded-For", "203.0.113.42");
            using HttpResponseMessage started = await _client.SendAsync(startRequest);
            started.EnsureSuccessStatusCode();
            AppCodeStartResponseDto? start = await started.Content.ReadFromJsonAsync<AppCodeStartResponseDto>();
            Assert.That(start, Is.Not.Null);

            using HttpResponseMessage details = await _client.GetAsync($"/api/v1/oauth/app-code/{start!.ApprovalId:D}");
            details.EnsureSuccessStatusCode();
            AppCodeDetailsDto? pending = await details.Content.ReadFromJsonAsync<AppCodeDetailsDto>();
            Assert.Multiple(() =>
            {
                Assert.That(pending?.ApplicationName, Is.EqualTo("Desktop"));
                Assert.That(pending?.Origin, Is.EqualTo("203.0.113.42"));
                Assert.That(pending?.Status, Is.EqualTo("pending"));
            });

            using HttpResponseMessage approved = await _client.PostAsync(
                $"/api/v1/oauth/app-code/{start.ApprovalId:D}/approve", null);
            approved.EnsureSuccessStatusCode();
            using HttpResponseMessage polled = await _client.PostAsJsonAsync(
                "/api/v1/oauth/app-code/poll", new AppCodePollRequestDto { PollToken = start.PollToken });
            polled.EnsureSuccessStatusCode();
            TokenPairResponseDto? deviceSession = await polled.Content.ReadFromJsonAsync<TokenPairResponseDto>();
            Assert.That(deviceSession?.AccessToken, Is.Not.Empty);

            using HttpResponseMessage consumed = await _client.PostAsJsonAsync(
                "/api/v1/oauth/app-code/poll", new AppCodePollRequestDto { PollToken = start.PollToken });
            Assert.That(consumed.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task AppCode_DeniedRequestCannotBeApproved()
        {
            Assert.That(_client, Is.Not.Null);
            TokenPairResponseDto browserSession = await LoginAsync("app-code-denied-user", "testpassword");
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", browserSession.AccessToken);

            using HttpResponseMessage started = await _client.PostAsJsonAsync(
                "/api/v1/oauth/app-code/start", new AppCodeStartRequestDto { ApplicationName = "Desktop" });
            started.EnsureSuccessStatusCode();
            AppCodeStartResponseDto? start = await started.Content.ReadFromJsonAsync<AppCodeStartResponseDto>();
            Assert.That(start, Is.Not.Null);

            using HttpResponseMessage denied = await _client.PostAsync(
                $"/api/v1/oauth/app-code/{start!.ApprovalId:D}/deny", null);
            denied.EnsureSuccessStatusCode();
            using HttpResponseMessage polled = await _client.PostAsJsonAsync(
                "/api/v1/oauth/app-code/poll", new AppCodePollRequestDto { PollToken = start.PollToken });
            Assert.That(polled.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }
    }
}
