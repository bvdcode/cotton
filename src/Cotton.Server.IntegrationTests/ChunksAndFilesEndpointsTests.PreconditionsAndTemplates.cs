// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
using FileVersionDto = Cotton.Files.FileVersionDto;

namespace Cotton.Server.IntegrationTests
{
    public partial class ChunksAndFilesEndpointsTests
    {
        [Test]
        public async Task Update_File_Content_With_Stale_If_Match_Returns_Precondition_Failed()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);
            NodeDto rootNode = root!;

            NodeFileManifestDto file = await UploadTextFileAsync(rootNode, "etag-update.txt", "first");
            string staleETag = file.ETag;
            file = await UpdateTextFileAsync(file, rootNode, "second");
            string rejectedHash = await UploadChunkAndGetHashAsync("third");

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/files/{file.Id}/update-content")
            {
                Content = JsonContent.Create(new CreateFileFromChunksRequestDto
                {
                    ChunkHashes = [rejectedHash],
                    Name = file.Name,
                    ContentType = "text/plain",
                    Hash = rejectedHash,
                    NodeId = rootNode.Id,
                })
            };
            request.Headers.TryAddWithoutValidation("If-Match", staleETag);

            HttpResponseMessage response = await _client.SendAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.PreconditionFailed));
        }

        [Test]
        public async Task Delete_File_With_Stale_If_Match_Returns_Precondition_Failed_And_Keeps_File()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);
            NodeDto rootNode = root!;

            NodeFileManifestDto file = await UploadTextFileAsync(rootNode, "etag-delete.txt", "first");
            string staleETag = file.ETag;
            file = await UpdateTextFileAsync(file, rootNode, "second");

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/files/{file.Id}");
            request.Headers.TryAddWithoutValidation("If-Match", staleETag);

            HttpResponseMessage response = await _client.SendAsync(request);
            NodeContentDto? list = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{rootNode.Id}/children");

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.PreconditionFailed));
                Assert.That(list, Is.Not.Null);
                Assert.That(list!.Files.Select(x => x.Id), Does.Contain(file.Id));
            });
        }

        [Test]
        public async Task Rename_File_With_Stale_If_Match_Returns_Precondition_Failed_And_Keeps_Name()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);
            NodeDto rootNode = root!;

            NodeFileManifestDto file = await UploadTextFileAsync(rootNode, "etag-rename.txt", "first");
            string staleETag = file.ETag;
            file = await UpdateTextFileAsync(file, rootNode, "second");

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/files/{file.Id}/rename")
            {
                Content = JsonContent.Create(new RenameFileRequestDto { Name = "renamed.txt" })
            };
            request.Headers.TryAddWithoutValidation("If-Match", staleETag);

            HttpResponseMessage response = await _client.SendAsync(request);
            NodeContentDto? list = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{rootNode.Id}/children");

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.PreconditionFailed));
                Assert.That(list, Is.Not.Null);
                Assert.That(list!.Files.Single(x => x.Id == file.Id).Name, Is.EqualTo("etag-rename.txt"));
            });
        }

        [Test]
        public async Task Move_File_With_Stale_If_Match_Returns_Precondition_Failed_And_Keeps_Parent()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);
            NodeDto rootNode = root!;
            NodeDto destination = await CreateFolderAsync(rootNode.Id, "etag-move-destination");

            NodeFileManifestDto file = await UploadTextFileAsync(rootNode, "etag-move.txt", "first");
            string staleETag = file.ETag;
            file = await UpdateTextFileAsync(file, rootNode, "second");

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/files/{file.Id}/move")
            {
                Content = JsonContent.Create(new MoveFileRequestDto { ParentId = destination.Id })
            };
            request.Headers.TryAddWithoutValidation("If-Match", staleETag);

            HttpResponseMessage response = await _client.SendAsync(request);
            NodeContentDto? rootList = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{rootNode.Id}/children");
            NodeContentDto? destinationList = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{destination.Id}/children");

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.PreconditionFailed));
                Assert.That(rootList, Is.Not.Null);
                Assert.That(destinationList, Is.Not.Null);
                Assert.That(rootList!.Files.Select(x => x.Id), Does.Contain(file.Id));
                Assert.That(destinationList!.Files.Select(x => x.Id), Does.Not.Contain(file.Id));
            });
        }

        [Test]
        public async Task Admin_Created_User_Gets_Default_Template_Files()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);
            await UploadTextFileAsync(root!, "welcome.txt", "hello from the template");

            HttpResponseMessage templateResponse = await _client.PatchAsJsonAsync(
                "/api/v1/server/settings/default-user-template-node",
                root!.Id);
            templateResponse.EnsureSuccessStatusCode();

            HttpResponseMessage createUserResponse = await _client.PostAsJsonAsync("/api/v1/users", new
            {
                username = "seededuser",
                password = "seededpass",
                role = UserRole.User
            });
            createUserResponse.EnsureSuccessStatusCode();

            _client.DefaultRequestHeaders.Authorization = null;
            string seededToken = await LoginAsync("seededuser", "seededpass");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seededToken);

            NodeDto? seededRoot = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(seededRoot, Is.Not.Null);

            NodeContentDto? list = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{seededRoot!.Id}/children");
            Assert.That(list, Is.Not.Null);
            NodeFileManifestDto? seededFile = list!.Files.SingleOrDefault(x => x.Name == "welcome.txt");
            Assert.That(seededFile, Is.Not.Null);
            Assert.That(seededFile!.SizeBytes, Is.EqualTo("hello from the template".Length));
        }

        [Test]
        public async Task Default_Template_Node_Rejects_Another_Users_Node()
        {
            string adminToken = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            HttpResponseMessage createUserResponse = await _client.PostAsJsonAsync("/api/v1/users", new
            {
                username = "templateowner",
                password = "templatepass",
                role = UserRole.User
            });
            createUserResponse.EnsureSuccessStatusCode();

            _client.DefaultRequestHeaders.Authorization = null;
            string otherToken = await LoginAsync("templateowner", "templatepass");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);

            NodeDto? otherRoot = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(otherRoot, Is.Not.Null);

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            HttpResponseMessage templateResponse = await _client.PatchAsJsonAsync(
                "/api/v1/server/settings/default-user-template-node",
                otherRoot!.Id);

            Assert.That(templateResponse.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.BadRequest));
        }

    }
}
