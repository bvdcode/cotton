// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

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
}
