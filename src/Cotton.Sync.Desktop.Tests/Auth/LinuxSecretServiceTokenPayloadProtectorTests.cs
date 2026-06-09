// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Cotton.Sync.Desktop.Auth;

namespace Cotton.Sync.Desktop.Tests.Auth
{
    public sealed class LinuxSecretServiceTokenPayloadProtectorTests
    {
        [Test]
        public void CreateStoreStartInfo_UsesSecretToolStoreArguments()
        {
            ProcessStartInfo startInfo = LinuxSecretServiceTokenPayloadProtector
                .CreateStoreStartInfo("/usr/bin/secret-tool", "payload-id");

            Assert.Multiple(() =>
            {
                Assert.That(startInfo.FileName, Is.EqualTo("/usr/bin/secret-tool"));
                Assert.That(startInfo.UseShellExecute, Is.False);
                Assert.That(startInfo.CreateNoWindow, Is.True);
                Assert.That(
                    startInfo.ArgumentList.ToArray(),
                    Is.EqualTo(new[]
                    {
                        "store",
                        "--label",
                        "Cotton Sync Desktop tokens",
                        "service",
                        "cotton-sync",
                        "application",
                        "cotton-sync-desktop",
                        "purpose",
                        "token-payload",
                        "id",
                        "payload-id",
                    }));
            });
        }

        [Test]
        public void CreateLookupStartInfo_UsesSecretToolLookupArguments()
        {
            ProcessStartInfo startInfo = LinuxSecretServiceTokenPayloadProtector
                .CreateLookupStartInfo("/usr/bin/secret-tool", "payload-id");

            Assert.Multiple(() =>
            {
                Assert.That(startInfo.FileName, Is.EqualTo("/usr/bin/secret-tool"));
                Assert.That(
                    startInfo.ArgumentList.ToArray(),
                    Is.EqualTo(new[]
                    {
                        "lookup",
                        "service",
                        "cotton-sync",
                        "application",
                        "cotton-sync-desktop",
                        "purpose",
                        "token-payload",
                        "id",
                        "payload-id",
                    }));
            });
        }

        [Test]
        public void CreateClearStartInfo_UsesSecretToolClearArguments()
        {
            ProcessStartInfo startInfo = LinuxSecretServiceTokenPayloadProtector
                .CreateClearStartInfo("/usr/bin/secret-tool", "payload-id");

            Assert.Multiple(() =>
            {
                Assert.That(startInfo.FileName, Is.EqualTo("/usr/bin/secret-tool"));
                Assert.That(
                    startInfo.ArgumentList.ToArray(),
                    Is.EqualTo(new[]
                    {
                        "clear",
                        "service",
                        "cotton-sync",
                        "application",
                        "cotton-sync-desktop",
                        "purpose",
                        "token-payload",
                        "id",
                        "payload-id",
                    }));
            });
        }

        [Test]
        public async Task ProtectAndUnprotectAsync_RoundtripsThroughSecretToolRunner()
        {
            var runner = new FakeSecretToolProcessRunner();
            var protector = new LinuxSecretServiceTokenPayloadProtector("/usr/bin/secret-tool", runner);
            byte[] plaintext = Encoding.UTF8.GetBytes("token payload");

            byte[] protectedPayload = await protector.ProtectAsync(plaintext);
            byte[] roundtrip = await protector.UnprotectAsync(protectedPayload);

            Assert.Multiple(() =>
            {
                Assert.That(Encoding.UTF8.GetString(protectedPayload), Is.Not.EqualTo("token payload"));
                Assert.That(Encoding.UTF8.GetString(roundtrip), Is.EqualTo("token payload"));
                Assert.That(runner.StoredSecretIds, Has.Count.EqualTo(1));
                Assert.That(runner.StoreCallCount, Is.EqualTo(1));
                Assert.That(runner.LookupCallCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task DeleteAsync_ClearsSecretToolPayload()
        {
            var runner = new FakeSecretToolProcessRunner();
            var protector = new LinuxSecretServiceTokenPayloadProtector("/usr/bin/secret-tool", runner);

            byte[] protectedPayload = await protector.ProtectAsync(Encoding.UTF8.GetBytes("token payload"));
            string payloadId = Encoding.UTF8.GetString(protectedPayload);
            await protector.DeleteAsync(protectedPayload);

            Assert.Multiple(() =>
            {
                Assert.That(runner.StoredSecretIds, Is.Empty);
                Assert.That(runner.ClearedSecretIds, Is.EqualTo(new[] { payloadId }));
            });
        }

        [Test]
        public void UnprotectAsync_ThrowsWhenSecretToolReturnsInvalidBase64()
        {
            var runner = new FakeSecretToolProcessRunner
            {
                LookupOverride = "not base64",
            };
            var protector = new LinuxSecretServiceTokenPayloadProtector("/usr/bin/secret-tool", runner);
            byte[] protectedPayload = Encoding.UTF8.GetBytes("payload-id");

            Assert.ThrowsAsync<CryptographicException>(async () => await protector.UnprotectAsync(protectedPayload));
        }

        private sealed class FakeSecretToolProcessRunner : ISecretToolProcessRunner
        {
            private readonly Dictionary<string, string> _secrets = [];

            public int StoreCallCount { get; private set; }

            public int LookupCallCount { get; private set; }

            public IReadOnlyList<string> StoredSecretIds => _secrets.Keys.Order().ToList();

            public List<string> ClearedSecretIds { get; } = [];

            public string? LookupOverride { get; set; }

            public Task RunAsync(
                ProcessStartInfo startInfo,
                string? standardInput,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string command = startInfo.ArgumentList[0];
                string id = ReadId(startInfo);
                if (command == "store")
                {
                    StoreCallCount++;
                    _secrets[id] = standardInput ?? string.Empty;
                }
                else if (command == "clear")
                {
                    _secrets.Remove(id);
                    ClearedSecretIds.Add(id);
                }
                else
                {
                    throw new InvalidOperationException("Unsupported fake secret-tool command: " + command);
                }

                return Task.CompletedTask;
            }

            public Task<string> ReadAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LookupCallCount++;
                if (LookupOverride is not null)
                {
                    return Task.FromResult(LookupOverride);
                }

                string id = ReadId(startInfo);
                return Task.FromResult(_secrets[id]);
            }

            private static string ReadId(ProcessStartInfo startInfo)
            {
                string[] arguments = startInfo.ArgumentList.ToArray();
                int idIndex = Array.IndexOf(arguments, "id");
                if (idIndex < 0 || idIndex + 1 >= arguments.Length)
                {
                    throw new InvalidOperationException("Fake secret-tool call has no id attribute.");
                }

                return arguments[idIndex + 1];
            }
        }
    }
}
