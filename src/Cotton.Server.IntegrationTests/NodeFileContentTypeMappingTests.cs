// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Files;
using Cotton.Server.Mappings;
using Mapster;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class NodeFileContentTypeMappingTests
    {
        [TestCase("notes.txt", "text/plain")]
        [TestCase("notes.md", "text/markdown")]
        [TestCase("styles.css", "text/css")]
        [TestCase("song.ogg", "audio/ogg")]
        [TestCase("script.ts", "text/plain")]
        [TestCase("opaque-file-name", "application/octet-stream")]
        public void Mapping_UsesFileNameInsteadOfSharedManifestType(string name, string expectedContentType)
        {
            MapsterConfig.Register();
            FileManifest manifest = new()
            {
                ContentType = "image/png",
                ProposedContentHash = [1, 2, 3],
            };
            NodeFile file = new() { FileManifest = manifest };
            file.SetName(name);

            NodeFileManifestDto dto = file.Adapt<NodeFileManifestDto>();

            Assert.Multiple(() =>
            {
                Assert.That(dto.ContentType, Is.EqualTo(expectedContentType));
                Assert.That(manifest.ContentType, Is.EqualTo("image/png"));
            });
        }

        [Test]
        public void DatabaseProjection_SupportsFileNameResolution()
        {
            MapsterConfig.Register();
            DbContextOptions<CottonDbContext> options = new DbContextOptionsBuilder<CottonDbContext>()
                .UseNpgsql()
                .Options;
            using CottonDbContext dbContext = new(options);

            string query = dbContext.NodeFiles
                .ProjectToType<NodeFileManifestDto>()
                .ToQueryString();

            Assert.That(query, Does.Contain("SELECT"));
        }
    }
}
