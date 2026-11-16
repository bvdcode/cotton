// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.WebUtilities;

namespace Cotton.Server.IntegrationTests
{
    public class PreviewEmptyFilesTests : PreviewTestBase
    {
        [Test]
        public async Task PreviewQueue_EmptyFile_IsSkippedWithoutRecordingFailure()
        {
            Pipeline.SetBearer(await Pipeline.LoginAsync());
            NodeDto root = await Pipeline.GetRootNodeAsync();
            using HttpResponseMessage response = await _client!.PostAsJsonAsync("/api/v1/files/from-chunks",
                new CreateFileFromChunksRequestDto
                {
                    NodeId = root.Id,
                    Name = "empty.apk",
                    ContentType = "application/vnd.android.package-archive",
                    Hash = Hasher.ZeroHashHexString,
                    ChunkHashes = [],
                });
            response.EnsureSuccessStatusCode();
            NodeFileManifestDto file = await Pipeline.GetNodeFileAsync(root.Id, "empty.apk");
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Assert.That(await PreviewQueueLoader.LoadNextIdsAsync(dbContext, 100, new HashSet<Guid>(), CancellationToken.None), Is.Empty);
            Assert.That(await PreviewQueueLoader.LoadItemAsync(dbContext, file.FileManifestId, CancellationToken.None), Is.Null);

            await Pipeline.ExecuteGeneratePreviewJobAsync();

            dbContext.ChangeTracker.Clear();
            FileManifestPreviewState result = await Pipeline.GetFileManifestByNodeFileIdAsync(file.Id);
            FileManifest cleaned = await PreviewTestPipeline.LoadFileManifestAsync(dbContext, file.Id);
            Assert.Multiple(() =>
            {
                Assert.That(result.PreviewGenerationError, Is.Null);
                Assert.That(result.SmallFilePreviewHash, Is.Null);
                Assert.That(result.SmallFilePreviewHashEncrypted, Is.Null);
                Assert.That(result.LargeFilePreviewHash, Is.Null);
                Assert.That(cleaned.PreviewGeneratorId, Is.Null);
                Assert.That(cleaned.PreviewGeneratorVersion, Is.EqualTo(PreviewGeneratorProvider.DefaultGeneratorVersion));
            });
            scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityVerifier>()
                .RequireValid(dbContext, cleaned, "test.empty-preview");
            NodeFileManifestDto listed = await Pipeline.GetNodeFileAsync(root.Id, "empty.apk");
            Assert.That(listed.PreviewHashEncryptedHex, Is.Null);
            string downloadLink = (await _client!.GetStringAsync($"/api/v1/files/{file.Id}/download-link")).Trim().Trim('"');
            string shareToken = QueryHelpers.ParseQuery(new Uri(_client.BaseAddress!, downloadLink).Query)["token"].ToString();
            using HttpResponseMessage sharedPreview = await _client.GetAsync($"/s/{shareToken}?view=inline&preview=true");
            Assert.That(sharedPreview.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }
    }
}
