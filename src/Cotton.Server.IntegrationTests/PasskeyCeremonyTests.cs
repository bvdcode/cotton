// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
using Microsoft.AspNetCore.WebUtilities;

namespace Cotton.Server.IntegrationTests
{
    public class PasskeyCeremonyTests : AuthEndpointTestBase
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task RegistrationAndSignInPreserveBrowserContractAndStoredKey(bool withUsername)
        {
            AuthSessionResponseDto login = await LoginAsync("passkey-user", "testpassword");
            using PasskeyTestAuthenticator authenticator = new();
            await RegisterAsync(login, authenticator);

            using HttpResponseMessage optionsResponse = await _client!.PostAsJsonAsync("/api/v1/auth/passkeys/assertion/options",
                new { username = withUsername ? login.User.Username : null });
            optionsResponse.EnsureSuccessStatusCode();
            PasskeyAssertionOptionsResponseDto options = (await optionsResponse.Content.ReadFromJsonAsync<PasskeyAssertionOptionsResponseDto>())!;
            Assert.That(options.Options.GetProperty("userVerification").GetString(), Is.EqualTo("required"));
            Assert.That(options.Options.GetProperty("allowCredentials").GetArrayLength(), Is.EqualTo(withUsername ? 1 : 0));
            FinishPasskeyAssertionRequestDto request = new()
            {
                RequestId = options.RequestId,
                Credential = authenticator.Assert(options.Options, withUsername ? null : login.User.Id.ToByteArray()),
            };
            using HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/auth/passkeys/assertion/verify", request);
            response.EnsureSuccessStatusCode();
            AuthSessionResponseDto signedIn = (await response.Content.ReadFromJsonAsync<AuthSessionResponseDto>())!;
            Assert.That(signedIn.User.Id, Is.EqualTo(login.User.Id));
            Assert.That(signedIn.AccessToken, Is.Not.Empty);

            await using AsyncServiceScope scope = _customFactory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            UserPasskeyCredential stored = await db.UserPasskeyCredentials.SingleAsync();
            Assert.Multiple(() =>
            {
                Assert.That(stored.PublicKey, Is.EqualTo(authenticator.PublicKey));
                Assert.That(stored.UserHandle, Is.EqualTo(login.User.Id.ToByteArray()));
                Assert.That(stored.SignatureCounter, Is.EqualTo(1));
                Assert.That(stored.LastUsedAt, Is.Not.Null);
                Assert.That(stored.Transports, Is.EquivalentTo(new[] { "internal", "hybrid" }));
            });
            scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityVerifier>().RequireValid(db, stored, "passkey.test");
            using HttpResponseMessage replay = await _client.PostAsJsonAsync("/api/v1/auth/passkeys/assertion/verify", request);
            Assert.That(replay.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [TestCase("origin")]
        [TestCase("challenge")]
        [TestCase("signature")]
        [TestCase("user-handle")]
        [TestCase("missing-user-handle")]
        [TestCase("base64")]
        [TestCase("cbor")]
        [TestCase("json")]
        [TestCase("type")]
        [TestCase("counter")]
        public async Task InvalidAssertionDoesNotUpdateCredentialOrIssueSession(string failure)
        {
            AuthSessionResponseDto login = await LoginAsync("passkey-user", "testpassword");
            using PasskeyTestAuthenticator authenticator = new();
            await RegisterAsync(login, authenticator);
            using HttpResponseMessage optionsResponse = await _client!.PostAsJsonAsync("/api/v1/auth/passkeys/assertion/options", new { });
            optionsResponse.EnsureSuccessStatusCode();
            PasskeyAssertionOptionsResponseDto options = (await optionsResponse.Content.ReadFromJsonAsync<PasskeyAssertionOptionsResponseDto>())!;
            PasskeyAssertionCredentialDto credential = authenticator.Assert(options.Options, login.User.Id.ToByteArray());
            switch (failure)
            {
                case "origin":
                    credential = authenticator.Assert(options.Options, login.User.Id.ToByteArray(), origin: "https://attacker.example");
                    break;
                case "challenge":
                    byte[] data = WebEncoders.Base64UrlDecode(credential.Response.ClientDataJson);
                    string text = Encoding.UTF8.GetString(data).Replace(options.Options.GetProperty("challenge").GetString()!, "wrong");
                    credential.Response.ClientDataJson = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(text));
                    break;
                case "signature":
                    credential.Response.Signature = "AQ";
                    break;
                case "user-handle":
                    credential.Response.UserHandle = "AQ";
                    break;
                case "missing-user-handle":
                    credential.Response.UserHandle = null;
                    break;
                case "base64":
                    credential.Response.ClientDataJson = "!";
                    break;
                case "cbor":
                    credential.Response.AuthenticatorData = "AQ";
                    break;
                case "json":
                    credential.Response.ClientDataJson = "AQ";
                    break;
                case "type":
                    credential.Type = "password";
                    break;
                case "counter":
                    await using (AsyncServiceScope scope = _customFactory!.Services.CreateAsyncScope())
                    {
                        CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                        UserPasskeyCredential stored = await db.UserPasskeyCredentials.SingleAsync();
                        stored.SignatureCounter = 1;
                        await db.SaveChangesAsync();
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
            using HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/auth/passkeys/assertion/verify",
                new FinishPasskeyAssertionRequestDto { RequestId = options.RequestId, Credential = credential });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            await using AsyncServiceScope verificationScope = _customFactory!.Services.CreateAsyncScope();
            CottonDbContext verificationDb = verificationScope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Assert.That((await verificationDb.UserPasskeyCredentials.SingleAsync()).LastUsedAt, Is.Null);
        }

        [TestCase("base64")]
        [TestCase("json")]
        [TestCase("cbor")]
        [TestCase("type")]
        public async Task MalformedRegistrationIsRejectedWithoutSavingCredential(string failure)
        {
            AuthSessionResponseDto login = await LoginAsync("passkey-user", "testpassword");
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
            using HttpResponseMessage optionsResponse = await _client.PostAsJsonAsync("/api/v1/auth/passkeys/registration/options", new { });
            optionsResponse.EnsureSuccessStatusCode();
            PasskeyRegistrationOptionsResponseDto options = (await optionsResponse.Content.ReadFromJsonAsync<PasskeyRegistrationOptionsResponseDto>())!;
            using PasskeyTestAuthenticator authenticator = new();
            PasskeyAttestationCredentialDto credential = authenticator.Register(options.Options);
            switch (failure)
            {
                case "base64":
                    credential.Response.AttestationObject = "!";
                    break;
                case "json":
                    credential.Response.ClientDataJson = "AQ";
                    break;
                case "cbor":
                    credential.Response.AttestationObject = "AQ";
                    break;
                case "type":
                    credential.Type = "password";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
            using HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/auth/passkeys/registration/verify",
                new FinishPasskeyRegistrationRequestDto { RequestId = options.RequestId, Credential = credential });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            await using AsyncServiceScope scope = _customFactory!.Services.CreateAsyncScope();
            Assert.That(await scope.ServiceProvider.GetRequiredService<CottonDbContext>().UserPasskeyCredentials.AnyAsync(), Is.False);
        }

        [Test]
        public async Task SignInCannotUseAnotherAccountsCredential()
        {
            AuthSessionResponseDto owner = await LoginAsync("key-owner", "testpassword");
            using PasskeyTestAuthenticator authenticator = new();
            await RegisterAsync(owner, authenticator);
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.AccessToken);
            using HttpResponseMessage created = await _client.PostAsJsonAsync("/api/v1/users",
                new { username = "otheruser", email = "other@example.com", password = "UserPass_123", role = UserRole.User });
            created.EnsureSuccessStatusCode();
            _client.DefaultRequestHeaders.Authorization = null;
            using HttpResponseMessage optionsResponse = await _client!.PostAsJsonAsync("/api/v1/auth/passkeys/assertion/options",
                new { username = "otheruser" });
            optionsResponse.EnsureSuccessStatusCode();
            PasskeyAssertionOptionsResponseDto options = (await optionsResponse.Content.ReadFromJsonAsync<PasskeyAssertionOptionsResponseDto>())!;
            using HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/auth/passkeys/assertion/verify",
                new FinishPasskeyAssertionRequestDto
                {
                    RequestId = options.RequestId,
                    Credential = authenticator.Assert(options.Options, owner.User.Id.ToByteArray()),
                });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        private async Task RegisterAsync(AuthSessionResponseDto login, PasskeyTestAuthenticator authenticator)
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
            using HttpResponseMessage optionsResponse = await _client.PostAsJsonAsync("/api/v1/auth/passkeys/registration/options", new { label = "Device" });
            optionsResponse.EnsureSuccessStatusCode();
            PasskeyRegistrationOptionsResponseDto options = (await optionsResponse.Content.ReadFromJsonAsync<PasskeyRegistrationOptionsResponseDto>())!;
            Assert.That(options.Options.GetProperty("challenge").GetString(), Does.Not.Contain("="));
            Assert.That(options.Options.GetProperty("user").GetProperty("id").GetString(),
                Is.EqualTo(WebEncoders.Base64UrlEncode(login.User.Id.ToByteArray())));
            Assert.That(options.Options.GetProperty("authenticatorSelection").GetProperty("residentKey").GetString(), Is.EqualTo("required"));
            using HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/auth/passkeys/registration/verify",
                new FinishPasskeyRegistrationRequestDto { RequestId = options.RequestId, Credential = authenticator.Register(options.Options) });
            response.EnsureSuccessStatusCode();
            PasskeyCredentialDto registered = (await response.Content.ReadFromJsonAsync<PasskeyCredentialDto>())!;
            Assert.That(registered.Label, Is.EqualTo("Device"));
            _client.DefaultRequestHeaders.Authorization = null;
        }
    }
}
