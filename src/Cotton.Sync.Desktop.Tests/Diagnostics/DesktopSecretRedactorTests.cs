// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Diagnostics;

namespace Cotton.Sync.Desktop.Tests.Diagnostics
{

    public sealed class DesktopSecretRedactorTests
    {
        [Test]
        public void Redact_RemovesBearerToken()
        {
            string redacted = DesktopSecretRedactor.Redact("Authorization: Bearer access.token-value");

            Assert.Multiple(() =>
            {
                Assert.That(redacted, Does.Contain("Bearer [redacted]"));
                Assert.That(redacted, Does.Not.Contain("access.token-value"));
            });
        }

        [Test]
        public void Redact_RemovesBasicAuthorizationHeader()
        {
            string redacted = DesktopSecretRedactor.Redact("Authorization: Basic dXNlcjpwYXNzd29yZA==");

            Assert.Multiple(() =>
            {
                Assert.That(redacted, Does.Contain("Authorization: Basic [redacted]"));
                Assert.That(redacted, Does.Not.Contain("dXNlcjpwYXNzd29yZA=="));
            });
        }

        [Test]
        public void Redact_RemovesJsonSecrets()
        {
            string redacted = DesktopSecretRedactor.Redact(
                """{"accessToken":"access-token","refreshToken":"refresh-token","idToken":"id-token","clientSecret":"client-secret","password":"secret","apiKey":"api-key"}""");

            Assert.Multiple(() =>
            {
                Assert.That(redacted, Does.Contain("""accessToken":"[redacted]"""));
                Assert.That(redacted, Does.Contain("""refreshToken":"[redacted]"""));
                Assert.That(redacted, Does.Contain("""idToken":"[redacted]"""));
                Assert.That(redacted, Does.Contain("""clientSecret":"[redacted]"""));
                Assert.That(redacted, Does.Contain("""password":"[redacted]"""));
                Assert.That(redacted, Does.Contain("""apiKey":"[redacted]"""));
                Assert.That(redacted, Does.Not.Contain("access-token"));
                Assert.That(redacted, Does.Not.Contain("refresh-token"));
                Assert.That(redacted, Does.Not.Contain("id-token"));
                Assert.That(redacted, Does.Not.Contain("client-secret"));
                Assert.That(redacted, Does.Not.Contain("secret"));
                Assert.That(redacted, Does.Not.Contain("api-key"));
            });
        }

        [Test]
        public void Redact_RemovesQuerySecrets()
        {
            string redacted = DesktopSecretRedactor.Redact(
                "https://example.test/?accessToken=access-token&refresh_token=refresh-token&id_token=id-token&clientSecret=client-secret&totp_code=123456&api_key=api-key");

            Assert.Multiple(() =>
            {
                Assert.That(redacted, Does.Contain("accessToken=[redacted]&"));
                Assert.That(redacted, Does.Contain("refresh_token=[redacted]&"));
                Assert.That(redacted, Does.Contain("id_token=[redacted]&"));
                Assert.That(redacted, Does.Contain("clientSecret=[redacted]&"));
                Assert.That(redacted, Does.Contain("totp_code=[redacted]"));
                Assert.That(redacted, Does.Contain("api_key=[redacted]"));
                Assert.That(redacted, Does.Not.Contain("access-token"));
                Assert.That(redacted, Does.Not.Contain("refresh-token"));
                Assert.That(redacted, Does.Not.Contain("id-token"));
                Assert.That(redacted, Does.Not.Contain("client-secret"));
                Assert.That(redacted, Does.Not.Contain("123456"));
                Assert.That(redacted, Does.Not.Contain("api-key"));
            });
        }
    }
}
