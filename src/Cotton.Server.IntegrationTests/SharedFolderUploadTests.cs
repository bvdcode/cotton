// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class SharedFolderUploadTests : LayoutEndpointTestBase
    {
        private HttpClient _guest = null!;
        private NodeDto _root = null!;
        private NodeDto _folder = null!;
        private string _token = null!;
        private Guid _session;

        [SetUp]
        public async Task SetUpShare()
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            _root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            _folder = await CreateNodeAsync(_root.Id, "shared uploads");
            _token = await ShareAsync(_folder.Id, true);
            _guest = _factory!.CreateClient();
            _session = Guid.NewGuid();
        }

        [TearDown]
        public void DisposeGuest() => _guest?.Dispose();

        [Test]
        public async Task AnonymousUploadCreatesOwnedFileInSharedSubfolderAndDownloadsExactBytes()
        {
            NodeDto child = await CreateNodeAsync(_folder.Id, "child");
            string first = "first chunk ";
            string second = "second chunk";
            string[] hashes = [Hash(first), Hash(second)];
            (await UploadAsync(child.Id, first)).EnsureSuccessStatusCode();
            (await UploadAsync(child.Id, second)).EnsureSuccessStatusCode();
            HttpResponseMessage created = await CreateAsync(child.Id, hashes, Hash(first + second));
            created.EnsureSuccessStatusCode();
            NodeFileManifestDto file = (await created.Content.ReadFromJsonAsync<NodeFileManifestDto>())!;
            Assert.That(await _guest.GetStringAsync($"/api/v1/layouts/shared/{_token}/files/{file.Id}/content"), Is.EqualTo(first + second));
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            NodeFile row = await db.NodeFiles.SingleAsync(x => x.Id == file.Id);
            NodeShareToken share = await db.NodeShareTokens.SingleAsync(x => x.Token == _token);
            Assert.That(row.OwnerId, Is.EqualTo(share.CreatedByUserId));
            Assert.That(row.NodeId, Is.EqualTo(child.Id));
        }

        [Test]
        public async Task ReadOnlySharesDenyChunkUploadAndFileCreation()
        {
            _token = await ShareAsync(_folder.Id, false);
            Assert.That((await UploadAsync(_folder.Id, "blocked")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That((await CreateAsync(_folder.Id, [Hash("blocked")], Hash("blocked"))).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            SharedNodeInfoDto info = (await _guest.GetFromJsonAsync<SharedNodeInfoDto>($"/api/v1/layouts/shared/{_token}"))!;
            Assert.That(info.AllowUpload, Is.False);
        }

        [Test]
        public async Task CannotWriteOutsideSharedTree()
        {
            Assert.That((await UploadAsync(_root.Id, "outside")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That((await CreateAsync(_root.Id, [Hash("outside")], Hash("outside"))).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task SessionCannotReferencePrivateOwnerChunksOrForgeExistingManifestHash()
        {
            string secret = "private content";
            string privateHash = Hash(secret);
            using ByteArrayContent body = new(Encoding.UTF8.GetBytes(secret));
            (await _client!.PostAsync($"/api/v1/chunks/raw?hash={privateHash}", body)).EnsureSuccessStatusCode();
            (await _client.PostAsJsonAsync("/api/v1/files/from-chunks", new CreateFileFromChunksRequestDto
            {
                NodeId = _root.Id, Name = "private.txt", ContentType = "text/plain", Hash = privateHash, ChunkHashes = [privateHash],
            })).EnsureSuccessStatusCode();
            (await UploadAsync(_folder.Id, "guest content")).EnsureSuccessStatusCode();
            Assert.That((await CreateAsync(_folder.Id, [privateHash], privateHash)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That((await CreateAsync(_folder.Id, [Hash("guest content")], privateHash)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            NodeContentDto content = (await _client!.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{_folder.Id}/children"))!;
            Assert.That(content.Files, Is.Empty);
        }

        [Test]
        public async Task ChunkPermissionsAreBoundToSessionShareAndFolder()
        {
            (await UploadAsync(_folder.Id, "scoped")).EnsureSuccessStatusCode();
            Guid originalSession = _session;
            _session = Guid.NewGuid();
            Assert.That((await CreateAsync(_folder.Id, [Hash("scoped")], Hash("scoped"))).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            _session = originalSession;
            string originalToken = _token;
            _token = await ShareAsync(_folder.Id, true);
            Assert.That((await CreateAsync(_folder.Id, [Hash("scoped")], Hash("scoped"))).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            _token = originalToken;
            NodeDto child = await CreateNodeAsync(_folder.Id, "other folder");
            Assert.That((await CreateAsync(child.Id, [Hash("scoped")], Hash("scoped"))).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            (await CreateAsync(_folder.Id, [Hash("scoped")], Hash("scoped"))).EnsureSuccessStatusCode();
        }

        [Test]
        public async Task InvalidChunkContentDoesNotGrantPermission()
        {
            HttpResponseMessage upload = await UploadAsync(_folder.Id, "wrong", Hash("expected"));
            Assert.That(upload.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That((await CreateAsync(_folder.Id, [Hash("expected")], Hash("expected"))).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task EvictedSessionCannotReferencePreviouslyUploadedContent()
        {
            (await UploadAsync(_folder.Id, "expired")).EnsureSuccessStatusCode();
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Guid shareId = (await db.NodeShareTokens.SingleAsync(x => x.Token == _token)).Id;
            scope.ServiceProvider.GetRequiredService<IMemoryCache>().Remove((nameof(SharedUploadSessionCache), shareId, _folder.Id, _session));
            Assert.That((await CreateAsync(_folder.Id, [Hash("expired")], Hash("expired"))).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ExpirationOrRevocationIsCheckedAgainBeforeFileCreation(bool expire)
        {
            (await UploadAsync(_folder.Id, "revoked")).EnsureSuccessStatusCode();
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            NodeShareToken share = await db.NodeShareTokens.SingleAsync(x => x.Token == _token);
            if (expire)
            {
                share.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            }
            else
            {
                share.AllowUpload = false;
            }
            await db.SaveChangesAsync();
            Assert.That((await CreateAsync(_folder.Id, [Hash("revoked")], Hash("revoked"))).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task NameConflictsCannotOverwriteAndEmptyFilesAreSupported()
        {
            using HttpResponseMessage created = await CreateAsync(_folder.Id, [], Hash(string.Empty));
            created.EnsureSuccessStatusCode();
            NodeFileManifestDto file = (await created.Content.ReadFromJsonAsync<NodeFileManifestDto>())!;
            Assert.That(file.SizeBytes, Is.Zero);
            Assert.That(await _guest.GetByteArrayAsync($"/api/v1/layouts/shared/{_token}/files/{file.Id}/content"), Is.Empty);
            (await UploadAsync(_folder.Id, "replacement")).EnsureSuccessStatusCode();
            Assert.That((await CreateAsync(_folder.Id, [Hash("replacement")], Hash("replacement"))).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        }

        [Test]
        public async Task EmptyChunkListCannotReferenceNonEmptyContent()
        {
            (await UploadAsync(_folder.Id, "private content")).EnsureSuccessStatusCode();
            (await CreateAsync(_folder.Id, [Hash("private content")], Hash("private content"))).EnsureSuccessStatusCode();
            HttpResponseMessage response = await _guest.PostAsJsonAsync(
                $"/api/v1/layouts/shared/{_token}/files/from-chunks?sessionId={Guid.NewGuid()}", new CreateFileFromChunksRequestDto
                {
                    NodeId = _folder.Id, Name = "forged.txt", ContentType = "text/plain",
                    Hash = Hash("private content"), ChunkHashes = [],
                });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task FailedUploadLookupsShareThePublicLinkFailureBudget(bool chunks)
        {
            (await UploadAsync(_folder.Id, "allowed")).EnsureSuccessStatusCode();
            (await CreateAsync(_folder.Id, [Hash("allowed")], Hash("allowed"))).EnsureSuccessStatusCode();
            string validToken = _token;
            for (int index = 0; index < 60; index++)
            {
                _token = $"s{index:D7}";
                using HttpResponseMessage failed = chunks
                    ? await UploadAsync(_folder.Id, "missing")
                    : await CreateAsync(_folder.Id, [], Hash(string.Empty));
                Assert.That(failed.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            }
            _token = validToken;
            using HttpResponseMessage blockedChunk = await UploadAsync(_folder.Id, "blocked");
            using HttpResponseMessage blockedFile = await CreateAsync(_folder.Id, [], Hash(string.Empty));
            using HttpResponseMessage blockedInfo = await _guest.GetAsync($"/api/v1/layouts/shared/{_token}");
            Assert.Multiple(() =>
            {
                Assert.That(blockedChunk.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
                Assert.That(blockedFile.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
                Assert.That(blockedInfo.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
                Assert.That(blockedChunk.Headers.RetryAfter, Is.Not.Null);
                Assert.That(blockedFile.Headers.RetryAfter, Is.Not.Null);
            });
        }

        [Test]
        public async Task GuestUploadCannotExceedFolderOwnersQuota()
        {
            (await _client!.PatchAsJsonAsync("/api/v1/server/settings/default-user-storage-quota-bytes", 5L)).EnsureSuccessStatusCode();
            (await UploadAsync(_folder.Id, "123456")).EnsureSuccessStatusCode();
            HttpResponseMessage response = await CreateAsync(_folder.Id, [Hash("123456")], Hash("123456"));
            Assert.That(response.StatusCode, Is.EqualTo((HttpStatusCode)507));
            NodeContentDto content = (await _client!.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{_folder.Id}/children"))!;
            Assert.That(content.Files, Is.Empty);
        }

        private async Task<string> ShareAsync(Guid nodeId, bool allowUpload)
        {
            HttpResponseMessage response = await _client!.GetAsync($"/api/v1/layouts/nodes/{nodeId}/share-link?allowUpload={allowUpload}");
            response.EnsureSuccessStatusCode();
            string link = (await response.Content.ReadAsStringAsync()).Trim('"');
            return link.Split('/').Last();
        }

        private async Task<HttpResponseMessage> UploadAsync(Guid nodeId, string content, string? hash = null)
        {
            using ByteArrayContent body = new(Encoding.UTF8.GetBytes(content));
            return await _guest.PostAsync($"/api/v1/layouts/shared/{_token}/chunks/raw?nodeId={nodeId}&sessionId={_session}&hash={hash ?? Hash(content)}", body);
        }

        private Task<HttpResponseMessage> CreateAsync(Guid nodeId, string[] chunks, string hash)
        {
            return _guest.PostAsJsonAsync($"/api/v1/layouts/shared/{_token}/files/from-chunks?sessionId={_session}", new CreateFileFromChunksRequestDto
            {
                NodeId = nodeId, Name = "uploaded.txt", ContentType = "text/plain", Hash = hash, ChunkHashes = [.. chunks],
            });
        }

        private static string Hash(string content) => Hasher.ToHexStringHash(Hasher.HashData(Encoding.UTF8.GetBytes(content)));
    }
}
