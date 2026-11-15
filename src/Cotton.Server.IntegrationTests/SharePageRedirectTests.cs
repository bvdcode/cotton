// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Helpers;

namespace Cotton.Server.IntegrationTests
{
    public class SharePageRedirectTests
    {
        [Test]
        public void InternalNavigationPreservesTheCurrentOriginAndEncodesTheToken()
        {
            string html = SharePageHtml.Create("http://configured.example:8080", "test/token", "Folder", false);
            Assert.Multiple(() =>
            {
                Assert.That(html, Does.Contain("content=\"0;url=/share/test%2Ftoken\""));
                Assert.That(html, Does.Contain("window.location.replace(\"/share/test%2Ftoken\")"));
                Assert.That(html, Does.Contain("href=\"/share/test%2Ftoken\">Continue"));
            });
        }
    }
}
