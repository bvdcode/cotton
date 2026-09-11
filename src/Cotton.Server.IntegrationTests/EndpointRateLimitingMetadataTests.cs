// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Server.Auth;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NUnit.Framework;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    public class EndpointRateLimitingMetadataTests : IntegrationTestBase
    {
        private TestAppFactory? _factory;

        [OneTimeSetUp]
        public async Task SetUp()
        {
            NpgsqlConnection.ClearAllPools();
            await DbContext.Database.EnsureDeletedAsync();
            string connectionString = DbContext.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Test database connection string is not configured.");
            NpgsqlConnectionStringBuilder database = new(connectionString);
            Dictionary<string, string?> overrides = new()
            {
                ["DatabaseSettings:Host"] = database.Host,
                ["DatabaseSettings:Port"] = database.Port.ToString(),
                ["DatabaseSettings:Database"] = database.Database,
                ["DatabaseSettings:Username"] = database.Username,
                ["DatabaseSettings:Password"] = database.Password,
                ["MasterEncryptionKey"] = Convert.ToBase64String(Hasher.HashData(Encoding.UTF8.GetBytes("super"))),
                ["MasterEncryptionKeyId"] = "1",
                ["EncryptionThreads"] = "1",
                ["MaxChunkSizeBytes"] = "16777216",
                ["CipherChunkSizeBytes"] = "20971520",
                ["JwtSettings:Key"] = "T3wNTuKqmTXKjJKXHJRGUpG9sdrmpSX4",
            };

            _factory = new TestAppFactory(overrides);
            using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });
        }

        [OneTimeTearDown]
        public async Task TearDown()
        {
            if (_factory is not null)
            {
                await _factory.DisposeAsync();
            }

            NpgsqlConnection.ClearAllPools();
            await DbContext.Database.EnsureDeletedAsync();
            NpgsqlConnection.ClearAllPools();
        }

        [Test]
        public void PublicShareArchive_UsesArchiveRateLimit()
        {
            IReadOnlyList<RouteEndpoint> endpoints = GetControllerEndpoints();
            RouteEndpoint archiveEndpoint = endpoints.Single(endpoint =>
                RouteMatches(
                    endpoint,
                    $"{Routes.V1.Layouts}/shared/{{token}}/archives/download-link")
                && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
                    .Contains(HttpMethods.Post) == true);
            EnableRateLimitingAttribute? attribute =
                archiveEndpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>();

            Assert.That(attribute?.PolicyName, Is.EqualTo(AuthRateLimitPolicies.PublicShareArchive));
        }

        [Test]
        public void PreviewEndpoints_ExplicitlyBypassRateLimiting()
        {
            string[] previewRoutes =
            [
                $"{Routes.V1.Previews}/{{previewHashEncryptedHex}}",
                $"{Routes.V1.Previews}/{{previewHashEncryptedHex}}.webp",
            ];
            IReadOnlyList<RouteEndpoint> previewEndpoints = GetControllerEndpoints()
                .Where(endpoint => previewRoutes.Any(route => RouteMatches(endpoint, route)))
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(previewEndpoints, Has.Count.EqualTo(previewRoutes.Length));
                Assert.That(
                    previewEndpoints.All(endpoint =>
                        endpoint.Metadata.GetMetadata<DisableRateLimitingAttribute>() is not null),
                    Is.True);
            });
        }

        private IReadOnlyList<RouteEndpoint> GetControllerEndpoints()
        {
            EndpointDataSource source = _factory!.Services.GetRequiredService<EndpointDataSource>();
            return source.Endpoints
                .OfType<RouteEndpoint>()
                .ToArray();
        }

        private static bool RouteMatches(RouteEndpoint endpoint, string route)
        {
            return string.Equals(
                endpoint.RoutePattern.RawText?.TrimStart('/'),
                route.TrimStart('/'),
                StringComparison.Ordinal);
        }
    }
}
