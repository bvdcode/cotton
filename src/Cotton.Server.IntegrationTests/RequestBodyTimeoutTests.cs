// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Net.Sockets;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class RequestBodyTimeoutTests : FileEndpointTestBase
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task StalledRequestBodyReturnsRequestTimeout(bool rawChunk)
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
            };
            using TestAppFactory networkFactory = new(overrides);
            networkFactory.UseKestrel(0);
            using HttpClient client = networkFactory.CreateClient();
            Uri address = client.BaseAddress!;
            string path = rawChunk
                ? "/api/v1/chunks/raw?hash=" + Hasher.ToHexStringHash(Hasher.HashData([0]))
                : "/api/v1/auth/login";
            string contentType = rawChunk ? "application/octet-stream" : "application/json";
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
            using TcpClient connection = new();
            await connection.ConnectAsync(address.Host, address.Port, timeout.Token);
            await using NetworkStream stream = connection.GetStream();
            string headers = $"POST {path} HTTP/1.1\r\nHost: {address.Authority}\r\n"
                + $"Authorization: Bearer {token}\r\nContent-Type: {contentType}\r\n"
                + "Content-Length: 1048576\r\nConnection: close\r\n\r\n{";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), timeout.Token);
            using StreamReader reader = new(stream, Encoding.ASCII, leaveOpen: true);
            string? status = await reader.ReadLineAsync(timeout.Token);

            Assert.That(status, Does.Contain(" 408 "));
            if (rawChunk)
            {
                using HttpRequestMessage retry = new(HttpMethod.Post, path)
                {
                    Content = new ByteArrayContent([0]),
                };
                retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using HttpResponseMessage response = await client.SendAsync(retry, timeout.Token);
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            }
        }
    }
}
