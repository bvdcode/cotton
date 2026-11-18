// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services.WebDav;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class WebDavRequestHeadersTests
    {
        [Test]
        public void ParsesWebDavHeaders()
        {
            HeaderDictionary headers = new HeaderDictionary
            {
                ["If"] = "(<opaquelocktoken:lock-id>)",
                ["Timeout"] = "Second-90",
                ["Depth"] = "infinity",
                ["Destination"] = "https://cloud.example/api/v1/webdav/folder/file.txt",
                ["Overwrite"] = "F",
            };

            Assert.Multiple(() =>
            {
                Assert.That(WebDavRequestHeaders.GetLockToken(headers), Is.EqualTo("opaquelocktoken:lock-id"));
                Assert.That(WebDavRequestHeaders.GetLockTimeout(headers), Is.EqualTo(TimeSpan.FromSeconds(90)));
                Assert.That(WebDavRequestHeaders.GetDepth(headers), Is.EqualTo(25));
                Assert.That(WebDavRequestHeaders.GetDestinationPath(headers), Is.EqualTo("folder/file.txt"));
                Assert.That(WebDavRequestHeaders.GetOverwrite(headers), Is.False);
            });
        }

        [TestCase("invoice%2541.txt", "invoice%41.txt")]
        [TestCase("report%20%2350%25.txt", "report #50%.txt")]
        [TestCase("report%252F.txt", "report%2F.txt")]
        [TestCase("caf%C3%A9.txt", "café.txt")]
        public void Destination_DecodesOnce(string encodedName, string expectedName)
        {
            HeaderDictionary headers = new()
            {
                ["Destination"] = "https://cloud.example/api/v1/webdav/" + encodedName,
            };

            Assert.That(WebDavRequestHeaders.GetDestinationPath(headers), Is.EqualTo(expectedName));
        }
    }
}
