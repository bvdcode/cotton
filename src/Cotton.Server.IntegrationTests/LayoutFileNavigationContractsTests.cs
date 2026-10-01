// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
using Cotton.Server.Handlers.WebDav;
using Cotton.Server.Services.WebDav;
using Cotton.Topology.Abstractions;
using EasyExtensions.Mediator;
using System.Xml.Linq;

namespace Cotton.Server.IntegrationTests
{
    public class LayoutFileNavigationContractsTests : LayoutFileTestBase
    {
        [Test]
        public async Task NavigationContract_RejectsUnboundedListingRequests()
        {
            NodeDto root = await PrepareNavigationRootAsync();
            NodeDto folder = await CreateNodeAsync(root.Id, "bounded-listing");
            string shareToken = await GetNavigationShareTokenAsync(folder.Id);

            using HttpResponseMessage validOwned = await _client!.GetAsync(
                $"/api/v1/layouts/nodes/{folder.Id}/children?pageSize={ListingRequestLimits.MaxPageSize}");
            using HttpResponseMessage oversizedOwned = await _client.GetAsync(
                $"/api/v1/layouts/nodes/{folder.Id}/children?pageSize={ListingRequestLimits.MaxPageSize + 1}");
            using HttpResponseMessage excessiveDepth = await _client.GetAsync(
                $"/api/v1/layouts/nodes/{folder.Id}/children?depth={ListingRequestLimits.MaxDepth + 1}");
            using HttpResponseMessage overflowingPage = await _client.GetAsync(
                $"/api/v1/layouts/nodes/{folder.Id}/children?page={int.MaxValue}&pageSize={ListingRequestLimits.MaxPageSize}");
            using HttpResponseMessage validRecent = await _client.GetAsync(
                $"/api/v1/layouts/{root.LayoutId}/recent?count={ListingRequestLimits.MaxRecentCount}");
            using HttpResponseMessage oversizedRecent = await _client.GetAsync(
                $"/api/v1/layouts/{root.LayoutId}/recent?count={ListingRequestLimits.MaxRecentCount + 1}");

            _client.DefaultRequestHeaders.Authorization = null;
            using HttpResponseMessage validShared = await _client.GetAsync(
                $"/api/v1/layouts/shared/{shareToken}/children?pageSize={ListingRequestLimits.MaxPageSize}");
            using HttpResponseMessage oversizedShared = await _client.GetAsync(
                $"/api/v1/layouts/shared/{shareToken}/children?pageSize={ListingRequestLimits.MaxPageSize + 1}");
            using HttpResponseMessage overflowingShared = await _client.GetAsync(
                $"/api/v1/layouts/shared/{shareToken}/children?page={int.MaxValue}&pageSize={ListingRequestLimits.MaxPageSize}");

            Assert.Multiple(() =>
            {
                Assert.That(validOwned.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(oversizedOwned.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                Assert.That(excessiveDepth.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                Assert.That(overflowingPage.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                Assert.That(validRecent.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(oversizedRecent.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                Assert.That(validShared.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(oversizedShared.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                Assert.That(overflowingShared.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            });
        }

        [Test]
        public async Task NavigationContract_PrivateAndSharedPagesCrossFolderFileBoundary()
        {
            NodeDto root = await PrepareNavigationRootAsync();
            NodeDto folder = await CreateNodeAsync(root.Id, "mixed-page");
            await CreateNodeAsync(folder.Id, "beta");
            await CreateNodeAsync(folder.Id, "alpha");
            await UploadTextFileAsync(folder.Id, "delta.txt", "delta");
            await UploadTextFileAsync(folder.Id, "charlie.txt", "charlie");

            using HttpResponseMessage owned = await _client!.GetAsync(
                $"/api/v1/layouts/nodes/{folder.Id}/children?page=1&pageSize=3");
            owned.EnsureSuccessStatusCode();
            NodeContentDto ownPage = (await owned.Content.ReadFromJsonAsync<NodeContentDto>())!;
            string shareToken = await GetNavigationShareTokenAsync(folder.Id);
            _client.DefaultRequestHeaders.Authorization = null;
            using HttpResponseMessage shared = await _client.GetAsync(
                $"/api/v1/layouts/shared/{shareToken}/children?page=1&pageSize=3");
            shared.EnsureSuccessStatusCode();
            SharedNodeContentDto sharedPage = (await shared.Content.ReadFromJsonAsync<SharedNodeContentDto>())!;
            Assert.Multiple(() =>
            {
                Assert.That(ownPage.Nodes.Select(n => n.Name), Is.EqualTo(new[] { "alpha", "beta" }));
                Assert.That(ownPage.Files.Select(f => f.Name), Is.EqualTo(new[] { "charlie.txt" }));
                Assert.That(sharedPage.Nodes.Select(n => n.Id), Is.EqualTo(ownPage.Nodes.Select(n => n.Id)));
                Assert.That(sharedPage.Files.Select(f => f.Id), Is.EqualTo(ownPage.Files.Select(f => f.Id)));
                Assert.That(owned.Headers.GetValues("X-Total-Count").Single(), Is.EqualTo("4"));
                Assert.That(shared.Headers.GetValues("X-Total-Count").Single(), Is.EqualTo("4"));
            });

            SharedNodeContentDto lastPage = (await _client.GetFromJsonAsync<SharedNodeContentDto>(
                $"/api/v1/layouts/shared/{shareToken}/children?page=2&pageSize=3"))!;
            Assert.Multiple(() =>
            {
                Assert.That(lastPage.Nodes, Is.Empty);
                Assert.That(lastPage.Files.Select(f => f.Name), Is.EqualTo(new[] { "delta.txt" }));
            });
        }

        [Test]
        public async Task NavigationContract_SiblingNameLookupReturnsOnlyMatchesUntilNamesAreNeeded()
        {
            NodeDto root = await PrepareNavigationRootAsync();
            NodeDto folder = await CreateNodeAsync(root.Id, "lookup-parent");
            NodeDto existing = await CreateNodeAsync(folder.Id, "Photos");
            await CreateNodeAsync(folder.Id, "Other");
            NodeFileManifestDto file = await UploadTextFileAsync(folder.Id, "Report.txt", "report");
            string route = $"/api/v1/layouts/nodes/{folder.Id}/sibling-names";

            using HttpResponseMessage unmatchedResponse = await _client!.PostAsJsonAsync(route,
                new SiblingNameLookupRequestDto { Names = ["missing"] });
            unmatchedResponse.EnsureSuccessStatusCode();
            SiblingNameLookupDto unmatched = (await unmatchedResponse.Content
                .ReadFromJsonAsync<SiblingNameLookupDto>())!;

            using HttpResponseMessage matchedResponse = await _client.PostAsJsonAsync(route,
                new SiblingNameLookupRequestDto { Names = ["photos", "REPORT.TXT", "missing"] });
            matchedResponse.EnsureSuccessStatusCode();
            SiblingNameLookupDto matched = (await matchedResponse.Content
                .ReadFromJsonAsync<SiblingNameLookupDto>())!;

            using HttpResponseMessage takenResponse = await _client.PostAsJsonAsync(route,
                new SiblingNameLookupRequestDto
                {
                    Names = ["photos"],
                    IncludeTakenNamesOnConflict = true,
                });
            takenResponse.EnsureSuccessStatusCode();
            SiblingNameLookupDto taken = (await takenResponse.Content
                .ReadFromJsonAsync<SiblingNameLookupDto>())!;

            Assert.Multiple(() =>
            {
                Assert.That(unmatched.Nodes, Is.Empty);
                Assert.That(unmatched.Files, Is.Empty);
                Assert.That(unmatched.TakenNameKeys, Is.Empty);
                Assert.That(matched.Nodes.Select(node => (node.Id, node.Name)),
                    Is.EqualTo(new[] { (existing.Id, "Photos") }));
                Assert.That(matched.Files.Select(item => (item.Id, item.Name)),
                    Is.EqualTo(new[] { (file.Id, "Report.txt") }));
                Assert.That(matched.TakenNameKeys, Is.Empty);
                Assert.That(taken.TakenNameKeys,
                    Is.EquivalentTo(new[] { "photos", "other", "report.txt" }));
            });
        }

        [Test]
        public async Task NavigationContract_FolderStatsIncludeOnlyDirectChildrenWhenRequested()
        {
            NodeDto root = await PrepareNavigationRootAsync();
            NodeDto folder = await CreateNodeAsync(root.Id, "stats-parent");
            NodeDto nested = await CreateNodeAsync(folder.Id, "nested");
            await CreateNodeAsync(folder.Id, "second");
            await UploadTextFileAsync(folder.Id, "direct.txt", "direct contents");
            NodeFileManifestDto encrypted = await UploadTextFileAsync(folder.Id, "encrypted.txt", "ciphertext");
            using HttpResponseMessage metadataResponse = await _client!.PatchAsJsonAsync(
                $"/api/v1/files/{encrypted.Id}/metadata",
                new Dictionary<string, string?> { ["isClientEncrypted"] = "true" });
            metadataResponse.EnsureSuccessStatusCode();
            await UploadTextFileAsync(nested.Id, "nested.txt", "nested contents are excluded");

            using HttpResponseMessage first = await _client!.GetAsync(
                $"/api/v1/layouts/nodes/{folder.Id}/children?page=1&pageSize=2&includeStats=true");
            first.EnsureSuccessStatusCode();
            NodeContentDto firstPage = (await first.Content.ReadFromJsonAsync<NodeContentDto>())!;
            using HttpResponseMessage second = await _client.GetAsync(
                $"/api/v1/layouts/nodes/{folder.Id}/children?page=2&pageSize=2");
            second.EnsureSuccessStatusCode();
            NodeContentDto secondPage = (await second.Content.ReadFromJsonAsync<NodeContentDto>())!;

            Assert.Multiple(() =>
            {
                Assert.That(firstPage.Stats, Is.Not.Null);
                Assert.That(firstPage.Stats!.Folders, Is.EqualTo(2));
                Assert.That(firstPage.Stats.Files, Is.EqualTo(2));
                Assert.That(firstPage.Stats.EncryptedFiles, Is.EqualTo(1));
                Assert.That(firstPage.Stats.SizeBytes, Is.EqualTo("direct contents".Length + "ciphertext".Length));
                Assert.That(first.Headers.GetValues("X-Total-Count").Single(), Is.EqualTo("4"));
                Assert.That(firstPage.Nodes, Has.Count.EqualTo(2));
                Assert.That(secondPage.Files, Has.Count.EqualTo(2));
                Assert.That(secondPage.Stats, Is.Null);
            });
        }

        [Test]
        public async Task NavigationContract_RequestedFolderStatsCanIncludeDescendants()
        {
            NodeDto root = await PrepareNavigationRootAsync();
            NodeDto folder = await CreateNodeAsync(root.Id, "stats-details");
            NodeDto nested = await CreateNodeAsync(folder.Id, "nested-details");
            await CreateNodeAsync(nested.Id, "grandchild-details");
            await UploadTextFileAsync(folder.Id, "direct-details.txt", "direct");
            await UploadTextFileAsync(nested.Id, "nested-details.txt", "nested");

            FolderStatsDto direct = (await _client!.GetFromJsonAsync<FolderStatsDto>(
                $"/api/v1/layouts/nodes/{folder.Id}/stats"))!;
            FolderStatsDto recursive = (await _client.GetFromJsonAsync<FolderStatsDto>(
                $"/api/v1/layouts/nodes/{folder.Id}/stats?recursive=true"))!;
            using HttpResponseMessage missing = await _client.GetAsync(
                $"/api/v1/layouts/nodes/{Guid.NewGuid()}/stats?recursive=true");

            Assert.Multiple(() =>
            {
                Assert.That(direct.Folders, Is.EqualTo(1));
                Assert.That(direct.Files, Is.EqualTo(1));
                Assert.That(direct.SizeBytes, Is.EqualTo("direct".Length));
                Assert.That(recursive.Folders, Is.EqualTo(2));
                Assert.That(recursive.Files, Is.EqualTo(2));
                Assert.That(recursive.SizeBytes, Is.EqualTo("direct".Length + "nested".Length));
                Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            });
        }

        [Test]
        public async Task NavigationContract_PrivateDepthSkipsIntermediateLevels()
        {
            NodeDto root = await PrepareNavigationRootAsync();
            NodeDto parent = await CreateNodeAsync(root.Id, "depth-parent");
            NodeDto child = await CreateNodeAsync(parent.Id, "child");
            NodeDto grandchild = await CreateNodeAsync(child.Id, "grandchild");
            await UploadTextFileAsync(parent.Id, "parent.txt", "parent");
            NodeFileManifestDto nestedFile = await UploadTextFileAsync(child.Id, "child.txt", "child");
            NodeContentDto page = (await _client!.GetFromJsonAsync<NodeContentDto>(
                $"/api/v1/layouts/nodes/{parent.Id}/children?depth=1"))!;
            Assert.Multiple(() =>
            {
                Assert.That(page.Nodes.Select(n => n.Id), Is.EqualTo(new[] { grandchild.Id }));
                Assert.That(page.Files.Select(f => f.Id), Is.EqualTo(new[] { nestedFile.Id }));
            });
            NodeContentDto empty = (await _client.GetFromJsonAsync<NodeContentDto>(
                $"/api/v1/layouts/nodes/{parent.Id}/children?depth=3"))!;
            Assert.That(empty.Nodes.Concat<object>(empty.Files), Is.Empty);
        }

        [TestCase(0, 1)]
        [TestCase(1, 3)]
        [TestCase(2, 5)]
        public async Task NavigationContract_WebDavDepthIncludesCollectionAndRequestedLevels(int depth, int expectedCount)
        {
            NodeDto root = await PrepareNavigationRootAsync();
            NodeDto parent = await CreateNodeAsync(root.Id, "dav-depth");
            NodeDto child = await CreateNodeAsync(parent.Id, "child");
            await CreateNodeAsync(child.Id, "grandchild");
            await UploadTextFileAsync(parent.Id, "parent.txt", "parent");
            await UploadTextFileAsync(child.Id, "child.txt", "child");
            Guid ownerId = await DbContext.Nodes.Where(n => n.Id == root.Id).Select(n => n.OwnerId).SingleAsync();
            using IServiceScope scope = _factory!.Services.CreateScope();
            WebDavPropFindResult result = await scope.ServiceProvider.GetRequiredService<IMediator>()
                .Send(new WebDavPropFindQuery(ownerId, "dav-depth", "/dav/", depth));
            XNamespace dav = "DAV:";
            XDocument xml = XDocument.Parse(result.XmlResponse!);
            Assert.Multiple(() =>
            {
                Assert.That(result.Found, Is.True);
                Assert.That(xml.Descendants(dav + "response").Count(), Is.EqualTo(expectedCount));
                Assert.That(xml.Descendants(dav + "href").First().Value, Is.EqualTo("/dav/dav-depth/"));
            });
        }

        [Test]
        public async Task NavigationContract_WebDavResolvesEncodedFolderAndFileNames()
        {
            NodeDto root = await PrepareNavigationRootAsync();
            NodeDto parent = await CreateNodeAsync(root.Id, "folder #100%");
            NodeFileManifestDto file = await UploadTextFileAsync(parent.Id, "report #50%.txt", "report");
            Guid ownerId = await DbContext.Nodes.Where(n => n.Id == root.Id).Select(n => n.OwnerId).SingleAsync();
            using IServiceScope scope = _factory!.Services.CreateScope();
            IWebDavPathResolver resolver = scope.ServiceProvider.GetRequiredService<IWebDavPathResolver>();
            string folderPath = Uri.EscapeDataString(parent.Name.ToUpperInvariant());
            WebDavResolveResult folder = await resolver.ResolveMetadataAsync(ownerId, folderPath);
            WebDavResolveResult result = await resolver.ResolveMetadataAsync(ownerId, folderPath + "/" + Uri.EscapeDataString(file.Name));
            WebDavResolveResult missing = await resolver.ResolveMetadataAsync(ownerId, folderPath + "/missing");
            Assert.Multiple(() =>
            {
                Assert.That(folder.Node?.Id, Is.EqualTo(parent.Id));
                Assert.That(result.NodeFile?.Id, Is.EqualTo(file.Id));
                Assert.That(missing.Found, Is.False);
            });
            WebDavPropFindResult properties = await scope.ServiceProvider.GetRequiredService<IMediator>()
                .Send(new WebDavPropFindQuery(ownerId, folderPath + "/" + Uri.EscapeDataString(file.Name), "/dav/", 0));
            XNamespace dav = "DAV:";
            Assert.That(XDocument.Parse(properties.XmlResponse!).Descendants(dav + "href").Single().Value,
                Is.EqualTo("/dav/" + Uri.EscapeDataString(parent.Name) + "/" + Uri.EscapeDataString(file.Name)));
        }

        [Test]
        public async Task NavigationContract_SharedAncestorsStopAtGrantedRoot()
        {
            NodeDto root = await PrepareNavigationRootAsync();
            NodeDto privateParent = await CreateNodeAsync(root.Id, "private-parent");
            NodeDto sharedRoot = await CreateNodeAsync(privateParent.Id, "shared-root");
            NodeDto nested = await CreateNodeAsync(sharedRoot.Id, "nested");
            NodeDto leaf = await CreateNodeAsync(nested.Id, "leaf");
            string shareToken = await GetNavigationShareTokenAsync(sharedRoot.Id);
            _client!.DefaultRequestHeaders.Authorization = null;
            NodeDto[] ancestors = (await _client.GetFromJsonAsync<NodeDto[]>(
                $"/api/v1/layouts/shared/{shareToken}/ancestors/{leaf.Id}"))!;
            Assert.That(ancestors.Select(n => n.Id), Is.EqualTo(new[] { sharedRoot.Id, nested.Id }));
            using HttpResponseMessage outside = await _client.GetAsync(
                $"/api/v1/layouts/shared/{shareToken}/children?nodeId={privateParent.Id}");
            Assert.That(outside.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task NavigationContract_SharedRootHasNoVisibleAncestors()
        {
            NodeDto root = await PrepareNavigationRootAsync();
            NodeDto privateParent = await CreateNodeAsync(root.Id, "private-parent");
            NodeDto sharedRoot = await CreateNodeAsync(privateParent.Id, "shared-root");
            string shareToken = await GetNavigationShareTokenAsync(sharedRoot.Id);
            _client!.DefaultRequestHeaders.Authorization = null;
            NodeDto[] ancestors = (await _client.GetFromJsonAsync<NodeDto[]>(
                $"/api/v1/layouts/shared/{shareToken}/ancestors/{sharedRoot.Id}"))!;
            Assert.That(ancestors, Is.Empty);
        }

        [Test]
        public async Task NavigationContract_CyclicAncestryIsRejected()
        {
            NodeDto root = await PrepareNavigationRootAsync();
            NodeDto parent = await CreateNodeAsync(root.Id, "cyclic-parent");
            NodeDto child = await CreateNodeAsync(parent.Id, "cyclic-child");
            string shareToken = await GetNavigationShareTokenAsync(root.Id);
            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Node parentEntity = await db.Nodes.SingleAsync(node => node.Id == parent.Id);
            parentEntity.ParentId = child.Id;
            await db.SaveChangesAsync();

            using HttpResponseMessage ancestors = await _client!.GetAsync($"/api/v1/layouts/nodes/{child.Id}/ancestors");
            Assert.That(ancestors.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            string? path = await scope.ServiceProvider.GetRequiredService<ILayoutNavigator>()
                .GetNodePathFromRootAsync(parentEntity.OwnerId, child.Id, NodeType.Default);
            Assert.That(path, Is.Null);
            _client.DefaultRequestHeaders.Authorization = null;
            using HttpResponseMessage shared = await _client.GetAsync(
                $"/api/v1/layouts/shared/{shareToken}/children?nodeId={child.Id}");
            Assert.That(shared.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        private async Task<NodeDto> PrepareNavigationRootAsync()
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            return (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
        }

        private async Task<string> GetNavigationShareTokenAsync(Guid nodeId)
        {
            using HttpResponseMessage response = await _client!.GetAsync($"/api/v1/layouts/nodes/{nodeId}/share-link");
            response.EnsureSuccessStatusCode();
            string link = (await response.Content.ReadAsStringAsync()).Trim('"');
            return link.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
        }
    }
}
