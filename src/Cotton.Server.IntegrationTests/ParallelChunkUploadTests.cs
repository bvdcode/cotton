// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class ParallelChunkUploadTests : FileEndpointTestBase
    {
        [TestCase(8, 16)]
        [TestCase(32, 16)]
        [TestCase(32, 2)]
        public async Task ConcurrentHttp2ChunksComplete(int concurrency, int connectionWindowMiB)
        {
            string token = await LoginAsync();
            IConfiguration configuration = _factory!.Services.GetRequiredService<IConfiguration>();
            Dictionary<string, string?> overrides = new()
            {
                ["DatabaseSettings:Host"] = TestPostgresHost,
                ["DatabaseSettings:Port"] = TestPostgresPort.ToString(),
                ["DatabaseSettings:Database"] = CurrentDatabaseName,
                ["DatabaseSettings:Username"] = TestPostgresUsername,
                ["DatabaseSettings:Password"] = TestPostgresPassword,
                ["MasterEncryptionKey"] = configuration["MasterEncryptionKey"],
                ["MasterEncryptionKeyId"] = configuration["MasterEncryptionKeyId"],
                ["JwtSettings:Key"] = configuration["JwtSettings:Key"],
                ["Http2Transport:InitialConnectionWindowSize"] = (connectionWindowMiB * 1024 * 1024).ToString(),
            };
            using TestAppFactory factory = new(overrides);
            using WebApplicationFactory<Program> networkFactory = factory.WithWebHostBuilder(builder =>
                builder.ConfigureKestrel(options => options.ConfigureEndpointDefaults(endpoint =>
                    endpoint.Protocols = HttpProtocols.Http2)));
            networkFactory.UseKestrel(0);
            using HttpClient client = networkFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));

            Task[] uploads = Enumerable.Range(0, concurrency).Select(async _ =>
            {
                byte[] content = RandomNumberGenerator.GetBytes(4 * 1024 * 1024);
                string hash = Hasher.ToHexStringHash(Hasher.HashData(content));
                using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/chunks/raw?hash=" + hash)
                {
                    Version = HttpVersion.Version20,
                    VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                    Content = new ByteArrayContent(content),
                };
                using HttpResponseMessage response = await client.SendAsync(request, timeout.Token);
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            }).ToArray();
            await Task.WhenAll(uploads);
        }
    }
}
