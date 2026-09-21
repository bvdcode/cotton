// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Models.Computation;
using Cotton.Server.Services.Computation;
using NUnit.Framework;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    public class TextEmbeddingChunkerTests
    {
        [TestCase("Привет 😀 世界!", 6)]
        [TestCase("one two three four five six", 9)]
        public async Task Split_PreservesUnicodeAndOrder_AndIncludesSpecialTokens(string text, int limit)
        {
            using TeiTestHandler handler = new();
            TextEmbeddingChunker chunker = new(new TeiClient(new TeiTestClientFactory(handler)));
            List<TextEmbeddingChunk> chunks = [];

            await foreach (TextEmbeddingChunk chunk in chunker.SplitAsync(new Uri("https://runner.example/"), text, limit, default))
            {
                chunks.Add(chunk);
            }

            Assert.Multiple(() =>
            {
                Assert.That(string.Concat(chunks.Select(chunk => chunk.Text)), Is.EqualTo(text));
                Assert.That(chunks.All(chunk => chunk.TokenCount <= limit), Is.True);
                Assert.That(chunks.All(chunk => chunk.TokenCount == chunk.Text.EnumerateRunes().Count() + 2), Is.True);
                Assert.That(chunks.All(chunk => !chunk.Text.Contains('\uFFFD')), Is.True);
            });
        }

        [Test]
        public async Task Split_LongDocument_DoesNotCutSurrogatePairsAtRequestBoundary()
        {
            using TeiTestHandler handler = new();
            TextEmbeddingChunker chunker = new(new TeiClient(new TeiTestClientFactory(handler)));
            string text = new string('a', 32767) + "😀конец";
            StringBuilder result = new();
            await foreach (TextEmbeddingChunk chunk in chunker.SplitAsync(new Uri("https://runner.example/"), text, 8192, default))
            {
                result.Append(chunk.Text);
                Assert.That(chunk.TokenCount, Is.LessThanOrEqualTo(8192));
            }
            Assert.That(result.ToString(), Is.EqualTo(text));
        }

        [Test]
        public async Task Split_Whitespace_DoesNotContactWorker()
        {
            using TeiTestHandler handler = new();
            TextEmbeddingChunker chunker = new(new TeiClient(new TeiTestClientFactory(handler)));
            await foreach (TextEmbeddingChunk chunk in chunker.SplitAsync(new Uri("https://runner.example/"), " \n\t", 8, default))
            {
                Assert.Fail($"Unexpected fragment: {chunk.Text}");
            }
            Assert.That(handler.TokenizeCalls, Is.Zero);
        }
    }
}
