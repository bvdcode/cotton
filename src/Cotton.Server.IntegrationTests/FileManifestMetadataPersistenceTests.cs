// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Services.FileMetadata;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;

namespace Cotton.Server.IntegrationTests
{
    public class FileManifestMetadataPersistenceTests
    {
        [Test]
        public void SaveManifestMetadataAsync_MapsConcurrencyConflict()
        {
            DbContextOptionsBuilder<CottonDbContext> optionsBuilder = new();
            using ConcurrencyThrowingCottonDbContext dbContext = new(optionsBuilder.Options);
            ExtractFileManifestMetadataRequestHandler handler = new(
                dbContext,
                null!,
                new FileContentMetadataExtractorProvider([]),
                null!,
                NullLogger<ExtractFileManifestMetadataRequestHandler>.Instance);
            FileManifest manifest = new();

            WebApiException? exception = Assert.ThrowsAsync<WebApiException>(
                async () => await handler.SaveManifestMetadataAsync(
                    manifest,
                    CancellationToken.None));

            Assert.That(exception?.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        }

        private class ConcurrencyThrowingCottonDbContext(DbContextOptions options)
            : CottonDbContext(options)
        {
            public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromException<int>(new DbUpdateConcurrencyException());
            }
        }
    }
}
