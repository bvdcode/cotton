// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class FileETagsTests
    {
        [TestCase("\"sha256-current\"", true)]
        [TestCase("\"sha256-other\", \"sha256-current\"", true)]
        [TestCase("\"sha256-other\"", false)]
        [TestCase("W/\"sha256-current\"", false)]
        [TestCase("*", false)]
        public void MatchesIfNoneMatchHeader_UsesStrongComparison(string headerValue, bool expected)
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
