// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Database;
using Cotton.Database.Integrity;
using Cotton.Database.Models;
using Cotton.Previews;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Jobs;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.DatabaseIntegrity.Descriptors;
using Cotton.Server.Services.FileMetadata;
using Cotton.Storage.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    public partial class PreviewGenerationPipelineTests : IntegrationTestBase
    {
        private const string PreviewRouteBase = "/api/v1/preview";
        private const string DefaultExternalFixturesDir = @"C:\Temp\cotton-tests";

        private TestAppFactory? _factory;
        private HttpClient? _client;
        private MetadataPersistenceFailureInterceptor _metadataFailure = null!;

        private record FixtureUpload(
            Guid NodeFileId,
            string FileName,
            string ContentType,
            int SourceLength,
            bool ExpectLargePreview);

        private record FileManifestPreviewState(
            Guid Id,
            byte[]? SmallFilePreviewHash,
            byte[]? SmallFilePreviewHashEncrypted,
            byte[]? LargeFilePreviewHash,
            string? PreviewGenerationError);

        private record FileManifestMetadataState(
            Dictionary<string, string>? Metadata);

        [SetUp]
        public void SetUp()
        {
            IRelationalDatabaseCreator creator = DbContext.GetService<IRelationalDatabaseCreator>();
            creator.EnsureDeleted();
            creator.Create();

            Assert.Multiple(() =>
            {
                Assert.That(creator.Exists(), Is.True);
                Assert.That(creator.HasTables(), Is.False);
            });

            NpgsqlConnectionStringBuilder csb = new NpgsqlConnectionStringBuilder
            {
                Host = "localhost",
                Port = 5432,
                Database = DatabaseName,
                Username = "postgres",
                Password = "postgres"
            };

            Dictionary<string, string?> overrides = new Dictionary<string, string?>
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
                ["JwtSettings:Key"] = "T3wNTuKqmTXKjJKXHJRGUpG9sdrmpSX4"
            };

            _metadataFailure = new MetadataPersistenceFailureInterceptor();
            _factory = new TestAppFactory(overrides, services =>
            {
                services.AddSingleton(_metadataFailure);
                services.AddDbContext<CottonDbContext>((serviceProvider, options) =>
                    options.AddInterceptors(
                        serviceProvider.GetRequiredService<MetadataPersistenceFailureInterceptor>()));
            });
            _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();

            _client = null;
            _factory = null;
        }
    }
}
