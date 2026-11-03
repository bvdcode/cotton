// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public class LayoutSearchAndRecentFilesTests : LayoutEndpointTestBase
    {
        [Test]
        public async Task Search_ByNodeGuid_ReturnsOnlyExactNode()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            HttpResponseMessage targetResponse = await _client.PutAsJsonAsync(
                "/api/v1/layouts/nodes",
                new CreateNodeRequestDto { ParentId = root!.Id, Name = "target" });
            targetResponse.EnsureSuccessStatusCode();
            NodeDto? target = await targetResponse.Content.ReadFromJsonAsync<NodeDto>();
            Assert.That(target, Is.Not.Null);

            HttpResponseMessage textMatchResponse = await _client.PutAsJsonAsync(
                "/api/v1/layouts/nodes",
                new CreateNodeRequestDto { ParentId = root.Id, Name = "why-log" });
            textMatchResponse.EnsureSuccessStatusCode();

            (SearchResultDto exact, int exactTotal) = await SearchAsync(root.LayoutId, target!.Id.ToString());
            Assert.Multiple(() =>
            {
                Assert.That(exactTotal, Is.EqualTo(1));
                Assert.That(exact.Nodes.Single().Id, Is.EqualTo(target.Id));
                Assert.That(exact.Files, Is.Empty);
            });

            (SearchResultDto copiedLogLine, int copiedTotal) = await SearchAsync(root.LayoutId, $"{target.Id} why");
            Assert.Multiple(() =>
            {
                Assert.That(copiedTotal, Is.EqualTo(1));
                Assert.That(copiedLogLine.Nodes.Single().Id, Is.EqualTo(target.Id));
                Assert.That(copiedLogLine.Files, Is.Empty);
            });
        }

        [Test]
        public async Task Search_ByText_ReturnsFoldersAndFilesWithPathsAndPagination()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            NodeDto exactFolder = await CreateNodeAsync(root!.Id, "demo");
            NodeDto fileParent = await CreateNodeAsync(root.Id, "file-parent");
            Dictionary<string, string> parentMetadata = new() { ["display-label"] = "opaque-value" };
            using HttpResponseMessage metadataResponse = await _client.PatchAsJsonAsync(
                $"/api/v1/layouts/nodes/{fileParent.Id}/metadata", parentMetadata);
            metadataResponse.EnsureSuccessStatusCode();
            NodeFileManifestDto exactFile = await CreateFileAsync(fileParent.Id, "demo", "file exact");
            NodeDto prefixFolder = await CreateNodeAsync(root.Id, "demo archive");
            NodeDto substringFolder = await CreateNodeAsync(root.Id, "old demo backup");

            (SearchResultDto firstPage, int firstPageTotal) = await SearchAsync(root.LayoutId, "demo", page: 1, pageSize: 2);
            Assert.Multiple(() =>
            {
                Assert.That(firstPageTotal, Is.EqualTo(4));
                Assert.That(firstPage.Nodes.Single().Id, Is.EqualTo(exactFolder.Id));
                Assert.That(firstPage.Files.Single().Id, Is.EqualTo(exactFile.Id));
                Assert.That(firstPage.NodePaths[exactFolder.Id], Is.EqualTo($"/{root.Name}/demo"));
                Assert.That(firstPage.FilePaths[exactFile.Id], Is.EqualTo($"/{root.Name}/file-parent/demo"));
                Assert.That(firstPage.Ancestors.Select(node => node.Id),
                    Does.Contain(root.Id).And.Contain(fileParent.Id));
                Assert.That(firstPage.Ancestors.Single(node => node.Id == fileParent.Id).Metadata,
                    Is.EquivalentTo(parentMetadata));
                Assert.That(firstPage.Ancestors.Select(node => node.Id),
                    Does.Not.Contain(prefixFolder.Id).And.Not.Contain(substringFolder.Id));
            });

            (SearchResultDto secondPage, int secondPageTotal) = await SearchAsync(root.LayoutId, "demo", page: 2, pageSize: 2);
            Assert.Multiple(() =>
            {
                Assert.That(secondPageTotal, Is.EqualTo(4));
                Assert.That(secondPage.Files, Is.Empty);
                Assert.That(secondPage.Nodes.Select(x => x.Id), Is.EqualTo(new[]
                {
                    prefixFolder.Id,
                    substringFolder.Id,
                }));
                Assert.That(secondPage.Ancestors.Select(node => node.Id), Does.Not.Contain(fileParent.Id));
            });
        }

        [Test]
        public async Task Search_CurrentFolderPrioritizesEntireSubtreeBeforePaging()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            NodeDto current = await CreateNodeAsync(root.Id, "current");
            NodeDto nested = await CreateNodeAsync(current.Id, "nested");
            NodeDto deep = await CreateNodeAsync(nested.Id, "deep");
            NodeDto other = await CreateNodeAsync(root.Id, "current-copy");
            NodeDto globalFolder = await CreateNodeAsync(root.Id, "report");
            NodeFileManifestDto globalFile = await CreateFileAsync(other.Id, "report", "global");
            NodeFileManifestDto nestedFile = await CreateFileAsync(deep.Id, "report.txt", "nested");
            NodeDto localFolder = await CreateNodeAsync(current.Id, "report archive");
            NodeDto nestedFolder = await CreateNodeAsync(deep.Id, "report notes");
            NodeFileManifestDto localExact = await CreateFileAsync(current.Id, "report", "exact");
            NodeFileManifestDto localPartial = await CreateFileAsync(current.Id, "old report.txt", "partial");

            (SearchResultDto first, int total) = await SearchAsync(
                root.LayoutId, "report", pageSize: 5, currentNodeId: current.Id);
            (SearchResultDto second, int secondTotal) = await SearchAsync(
                root.LayoutId, "report", page: 2, pageSize: 5, currentNodeId: current.Id);
            (SearchResultDto ordinary, _) = await SearchAsync(root.LayoutId, "report", pageSize: 1);
            (SearchResultDto unknownFolder, _) = await SearchAsync(
                root.LayoutId, "report", pageSize: 1, currentNodeId: Guid.NewGuid());

            Assert.Multiple(() =>
            {
                Assert.That(total, Is.EqualTo(7));
                Assert.That(secondTotal, Is.EqualTo(total));
                Assert.That(first.Nodes.Select(node => node.Id), Is.EqualTo(new[] { localFolder.Id, nestedFolder.Id }));
                Assert.That(first.Files.Select(file => file.Id), Is.EqualTo(new[] { localExact.Id, nestedFile.Id, localPartial.Id }));
                Assert.That(first.OrderedIds, Is.EqualTo(new[]
                {
                    localExact.Id, localFolder.Id, nestedFolder.Id, nestedFile.Id, localPartial.Id,
                }));
                Assert.That(second.Nodes.Select(node => node.Id), Is.EqualTo(new[] { globalFolder.Id }));
                Assert.That(second.Files.Select(file => file.Id), Is.EqualTo(new[] { globalFile.Id }));
                Assert.That(second.OrderedIds, Is.EqualTo(new[] { globalFolder.Id, globalFile.Id }));
                Assert.That(ordinary.Nodes.Single().Id, Is.EqualTo(globalFolder.Id));
                Assert.That(unknownFolder.Nodes.Single().Id, Is.EqualTo(globalFolder.Id));
            });
        }

        [Test]
        public async Task Search_DoesNotReturnTrashedNodesOrFiles()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            NodeDto visible = await CreateNodeAsync(root!.Id, "archive-visible");
            NodeDto trashedFolder = await CreateNodeAsync(root.Id, "archive-trash-folder");
            _ = await CreateNodeAsync(trashedFolder.Id, "archive-trash-child");
            NodeFileManifestDto trashedFile = await CreateFileAsync(root.Id, "archive-trash-file.txt", "trash me");

            (await _client.DeleteAsync($"/api/v1/layouts/nodes/{trashedFolder.Id}")).EnsureSuccessStatusCode();
            (await _client.DeleteAsync($"/api/v1/files/{trashedFile.Id}")).EnsureSuccessStatusCode();

            (SearchResultDto visibleResult, int visibleTotal) = await SearchAsync(root.LayoutId, "archive-visible");
            (SearchResultDto trashResult, int trashTotal) = await SearchAsync(root.LayoutId, "archive-trash");

            Assert.Multiple(() =>
            {
                Assert.That(visibleTotal, Is.EqualTo(1));
                Assert.That(visibleResult.Nodes.Single().Id, Is.EqualTo(visible.Id));
                Assert.That(trashTotal, Is.EqualTo(0));
                Assert.That(trashResult.Nodes, Is.Empty);
                Assert.That(trashResult.Files, Is.Empty);
            });
        }

        [Test]
        public async Task RecentFiles_FiltersByExactAndWildcardContentTypes()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            await CreateFileAsync(root!.Id, "photo.jpg", "image body", "image/jpeg");
            await CreateFileAsync(root.Id, "clip.mp4", "video body", "video/mp4");
            await CreateFileAsync(root.Id, "notes.txt", "text body", "text/plain");
            await CreateFileAsync(
                root.Id,
                "sheet.xlsx",
                "sheet body",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            NodeFileManifestDto encrypted = await CreateFileAsync(
                root.Id,
                "opaque-file",
                "encrypted body",
                "application/octet-stream");
            NodeFileManifestDto binary = await CreateFileAsync(
                root.Id,
                "disk.iso",
                "binary body",
                "application/octet-stream");
            HttpResponseMessage encryptedMetadataResponse = await _client.PatchAsJsonAsync(
                $"/api/v1/files/{encrypted.Id}/metadata",
                new Dictionary<string, string>
                {
                    ["isClientEncrypted"] = "true",
                });
            encryptedMetadataResponse.EnsureSuccessStatusCode();
            HttpResponseMessage binaryMetadataResponse = await _client.PatchAsJsonAsync(
                $"/api/v1/files/{binary.Id}/metadata",
                new Dictionary<string, string>
                {
                    ["category"] = "disk-image",
                });
            binaryMetadataResponse.EnsureSuccessStatusCode();

            NodeFileManifestDto[]? media = await _client.GetFromJsonAsync<NodeFileManifestDto[]>(
                $"/api/v1/layouts/{root.LayoutId}/recent?count=10&contentType=image/*&contentType=video/*");
            NodeFileManifestDto[]? nonMedia = await _client.GetFromJsonAsync<NodeFileManifestDto[]>(
                $"/api/v1/layouts/{root.LayoutId}/recent?count=10&excludeContentType=image/*&excludeContentType=video/*");
            NodeFileManifestDto[]? documents = await _client.GetFromJsonAsync<NodeFileManifestDto[]>(
                $"/api/v1/layouts/{root.LayoutId}/recent?count=10&contentType=text/*&contentType=application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            NodeFileManifestDto[]? other = await _client.GetFromJsonAsync<NodeFileManifestDto[]>(
                $"/api/v1/layouts/{root.LayoutId}/recent?count=10&excludeClientEncrypted=true&excludeContentType=image/*&excludeContentType=video/*&excludeContentType=audio/*&excludeContentType=text/*&excludeContentType=application/pdf&excludeContentType=application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

            Assert.Multiple(() =>
            {
                Assert.That(media?.Select(file => file.Name), Is.EquivalentTo(new[] { "photo.jpg", "clip.mp4" }));
                Assert.That(
                    nonMedia?.Select(file => file.Name),
                    Is.EquivalentTo(new[] { "notes.txt", "sheet.xlsx", "opaque-file", "disk.iso" }));
                Assert.That(documents?.Select(file => file.Name), Is.EquivalentTo(new[] { "notes.txt", "sheet.xlsx" }));
                Assert.That(other?.Select(file => file.Id), Is.EquivalentTo(new[] { binary.Id }));
            });
        }

        [Test]
        public async Task RecentFiles_RejectsInvalidContentTypePattern()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            HttpResponseMessage response = await _client.GetAsync(
                $"/api/v1/layouts/{root!.LayoutId}/recent?contentType=video/mp*");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        private async Task<(SearchResultDto Result, int TotalCount)> SearchAsync(
            Guid layoutId, string query, int page = 1, int pageSize = 20, Guid? currentNodeId = null)
        {
            HttpResponseMessage response = await _client!.GetAsync(
                $"/api/v1/layouts/{layoutId}/search?query={Uri.EscapeDataString(query)}&page={page}&pageSize={pageSize}&currentNodeId={currentNodeId}");
            response.EnsureSuccessStatusCode();
            SearchResultDto result = (await response.Content.ReadFromJsonAsync<SearchResultDto>())!;
            int totalCount = int.Parse(response.Headers.GetValues("X-Total-Count").Single());
            return (result, totalCount);
        }

        private async Task<NodeFileManifestDto> CreateFileAsync(
            Guid nodeId,
            string name,
            string body,
            string contentType = "application/octet-stream")
        {
            string hash = await UploadChunkAsync(body);
            CreateFileFromChunksRequestDto fileReq = new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [hash],
                Name = name,
                ContentType = contentType,
                Hash = hash,
                NodeId = nodeId,
            };
            HttpResponseMessage response = await _client!.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
            response.EnsureSuccessStatusCode();

            NodeContentDto? children = await _client!.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{nodeId}/children");
            return children!.Files.Single(x => x.Name == name);
        }

        private async Task<string> UploadChunkAsync(string body)
        {
            byte[] content = Encoding.UTF8.GetBytes(body);
            string hash = Hasher.ToHexStringHash(Hasher.HashData(content));
            using MultipartFormDataContent form = new MultipartFormDataContent
            {
                {
                    new ByteArrayContent(content)
                    {
                        Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") },
                    },
                    "file",
                    "chunk.bin"
                },
                { new StringContent(hash), "hash" },
            };

            HttpResponseMessage response = await _client!.PostAsync("/api/v1/chunks", form);
            response.EnsureSuccessStatusCode();
            return hash;
        }
    }
}
