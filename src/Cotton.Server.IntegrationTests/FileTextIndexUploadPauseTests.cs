// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.TextExtraction;

namespace Cotton.Server.IntegrationTests
{
    public class FileTextIndexUploadPauseTests : FileTextIndexingTestBase
    {
        [Test]
        public async Task Job_AfterChunkUpload_WaitsForPauseAndContinues()
        {
            _services.GetRequiredService<PerfTracker>().OnChunkCreated();

            Task execution = _services.GetRequiredService<GenerateFileEmbeddingsJob>().Execute(null!);

            Assert.That(execution.IsCompleted, Is.False);
            await execution.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.That(_services.GetRequiredService<PerfTracker>().IsUploading(), Is.False);
        }

        [Test]
        public async Task Job_WithReadyIndex_WaitsThenIndexesUploadedPdf()
        {
            await PrepareVectorIndexAsync();
            FileManifest file = await AddFileAsync("uploaded.pdf", PdfTextExtractor.ContentType,
                PdfTestDocument.Create("Uploaded PDF content"));
            Guid manifestId = file.Id;
            _db.ChangeTracker.Clear();
            _services.GetRequiredService<PerfTracker>().OnChunkCreated();

            Task execution = _services.GetRequiredService<GenerateFileEmbeddingsJob>().Execute(null!);

            Assert.That(execution.IsCompleted, Is.False);
            await execution.WaitAsync(TimeSpan.FromSeconds(20));
            _db.ChangeTracker.Clear();
            Assert.That(await _db.FileEmbeddings.AnyAsync(vector => vector.FileManifestId == manifestId), Is.True);
        }

        [Test]
        public async Task Job_IndexingDisabledDuringUpload_StopsWithoutDatabaseOrWorkerAccess()
        {
            _services.GetRequiredService<PerfTracker>().OnChunkCreated();
            string? connectionString = _db.Database.GetConnectionString();
            _db.Database.SetConnectionString("Host=localhost;Port=1;Database=unused;Username=unused;Timeout=1");
            try
            {
                Task execution = _services.GetRequiredService<GenerateFileEmbeddingsJob>().Execute(null!);
                Assert.That(execution.IsCompleted, Is.False);
                _settingsCache.InvalidateSettings(serverIsInitialized: true);
                _settingsCache.GetOrAdd(() => ServerSettingsSnapshot.FromEntity(new CottonServerSettings
                {
                    AllowGlobalIndexing = false,
                }));

                await execution.WaitAsync(TimeSpan.FromSeconds(10));

                Assert.That(_worker.Addresses, Is.Empty);
                Assert.That(_services.GetRequiredService<PerfTracker>().IsUploading(), Is.True);
            }
            finally
            {
                _db.Database.SetConnectionString(connectionString);
            }
        }
    }
}
