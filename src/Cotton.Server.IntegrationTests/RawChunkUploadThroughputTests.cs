// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Providers;
using Cotton.Storage.Processors;
using Microsoft.Extensions.DependencyInjection;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NUnit.Framework;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;

namespace Cotton.Server.IntegrationTests;

[NonParallelizable]
public sealed class RawChunkUploadThroughputTests : IntegrationTestBase
{
    private const int MiB = 1024 * 1024;
    private const int WarmupChunks = 1;
    private const int MeasuredBytesTarget = 128 * MiB;

    private TestAppFactory? _factory;
    private HttpClient? _client;

    [SetUp]
    public void SetUp()
    {
        ResetSettingsProviderCaches();

        var creator = DbContext.GetService<IRelationalDatabaseCreator>();
        creator.EnsureDeleted();
        creator.Create();

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = TestPostgresHost,
            Port = TestPostgresPort,
            Database = CurrentDatabaseName,
            Username = TestPostgresUsername,
            Password = TestPostgresPassword
        };
        var overrides = new Dictionary<string, string?>
        {
            ["DatabaseSettings:Host"] = csb.Host,
            ["DatabaseSettings:Port"] = csb.Port.ToString(),
            ["DatabaseSettings:Database"] = csb.Database,
            ["DatabaseSettings:Username"] = csb.Username,
            ["DatabaseSettings:Password"] = csb.Password,
            ["MasterEncryptionKey"] = Convert.ToBase64String(Hasher.HashData(Encoding.UTF8.GetBytes("super"))),
            ["MasterEncryptionKeyId"] = "1",
            ["EncryptionThreads"] = "1",
            ["MaxChunkSizeBytes"] = (16 * MiB).ToString(),
            ["CipherChunkSizeBytes"] = "20971520",
            ["JwtSettings:Key"] = "T3wNTuKqmTXKjJKXHJRGUpG9sdrmpSX4"
        };

        _factory = new TestAppFactory(overrides);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _client.Timeout = TimeSpan.FromMinutes(10);
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
        ResetSettingsProviderCaches();
    }

    [TestCase(4)]
    [TestCase(16)]
    [Explicit("Local backend upload throughput diagnostic. Run by name when investigating chunk ingest performance.")]
    public async Task Upload_Raw_Chunks_Reports_Backend_Throughput(int chunkSizeMiB)
    {
        int chunkSizeBytes = checked(chunkSizeMiB * MiB);
        int measuredChunks = Math.Max(1, MeasuredBytesTarget / chunkSizeBytes);

        await SetMaxChunkSizeAsync(chunkSizeBytes);

        string token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        IReadOnlyList<ChunkPayload> warmup = CreatePayloads(WarmupChunks, chunkSizeBytes, seedOffset: 0);
        IReadOnlyList<ChunkPayload> measured = CreatePayloads(measuredChunks, chunkSizeBytes, seedOffset: WarmupChunks);

        foreach (ChunkPayload payload in warmup)
        {
            await UploadRawChunkAsync(payload);
        }

        var stopwatch = Stopwatch.StartNew();
        foreach (ChunkPayload payload in measured)
        {
            await UploadRawChunkAsync(payload);
        }

        stopwatch.Stop();

        long bytes = (long)measuredChunks * chunkSizeBytes;
        double mib = bytes / 1024d / 1024d;
        double mibPerSecond = mib / stopwatch.Elapsed.TotalSeconds;

        TestContext.Progress.WriteLine($"Raw endpoint backend path: {mibPerSecond:F2} MiB/s");
        TestContext.Progress.WriteLine($"Uploaded: {mib:F2} MiB in {stopwatch.Elapsed.TotalSeconds:F2} sec");
        TestContext.Progress.WriteLine($"Chunk size: {chunkSizeMiB} MiB");
        TestContext.Progress.WriteLine($"Chunks: {measuredChunks}");
        TestContext.Progress.WriteLine($"Compression level: {CompressionProcessor.DefaultCompressionLevel}");
        TestContext.Progress.WriteLine("Path: TestServer -> auth -> ChunkController.UploadRawChunk -> ChunkIngestService -> storage pipeline -> filesystem backend");

        Assert.That(mibPerSecond, Is.GreaterThan(1));
    }

    private async Task UploadRawChunkAsync(ChunkPayload payload)
    {
        using var body = new ByteArrayContent(payload.Bytes)
        {
            Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") }
        };

        using HttpResponseMessage response = await _client!.PostAsync($"/api/v1/chunks/raw?hash={payload.Hash}", body);
        if (!response.IsSuccessStatusCode)
        {
            string bodyText = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Raw chunk upload failed with {(int)response.StatusCode} {response.StatusCode}: {bodyText}");
        }
    }

    private static IReadOnlyList<ChunkPayload> CreatePayloads(int count, int chunkSizeBytes, int seedOffset)
    {
        var payloads = new ChunkPayload[count];
        for (int i = 0; i < payloads.Length; i++)
        {
            byte[] bytes = new byte[chunkSizeBytes];
            new Random(seedOffset + i).NextBytes(bytes);
            string hash = Hasher.ToHexStringHash(Hasher.HashData(bytes));
            payloads[i] = new ChunkPayload(bytes, hash);
        }

        return payloads;
    }

    private async Task SetMaxChunkSizeAsync(int maxChunkSizeBytes)
    {
        using IServiceScope scope = _factory!.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<SettingsProvider>();
        await settings.SetPropertyAsync(x => x.MaxChunkSizeBytes, maxChunkSizeBytes);
    }

    private async Task<string> LoginAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new LoginRequestDto
            {
                Username = "testuser",
                Password = "testpassword"
            })
        };
        request.Headers.Add("X-Forwarded-For", "8.8.8.8");

        using HttpResponseMessage response = await _client!.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<TokenPairResponseDto>();
        Assert.That(login, Is.Not.Null);
        return login!.AccessToken;
    }

    private static void ResetSettingsProviderCaches()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        Type settingsProviderType = typeof(SettingsProvider);

        settingsProviderType.GetField("_cache", flags)?.SetValue(null, null);
        settingsProviderType.GetField("_isServerInitializedCache", flags)?.SetValue(null, null);
        settingsProviderType.GetField("_serverHasUsersCache", flags)?.SetValue(null, null);
    }

    private sealed record ChunkPayload(byte[] Bytes, string Hash);
}
