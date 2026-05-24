// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services.WebDav;
using NUnit.Framework;
using System.Xml.Linq;

namespace Cotton.Server.IntegrationTests;

public class WebDavXmlBuilderTests
{
    private static readonly XNamespace Dav = "DAV:";

    [Test]
    public void BuildMultiStatusResponse_WritesQuotaProperties_WhenQuotaIsKnown()
    {
        string xml = WebDavXmlBuilder.BuildMultiStatusResponse([
            new WebDavResource(
                Href: "/api/v1/webdav/",
                DisplayName: "Home",
                IsCollection: true,
                ContentLength: 0,
                LastModified: DateTimeOffset.UnixEpoch,
                ETag: "\"root\"",
                Quota: new WebDavQuota(UsedBytes: 123, AvailableBytes: 877))
        ]);

        XElement prop = XDocument.Parse(xml).Descendants(Dav + "prop").Single();

        Assert.That(prop.Element(Dav + "quota-used-bytes")?.Value, Is.EqualTo("123"));
        Assert.That(prop.Element(Dav + "quota-available-bytes")?.Value, Is.EqualTo("877"));
    }

    [Test]
    public void BuildMultiStatusResponse_OmitsAvailableQuota_WhenInstanceQuotaIsUnlimited()
    {
        string xml = WebDavXmlBuilder.BuildMultiStatusResponse([
            new WebDavResource(
                Href: "/api/v1/webdav/",
                DisplayName: "Home",
                IsCollection: true,
                ContentLength: 0,
                LastModified: DateTimeOffset.UnixEpoch,
                ETag: "\"root\"",
                Quota: new WebDavQuota(UsedBytes: 123, AvailableBytes: null))
        ]);

        XElement prop = XDocument.Parse(xml).Descendants(Dav + "prop").Single();

        Assert.That(prop.Element(Dav + "quota-used-bytes")?.Value, Is.EqualTo("123"));
        Assert.That(prop.Element(Dav + "quota-available-bytes"), Is.Null);
    }
}
