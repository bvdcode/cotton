// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Diagnostics;

namespace Cotton.Sync.Desktop.Tests.Diagnostics;

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
    public void Redact_RemovesJsonSecrets()
    {
        string redacted = DesktopSecretRedactor.Redact(
            """{"accessToken":"access-token","refreshToken":"refresh-token","password":"secret"}""");

        Assert.Multiple(() =>
        {
            Assert.That(redacted, Does.Contain("""accessToken":"[redacted]"""));
            Assert.That(redacted, Does.Contain("""refreshToken":"[redacted]"""));
            Assert.That(redacted, Does.Contain("""password":"[redacted]"""));
            Assert.That(redacted, Does.Not.Contain("access-token"));
            Assert.That(redacted, Does.Not.Contain("refresh-token"));
            Assert.That(redacted, Does.Not.Contain("secret"));
        });
    }

    [Test]
    public void Redact_RemovesQuerySecrets()
    {
        string redacted = DesktopSecretRedactor.Redact(
            "https://example.test/?access_token=access-token&refresh_token=refresh-token&totp_code=123456");

        Assert.Multiple(() =>
        {
            Assert.That(redacted, Does.Contain("access_token=[redacted]&"));
            Assert.That(redacted, Does.Contain("refresh_token=[redacted]&"));
            Assert.That(redacted, Does.Contain("totp_code=[redacted]"));
            Assert.That(redacted, Does.Not.Contain("access-token"));
            Assert.That(redacted, Does.Not.Contain("refresh-token"));
            Assert.That(redacted, Does.Not.Contain("123456"));
        });
    }
}
