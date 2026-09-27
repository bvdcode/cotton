// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
using Cotton.Server.Models;
using Cotton.Server.Models.Requests;
using OtpNet;

namespace Cotton.Server.IntegrationTests
{
    public class AuthTotpTests : AuthEndpointTestBase
    {
        [Test]
        public async Task Totp_Setup_Confirm_And_Disable_Reset_Failed_Attempts()
        {
            Assert.That(_client, Is.Not.Null);

            AuthSessionResponseDto login = await LoginAsync("totpuser", "testpassword");
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

            await SetupAndConfirmTotpAsync();

            await SetTotpFailedAttemptsAsync("totpuser", 1);
            using HttpRequestMessage disableRequest = new(HttpMethod.Delete, "/api/v1/auth/totp/disable")
            {
                Content = JsonContent.Create(new DisableTotpRequestDto { Password = "testpassword" }),
            };
            using HttpResponseMessage disableResponse = await _client.SendAsync(disableRequest);
            Assert.That(disableResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await GetTotpFailedAttemptsAsync("totpuser"), Is.Zero);

            await SetTotpFailedAttemptsAsync("totpuser", 1);
            await SetupAndConfirmTotpAsync();
            Assert.That(await GetTotpFailedAttemptsAsync("totpuser"), Is.Zero);
        }

        private async Task SetupAndConfirmTotpAsync()
        {
            using HttpResponseMessage setupResponse = await _client!.PostAsync(
                "/api/v1/auth/totp/setup",
                content: null);
            TotpSetup? setup = await setupResponse.Content.ReadFromJsonAsync<TotpSetup>();
            Assert.Multiple(() =>
            {
                Assert.That(setupResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(setup, Is.Not.Null);
                Assert.That(
                    setup?.OtpAuthUri,
                    Does.StartWith("otpauth://totp/cotton:totpuser%40localhost?"));
                Assert.That(
                    setup?.OtpAuthUri,
                    Does.Contain("&imagelink=http%3A%2F%2Flocalhost%2Fassets%2Ficons%2Ficon-192.png"));
            });

            Totp totp = new(Base32Encoding.ToBytes(setup!.SecretBase32));
            using HttpResponseMessage confirmResponse = await _client.PostAsJsonAsync(
                "/api/v1/auth/totp/confirm",
                new ConfirmTotpRequestDto { TwoFactorCode = totp.ComputeTotp() });
            Assert.That(confirmResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        private async Task SetTotpFailedAttemptsAsync(string username, int failedAttempts)
        {
            await using AsyncServiceScope scope = _customFactory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            User user = await dbContext.Users.SingleAsync(user => user.Username == username);
            user.TotpFailedAttempts = failedAttempts;
            await dbContext.SaveChangesAsync();
        }
    }
}
