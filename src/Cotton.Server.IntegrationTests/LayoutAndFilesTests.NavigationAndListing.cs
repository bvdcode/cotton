// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public partial class LayoutAndFilesTests
    {
        [Test]
        public async Task Resolve_Root_Layout_Returns_RootNode()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            NodeDto? node = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(node, Is.Not.Null);
            Assert.That(node!.Name, Is.EqualTo(NodeType.Default.ToString()));
            Assert.That(node.ParentId, Is.Null);
            Assert.That(node.LayoutId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(node.Id, Is.Not.EqualTo(Guid.Empty));
            await TestContext.Progress.WriteLineAsync(
                $"Resolved root layout. LayoutId={node.LayoutId}, RootId={node.Id}");
        }

        [Test]
        public async Task Create_Node_And_10_Files()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            // Create a new child node under root
            string nodeName = "test";
            CreateNodeRequestDto createNodeReq = new CreateNodeRequestDto { ParentId = root!.Id, Name = nodeName };
            HttpResponseMessage createNodeRes = await _client.PutAsJsonAsync("/api/v1/layouts/nodes", createNodeReq);
            createNodeRes.EnsureSuccessStatusCode();
            NodeDto? child = await createNodeRes.Content.ReadFromJsonAsync<NodeDto>();
            Assert.That(child, Is.Not.Null);
            await TestContext.Progress.WriteLineAsync($"Created node '{nodeName}' with Id={child!.Id}");

            // Upload 10 unique chunks and create files from them
            for (int i = 1; i <= 10; i++)
            {
                byte[] content = Encoding.UTF8.GetBytes($"hello {i}");
                string chunkHashLower = Hasher.ToHexStringHash(Hasher.HashData(content));
                // Upload chunk
                using MultipartFormDataContent form = new MultipartFormDataContent
                {
                    {
                        new ByteArrayContent(content)
                        {
                            Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") }
                        },
                        "file",
                        $"chunk{i}.bin"
                    },
                    { new StringContent(chunkHashLower), "hash" }
                };
                HttpResponseMessage upRes = await _client.PostAsync("/api/v1/chunks", form);
                upRes.EnsureSuccessStatusCode();
                await TestContext.Progress.WriteLineAsync($"Uploaded chunk {i}: {chunkHashLower[..16]}...");

                // Create file (server validates and maps hex → byte[] itself)
                string fileName = $"file{i}.txt";
                CreateFileFromChunksRequestDto fileReq = new CreateFileFromChunksRequestDto
                {
                    ChunkHashes = [chunkHashLower],
                    Name = fileName,
                    ContentType = "text/plain",
                    Hash = chunkHashLower,
                    NodeId = child.Id
                };
                HttpResponseMessage createFileRes = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
                createFileRes.EnsureSuccessStatusCode();
            }

            // Verify children listing shows 10 files
            NodeContentDto? list = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{child!.Id}/children");
            Assert.That(list, Is.Not.Null);
            Assert.That(list!.Files.Count, Is.EqualTo(10));
            string[] names = list.Files
                .OrderBy(x => x.CreatedAt)
                .Select(f => f.Name)
                .ToArray();
            for (int i = 1; i <= 10; i++)
            {
                Assert.That(names[i - 1], Is.EqualTo($"file{i}.txt"));
            }
        }

        [Test]
        public async Task GetChildren_PaginatesFilesInNameOrderAndPreservesTotalCount()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            NodeDto folder = await CreateNodeAsync(root!.Id, "paged-files");
            await UploadTextFileAsync(folder.Id, "delta.txt", "delta");
            await UploadTextFileAsync(folder.Id, "alpha.txt", "alpha");
            await UploadTextFileAsync(folder.Id, "echo.txt", "echo");
            await UploadTextFileAsync(folder.Id, "charlie.txt", "charlie");
            await UploadTextFileAsync(folder.Id, "bravo.txt", "bravo");

            using HttpResponseMessage response = await _client.GetAsync(
                $"/api/v1/layouts/nodes/{folder.Id}/children?page=2&pageSize=2");
            response.EnsureSuccessStatusCode();
            NodeContentDto? page = await response.Content.ReadFromJsonAsync<NodeContentDto>();

            Assert.Multiple(() =>
            {
                Assert.That(response.Headers.GetValues("X-Total-Count").Single(), Is.EqualTo("5"));
                Assert.That(page, Is.Not.Null);
                Assert.That(page!.Files.Select(file => file.Name), Is.EqualTo(new[]
                {
                    "charlie.txt",
                    "delta.txt",
                }));
                Assert.That(page.Files.All(file => file.ContentType == "text/plain"), Is.True);
            });
        }

    }
}
