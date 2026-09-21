// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.TextExtraction;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    public class TextFileExtractionTests
    {
        [Test]
        public async Task Extract_SurrogatePairAcrossReadBlocks_RemainsIntact()
        {
            string prefix = new string('a', 4095) + "😀";
            using MemoryStream source = new(Encoding.UTF8.GetBytes(prefix + "tail"));
            TextFileExtractor extractor = new(NullLogger<TextFileExtractor>.Instance);
            Assert.That(await extractor.ExtractAsync(source, 4099), Is.EqualTo(new TextExtractionResult(prefix, true)));
        }

        [Test]
        public async Task Extract_StopsReadingAfterLimit_AndPreservesUnicode()
        {
            using MemoryStream source = new(Encoding.UTF8.GetBytes("aБ😀中" + new string('x', 65536)));
            TextFileExtractor extractor = new(NullLogger<TextFileExtractor>.Instance);
            TextExtractionResult result = await extractor.ExtractAsync(source, 7);

            Assert.That(result, Is.EqualTo(new TextExtractionResult("aБ😀", true)));
            Assert.That(source.Position, Is.LessThan(source.Length));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task Extract_ExactLimit_DoesNotMarkTruncated_ForUtf8OrBomUtf16(bool utf16)
        {
            const string Input = "aБ😀中";
            byte[] bytes = utf16
                ? Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(Input)).ToArray()
                : Encoding.UTF8.GetBytes(Input);
            using MemoryStream source = new(bytes);
            TextFileExtractor extractor = new(NullLogger<TextFileExtractor>.Instance);

            Assert.That(await extractor.ExtractAsync(source, 10), Is.EqualTo(new TextExtractionResult(Input, false)));
        }

        [Test]
        public async Task Extract_ReadsBomEncodedTextAndKeepsSourceOpen()
        {
            byte[] content = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("  Hello world  ")).ToArray();
            using MemoryStream source = new(content);
            TextFileExtractor extractor = new(NullLogger<TextFileExtractor>.Instance);

            string text = (await extractor.ExtractAsync(source)).Text;

            Assert.Multiple(() =>
            {
                Assert.That(text, Is.EqualTo("Hello world"));
                Assert.That(source.CanRead, Is.True);
            });
        }

        [Test]
        public void Extract_InvalidEncodingReportsUnreadableContent()
        {
            using MemoryStream source = new([0xC3, 0x28]);
            TextFileExtractor extractor = new(NullLogger<TextFileExtractor>.Instance);

            Assert.ThrowsAsync<FileTextExtractionException>(async () => await extractor.ExtractAsync(source));
        }
    }
}
