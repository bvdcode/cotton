// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Files;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.WebUtilities;

namespace Cotton.Server.IntegrationTests
{
    public partial class PreviewGenerationPipelineTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task PreviewQueue_EmptyFile_IsSkippedWithoutRecordingFailure(bool hasOldPreview)
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
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
            NodeFileManifestDto file = await GetNodeFileAsync(root.Id, "empty.apk");
            if (hasOldPreview)
            {
                await UpdateFileManifestAsync(file.Id, manifest =>
                {
                    manifest.SmallFilePreviewHash = Hasher.HashData("old preview"u8.ToArray());
                    manifest.SmallFilePreviewHashEncrypted = [1, 2, 3];
                    manifest.LargeFilePreviewHash = Hasher.HashData("old large preview"u8.ToArray());
                    manifest.PreviewGenerationError = "Previous attempt failed";
                    manifest.PreviewGeneratorId = "android-package";
                    manifest.PreviewGeneratorVersion = -1;
                });
            }

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Assert.That(await PreviewQueueLoader.LoadNextIdsAsync(dbContext, 100, new HashSet<Guid>(), CancellationToken.None), Is.Empty);
            Assert.That(await PreviewQueueLoader.LoadItemAsync(dbContext, file.FileManifestId, CancellationToken.None), Is.Null);

            await ExecuteGeneratePreviewJobAsync();

            PrepareUpgradeTo06Job hotfix = ActivatorUtilities.CreateInstance<PrepareUpgradeTo06Job>(scope.ServiceProvider);
            await hotfix.Execute(null!);
            dbContext.ChangeTracker.Clear();
            FileManifestPreviewState result = await GetFileManifestByNodeFileIdAsync(file.Id);
            FileManifest cleaned = await LoadFileManifestAsync(dbContext, file.Id);
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
                .RequireValid(dbContext, cleaned, "test.empty-preview-hotfix");
            DateTime? updatedAt = cleaned.UpdatedAt;
            IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            Assert.That(await mediator.Send(new ClearEmptyFilePreviewRequest(), CancellationToken.None), Is.Zero);
            Assert.That(cleaned.UpdatedAt, Is.EqualTo(updatedAt));
            NodeFileManifestDto listed = await GetNodeFileAsync(root.Id, "empty.apk");
            Assert.That(listed.PreviewHashEncryptedHex, Is.Null);
            string downloadLink = (await _client!.GetStringAsync($"/api/v1/files/{file.Id}/download-link")).Trim().Trim('"');
            string shareToken = QueryHelpers.ParseQuery(new Uri(_client.BaseAddress!, downloadLink).Query)["token"].ToString();
            using HttpResponseMessage sharedPreview = await _client.GetAsync($"/s/{shareToken}?view=inline&preview=true");
            Assert.That(sharedPreview.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task EmptyPreviewHotfix_NoEmptyManifest_PreservesOtherPreviews()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto file = await UploadAndCreateFileAsync(root.Id, "note.txt", "text/plain", "Preserved preview"u8.ToArray());
            await ExecuteGeneratePreviewJobAsync();
            FileManifestPreviewState before = await GetFileManifestByNodeFileIdAsync(file.Id);
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            Assert.That(await mediator.Send(new ClearEmptyFilePreviewRequest(), CancellationToken.None), Is.Zero);

            FileManifestPreviewState after = await GetFileManifestByNodeFileIdAsync(file.Id);
            Assert.Multiple(() =>
            {
                Assert.That(after.SmallFilePreviewHash, Is.EqualTo(before.SmallFilePreviewHash));
                Assert.That(after.SmallFilePreviewHashEncrypted, Is.EqualTo(before.SmallFilePreviewHashEncrypted));
                Assert.That(after.PreviewGenerationError, Is.EqualTo(before.PreviewGenerationError));
            });
        }
    }
}
