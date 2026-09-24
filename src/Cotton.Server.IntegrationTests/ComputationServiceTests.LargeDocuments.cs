// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public partial class ComputationServiceTests
    {
        [TestCase(1, 1_048_576)]
        [TestCase(65, 40_000)]
        public async Task LargeDocuments_PreserveEveryFragmentAndRespectAllRequestLimits(int count, int characters)
        {
            const string pattern = "aБ😀中";
            string content = string.Concat(Enumerable.Repeat(pattern, characters / pattern.Length));
            string[] documents = Enumerable.Range(0, count).Select(index => content + index).ToArray();
            float[][][] results = await _service.GetTextEmbeddingFragmentsAsync(documents.ToAsyncEnumerable());
            string[] fragments = _handler.Batches.SelectMany(batch => batch).ToArray();
            Assert.That(results.Length, Is.EqualTo(count));
            int offset = 0;
            for (int index = 0; index < documents.Length; index++)
            {
                string[] documentFragments = fragments.Skip(offset).Take(results[index].Length).ToArray();
                Assert.That(string.Concat(documentFragments), Is.EqualTo(documents[index]));
                Assert.That(results[index].Select(vector => vector[0]),
                    Is.EqualTo(documentFragments.Select(text => (float)text.Length)));
                offset += results[index].Length;
            }
            Assert.Multiple(() =>
            {
                Assert.That(offset, Is.EqualTo(fragments.Length));
                Assert.That(_handler.InfoCalls, Is.EqualTo(1));
                Assert.That(_handler.TokenizationInputLengths.Max(), Is.LessThanOrEqualTo(32768));
                Assert.That(fragments.All(text => text.EnumerateRunes().Count() + 2 <= _handler.MaxInputTokens), Is.True);
                Assert.That(_handler.Batches.All(batch => batch.Length <= _handler.MaxBatchInputs
                    && (long)batch.Max(text => text.EnumerateRunes().Count() + 2) * batch.Length <= _handler.MaxBatchTokens), Is.True);
                Assert.That(results.SelectMany(document => document).All(vector => vector.Length == 1024), Is.True);
            });
        }
    }
}
