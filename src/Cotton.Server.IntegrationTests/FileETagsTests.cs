// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class FileETagsTests
    {
        [TestCase(null, true)]
        [TestCase("\"sha256-010203\"", true)]
        [TestCase(" \"sha256-010203\" ", true)]
        [TestCase("\"sha256-other\", \"sha256-010203\"", true)]
        [TestCase("\"sha256-other\"", false)]
        [TestCase("W/\"sha256-010203\"", false)]
        [TestCase("*", true)]
        [TestCase("sha256-010203", false)]
        [TestCase("\"sha256-010203", false)]
        [TestCase("\"sha256-010203\", invalid", false)]
        [TestCase("", false)]
        public void MatchesIfMatchHeader_UsesStrongConditionalRequestComparison(string? headerValue, bool expected)
        {
            NodeFile nodeFile = new()
            {
                FileManifest = new FileManifest { ProposedContentHash = [1, 2, 3] },
            };

            bool matches = FileETags.MatchesIfMatchHeader(headerValue, nodeFile);

            Assert.That(matches, Is.EqualTo(expected));
        }

        [TestCase("\"sha256-current\"", true)]
        [TestCase("\"sha256-other\", \"sha256-current\"", true)]
        [TestCase("\"sha256-other\"", false)]
        [TestCase("W/\"sha256-current\"", true)]
        [TestCase("*", true)]
        public void MatchesIfNoneMatchHeader_UsesConditionalRequestComparison(string headerValue, bool expected)
        {
            DefaultHttpContext context = new();
            context.Request.Headers[HeaderNames.IfNoneMatch] = headerValue;
            EntityTagHeaderValue entityTag = EntityTagHeaderValue.Parse("\"sha256-current\"");

            bool matches = FileETags.MatchesIfNoneMatchHeader(context.Request, entityTag);

            Assert.That(matches, Is.EqualTo(expected));
        }

        [Test]
        public void MatchesIfNoneMatchHeader_WithoutHeader_ReturnsFalse()
        {
            DefaultHttpContext context = new();
            EntityTagHeaderValue entityTag = EntityTagHeaderValue.Parse("\"sha256-current\"");

            bool matches = FileETags.MatchesIfNoneMatchHeader(context.Request, entityTag);

            Assert.That(matches, Is.False);
        }
    }
}
