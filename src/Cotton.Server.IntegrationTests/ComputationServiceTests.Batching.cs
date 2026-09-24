// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public partial class ComputationServiceTests
    {
        [Test]
        public async Task Documents_UseFullAdvertisedBatchesWithoutWaitingForMoreFiles()
        {
            string[] texts = Enumerable.Range(0, 65).Select(index => $"Document {index}").ToArray();
            float[][][] results = await _service.GetTextEmbeddingFragmentsAsync(texts.ToAsyncEnumerable());
            Assert.That(_handler.Batches.Select(batch => batch.Length), Is.EqualTo(new[] { 32, 32, 1 }));
            Assert.That(results, Has.Length.EqualTo(65));
            Assert.That(_handler.InfoCalls, Is.EqualTo(1));
        }

        [Test]
        public async Task Documents_FillBatchesAcrossFileBoundariesAndKeepEmptyResults()
        {
            _handler.MaxBatchInputs = 3;
            string[] texts = ["one", "", "four", " ", "three", "xx"];
            float[][][] results = await _service.GetTextEmbeddingFragmentsAsync(texts.ToAsyncEnumerable());
            Assert.Multiple(() =>
            {
                Assert.That(_handler.Batches.Select(batch => batch.Length), Is.EqualTo(new[] { 3, 1 }));
                Assert.That(results.Select(document => document.Length), Is.EqualTo(new[] { 1, 0, 1, 0, 1, 1 }));
                Assert.That(results.Where(document => document.Length > 0).Select(document => document[0][0]),
                    Is.EqualTo(new[] { 3f, 4f, 5f, 2f }));
                Assert.That(_handler.InfoCalls, Is.EqualTo(1));
            });
        }

        [TestCase(32, 16)]
        [TestCase(2, 100)]
        public async Task Documents_RespectTokenAndInputLimitsAcrossLongFiles(int inputLimit, int tokenLimit)
        {
            _handler.MaxInputTokens = 8;
            _handler.MaxBatchInputs = inputLimit;
            _handler.MaxBatchTokens = tokenLimit;
            string[] texts = ["abcdefghi", "первый 😀", "second longer document"];
            float[][][] results = await _service.GetTextEmbeddingFragmentsAsync(texts.ToAsyncEnumerable());
            Assert.That(results.Length, Is.EqualTo(texts.Length));
            string[] fragments = _handler.Batches.SelectMany(batch => batch).ToArray();
            int offset = 0;
            for (int index = 0; index < texts.Length; index++)
            {
                string[] document = fragments.Skip(offset).Take(results[index].Length).ToArray();
                Assert.That(string.Concat(document), Is.EqualTo(texts[index]));
                Assert.That(results[index].Select(vector => vector[0]), Is.EqualTo(document.Select(text => (float)text.Length)));
                offset += results[index].Length;
            }
            Assert.That(_handler.Batches.All(batch => batch.Length <= inputLimit
                && batch.Max(text => text.EnumerateRunes().Count() + 2) * batch.Length <= tokenLimit), Is.True);
        }
    }
}
