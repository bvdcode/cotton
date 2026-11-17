// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
using System.Xml.Linq;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class WebDavEncodedPathsTests : FileEndpointTestBase
    {
        [TestCase("invoice%41.txt", "invoiceA.txt")]
        [TestCase("invoice%23.txt", "invoice#.txt")]
        [TestCase("invoice%25.txt", "invoice%.txt")]
        public async Task ReadOperations_PreserveLiteralPercentNames(string name, string otherName)
        {
            NodeDto root = await PrepareRootAsync();
            await UploadTextFileAsync(root, name, "requested-content");
            await UploadTextFileAsync(root, otherName, "other-content");
            await UseWebDavAsync();
            string path = WebDavPath(name);

            using HttpResponseMessage get = await _client!.GetAsync(path);
            using HttpRequestMessage headRequest = new(HttpMethod.Head, path);
            using HttpResponseMessage head = await _client.SendAsync(headRequest);
            using HttpRequestMessage propertiesRequest = new(new HttpMethod("PROPFIND"), path);
            propertiesRequest.Headers.Add("Depth", "0");
            using HttpResponseMessage properties = await _client.SendAsync(propertiesRequest);
            string content = await get.Content.ReadAsStringAsync();
            XDocument xml = XDocument.Parse(await properties.Content.ReadAsStringAsync());
            XNamespace dav = "DAV:";

            Assert.Multiple(() =>
            {
                Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(content, Is.EqualTo("requested-content"));
                Assert.That(head.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(head.Headers.ETag, Is.EqualTo(get.Headers.ETag));
                Assert.That(properties.StatusCode, Is.EqualTo(HttpStatusCode.MultiStatus));
                Assert.That(xml.Descendants(dav + "displayname").Single().Value, Is.EqualTo(name));
                Assert.That(xml.Descendants(dav + "href").Single().Value, Is.EqualTo(path));
            });
        }

        [TestCase("report #50%.txt")]
        [TestCase("invoice%41.txt")]
        [TestCase("report%2F.txt")]
        [TestCase("café-отчёт.txt")]
        public async Task Put_PreservesNamesInsidePercentNamedFolder(string name)
        {
            NodeDto root = await PrepareRootAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "folder%41");
            await UseWebDavAsync();
            string path = WebDavPath(folder.Name, name);
            using StringContent body = new("uploaded-content", Encoding.UTF8, "text/plain");
            using HttpResponseMessage upload = await _client!.PutAsync(path, body);
            upload.EnsureSuccessStatusCode();
            using HttpResponseMessage download = await _client.GetAsync(path);
            download.EnsureSuccessStatusCode();

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext database = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            NodeFile stored = await database.NodeFiles.AsNoTracking().SingleAsync(file => file.NodeId == folder.Id);
            string content = await download.Content.ReadAsStringAsync();
            Assert.Multiple(() =>
            {
                Assert.That(stored.Name, Is.EqualTo(name));
                Assert.That(content, Is.EqualTo("uploaded-content"));
            });
        }

        [TestCase("COPY")]
        [TestCase("MOVE")]
        public async Task Destination_OverwritesOnlyTheRequestedLiteralName(string method)
        {
            NodeDto root = await PrepareRootAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "folder%41");
            NodeFileManifestDto source = await UploadTextFileAsync(root, "source%41.txt", "source-content");
            await UploadTextFileAsync(folder, "invoice%41.txt", "old-content");
            NodeFileManifestDto other = await UploadTextFileAsync(folder, "invoiceA.txt", "other-content");
            await UseWebDavAsync();

            using HttpRequestMessage request = new(new HttpMethod(method), WebDavPath(source.Name));
            request.Headers.Add("Destination", "http://localhost" + WebDavPath(folder.Name, "invoice%41.txt"));
            request.Headers.Add("Overwrite", "T");
            using HttpResponseMessage response = await _client!.SendAsync(request);
            response.EnsureSuccessStatusCode();

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext database = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            NodeFile target = await database.NodeFiles.AsNoTracking()
                .SingleAsync(file => file.NodeId == folder.Id && file.Name == "invoice%41.txt");
            NodeFile unchanged = await database.NodeFiles.AsNoTracking().SingleAsync(file => file.Id == other.Id);
            Assert.Multiple(() =>
            {
                Assert.That(target.FileManifestId, Is.EqualTo(source.FileManifestId));
                Assert.That(unchanged.NodeId, Is.EqualTo(folder.Id));
                Assert.That(unchanged.FileManifestId, Is.EqualTo(other.FileManifestId));
            });
        }

        [TestCase("folder%41")]
        [TestCase("folder #50%")]
        [TestCase("папка café")]
        public async Task MkCol_PreservesName(string name)
        {
            NodeDto root = await PrepareRootAsync();
            await UseWebDavAsync();
            using HttpRequestMessage request = new(new HttpMethod("MKCOL"), WebDavPath(name));
            using HttpResponseMessage response = await _client!.SendAsync(request);
            response.EnsureSuccessStatusCode();

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext database = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            string storedName = await database.Nodes.Where(node => node.ParentId == root.Id)
                .Select(node => node.Name).SingleAsync();
            Assert.That(storedName, Is.EqualTo(name));
        }

        [TestCase("invalid?.txt")]
        [TestCase("invalid*.txt")]
        public async Task MkCol_InvalidNameReturnsBadRequest(string name)
        {
            await PrepareRootAsync();
            await UseWebDavAsync();
            using HttpRequestMessage request = new(new HttpMethod("MKCOL"), WebDavPath(name));
            using HttpResponseMessage response = await _client!.SendAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task Delete_LiteralPercentNameCannotDeleteLockedAlias()
        {
            NodeDto root = await PrepareRootAsync();
            NodeFileManifestDto lockedFile = await UploadTextFileAsync(root, "invoiceA.txt", "locked-content");
            await UploadTextFileAsync(root, "invoice%41.txt", "literal-content");
            await UseWebDavAsync();
            using HttpRequestMessage lockRequest = new(new HttpMethod("LOCK"), WebDavPath(lockedFile.Name));
            using HttpResponseMessage locked = await _client!.SendAsync(lockRequest);
            locked.EnsureSuccessStatusCode();

            using HttpResponseMessage ordinary = await _client.DeleteAsync(WebDavPath(lockedFile.Name));
            using HttpResponseMessage encoded = await _client.DeleteAsync("/api/v1/webdav/invoice%41.txt");
            using HttpResponseMessage literal = await _client.DeleteAsync(WebDavPath("invoice%41.txt"));
            using HttpResponseMessage missingLiteral = await _client.DeleteAsync(WebDavPath("invoice%41.txt"));
            using HttpResponseMessage remaining = await _client.GetAsync(WebDavPath(lockedFile.Name));
            string content = await remaining.Content.ReadAsStringAsync();
            Assert.Multiple(() =>
            {
                Assert.That((int)ordinary.StatusCode, Is.EqualTo(423));
                Assert.That((int)encoded.StatusCode, Is.EqualTo(423));
                Assert.That(literal.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
                Assert.That(missingLiteral.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                Assert.That(remaining.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(content, Is.EqualTo("locked-content"));
            });
        }

        [TestCase("PUT")]
        [TestCase("DELETE")]
        [TestCase("MOVE")]
        public async Task Mutations_RespectLockForEquivalentName(string method)
        {
            NodeDto root = await PrepareRootAsync();
            await UploadTextFileAsync(root, "résumé.txt", "locked-content");
            await UseWebDavAsync();
            using HttpRequestMessage lockRequest = new(new HttpMethod("LOCK"), WebDavPath("résumé.txt"));
            using HttpResponseMessage locked = await _client!.SendAsync(lockRequest);
            locked.EnsureSuccessStatusCode();

            using HttpRequestMessage mutation = new(new HttpMethod(method), WebDavPath("RESUME.TXT"));
            mutation.Headers.Add("Destination", "http://localhost" + WebDavPath("renamed.txt"));
            using HttpResponseMessage response = await _client.SendAsync(mutation);
            Assert.That((int)response.StatusCode, Is.EqualTo(423));
        }

        private async Task<NodeDto> PrepareRootAsync()
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            return (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
        }

        private async Task UseWebDavAsync()
        {
            string token = await GetWebDavTokenAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes("testuser:" + token)));
        }

        private static string WebDavPath(params string[] segments) =>
            "/api/v1/webdav/" + string.Join('/', segments.Select(Uri.EscapeDataString));
    }
}
