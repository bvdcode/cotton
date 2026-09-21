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
        public async Task Extract_ReadsBomEncodedTextAndKeepsSourceOpen()
        {
            byte[] content = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("  Hello world  ")).ToArray();
            using MemoryStream source = new(content);
            TextFileExtractor extractor = new(NullLogger<TextFileExtractor>.Instance);

            string text = await extractor.ExtractAsync(source);

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
