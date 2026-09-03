// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public partial class LayoutAndFilesTests
    {
        [Test]
        public async Task GetChildren_NullMetadataInDb_IsSerializedAsEmptyObject()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            HttpResponseMessage createNodeRes = await _client.PutAsJsonAsync(
                "/api/v1/layouts/nodes",
                new CreateNodeRequestDto { ParentId = root!.Id, Name = "null-meta" });
            createNodeRes.EnsureSuccessStatusCode();
            NodeDto? folder = await createNodeRes.Content.ReadFromJsonAsync<NodeDto>();
            Assert.That(folder, Is.Not.Null);

            byte[] content = Encoding.UTF8.GetBytes("payload");
            string hash = Hasher.ToHexStringHash(Hasher.HashData(content));
            using MultipartFormDataContent form = new MultipartFormDataContent
            {
                {
                    new ByteArrayContent(content)
                    {
                        Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") }
                    },
                    "file",
                    "chunk.bin"
                },
                { new StringContent(hash), "hash" }
            };
            HttpResponseMessage uploadRes = await _client.PostAsync("/api/v1/chunks", form);
            uploadRes.EnsureSuccessStatusCode();

            CreateFileFromChunksRequestDto fileReq = new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [hash],
                Name = "legacy.txt",
                ContentType = "text/plain",
                Hash = hash,
                NodeId = folder!.Id
            };
            HttpResponseMessage createFileRes = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
            createFileRes.EnsureSuccessStatusCode();

            int updated = await DbContext.NodeFiles
                .Where(f => f.NodeId == folder.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    x => x.Metadata,
                    (Dictionary<string, string>?)null));
            Assert.That(updated, Is.EqualTo(1));

            HttpResponseMessage listRes = await _client.GetAsync($"/api/v1/layouts/nodes/{folder.Id}/children");
            listRes.EnsureSuccessStatusCode();
            string rawJson = await listRes.Content.ReadAsStringAsync();
            Assert.That(rawJson, Does.Not.Contain("\"metadata\":null"));

            NodeContentDto? list = JsonSerializer.Deserialize<NodeContentDto>(
                rawJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.That(list, Is.Not.Null);
            NodeFileManifestDto[] files = list!.Files.ToArray();
            Assert.That(files, Has.Length.EqualTo(1));
            Assert.That(files[0].Metadata, Is.Not.Null);
            Assert.That(files[0].Metadata, Is.Empty);
        }

        [Test]
        public async Task Cannot_Create_Duplicate_Node_Name_Within_Same_Parent()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            string name = "dup";
            CreateNodeRequestDto req = new CreateNodeRequestDto { ParentId = root!.Id, Name = name };
            // First create should succeed
            HttpResponseMessage r1 = await _client.PutAsJsonAsync("/api/v1/layouts/nodes", req);
            r1.EnsureSuccessStatusCode();
            // Second create with same name under same parent should return conflict (409)
            HttpResponseMessage r2 = await _client.PutAsJsonAsync("/api/v1/layouts/nodes", req);
            Assert.That(r2.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            await TestContext.Progress.WriteLineAsync(
                $"Duplicate create returned status: {(int)r2.StatusCode} {r2.StatusCode}");

            // Verify DB has only one such node
            int duplicates = await DbContext.Nodes
                .AsNoTracking()
                .Where(n => n.ParentId == root.Id && n.Name == name)
                .CountAsync();
            Assert.That(duplicates, Is.EqualTo(1));
        }

    }
}
