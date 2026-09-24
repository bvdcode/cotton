// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Services.Search;
using EasyExtensions.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    public partial class FileTextIndexingTests
    {
        [Test]
        public async Task Batch_CombinesSmallFilesAndStoresVectorsUnderTheirOwnManifest()
        {
            string[] texts = ["one", "", "four", "three", "xx"];
            List<Guid> ids = [];
            for (int index = 0; index < texts.Length; index++)
            {
                FileManifest file = await AddFileAsync($"file-{index}.txt", "text/plain", Encoding.UTF8.GetBytes(texts[index]));
                ids.Add(file.Id);
            }
            _db.ChangeTracker.Clear();
            await _services.GetRequiredService<IMediator>()
                .Send(new IndexFileTextRequest(ids.Concat([ids[0], Guid.NewGuid()])), CancellationToken.None);
            _db.ChangeTracker.Clear();

            List<FileEmbedding> stored = await _db.FileEmbeddings.ToListAsync();
            Assert.That(_worker.Batches.Select(batch => batch.Length), Is.EqualTo(new[] { 2, 2 }));
            for (int index = 0; index < ids.Count; index++)
            {
                FileEmbedding[] vectors = stored.Where(vector => vector.FileManifestId == ids[index]).ToArray();
                if (texts[index].Length == 0)
                {
                    Assert.That(vectors, Is.Empty);
                    continue;
                }
                Assert.That(vectors, Has.Length.EqualTo(1));
                Assert.That(vectors[0].Embedding[0], Is.EqualTo(texts[index].Length));
                Assert.That(vectors[0].FragmentIndex, Is.Zero);
            }
            Assert.That(await _db.FileManifests.AllAsync(file => file.TextIndexVersion == VectorIndexDefinition.Version), Is.True);
        }

        [Test]
        public async Task Batch_InferenceFailureInLaterFile_LeavesEveryFilePending()
        {
            List<Guid> ids = [];
            for (int index = 0; index < 3; index++)
            {
                FileManifest file = await AddFileAsync($"file-{index}.txt", "text/plain", Encoding.UTF8.GetBytes($"File {index}"));
                ids.Add(file.Id);
            }
            _db.ChangeTracker.Clear();
            _worker.FailingEmbeddingCall = 2;
            Assert.ThrowsAsync<HttpRequestException>(() => _services.GetRequiredService<IMediator>()
                .Send(new IndexFileTextRequest(ids), CancellationToken.None));
            _db.ChangeTracker.Clear();
            Assert.That(await _db.FileEmbeddings.AnyAsync(), Is.False);
            Assert.That(await _db.FileManifests.AllAsync(file => file.TextIndexVersion == 0), Is.True);
        }

        [Test]
        public async Task Batch_SaveFailure_RollsBackEveryFileAndAllowsRetry()
        {
            FileManifest first = await AddFileAsync("first.txt", "text/plain", "First"u8.ToArray());
            FileManifest second = await AddFileAsync("second.txt", "text/plain", "Second"u8.ToArray());
            Guid[] ids = [first.Id, second.Id];
            _db.ChangeTracker.Clear();
            _failure.Enabled = true;
            Assert.ThrowsAsync<DbUpdateException>(() => _services.GetRequiredService<IMediator>()
                .Send(new IndexFileTextRequest(ids), CancellationToken.None));
            _failure.Enabled = false;
            _db.ChangeTracker.Clear();
            Assert.That(await _db.FileEmbeddings.AnyAsync(), Is.False);
            Assert.That(await _db.FileManifests.AllAsync(file => file.TextIndexVersion == 0), Is.True);
            _db.ChangeTracker.Clear();
            await _services.GetRequiredService<IMediator>().Send(new IndexFileTextRequest(ids), CancellationToken.None);
            Assert.That(await _db.FileEmbeddings.CountAsync(), Is.EqualTo(2));
            Assert.That(await _db.FileManifests.AllAsync(file => file.TextIndexVersion == VectorIndexDefinition.Version), Is.True);
        }

        [Test]
        public async Task Batch_StoppedFileEnumeration_FlushesOnlyAlreadyReadFiles()
        {
            FileManifest first = await AddFileAsync("first.txt", "text/plain", "First"u8.ToArray());
            FileManifest second = await AddFileAsync("second.txt", "text/plain", "Second"u8.ToArray());
            Guid[] ids = [first.Id, second.Id];
            _db.ChangeTracker.Clear();
            IEnumerable<Guid> available = ids.TakeWhile(_ => _worker.TokenizeCalls == 0);
            await _services.GetRequiredService<IMediator>().Send(new IndexFileTextRequest(available), CancellationToken.None);
            _db.ChangeTracker.Clear();
            Assert.That(await _db.FileEmbeddings.CountAsync(), Is.EqualTo(1));
            Assert.That((await _db.FileEmbeddings.SingleAsync()).FileManifestId, Is.EqualTo(first.Id));
            Assert.That((await _db.FileManifests.SingleAsync(file => file.Id == second.Id)).TextIndexVersion, Is.Zero);
        }
    }
}
