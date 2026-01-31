// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Text;
using System.Xml;

namespace Cotton.Server.Services.WebDav;

/// <summary>
/// Builds WebDAV XML responses (multistatus, responses, etc.)
/// </summary>
public static class WebDavXmlBuilder
{
    private const string DavNamespace = "DAV:";

    public static string BuildMultiStatusResponse(IEnumerable<WebDavResource> resources)
    {
        var sb = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = false,
            Indent = true,
            Encoding = Encoding.UTF8
        };

        using var stringWriter = new StringWriter(sb);
        using (var writer = XmlWriter.Create(stringWriter, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("d", "multistatus", DavNamespace);

            foreach (var resource in resources)
            {
                WriteResourceResponse(writer, resource);
            }

            writer.WriteEndElement(); // multistatus
            writer.WriteEndDocument();
        }

        return sb.ToString();
    }

    private static void WriteResourceResponse(XmlWriter writer, WebDavResource resource)
    {
        writer.WriteStartElement("d", "response", null);
        writer.WriteElementString("d", "href", null, resource.Href);

        writer.WriteStartElement("d", "propstat", null);
        writer.WriteStartElement("d", "prop", null);

        writer.WriteElementString("d", "displayname", null, resource.DisplayName);

        writer.WriteStartElement("d", "resourcetype", null);
        if (resource.IsCollection)
        {
            writer.WriteElementString("d", "collection", null, string.Empty);
        }
        writer.WriteEndElement(); // resourcetype

        writer.WriteElementString("d", "getcontentlength", null, resource.ContentLength.ToString());
        writer.WriteElementString("d", "getlastmodified", null, resource.LastModified.ToString("R"));
        writer.WriteElementString("d", "getetag", null, resource.ETag);

        if (!resource.IsCollection && !string.IsNullOrEmpty(resource.ContentType))
        {
            writer.WriteElementString("d", "getcontenttype", null, resource.ContentType);
        }

        writer.WriteEndElement(); // prop
        writer.WriteElementString("d", "status", null, "HTTP/1.1 200 OK");
        writer.WriteEndElement(); // propstat

        writer.WriteEndElement(); // response
    }
}

/// <summary>
/// Represents a WebDAV resource (file or collection)
/// </summary>
public record WebDavResource(
    string Href,
    string DisplayName,
    bool IsCollection,
    long ContentLength,
    DateTimeOffset LastModified,
    string ETag,
    string? ContentType = null);
