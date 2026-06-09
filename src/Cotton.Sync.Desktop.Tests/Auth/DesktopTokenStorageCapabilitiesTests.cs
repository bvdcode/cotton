// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Security.Cryptography;
using Cotton.Sync.Desktop.Auth;

namespace Cotton.Sync.Desktop.Tests.Auth
{

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
        public void CreateSnapshot_MarksUnsupportedProtectorAsUnavailable()
        {
            DesktopTokenStorageCapabilitySnapshot snapshot = DesktopTokenStorageCapabilities
                .CreateSnapshot(new UnsupportedTokenPayloadProtector(
                    "test-unsupported-v1",
                    "Secure token storage is unavailable in this test session."));

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Scheme, Is.EqualTo("test-unsupported-v1"));
                Assert.That(snapshot.IsReleaseSecure, Is.False);
                Assert.That(snapshot.Details, Does.Contain("unavailable"));
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

        [Test]
        public async Task CreateVerifiedSnapshotAsync_VerifiesLinuxSecretServiceRoundtrip()
        {
            var runner = new RoundtripSecretToolProcessRunner();
            var protector = new LinuxSecretServiceTokenPayloadProtector("/usr/bin/secret-tool", runner);

            DesktopTokenStorageCapabilitySnapshot snapshot = await DesktopTokenStorageCapabilities
                .CreateVerifiedSnapshotAsync(protector, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.IsReleaseSecure, Is.True);
                Assert.That(snapshot.Details, Does.Contain("verified"));
                Assert.That(runner.RemainingSecretCount, Is.Zero);
            });
        }

        [Test]
        public async Task CreateVerifiedSnapshotAsync_FailsWhenReleaseSecureProtectorIsUnavailable()
        {
            var protector = new LinuxSecretServiceTokenPayloadProtector(
                "/usr/bin/secret-tool",
                new FailingSecretToolProcessRunner());

            DesktopTokenStorageCapabilitySnapshot snapshot = await DesktopTokenStorageCapabilities
                .CreateVerifiedSnapshotAsync(protector, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.IsReleaseSecure, Is.False);
                Assert.That(snapshot.Details, Does.Contain("could not be verified"));
                Assert.That(snapshot.Details, Does.Not.Contain("Secret Service unavailable"));
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

        private sealed class RoundtripSecretToolProcessRunner : ISecretToolProcessRunner
        {
            private readonly Dictionary<string, string> _secrets = [];

            public int RemainingSecretCount => _secrets.Count;

            public Task RunAsync(
                ProcessStartInfo startInfo,
                string? standardInput,
                CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(startInfo);
                cancellationToken.ThrowIfCancellationRequested();
                string command = startInfo.ArgumentList[0];
                string id = ResolveId(startInfo);
                if (command == "store")
                {
                    _secrets[id] = standardInput ?? string.Empty;
                }
                else if (command == "clear")
                {
                    _secrets.Remove(id);
                }

                return Task.CompletedTask;
            }

            public Task<string> ReadAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(startInfo);
                cancellationToken.ThrowIfCancellationRequested();
                string id = ResolveId(startInfo);
                if (!_secrets.TryGetValue(id, out string? secret))
                {
                    throw new CryptographicException("Secret Service test secret was not found.");
                }

                return Task.FromResult(secret);
            }

            private static string ResolveId(ProcessStartInfo startInfo)
            {
                for (int index = 0; index < startInfo.ArgumentList.Count - 1; index++)
                {
                    if (startInfo.ArgumentList[index] == "id")
                    {
                        return startInfo.ArgumentList[index + 1];
                    }
                }

                throw new CryptographicException("Secret Service test command did not include an id.");
            }
        }

        private sealed class FailingSecretToolProcessRunner : ISecretToolProcessRunner
        {
            public Task RunAsync(
                ProcessStartInfo startInfo,
                string? standardInput,
                CancellationToken cancellationToken)
            {
                throw new CryptographicException("Secret Service unavailable.");
            }

            public Task<string> ReadAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
            {
                throw new CryptographicException("Secret Service unavailable.");
            }
        }
    }
}
