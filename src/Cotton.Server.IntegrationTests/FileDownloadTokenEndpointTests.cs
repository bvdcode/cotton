// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Files;
using Cotton.Server.IntegrationTests.Helpers;
using EasyExtensions.Mediator;
using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class FileDownloadTokenEndpointTests : FileEndpointTestBase
    {
        private NodeDto _root = null!;
        private NodeFileManifestDto _file = null!;

        [SetUp]
        public async Task PrepareFile()
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            _root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            _file = await UploadTextFileAsync(_root, "download.txt", "0123456789");
        }

        [Test]
        public async Task DownloadToken_SupportsAnonymousHeadAndParallelRanges_WithoutDatabaseTokens()
        {
            string link = await CreateTokenAsync();
            Assert.That(await DbContext.DownloadTokens.CountAsync(), Is.Zero);
            _client!.DefaultRequestHeaders.Authorization = null;
            using HttpRequestMessage head = new(HttpMethod.Head, link);
            using HttpResponseMessage probe = await _client.SendAsync(head);
            Assert.Multiple(() =>
            {
                Assert.That(probe.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(probe.Content.Headers.ContentLength, Is.EqualTo(10));
                Assert.That(probe.Headers.AcceptRanges, Does.Contain("bytes"));
            });

            string[] parts = await Task.WhenAll(ReadRangeAsync(link, 0, 3), ReadRangeAsync(link, 4, 9));
            Assert.That(string.Concat(parts), Is.EqualTo("0123456789"));
            Assert.That(await _client.GetStringAsync(link), Is.EqualTo("0123456789"));
            Assert.That(await DbContext.DownloadTokens.CountAsync(), Is.Zero);
        }

        [Test]
        public async Task Token_FromIndependentIssuer_WorksWithoutStoredState_AndEnforcesOwnership()
        {
            Guid ownerId = await DbContext.NodeFiles.Where(file => file.Id == _file.Id).Select(file => file.OwnerId).SingleAsync();
            using FileDownloadTokenService issuer = new(_factory!.Services.GetRequiredService<CottonEncryptionSettings>(),
                TimeProvider.System, NullLogger<FileDownloadTokenService>.Instance);
            string token = issuer.Create(ownerId, _file.Id, TimeSpan.FromHours(1));
            string wrongOwner = issuer.Create(Guid.NewGuid(), _file.Id, TimeSpan.FromHours(1));
            _client!.DefaultRequestHeaders.Authorization = null;
            Assert.That(await _client.GetStringAsync(FileDownloadToken.BuildUrl(_file.Id, token)), Is.EqualTo("0123456789"));
            using HttpResponseMessage denied = await _client.GetAsync(FileDownloadToken.BuildUrl(_file.Id, wrongOwner));
            Assert.That(denied.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(await DbContext.DownloadTokens.CountAsync(), Is.Zero);
        }

        [Test]
        public async Task Credentials_CannotBeSubstitutedAcrossFilesOrLinkKinds()
        {
            string link = await CreateTokenAsync();
            NodeFileManifestDto other = await UploadTextFileAsync(_root, "other.txt", "private");
            string share = await _client!.GetStringAsync($"/api/v1/files/{_file.Id}/download-link?customToken=public-file");
            share = share.Trim('"');
            _client.DefaultRequestHeaders.Authorization = null;

            string[] invalidLinks =
            [
                link.Replace(_file.Id.ToString(), other.Id.ToString(), StringComparison.Ordinal),
                link.Replace("?downloadToken=", "?token=", StringComparison.Ordinal),
                share.Replace("?token=", "?downloadToken=", StringComparison.Ordinal),
                link + "&token=public-file",
                link + "invalid",
            ];
            foreach (string invalid in invalidLinks)
            {
                using HttpResponseMessage result = await _client.GetAsync(invalid);
                Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), invalid);
            }
            Assert.That(await _client.GetStringAsync(share), Is.EqualTo("0123456789"));
            Assert.That(await DbContext.DownloadTokens.CountAsync(), Is.EqualTo(1));
        }

        [Test]
        public async Task TokenCreation_RequiresAuthenticationOwnershipAndPositiveExpiry()
        {
            foreach (string minutes in new[] { "0", "-1", "5256001", "" })
            {
                using HttpResponseMessage invalid = await _client!.PostAsync(
                    $"/api/v1/files/{_file.Id}/download-token?expireAfterMinutes={minutes}", null);
                Assert.That(invalid.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            }
            _client!.DefaultRequestHeaders.Authorization = null;
            using HttpResponseMessage anonymous = await _client.PostAsync($"/api/v1/files/{_file.Id}/download-token", null);
            Assert.That(anonymous.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            string? forbidden = await mediator.Send(new CreateFileDownloadTokenRequest(Guid.NewGuid(), _file.Id, 60));
            Assert.That(forbidden, Is.Null);
            Assert.That(await DbContext.DownloadTokens.CountAsync(), Is.Zero);
        }

        [Test]
        public async Task ExpiredToken_AndTrashedFile_AreUnavailable()
        {
            using FileDownloadTokenService issuer = new(_factory!.Services.GetRequiredService<CottonEncryptionSettings>(),
                new AdjustableTimeProvider(DateTimeOffset.UtcNow.AddMinutes(-2)), NullLogger<FileDownloadTokenService>.Instance);
            Guid ownerId = await DbContext.NodeFiles.Where(file => file.Id == _file.Id).Select(file => file.OwnerId).SingleAsync();
            string expired = issuer.Create(ownerId, _file.Id, TimeSpan.FromMinutes(1));
            using HttpResponseMessage expiredResponse = await _client!.GetAsync(FileDownloadToken.BuildUrl(_file.Id, expired));
            Assert.That(expiredResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            string link = await CreateTokenAsync();
            using HttpResponseMessage deleted = await _client.DeleteAsync($"/api/v1/files/{_file.Id}");
            deleted.EnsureSuccessStatusCode();
            using HttpResponseMessage unavailable = await _client.GetAsync(link);
            Assert.That(unavailable.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task HlsManifest_PropagatesTemporaryCredentials_WithoutConsumingToken()
        {
            byte[] content = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "short-video.mkv"));
            string hash = Hasher.ToHexStringHash(Hasher.HashData(content));
            using HttpResponseMessage chunk = await UploadRawChunkAsync(content, hash);
            chunk.EnsureSuccessStatusCode();
            using HttpResponseMessage created = await _client!.PostAsJsonAsync("/api/v1/files/from-chunks", new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [hash], Name = "video.mkv", ContentType = "video/x-matroska", Hash = hash, NodeId = _root.Id,
            });
            created.EnsureSuccessStatusCode();
            NodeContentDto listing = (await _client!.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{_root.Id}/children"))!;
            NodeFileManifestDto video = listing.Files.Single(file => file.Name == "video.mkv");
            byte[] preview = CreateWebpSignatureBytes("video preview");
            await StoreSmallPreviewAsync(video.Id, Hasher.HashData(preview), preview);
            using HttpResponseMessage issued = await _client!.PostAsync($"/api/v1/files/{video.Id}/download-token", null);
            issued.EnsureSuccessStatusCode();
            string link = (await issued.Content.ReadAsStringAsync()).Trim('"');
            string manifestUrl = link.Replace("/download?", "/hls/master.m3u8?", StringComparison.Ordinal);
            _client.DefaultRequestHeaders.Authorization = null;
            string manifest = await _client.GetStringAsync(manifestUrl);
            Assert.Multiple(() =>
            {
                Assert.That(manifest, Does.Contain("playlist.m3u8?downloadToken="));
                Assert.That(manifest, Does.Not.Contain("?token="));
            });
            string playlistUrl = manifest.Split('\n').First(line => line.StartsWith("/api/", StringComparison.Ordinal));
            string playlist = await _client.GetStringAsync(playlistUrl);
            string segmentUrl = playlist.Split('\n').First(line => line.StartsWith("/api/", StringComparison.Ordinal));
            Assert.That(segmentUrl, Does.Contain("?downloadToken="));
            using HttpResponseMessage segment = await _client.GetAsync(segmentUrl);
            segment.EnsureSuccessStatusCode();
            Assert.That(segment.Content.Headers.ContentType!.MediaType, Is.EqualTo("video/mp2t"));
            Assert.That(await segment.Content.ReadAsByteArrayAsync(), Is.Not.Empty);
            Assert.That(await _client.GetByteArrayAsync(link), Is.EqualTo(content));
            Assert.That(await DbContext.DownloadTokens.CountAsync(), Is.Zero);
        }

        [Test]
        public async Task Token_ServesLargePreviewWithoutConsumingAccessToOriginal()
        {
            byte[] preview = CreateWebpSignatureBytes("large preview");
            byte[] hash = Hasher.HashData(preview);
            await StorePreviewBytesAsync(hash, preview);
            await using (AsyncServiceScope scope = _factory!.Services.CreateAsyncScope())
            {
                CottonDbContext database = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                FileManifest manifest = await database.NodeFiles.Where(file => file.Id == _file.Id)
                    .Select(file => file.FileManifest).SingleAsync();
                manifest.LargeFilePreviewHash = hash;
                await database.SaveChangesAsync();
            }
            string link = await CreateTokenAsync();
            _client!.DefaultRequestHeaders.Authorization = null;
            using HttpResponseMessage response = await _client.GetAsync(link + "&preview=true&download=false");
            response.EnsureSuccessStatusCode();
            Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("image/webp"));
            Assert.That(await response.Content.ReadAsByteArrayAsync(), Is.EqualTo(preview));
            Assert.That(await _client.GetStringAsync(link), Is.EqualTo("0123456789"));
            Assert.That(await DbContext.DownloadTokens.CountAsync(), Is.Zero);
        }

        [Test]
        public async Task HistoricalVersion_UsesSelfContainedToken()
        {
            await UpdateTextFileAsync(_file, _root, "new content");
            List<Cotton.Files.FileVersionDto> versions = await GetVersionsAsync(_file.Id);
            Cotton.Files.FileVersionDto historical = versions.First(version => !version.IsCurrent);
            string link = await _client!.GetStringAsync($"/api/v1/files/{_file.Id}/versions/{historical.Id}/download-link");
            link = link.Trim('"');
            _client.DefaultRequestHeaders.Authorization = null;
            Assert.That(link, Does.Contain("?downloadToken="));
            Assert.That(await _client.GetStringAsync(link), Is.EqualTo("0123456789"));
            Assert.That(await DbContext.DownloadTokens.CountAsync(), Is.Zero);
        }

        private async Task<string> CreateTokenAsync()
        {
            using HttpResponseMessage response = await _client!.PostAsync($"/api/v1/files/{_file.Id}/download-token", null);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadAsStringAsync()).Trim('"');
        }

        private async Task<string> ReadRangeAsync(string url, long from, long to)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.Range = new RangeHeaderValue(from, to);
            using HttpResponseMessage response = await _client!.SendAsync(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.PartialContent));
            return await response.Content.ReadAsStringAsync();
        }
    }
}
