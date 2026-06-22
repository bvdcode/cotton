// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Helpers;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public class RequestBaseUrlHelpersTests
{
    [Test]
    public void GetBaseUrl_UsesRequestSchemeWhenForwardedProtoIsMissing()
    {
        HttpRequest request = CreateRequest("http", "cotton.test");

        string baseUrl = RequestBaseUrlHelpers.GetBaseUrl(request);

        Assert.That(baseUrl, Is.EqualTo("http://cotton.test"));
    }

    [Test]
    public void GetBaseUrl_UsesForwardedProtoForUrlGeneration()
    {
        HttpRequest request = CreateRequest("http", "cotton.test");
        request.Headers["X-Forwarded-Proto"] = "https";

        string baseUrl = RequestBaseUrlHelpers.GetBaseUrl(request);

        Assert.Multiple(() =>
        {
            Assert.That(baseUrl, Is.EqualTo("https://cotton.test"));
            Assert.That(request.Scheme, Is.EqualTo("http"));
            Assert.That(request.HttpContext.Connection.RemoteIpAddress, Is.Null);
        });
    }

    [Test]
    public void GetBaseUrl_UsesFirstForwardedProtoValue()
    {
        HttpRequest request = CreateRequest("http", "cotton.test");
        request.Headers["X-Forwarded-Proto"] = "https, http";

        string baseUrl = RequestBaseUrlHelpers.GetBaseUrl(request);

        Assert.That(baseUrl, Is.EqualTo("https://cotton.test"));
    }

    [Test]
    public void GetBaseUrl_IgnoresUnsupportedForwardedProto()
    {
        HttpRequest request = CreateRequest("http", "cotton.test");
        request.Headers["X-Forwarded-Proto"] = "ftp";

        string baseUrl = RequestBaseUrlHelpers.GetBaseUrl(request);

        Assert.That(baseUrl, Is.EqualTo("http://cotton.test"));
    }

    private static HttpRequest CreateRequest(string scheme, string host)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);
        return context.Request;
    }
}
