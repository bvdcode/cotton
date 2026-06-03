// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Auth;

namespace Cotton.Sync.Desktop.Tests.Auth;

public sealed class DesktopTokenStorageCapabilitiesTests
{
    [Test]
    public void CreateSnapshot_MarksRestrictedFileProtectorAsDevelopmentFallback()
    {
        DesktopTokenStorageCapabilitySnapshot snapshot = DesktopTokenStorageCapabilities
            .CreateSnapshot(new RestrictedFileTokenPayloadProtector());

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Scheme, Is.EqualTo("restricted-file-v1"));
            Assert.That(snapshot.IsReleaseSecure, Is.False);
            Assert.That(snapshot.Details, Does.Contain("Development fallback"));
        });
    }

    [Test]
    public void CreateSnapshot_MarksLinuxSecretServiceProtectorAsReleaseSecure()
    {
        DesktopTokenStorageCapabilitySnapshot snapshot = DesktopTokenStorageCapabilities
            .CreateSnapshot(new LinuxSecretServiceTokenPayloadProtector("/usr/bin/secret-tool", new NoopSecretToolProcessRunner()));

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Scheme, Is.EqualTo("linux-secret-service-v1"));
            Assert.That(snapshot.IsReleaseSecure, Is.True);
            Assert.That(snapshot.Details, Does.Contain("Linux Secret Service"));
        });
    }

    [Test]
    public void CreateSnapshot_MarksWindowsDpapiProtectorAsReleaseSecure()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Pass("DPAPI token storage capability check is only applicable on Windows.");
            return;
        }

        DesktopTokenStorageCapabilitySnapshot snapshot = DesktopTokenStorageCapabilities
            .CreateSnapshot(new WindowsDpapiTokenPayloadProtector());

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Scheme, Is.EqualTo("windows-dpapi-current-user-v1"));
            Assert.That(snapshot.IsReleaseSecure, Is.True);
            Assert.That(snapshot.Details, Does.Contain("Windows DPAPI"));
        });
    }

    private sealed class NoopSecretToolProcessRunner : ISecretToolProcessRunner
    {
        public Task RunAsync(
            System.Diagnostics.ProcessStartInfo startInfo,
            string? standardInput,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<string> ReadAsync(System.Diagnostics.ProcessStartInfo startInfo, CancellationToken cancellationToken)
        {
            return Task.FromResult(string.Empty);
        }
    }
}
