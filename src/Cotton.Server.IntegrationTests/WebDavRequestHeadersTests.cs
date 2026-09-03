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
    }
}
