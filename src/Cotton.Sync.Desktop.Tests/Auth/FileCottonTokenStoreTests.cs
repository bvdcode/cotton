// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;
using System.Text;
using Cotton.Contracts.Auth;
using Cotton.Sync.Desktop.Auth;

namespace Cotton.Sync.Desktop.Tests.Auth;

public sealed class FileCottonTokenStoreTests
{
    [Test]
    public async Task GetAsync_ReturnsNullWhenFileIsMissing()
    {
        string directory = CreateTempDirectory();
        try
        {
            var store = new FileCottonTokenStore(Path.Combine(directory, "tokens.json"));

            TokenPairDto? tokens = await store.GetAsync();

            Assert.That(tokens, Is.Null);
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Test]
    public async Task SaveAsync_WritesTokensAndLoadsIndependentCopy()
    {
        string directory = CreateTempDirectory();
        try
        {
            string path = Path.Combine(directory, "tokens.json");
            var store = new FileCottonTokenStore(path);
            var tokens = new TokenPairDto
            {
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
            };

            await store.SaveAsync(tokens);
            tokens.AccessToken = "mutated";
            tokens.RefreshToken = "mutated";
            TokenPairDto? loaded = await new FileCottonTokenStore(path).GetAsync();

            Assert.Multiple(() =>
            {
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded!.AccessToken, Is.EqualTo("access-token"));
                Assert.That(loaded.RefreshToken, Is.EqualTo("refresh-token"));
            });
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Test]
    public async Task SaveAsync_WritesProtectedEnvelopeWithoutPlaintextTokens()
    {
        string directory = CreateTempDirectory();
        try
        {
            string path = Path.Combine(directory, "tokens.json");
            var protector = new ReversingTokenPayloadProtector("test-protector-v1");
            var store = new FileCottonTokenStore(path, protector);

            await store.SaveAsync(new TokenPairDto
            {
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
            });

            string persisted = File.ReadAllText(path);
            TokenPairDto? loaded = await new FileCottonTokenStore(path, protector).GetAsync();

            Assert.Multiple(() =>
            {
                Assert.That(persisted, Does.Contain("test-protector-v1"));
                Assert.That(persisted, Does.Not.Contain("access-token"));
                Assert.That(persisted, Does.Not.Contain("refresh-token"));
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded!.AccessToken, Is.EqualTo("access-token"));
                Assert.That(loaded.RefreshToken, Is.EqualTo("refresh-token"));
            });
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Test]
    public async Task GetAsync_ReturnsNullForDifferentProtectionScheme()
    {
        string directory = CreateTempDirectory();
        try
        {
            string path = Path.Combine(directory, "tokens.json");
            await new FileCottonTokenStore(path, new ReversingTokenPayloadProtector("first-scheme"))
                .SaveAsync(new TokenPairDto
                {
                    AccessToken = "access-token",
                    RefreshToken = "refresh-token",
                });

            TokenPairDto? loaded = await new FileCottonTokenStore(
                    path,
                    new ReversingTokenPayloadProtector("second-scheme"))
                .GetAsync();

            Assert.That(loaded, Is.Null);
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Test]
    public async Task GetAsync_ReturnsNullForUnreadableProtectedPayload()
    {
        string directory = CreateTempDirectory();
        try
        {
            string path = Path.Combine(directory, "tokens.json");
            await new FileCottonTokenStore(path, new ReversingTokenPayloadProtector("broken-scheme"))
                .SaveAsync(new TokenPairDto
                {
                    AccessToken = "access-token",
                    RefreshToken = "refresh-token",
                });

            TokenPairDto? loaded = await new FileCottonTokenStore(
                    path,
                    new ThrowingTokenPayloadProtector("broken-scheme"))
                .GetAsync();

            Assert.That(loaded, Is.Null);
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Test]
    public async Task ClearAsync_RemovesPersistedTokens()
    {
        string directory = CreateTempDirectory();
        try
        {
            string path = Path.Combine(directory, "tokens.json");
            var store = new FileCottonTokenStore(path);
            await store.SaveAsync(new TokenPairDto
            {
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
            });

            await store.ClearAsync();
            TokenPairDto? loaded = await store.GetAsync();

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(path), Is.False);
                Assert.That(loaded, Is.Null);
            });
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Test]
    public async Task ClearAsync_DeletesExternalProtectedPayloadWhenSupported()
    {
        string directory = CreateTempDirectory();
        try
        {
            string path = Path.Combine(directory, "tokens.json");
            var protector = new RecordingDeletableTokenPayloadProtector("external-scheme");
            var store = new FileCottonTokenStore(path, protector);

            await store.SaveAsync(new TokenPairDto
            {
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
            });

            Assert.That(protector.StoredPayloadIds, Has.Count.EqualTo(1));
            string savedPayloadId = protector.StoredPayloadIds[0];
            await store.ClearAsync();

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(path), Is.False);
                Assert.That(protector.DeletedPayloadIds, Is.EqualTo(new[] { savedPayloadId }));
            });
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Test]
    public async Task SaveAsync_DeletesPreviousExternalProtectedPayloadAfterOverwrite()
    {
        string directory = CreateTempDirectory();
        try
        {
            string path = Path.Combine(directory, "tokens.json");
            var protector = new RecordingDeletableTokenPayloadProtector("external-scheme");
            var store = new FileCottonTokenStore(path, protector);

            await store.SaveAsync(new TokenPairDto
            {
                AccessToken = "first-access-token",
                RefreshToken = "first-refresh-token",
            });
            Assert.That(protector.StoredPayloadIds, Has.Count.EqualTo(1));
            string firstPayloadId = protector.StoredPayloadIds[0];

            await store.SaveAsync(new TokenPairDto
            {
                AccessToken = "second-access-token",
                RefreshToken = "second-refresh-token",
            });
            TokenPairDto? loaded = await store.GetAsync();

            Assert.Multiple(() =>
            {
                Assert.That(protector.StoredPayloadIds, Has.Count.EqualTo(1));
                Assert.That(protector.StoredPayloadIds, Does.Not.Contain(firstPayloadId));
                Assert.That(protector.DeletedPayloadIds, Is.EqualTo(new[] { firstPayloadId }));
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded!.AccessToken, Is.EqualTo("second-access-token"));
                Assert.That(loaded.RefreshToken, Is.EqualTo("second-refresh-token"));
            });
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Test]
    public async Task SaveAsync_DeletesNewExternalProtectedPayloadWhenCommitFails()
    {
        string directory = CreateTempDirectory();
        try
        {
            var protector = new RecordingDeletableTokenPayloadProtector("external-scheme");
            var store = new FileCottonTokenStore(directory, protector);

            Exception? exception = Assert.CatchAsync(async () => await store.SaveAsync(new TokenPairDto
            {
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
            }));

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.TypeOf<IOException>().Or.TypeOf<UnauthorizedAccessException>());
                Assert.That(protector.StoredPayloadIds, Is.Empty);
                Assert.That(protector.DeletedPayloadIds, Is.EqualTo(new[] { "payload-1" }));
            });
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Test]
    public async Task GetAsync_ReturnsNullForIncompleteTokenFile()
    {
        string directory = CreateTempDirectory();
        try
        {
            string path = Path.Combine(directory, "tokens.json");
            File.WriteAllText(path, """{"accessToken":"access-token"}""");
            var store = new FileCottonTokenStore(path);

            TokenPairDto? loaded = await store.GetAsync();

            Assert.That(loaded, Is.Null);
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Test]
    public async Task SaveAsync_RestrictsUnixFileAccess()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Pass("Unix file mode check is not applicable on this platform.");
            return;
        }

        string directory = CreateTempDirectory();
        try
        {
            string path = Path.Combine(directory, "tokens.json");
            var store = new FileCottonTokenStore(path);

            await store.SaveAsync(new TokenPairDto
            {
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
            });

            UnixFileMode mode = File.GetUnixFileMode(path);

            Assert.Multiple(() =>
            {
                Assert.That(mode.HasFlag(UnixFileMode.UserRead), Is.True);
                Assert.That(mode.HasFlag(UnixFileMode.UserWrite), Is.True);
                Assert.That(mode.HasFlag(UnixFileMode.GroupRead), Is.False);
                Assert.That(mode.HasFlag(UnixFileMode.OtherRead), Is.False);
            });
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Test]
    public async Task WindowsDpapiTokenPayloadProtector_RoundtripsOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Pass("DPAPI token protection is only available on Windows.");
            return;
        }

        var protector = new WindowsDpapiTokenPayloadProtector();
        byte[] plaintext = Encoding.UTF8.GetBytes("secret token payload");

        byte[] protectedPayload = await protector.ProtectAsync(plaintext);
        byte[] roundtrip = await protector.UnprotectAsync(protectedPayload);

        Assert.Multiple(() =>
        {
            Assert.That(protectedPayload, Is.Not.EqualTo(plaintext));
            Assert.That(Encoding.UTF8.GetString(roundtrip), Is.EqualTo("secret token payload"));
        });
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "cotton-desktop-token-store-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTempDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class ReversingTokenPayloadProtector : ITokenPayloadProtector
    {
        public ReversingTokenPayloadProtector(string scheme)
        {
            Scheme = scheme;
        }

        public string Scheme { get; }

        public Task<byte[]> ProtectAsync(byte[] plaintext, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Reverse(plaintext));
        }

        public Task<byte[]> UnprotectAsync(byte[] protectedPayload, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Reverse(protectedPayload));
        }

        private static byte[] Reverse(byte[] value)
        {
            byte[] copy = value.ToArray();
            Array.Reverse(copy);
            return copy;
        }
    }

    private sealed class ThrowingTokenPayloadProtector : ITokenPayloadProtector
    {
        public ThrowingTokenPayloadProtector(string scheme)
        {
            Scheme = scheme;
        }

        public string Scheme { get; }

        public Task<byte[]> ProtectAsync(byte[] plaintext, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<byte[]> UnprotectAsync(byte[] protectedPayload, CancellationToken cancellationToken = default)
        {
            return Task.FromException<byte[]>(new CryptographicException("payload is unreadable"));
        }
    }

    private sealed class RecordingDeletableTokenPayloadProtector : ITokenPayloadProtector, IDeletableTokenPayloadProtector
    {
        private readonly Dictionary<string, byte[]> _payloads = [];
        private int _nextId;

        public RecordingDeletableTokenPayloadProtector(string scheme)
        {
            Scheme = scheme;
        }

        public string Scheme { get; }

        public IReadOnlyList<string> StoredPayloadIds => _payloads.Keys.Order().ToList();

        public List<string> DeletedPayloadIds { get; } = [];

        public Task<byte[]> ProtectAsync(byte[] plaintext, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string id = "payload-" + Interlocked.Increment(ref _nextId).ToString("D");
            _payloads[id] = plaintext.ToArray();
            return Task.FromResult(Encoding.UTF8.GetBytes(id));
        }

        public Task<byte[]> UnprotectAsync(byte[] protectedPayload, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string id = Encoding.UTF8.GetString(protectedPayload);
            return Task.FromResult(_payloads[id].ToArray());
        }

        public Task DeleteAsync(byte[] protectedPayload, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string id = Encoding.UTF8.GetString(protectedPayload);
            _payloads.Remove(id);
            DeletedPayloadIds.Add(id);
            return Task.CompletedTask;
        }
    }
}
