// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.TextExtraction;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    public class EmailTextExtractionTests
    {
        [TestCase("text/plain", "Hello world")]
        [TestCase("text/html", "<p>Hello <b>world</b></p>")]
        public async Task Extract_LimitIsSharedBetweenHeadersAndBody(string contentType, string body)
        {
            using MemoryStream source = new(Encoding.UTF8.GetBytes(
                $"Subject: Title\r\nContent-Type: {contentType}; charset=utf-8\r\n\r\n{body}"));
            EmailTextExtractor extractor = new(NullLogger<EmailTextExtractor>.Instance);
            string expected = $"Title{Environment.NewLine}Hello";
            Assert.That(await extractor.ExtractAsync(source, Encoding.UTF8.GetByteCount(expected)),
                Is.EqualTo(new TextExtractionResult(expected, true)));
        }

        [Test]
        public async Task Extract_ReadsHeadersAndPrefersPlainTextBody()
        {
            const string Message = "From: Alice <alice@example.com>\r\n"
                + "To: Bob <bob@example.com>\r\n"
                + "Subject: Project update\r\n"
                + "Content-Type: multipart/alternative; boundary=parts\r\n\r\n"
                + "--parts\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nPlain body\r\n"
                + "--parts\r\nContent-Type: text/html; charset=utf-8\r\n\r\n<p>HTML body</p>\r\n"
                + "--parts--\r\n";
            using MemoryStream source = new(Encoding.UTF8.GetBytes(Message));
            EmailTextExtractor extractor = new(NullLogger<EmailTextExtractor>.Instance);

            string text = (await extractor.ExtractAsync(source)).Text;

            Assert.That(text, Does.Contain("Project update"));
            Assert.That(text, Does.Contain("alice@example.com"));
            Assert.That(text, Does.Contain("bob@example.com"));
            Assert.That(text, Does.Contain("Plain body"));
            Assert.That(text, Does.Not.Contain("HTML body"));
            Assert.That(source.CanRead, Is.True);
        }
    }
}
