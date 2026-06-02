// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Contracts.Auth;
using Cotton.Sync.ClientState;

namespace Cotton.Sync.Tests.ClientState;

public sealed class SqliteClientStateStoreTests
{
    private string _tempDirectory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "cotton-client-state-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Test]
    public async Task SaveAsync_RoundtripsTokensAndServerBaseAddress()
    {
        var store = CreateStore();
        var tokens = new TokenPairDto
        {
            AccessToken = "access",
            RefreshToken = "refresh",
        };
        var server = new Uri("https://cotton.test/");

        await store.SaveAsync(tokens);
        await store.SaveServerBaseAddressAsync(server);

        TokenPairDto? storedTokens = await store.GetAsync();
        Uri? storedServer = await store.GetServerBaseAddressAsync();
        Assert.Multiple(() =>
        {
            Assert.That(storedTokens?.AccessToken, Is.EqualTo("access"));
            Assert.That(storedTokens?.RefreshToken, Is.EqualTo("refresh"));
            Assert.That(storedServer?.GetLeftPart(UriPartial.Authority), Is.EqualTo("https://cotton.test"));
        });
    }

    [Test]
    public async Task GetAsync_AllowsConcurrentColdReadsOnNewDatabase()
    {
        var store = CreateStore();

        TokenPairDto?[] results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => store.GetAsync()));

        Assert.That(results, Is.All.Null);
    }

    private SqliteClientStateStore CreateStore()
    {
        return new SqliteClientStateStore(Path.Combine(_tempDirectory, "client-state.sqlite"));
    }
}
