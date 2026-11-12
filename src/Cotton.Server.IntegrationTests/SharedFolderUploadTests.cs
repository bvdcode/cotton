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

        protected override void ConfigureTestServices(IServiceCollection services)
        {
            services.AddSingleton<SharedUploadNotificationRecorder>();
            services.AddSingleton<EasyExtensions.Mediator.INotificationHandler<Cotton.Server.Notifications.FileUploadedNotification>>(
                provider => provider.GetRequiredService<SharedUploadNotificationRecorder>());
        }

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

        [Test]
        public async Task HiddenFolderAcceptsUploadsButDoesNotExposeContentOrDescendants()
        {
            NodeDto child = await CreateNodeAsync(_folder.Id, "private child");
            _token = await ShareAsync(_folder.Id, true, true);
            SharedNodeInfoDto info = (await _guest.GetFromJsonAsync<SharedNodeInfoDto>($"/api/v1/layouts/shared/{_token}"))!;
            Assert.That(info.HideFolderContent, Is.True);
            Assert.That(info.Name, Is.Empty);
            string html = await _guest.GetStringAsync($"/s/{_token}?view=page");
            Assert.That(html, Does.Not.Contain(_folder.Name));
            Assert.That(html, Does.Contain("<title>Cotton Cloud</title>"));
            (await UploadAsync(_folder.Id, "private upload")).EnsureSuccessStatusCode();
            using HttpResponseMessage created = await CreateAsync(_folder.Id, [Hash("private upload")], Hash("private upload"));
            created.EnsureSuccessStatusCode();
            NodeFileManifestDto file = (await created.Content.ReadFromJsonAsync<NodeFileManifestDto>())!;
            string prefix = $"/api/v1/layouts/shared/{_token}";
            foreach (string path in new[] { "/children", $"/children?nodeId={child.Id}", $"/ancestors/{child.Id}",
                $"/files/{file.Id}/content", $"/files/{file.Id}/content?preview=true" })
            {
                using HttpResponseMessage denied = await _guest.GetAsync(prefix + path);
                Assert.That(denied.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), path);
            }
            using HttpResponseMessage archive = await _guest.PostAsync(prefix + "/archives/download-link", null);
            Assert.That(archive.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That((await UploadAsync(child.Id, "not allowed")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            NodeContentDto content = (await _client!.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{_folder.Id}/children"))!;
            Assert.That(content.Files.Single().Id, Is.EqualTo(file.Id));
        }

        [Test]
        public async Task HiddenUploadsKeepBothNamesIncludingFolderAndConcurrentConflicts()
        {
            await CreateNodeAsync(_folder.Id, "uploaded.txt");
            _token = await ShareAsync(_folder.Id, true, true);
            HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, 3)
                .Select(_ => CreateAsync(_folder.Id, [], Hash(string.Empty))));
            foreach (HttpResponseMessage response in responses)
            {
                response.EnsureSuccessStatusCode();
                response.Dispose();
            }
            NodeContentDto content = (await _client!.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{_folder.Id}/children"))!;
            Assert.That(content.Files.Select(file => file.Name), Is.EquivalentTo(new[] { "uploaded (1).txt", "uploaded (2).txt", "uploaded (3).txt" }));
            Assert.That(content.Nodes.Single().Name, Is.EqualTo("uploaded.txt"));
        }

        [Test]
        public async Task ContentCannotBeHiddenWithoutUploadPermission()
        {
            using HttpResponseMessage response = await _client!.GetAsync(
                $"/api/v1/layouts/nodes/{_folder.Id}/share-link?hideFolderContent=true");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task HiddenUploadsPreserveUnicodeAndExtensionsWhenSuffixExceedsNameLimit()
        {
            _token = await ShareAsync(_folder.Id, true, true);
            string name = string.Concat(Enumerable.Repeat("😀", 62)) + ".txt";
            (await CreateAsync(_folder.Id, [], Hash(string.Empty), name)).EnsureSuccessStatusCode();
            using HttpResponseMessage renamed = await CreateAsync(_folder.Id, [], Hash(string.Empty), name);
            renamed.EnsureSuccessStatusCode();
            NodeFileManifestDto file = (await renamed.Content.ReadFromJsonAsync<NodeFileManifestDto>())!;
            Assert.Multiple(() =>
            {
                Assert.That(file.Name, Does.EndWith(" (1).txt"));
                Assert.That(Encoding.UTF8.GetByteCount(file.Name), Is.LessThanOrEqualTo(Cotton.Validators.NameValidator.MaxSegmentBytes));
                Assert.That(Cotton.Validators.NameValidator.IsValidName(file.Name, out _), Is.True);
            });
        }

        [Test]
        public async Task UploadEventIsPublishedAfterCommitAndNotForRejectedUploads()
        {
            SharedUploadNotificationRecorder recorder = _factory!.Services.GetRequiredService<SharedUploadNotificationRecorder>();
            using HttpResponseMessage response = await CreateAsync(_folder.Id, [], Hash(string.Empty));
            response.EnsureSuccessStatusCode();
            NodeFileManifestDto file = (await response.Content.ReadFromJsonAsync<NodeFileManifestDto>())!;
            Assert.That(recorder.Uploads.Single().NodeFileId, Is.EqualTo(file.Id));
            Assert.That(recorder.Uploads.Single().ShareId, Is.Not.Null);
            Assert.That((await CreateAsync(_folder.Id, [], Hash(string.Empty))).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(recorder.Uploads, Has.Count.EqualTo(1));
        }

        private async Task<string> ShareAsync(Guid nodeId, bool allowUpload, bool hideFolderContent = false)
        {
            HttpResponseMessage response = await _client!.GetAsync($"/api/v1/layouts/nodes/{nodeId}/share-link?allowUpload={allowUpload}&hideFolderContent={hideFolderContent}");
            response.EnsureSuccessStatusCode();
            string link = (await response.Content.ReadAsStringAsync()).Trim('"');
            return link.Split('/').Last();
        }

        private async Task<HttpResponseMessage> UploadAsync(Guid nodeId, string content, string? hash = null)
        {
            using ByteArrayContent body = new(Encoding.UTF8.GetBytes(content));
            return await _guest.PostAsync($"/api/v1/layouts/shared/{_token}/chunks/raw?nodeId={nodeId}&sessionId={_session}&hash={hash ?? Hash(content)}", body);
        }

        private Task<HttpResponseMessage> CreateAsync(Guid nodeId, string[] chunks, string hash, string name = "uploaded.txt")
        {
            return _guest.PostAsJsonAsync($"/api/v1/layouts/shared/{_token}/files/from-chunks?sessionId={_session}", new CreateFileFromChunksRequestDto
            {
                NodeId = nodeId, Name = name, ContentType = "text/plain", Hash = hash, ChunkHashes = [.. chunks],
            });
        }

        private static string Hash(string content) => Hasher.ToHexStringHash(Hasher.HashData(Encoding.UTF8.GetBytes(content)));
    }
}
