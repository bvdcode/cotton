// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NUnit.Framework;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;

namespace Cotton.Server.IntegrationTests;

[NonParallelizable]
public class SyncChangesEndpointsTests : IntegrationTestBase
{
    private const string Username = "testuser";
    private const string Password = "testpassword";

    private TestAppFactory? _factory;
    private HttpClient? _client;

    [SetUp]
    public void SetUp()
    {
        ResetSettingsProviderCaches();

        IRelationalDatabaseCreator creator = DbContext.GetService<IRelationalDatabaseCreator>();
        creator.EnsureDeleted();
        creator.Create();

        _factory = new TestAppFactory(CreateOverrides());
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
        ResetSettingsProviderCaches();
    }

    [Test]
    public async Task GetChanges_WhenFeedIsEmpty_ReturnsEmptyPage()
    {
        await SignInAsync();

        SyncChangesResponseDto response = await GetChangesAsync(since: 0, limit: 10);

        Assert.Multiple(() =>
        {
            Assert.That(response.SinceCursor, Is.EqualTo(0));
            Assert.That(response.NextCursor, Is.EqualTo(0));
            Assert.That(response.HasMore, Is.False);
            Assert.That(response.Changes, Is.Empty);
        });
    }

    [Test]
    public async Task GetChanges_ReturnsOrderedCurrentUserPageAfterCursor()
    {
        await SignInAsync();
        Guid ownerId = await GetUserIdAsync(Username);
        await SignInAsync("otheruser", "otherpass");
        Guid otherOwnerId = await GetUserIdAsync("otheruser");

        await AddSyncChangeAsync(ownerId, revision: 1, "ignored-before-cursor");
        await AddSyncChangeAsync(ownerId, revision: 2, "included");
        await AddSyncChangeAsync(ownerId, revision: 3, "next-page");
        await AddSyncChangeAsync(otherOwnerId, revision: 1, "other-user");

        await SignInAsync();

        SyncChangesResponseDto response = await GetChangesAsync(since: 1, limit: 1);

        Assert.Multiple(() =>
        {
            Assert.That(response.SinceCursor, Is.EqualTo(1));
            Assert.That(response.NextCursor, Is.EqualTo(2));
            Assert.That(response.HasMore, Is.True);
            Assert.That(response.Changes, Has.Count.EqualTo(1));
            Assert.That(response.Changes[0].Revision, Is.EqualTo(2));
            Assert.That(response.Changes[0].Name, Is.EqualTo("included"));
            Assert.That(response.Changes[0].Kind, Is.EqualTo(SyncChangeKind.FileCreated));
        });
    }

    private Dictionary<string, string?> CreateOverrides()
    {
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = TestPostgresHost,
            Port = TestPostgresPort,
            Database = CurrentDatabaseName,
            Username = TestPostgresUsername,
            Password = TestPostgresPassword,
        };

        return new Dictionary<string, string?>
        {
            ["DatabaseSettings:Host"] = csb.Host,
            ["DatabaseSettings:Port"] = csb.Port.ToString(),
            ["DatabaseSettings:Database"] = csb.Database,
            ["DatabaseSettings:Username"] = csb.Username,
            ["DatabaseSettings:Password"] = csb.Password,
            ["MasterEncryptionKey"] = Convert.ToBase64String(Hasher.HashData(Encoding.UTF8.GetBytes("super"))),
            ["MasterEncryptionKeyId"] = "1",
            ["EncryptionThreads"] = "1",
            ["MaxChunkSizeBytes"] = "16777216",
            ["CipherChunkSizeBytes"] = "20971520",
            ["JwtSettings:Key"] = "T3wNTuKqmTXKjJKXHJRGUpG9sdrmpSX4",
        };
    }

    private async Task SignInAsync(string username = Username, string password = Password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Routes.V1.Auth}/login")
        {
            Content = JsonContent.Create(new LoginRequestDto
            {
                Username = username,
                Password = password,
            }),
        };
        request.Headers.Add("X-Forwarded-For", "8.8.8.8");

        using HttpResponseMessage response = await _client!.SendAsync(request);
        response.EnsureSuccessStatusCode();

        TokenPairResponseDto? login = await response.Content.ReadFromJsonAsync<TokenPairResponseDto>();
        Assert.That(login, Is.Not.Null);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            login!.AccessToken);
    }

    private async Task<Guid> GetUserIdAsync(string username)
    {
        DbContext.ChangeTracker.Clear();

        User user = await DbContext.Users
            .AsNoTracking()
            .SingleAsync(x => x.Username == username);

        return user.Id;
    }

    private async Task AddSyncChangeAsync(Guid ownerId, long revision, string name)
    {
        DbContext.SyncChanges.Add(new SyncChange
        {
            OwnerId = ownerId,
            Revision = revision,
            Kind = SyncChangeKind.FileCreated,
            LayoutId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            ParentNodeId = Guid.NewGuid(),
            Name = name,
        });

        await DbContext.SaveChangesAsync();
    }

    private async Task<SyncChangesResponseDto> GetChangesAsync(long since, int limit)
    {
        SyncChangesResponseDto? response = await _client!.GetFromJsonAsync<SyncChangesResponseDto>(
            $"{Routes.V1.Sync}/changes?since={since}&limit={limit}");

        Assert.That(response, Is.Not.Null);
        return response!;
    }

    private static void ResetSettingsProviderCaches()
    {
        const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
        Type settingsProviderType = typeof(SettingsProvider);

        settingsProviderType.GetField("_cache", Flags)?.SetValue(null, null);
        settingsProviderType.GetField("_isServerInitializedCache", Flags)?.SetValue(null, null);
        settingsProviderType.GetField("_serverHasUsersCache", Flags)?.SetValue(null, null);
    }
}
