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

        [Test]
        public async Task Extract_ChatExport_ReturnsOnlyTheActiveConversationPath()
        {
            const string Html = """
                <html><head><title>ChatGPT Data Export</title></head><body><div id="root"></div><script>
                var jsonData = [{
                  "title": "Useful &amp; chat",
                  "current_node": "answer",
                  "mapping": {
                    "system": {
                      "parent": null,
                      "message": {
                        "author": { "role": "system" },
                        "content": { "content_type": "text", "parts": ["Hidden system message"] }
                      }
                    },
                    "question": {
                      "parent": "system",
                      "message": {
                        "author": { "role": "user" },
                        "content": { "content_type": "text", "parts": ["Question &lt;one&gt;"] }
                      }
                    },
                    "answer": {
                      "parent": "question",
                      "message": {
                        "author": { "role": "assistant" },
                        "content": { "content_type": "text", "parts": ["Answer with ] in the text"] }
                      }
                    },
                    "unused": {
                      "parent": "question",
                      "message": {
                        "author": { "role": "assistant" },
                        "content": { "content_type": "text", "parts": ["Unused branch"] }
                      }
                    }
                  }
                }];
                </script></body></html>
                """;
            using MemoryStream source = new(Encoding.UTF8.GetBytes(Html));

            TextExtractionResult result = await new HtmlTextExtractor().ExtractAsync(source);

            Assert.Multiple(() =>
            {
                Assert.That(result.Text, Is.EqualTo(string.Join(Environment.NewLine,
                    "Useful & chat", "Question <one>", "Answer with ] in the text")));
                Assert.That(result.IsTruncated, Is.False);
            });
        }

        [Test]
        public void Extract_InvalidChatExport_ThrowsExtractionException()
        {
            using MemoryStream source = new(Encoding.UTF8.GetBytes(
                "<title>ChatGPT Data Export</title><script>var jsonData = [{ invalid }];</script>"));

            Assert.ThrowsAsync<FileTextExtractionException>(
                async () => await new HtmlTextExtractor().ExtractAsync(source));
        }

        [Test]
        public void Extract_ChatExportWithCyclicParent_ThrowsExtractionException()
        {
            const string Html = """
                <title>ChatGPT Data Export</title><script>var jsonData = [{
                  "current_node": "answer",
                  "mapping": {
                    "question": { "parent": "answer" },
                    "answer": { "parent": "question" }
                  }
                }];</script>
                """;
            using MemoryStream source = new(Encoding.UTF8.GetBytes(Html));

            Assert.ThrowsAsync<FileTextExtractionException>(
                async () => await new HtmlTextExtractor().ExtractAsync(source));
        }

        [Test]
        public async Task Extract_LargeGenericHtml_StopsReadingSourceAndMarksTextAsTruncated()
        {
            string html = $"<p>Visible text</p><script>{new string('x', 17 * 1024 * 1024)}</script>";
            using MemoryStream source = new(Encoding.UTF8.GetBytes(html));

            TextExtractionResult result = await new HtmlTextExtractor().ExtractAsync(source);

            Assert.Multiple(() =>
            {
                Assert.That(result.Text, Is.EqualTo("Visible text"));
                Assert.That(result.IsTruncated, Is.True);
                Assert.That(source.Position, Is.LessThan(source.Length));
            });
        }
    }
}
