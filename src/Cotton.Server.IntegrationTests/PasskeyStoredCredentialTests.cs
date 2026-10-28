// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services.Passkeys;
using EasyExtensions.Passkeys;
using EasyExtensions.Passkeys.Models;
using System.Buffers.Text;

namespace Cotton.Server.IntegrationTests
{
    public class PasskeyStoredCredentialTests
    {
        [TestCase("ES256")]
        [TestCase("RS256")]
        [TestCase("Ed25519")]
        public async Task ExistingCosePublicKeysVerifyWithoutConversion(string algorithm)
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "passkey-credentials.json");
            using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            JsonElement fixture = document.RootElement.GetProperty(algorithm);
            PasskeyServer server = new(new PasskeyServerOptions(fixture.GetProperty("rpId").GetString()!, "Cotton",
                [fixture.GetProperty("origin").GetString()!]));
            AuthenticationOptions options = server.CreateAuthenticationOptions() with
            {
                Challenge = fixture.GetProperty("challenge").GetBytesFromBase64(),
            };
            byte[] credentialId = fixture.GetProperty("credentialId").GetBytesFromBase64();
            StoredCredential stored = new(credentialId, fixture.GetProperty("publicKey").GetBytesFromBase64(),
                fixture.GetProperty("userHandle").GetBytesFromBase64(), 7, false);
            AuthenticationResponse response = new(Base64Url.EncodeToString(credentialId),
                credentialId, "public-key", new AssertionResponse(fixture.GetProperty("clientDataJson").GetBytesFromBase64(),
                    fixture.GetProperty("authenticatorData").GetBytesFromBase64(), fixture.GetProperty("signature").GetBytesFromBase64())
                {
                    UserHandle = stored.UserHandle,
                });
            AuthenticationResult result = server.VerifyAuthentication(response, options, stored);
            Assert.That(result.SignatureCounter, Is.EqualTo(8));
            response.Response.Signature[0] ^= 1;
            Assert.Throws<PasskeyVerificationException>(() => server.VerifyAuthentication(response, options, stored));
        }

        [Test]
        public void TransportNamesPreserveBrowserValues()
        {
            CredentialDescriptor descriptor = PasskeyProtocolMapper.CreateCredentialDescriptor([1], ["INTERNAL", "smart-card", "hybrid", "other", null!]);
            Assert.That(PasskeyProtocolMapper.NormalizeTransports(descriptor.Transports), Is.EqualTo(new[] { "hybrid", "internal", "smart-card" }));
        }
    }
}
