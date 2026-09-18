// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Files;
using Cotton.Nodes;
using Cotton.Server.Models.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Cotton.Server.IntegrationTests
{
    public partial class ChunksAndFilesEndpointsTests
    {
        [Test]
        public async Task FileDtos_UseEachFileName_AfterDeduplicationAndRename()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;

            NodeFileManifestDto text = await UploadTextFileAsync(root, "notes.txt", "shared file content");
            NodeFileManifestDto markdown = await UploadTextFileAsync(root, "notes.md", "shared file content");
            Assert.Multiple(() =>
            {
                Assert.That(markdown.FileManifestId, Is.EqualTo(text.FileManifestId));
                Assert.That(text.ContentType, Is.EqualTo("text/plain"));
                Assert.That(markdown.ContentType, Is.EqualTo("text/markdown"));
            });

            using HttpResponseMessage renameResponse = await _client.PatchAsJsonAsync(
                $"/api/v1/files/{markdown.Id}/rename",
                new RenameFileRequestDto { Name = "styles.css" });
            renameResponse.EnsureSuccessStatusCode();
            NodeFileManifestDto renamed = (await renameResponse.Content.ReadFromJsonAsync<NodeFileManifestDto>())!;
            List<NodeFileManifestDto> recent = (await _client.GetFromJsonAsync<List<NodeFileManifestDto>>(
                $"/api/v1/layouts/{root.LayoutId}/recent?count=10"))!;
            List<FileVersionDto> versions = await GetVersionsAsync(markdown.Id);

            string shareToken = Guid.NewGuid().ToString("N");
            using HttpResponseMessage shareResponse = await _client.GetAsync(
                $"/api/v1/layouts/nodes/{root.Id}/share-link?customToken={shareToken}");
            shareResponse.EnsureSuccessStatusCode();
            SharedNodeContentDto shared = (await _client.GetFromJsonAsync<SharedNodeContentDto>(
                $"/api/v1/layouts/shared/{shareToken}/children"))!;
            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            string storedContentType = await dbContext.FileManifests
                .Where(manifest => manifest.Id == text.FileManifestId)
                .Select(manifest => manifest.ContentType)
                .SingleAsync();
            Dictionary<Guid, string> fileContentTypes = await dbContext.NodeFiles
                .Where(file => file.FileManifestId == text.FileManifestId)
                .ToDictionaryAsync(file => file.Id, file => file.ContentType);
            List<NodeFileManifestDto> cssFiles = (await _client.GetFromJsonAsync<List<NodeFileManifestDto>>(
                $"/api/v1/layouts/{root.LayoutId}/recent?count=1&contentType=text/css"))!;
            List<NodeFileManifestDto> otherFiles = (await _client.GetFromJsonAsync<List<NodeFileManifestDto>>(
                $"/api/v1/layouts/{root.LayoutId}/recent?count=1&excludeContentType=text/css"))!;

            Assert.Multiple(() =>
            {
                Assert.That(renamed.ContentType, Is.EqualTo("text/css"));
                Assert.That(recent.Single(file => file.Id == text.Id).ContentType, Is.EqualTo("text/plain"));
                Assert.That(recent.Single(file => file.Id == markdown.Id).ContentType, Is.EqualTo("text/css"));
                Assert.That(versions.Single().ContentType, Is.EqualTo("text/css"));
                Assert.That(shared.Files.Single(file => file.Id == text.Id).ContentType, Is.EqualTo("text/plain"));
                Assert.That(shared.Files.Single(file => file.Id == markdown.Id).ContentType, Is.EqualTo("text/css"));
                Assert.That(storedContentType, Is.EqualTo("text/plain"));
                Assert.That(fileContentTypes[text.Id], Is.EqualTo("text/plain"));
                Assert.That(fileContentTypes[markdown.Id], Is.EqualTo("text/css"));
                Assert.That(cssFiles.Select(file => file.Id), Is.EqualTo(new[] { markdown.Id }));
                Assert.That(otherFiles.Select(file => file.Id), Is.EqualTo(new[] { text.Id }));
            });
        }
    }
}
