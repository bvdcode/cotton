// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public partial class LayoutAndFilesTests
    {
        [Test]
        public async Task Shared_Folder_Api_Exposes_Info_Navigation_And_File_Content()
        {
            string accessToken = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            NodeDto sharedRoot = await CreateNodeAsync(root!.Id, "shared-root-contract");
            NodeDto nested = await CreateNodeAsync(sharedRoot.Id, "nested-contract");
            NodeFileManifestDto file = await UploadTextFileAsync(nested.Id, "shared.txt", "shared body");

            HttpResponseMessage shareLinkResponse = await _client.GetAsync(
                $"/api/v1/layouts/nodes/{sharedRoot.Id}/share-link");
            shareLinkResponse.EnsureSuccessStatusCode();
            string shareLink = (await shareLinkResponse.Content.ReadAsStringAsync()).Trim('"');
            string shareToken = shareLink.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();

            _client.DefaultRequestHeaders.Authorization = null;

            SharedNodeInfoDto? info = await _client.GetFromJsonAsync<SharedNodeInfoDto>(
                $"/api/v1/layouts/shared/{shareToken}");
            Assert.Multiple(() =>
            {
                Assert.That(info, Is.Not.Null);
                Assert.That(info!.Token, Is.EqualTo(shareToken));
                Assert.That(info.NodeId, Is.EqualTo(sharedRoot.Id));
                Assert.That(info.Name, Is.EqualTo(sharedRoot.Name));
            });

            using HttpResponseMessage rootChildrenResponse = await _client.GetAsync(
                $"/api/v1/layouts/shared/{shareToken}/children");
            rootChildrenResponse.EnsureSuccessStatusCode();
            Assert.That(rootChildrenResponse.Headers.GetValues("X-Total-Count").Single(), Is.EqualTo("1"));
            SharedNodeContentDto? rootChildren = await rootChildrenResponse.Content
                .ReadFromJsonAsync<SharedNodeContentDto>();
            Assert.Multiple(() =>
            {
                Assert.That(rootChildren, Is.Not.Null);
                Assert.That(rootChildren!.Id, Is.EqualTo(sharedRoot.Id));
                Assert.That(rootChildren.Nodes.Select(node => node.Id), Is.EqualTo(new[] { nested.Id }));
                Assert.That(rootChildren.Files, Is.Empty);
            });

            NodeDto[]? ancestors = await _client.GetFromJsonAsync<NodeDto[]>(
                $"/api/v1/layouts/shared/{shareToken}/ancestors/{nested.Id}");
            Assert.That(ancestors?.Select(node => node.Id), Is.EqualTo(new[] { sharedRoot.Id }));

            SharedNodeContentDto? nestedChildren = await _client.GetFromJsonAsync<SharedNodeContentDto>(
                $"/api/v1/layouts/shared/{shareToken}/children?nodeId={nested.Id}");
            Assert.Multiple(() =>
            {
                Assert.That(nestedChildren, Is.Not.Null);
                Assert.That(nestedChildren!.Nodes, Is.Empty);
                Assert.That(nestedChildren.Files.Select(nodeFile => nodeFile.Id), Is.EqualTo(new[] { file.Id }));
            });

            string fileContent = await _client.GetStringAsync(
                $"/api/v1/layouts/shared/{shareToken}/files/{file.Id}/content?download=false");
            Assert.That(fileContent, Is.EqualTo("shared body"));
        }

        [Test]
        public async Task Shared_Children_Rejects_Tampered_Ancestor_Path_WithStrictIntegrity()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            NodeDto sharedRoot = await CreateNodeAsync(root!.Id, "shared-root");
            NodeDto outsideParent = await CreateNodeAsync(root.Id, "outside-parent");
            NodeDto outsideChild = await CreateNodeAsync(outsideParent.Id, "outside-child");

            HttpResponseMessage shareLinkRes = await _client.GetAsync($"/api/v1/layouts/nodes/{sharedRoot.Id}/share-link");
            shareLinkRes.EnsureSuccessStatusCode();
            string shareLink = (await shareLinkRes.Content.ReadAsStringAsync()).Trim('"');
            string shareToken = shareLink.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();

            int updatedRows = await DbContext.Nodes
                .Where(node => node.Id == outsideParent.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    node => node.ParentId,
                    sharedRoot.Id));
            Assert.That(updatedRows, Is.EqualTo(1));

            _client.DefaultRequestHeaders.Authorization = null;

            HttpResponseMessage response = await _client.GetAsync(
                $"/api/v1/layouts/shared/{shareToken}/children?nodeId={outsideChild.Id}");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task Shared_Archive_Download_Link_Allows_Current_Shared_Folder_Only()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            NodeDto sharedRoot = await CreateNodeAsync(root!.Id, "shared-root");
            NodeDto nested = await CreateNodeAsync(sharedRoot.Id, "nested");
            NodeDto outside = await CreateNodeAsync(root.Id, "outside");
            await UploadTextFileAsync(sharedRoot.Id, "root.txt", "root body");
            await UploadTextFileAsync(nested.Id, "deep.txt", "deep body");

            HttpResponseMessage shareLinkRes = await _client.GetAsync($"/api/v1/layouts/nodes/{sharedRoot.Id}/share-link");
            shareLinkRes.EnsureSuccessStatusCode();
            string shareLink = (await shareLinkRes.Content.ReadAsStringAsync()).Trim('"');
            string shareToken = shareLink.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();

            _client.DefaultRequestHeaders.Authorization = null;

            HttpResponseMessage linkResponse = await _client.PostAsync(
                $"/api/v1/layouts/shared/{shareToken}/archives/download-link?nodeId={sharedRoot.Id}",
                null);
            linkResponse.EnsureSuccessStatusCode();
            ArchiveDownloadLinkDto? archive = await linkResponse.Content.ReadFromJsonAsync<ArchiveDownloadLinkDto>();
            Assert.That(archive, Is.Not.Null);

            HttpResponseMessage download = await _client.GetAsync(archive!.Url);
            download.EnsureSuccessStatusCode();
            byte[] bytes = await download.Content.ReadAsByteArrayAsync();

            using ZipArchive zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            AssertZipEntry(zip, "shared-root/root.txt", "root body");
            AssertZipEntry(zip, "shared-root/nested/deep.txt", "deep body");

            HttpResponseMessage outsideResponse = await _client.PostAsync(
                $"/api/v1/layouts/shared/{shareToken}/archives/download-link?nodeId={outside.Id}",
                null);
            Assert.That(outsideResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task Shared_Archive_Download_Link_Enforces_Public_Entry_Limit()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            NodeDto sharedRoot = await CreateNodeAsync(root!.Id, "huge-shared-root");
            var sharedRootEntity = await DbContext.Nodes
                .AsNoTracking()
                .Select(x => new { x.Id, x.LayoutId, x.OwnerId })
                .SingleAsync(x => x.Id == sharedRoot.Id);

            const int publicShareEntryLimit = 5_000;
            List<Node> children = new(publicShareEntryLimit);
            for (int i = 0; i < publicShareEntryLimit; i++)
            {
                Node child = new()
                {
                    OwnerId = sharedRootEntity.OwnerId,
                    LayoutId = sharedRootEntity.LayoutId,
                    ParentId = sharedRoot.Id,
                    Type = NodeType.Default,
                };
                child.SetName($"child-{i:D4}");
                children.Add(child);
            }

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            await dbContext.Nodes.AddRangeAsync(children);
            await dbContext.SaveChangesAsync();

            HttpResponseMessage shareLinkRes = await _client.GetAsync($"/api/v1/layouts/nodes/{sharedRoot.Id}/share-link");
            shareLinkRes.EnsureSuccessStatusCode();
            string shareLink = (await shareLinkRes.Content.ReadAsStringAsync()).Trim('"');
            string shareToken = shareLink.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();

            _client.DefaultRequestHeaders.Authorization = null;
            HttpResponseMessage linkResponse = await _client.PostAsync(
                $"/api/v1/layouts/shared/{shareToken}/archives/download-link?nodeId={sharedRoot.Id}",
                null);

            Assert.That(linkResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            string body = await linkResponse.Content.ReadAsStringAsync();
            Assert.That(body, Does.Contain("limited to 5000 entries"));
        }

        [Test]
        public async Task Shared_Folder_Page_Contains_Social_Preview_Meta_Tags()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpResponseMessage publicBaseUrlRes = await _client.PatchAsJsonAsync(
                "/api/v1/server/settings/public-base-url",
                "https://public.example");
            publicBaseUrlRes.EnsureSuccessStatusCode();

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            CreateNodeRequestDto createNodeReq = new CreateNodeRequestDto { ParentId = root!.Id, Name = "shared-folder" };
            HttpResponseMessage createNodeRes = await _client.PutAsJsonAsync("/api/v1/layouts/nodes", createNodeReq);
            createNodeRes.EnsureSuccessStatusCode();

            NodeDto? child = await createNodeRes.Content.ReadFromJsonAsync<NodeDto>();
            Assert.That(child, Is.Not.Null);

            HttpResponseMessage shareLinkRes = await _client.GetAsync($"/api/v1/layouts/nodes/{child!.Id}/share-link");
            shareLinkRes.EnsureSuccessStatusCode();
            string shareLink = await shareLinkRes.Content.ReadAsStringAsync();
            Assert.That(shareLink, Is.Not.Null.And.Not.Empty);

            _client.DefaultRequestHeaders.Authorization = null;

            HttpResponseMessage sharedPageRes = await _client.GetAsync(shareLink.Trim('"'));
            sharedPageRes.EnsureSuccessStatusCode();

            Assert.That(sharedPageRes.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));

            string html = await sharedPageRes.Content.ReadAsStringAsync();
            Assert.That(html, Does.Not.Contain("\\\""));
            Assert.That(html, Does.Contain("<html lang=\"en\">"));
            Assert.That(html, Does.Contain("<meta charset=\"utf-8\">"));
            Assert.That(html, Does.Contain("<meta http-equiv=\"refresh\""));
            Assert.That(html, Does.Contain("<link rel=\"canonical\""));
            Assert.That(html, Does.Contain("<meta property=\"og:image\""));
            Assert.That(html, Does.Contain("<meta name=\"twitter:image\""));
            Assert.That(html, Does.Contain("<meta name=\"twitter:card\" content=\"summary_large_image\""));
            Assert.That(html, Does.Contain("https://public.example/assets/images/social-preview.jpg"));
            Assert.That(html, Does.Contain("https://public.example/share/"));
        }

    }
}
