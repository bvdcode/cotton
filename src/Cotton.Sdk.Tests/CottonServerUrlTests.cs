// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk.Tests
{
    public sealed class CottonServerUrlTests
    {
        [TestCase("app.cottoncloud.dev", "https://app.cottoncloud.dev/")]
        [TestCase(" app.cottoncloud.dev ", "https://app.cottoncloud.dev/")]
        [TestCase("localhost:5182", "https://localhost:5182/")]
        [TestCase("http://localhost:5182", "http://localhost:5182/")]
        [TestCase("https://app.cottoncloud.dev/cloud", "https://app.cottoncloud.dev/cloud")]
        public void NormalizeOptional_AcceptsHttpHttpsAndBareHosts(string value, string expected)
        {
            Uri? uri = CottonServerUrl.NormalizeOptional(value);

            Assert.That(uri, Is.EqualTo(new Uri(expected)));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("file:///tmp/cotton")]
        [TestCase("ftp://app.cottoncloud.dev")]
        public void NormalizeOptional_RejectsEmptyAndUnsupportedValues(string? value)
        {
            Uri? uri = CottonServerUrl.NormalizeOptional(value);

            Assert.That(uri, Is.Null);
        }

        [Test]
        public void NormalizeRequired_RejectsEmptyValue()
        {
            ArgumentException? exception = Assert.Throws<ArgumentException>(
                static () => CottonServerUrl.NormalizeRequired("  ", "serverUrl"));

            Assert.That(exception?.ParamName, Is.EqualTo("serverUrl"));
        }

        [Test]
        public void NormalizeRequired_RejectsUnsupportedScheme()
        {
            ArgumentException? exception = Assert.Throws<ArgumentException>(
                static () => CottonServerUrl.NormalizeRequired("ftp://app.cottoncloud.dev", "serverUrl"));

            Assert.That(exception?.ParamName, Is.EqualTo("serverUrl"));
        }
    }
}
