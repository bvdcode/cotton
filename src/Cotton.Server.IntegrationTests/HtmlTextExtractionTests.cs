// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.TextExtraction;
using NUnit.Framework;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    public class HtmlTextExtractionTests
    {
        [Test]
        public async Task Extract_LimitCountsVisibleTextAndPreservesInlineWords()
        {
            using MemoryStream source = new(Encoding.UTF8.GetBytes("<script>ignored</script><p>Hel<b>lo</b> world</p>"));
            TextExtractionResult result = await new HtmlTextExtractor().ExtractAsync(source, 5);
            Assert.That(result, Is.EqualTo(new TextExtractionResult("Hello", true)));
        }

        [Test]
        public async Task Extract_ReturnsVisibleTextInDocumentOrder()
        {
            const string Html = "<html><head><style>.hidden{}</style></head><body>"
                + "<h1>Heading</h1><p>Hello <strong>world</strong></p><script>ignored()</script>"
                + "<ul><li>First</li><li>Second</li></ul></body></html>";
            using MemoryStream source = new(Encoding.UTF8.GetBytes(Html));

            string text = (await new HtmlTextExtractor().ExtractAsync(source)).Text;

            Assert.That(text, Is.EqualTo(string.Join(Environment.NewLine,
                "Heading", "Hello world", "First", "Second")));
        }
    }
}
