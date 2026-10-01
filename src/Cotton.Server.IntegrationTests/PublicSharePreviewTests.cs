// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using static Cotton.Server.IntegrationTests.Helpers.PreviewFixtures;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class PublicSharePreviewTests : PreviewTestBase
    {
        private const string ShareToken = "photo-preview-test";

        [Test]
        public async Task SharePreview_ServesLargeImageAndPreservesOriginalDownload()
        {
            (NodeFileManifestDto file, byte[] original) = await CreatePhotoShareAsync();
            FileManifestPreviewState manifest = await Pipeline.GetFileManifestByNodeFileIdAsync(file.Id);
            byte[] expected = await Pipeline.ReadPreviewBlobAsync(manifest.LargeFilePreviewHash!);
            using HttpResponseMessage preview = await _client!.GetAsync($"/s/{ShareToken}?view=inline&preview=true");
            preview.EnsureSuccessStatusCode();
            Assert.Multiple(() =>
            {
                Assert.That(preview.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/webp"));
                Assert.That(preview.Headers.ETag?.Tag, Is.EqualTo($"\"sha256-{Convert.ToHexStringLower(manifest.LargeFilePreviewHash!)}\""));
            });
            Assert.That(await preview.Content.ReadAsByteArrayAsync(), Is.EqualTo(expected));
            Assert.That(await DbContext.DownloadTokens.AnyAsync(x => x.Token == ShareToken), Is.True);

            using HttpResponseMessage download = await _client.GetAsync($"/s/{ShareToken}?view=download");
            download.EnsureSuccessStatusCode();
            Assert.That(download.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/x-canon-cr3"));
            Assert.That(await download.Content.ReadAsByteArrayAsync(), Is.EqualTo(original));
        }

        [Test]
        public async Task SocialPreview_ServesJpegFromLargePreviewWithoutConsumingOneTimeLink()
        {
            await CreatePhotoShareAsync();
            using HttpResponseMessage page = await _client!.GetAsync($"/s/{ShareToken}");
            page.EnsureSuccessStatusCode();
            string html = await page.Content.ReadAsStringAsync();
            string imageUrl = $"/s/{ShareToken}/preview.jpg";
            Assert.That(html, Does.Contain(imageUrl));
            Assert.That(html, Does.Contain("property=\"og:image:type\" content=\"image/jpeg\""));

            using HttpResponseMessage jpeg = await _client.GetAsync(imageUrl);
            jpeg.EnsureSuccessStatusCode();
            byte[] bytes = await jpeg.Content.ReadAsByteArrayAsync();
            ImageInfo info = Image.Identify(bytes);
            Assert.Multiple(() =>
            {
                Assert.That(jpeg.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/jpeg"));
                Assert.That(bytes.Take(3), Is.EqualTo(new byte[] { 0xff, 0xd8, 0xff }));
                Assert.That(info.Width, Is.EqualTo(SocialPreviewImage.MaxSize));
                Assert.That(info.Height, Is.EqualTo(675));
                Assert.That(info.Metadata.ExifProfile, Is.Null);
            });
            Assert.That(await DbContext.DownloadTokens.AnyAsync(x => x.Token == ShareToken), Is.True);

            using HttpRequestMessage headRequest = new(HttpMethod.Head, imageUrl);
            using HttpResponseMessage head = await _client.SendAsync(headRequest);
            head.EnsureSuccessStatusCode();
            Assert.That(head.Content.Headers.ContentLength, Is.EqualTo(bytes.Length));
            Assert.That(await head.Content.ReadAsByteArrayAsync(), Is.Empty);
            Assert.That(await DbContext.DownloadTokens.AnyAsync(x => x.Token == ShareToken), Is.True);

            using HttpRequestMessage conditional = new(HttpMethod.Get, imageUrl);
            conditional.Headers.IfNoneMatch.Add(jpeg.Headers.ETag!);
            using HttpResponseMessage unchanged = await _client.SendAsync(conditional);
            Assert.That(unchanged.StatusCode, Is.EqualTo(HttpStatusCode.NotModified));
        }

        [Test]
        public async Task SocialPreview_ExpiredTokenCannotReadImage()
        {
            await CreatePhotoShareAsync();
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext context = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            DownloadToken token = await context.DownloadTokens.SingleAsync(x => x.Token == ShareToken);
            token.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await context.SaveChangesAsync();

            using HttpResponseMessage response = await _client!.GetAsync($"/s/{ShareToken}/preview.jpg");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task SharePreview_UsesAvailableSmallImageWhenNoLargeImageExists()
        {
            (NodeFileManifestDto file, byte[] _) = await CreatePhotoShareAsync();
            await Pipeline.UpdateFileManifestAsync(file.Id, manifest => manifest.LargeFilePreviewHash = null);
            FileManifestPreviewState state = await Pipeline.GetFileManifestByNodeFileIdAsync(file.Id);
            byte[] small = await Pipeline.ReadPreviewBlobAsync(state.SmallFilePreviewHash!);
            using HttpResponseMessage preview = await _client!.GetAsync($"/s/{ShareToken}?view=inline&preview=true");
            preview.EnsureSuccessStatusCode();
            Assert.That(await preview.Content.ReadAsByteArrayAsync(), Is.EqualTo(small));

            using HttpResponseMessage jpeg = await _client.GetAsync($"/s/{ShareToken}/preview.jpg");
            jpeg.EnsureSuccessStatusCode();
            ImageInfo info = Image.Identify(await jpeg.Content.ReadAsByteArrayAsync());
            Assert.That(Math.Max(info.Width, info.Height), Is.EqualTo(PreviewGeneratorProvider.DefaultSmallPreviewSize));
        }

        private async Task<(NodeFileManifestDto File, byte[] Original)> CreatePhotoShareAsync()
        {
            Pipeline.SetBearer(await Pipeline.LoginAsync());
            NodeDto root = await Pipeline.GetRootNodeAsync();
            byte[] original = CreateGradientPngBytes(1600, 900);
            NodeFileManifestDto file = await Pipeline.UploadAndCreateFileAsync(root.Id, "photo.png", "image/png", original);
            await Pipeline.ExecuteGeneratePreviewJobAsync();
            await Pipeline.RenamePreviewFileAsync(file.Id, "photo.CR3");
            using HttpResponseMessage link = await _client!.GetAsync(
                $"/api/v1/files/{file.Id}/download-link?customToken={ShareToken}&deleteAfterUse=true");
            link.EnsureSuccessStatusCode();
            _client.DefaultRequestHeaders.Authorization = null;
            return (file, original);
        }
    }
}
