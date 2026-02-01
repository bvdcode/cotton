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
            OmitXmlDeclaration = true, // Windows WebDAV client doesn't like encoding declaration
            Indent = false,
            Encoding = Encoding.UTF8
        };

        using var stringWriter = new StringWriter(sb);
        using (var writer = XmlWriter.Create(stringWriter, settings))
        {
            writer.WriteStartElement("d", "multistatus", DavNamespace);

            foreach (var resource in resources)
            {
                WriteResourceResponse(writer, resource);
            }

            writer.WriteEndElement(); // multistatus
        }

        return sb.ToString();
    }

    private static void WriteResourceResponse(XmlWriter writer, WebDavResource resource)
    {
        writer.WriteStartElement("d", "response", DavNamespace);
        writer.WriteElementString("d", "href", DavNamespace, resource.Href);

        writer.WriteStartElement("d", "propstat", DavNamespace);
        writer.WriteStartElement("d", "prop", DavNamespace);

        writer.WriteElementString("d", "displayname", DavNamespace, resource.DisplayName);

        writer.WriteStartElement("d", "resourcetype", DavNamespace);
        if (resource.IsCollection)
        {
            writer.WriteElementString("d", "collection", DavNamespace, string.Empty);
        }
        writer.WriteEndElement(); // resourcetype

        writer.WriteElementString("d", "getcontentlength", DavNamespace, resource.ContentLength.ToString());
        writer.WriteElementString("d", "getlastmodified", DavNamespace, resource.LastModified.ToString("R"));
        writer.WriteElementString("d", "getetag", DavNamespace, resource.ETag);

        if (!resource.IsCollection && !string.IsNullOrEmpty(resource.ContentType))
        {
            writer.WriteElementString("d", "getcontenttype", DavNamespace, resource.ContentType);
        }

        writer.WriteEndElement(); // prop
        writer.WriteElementString("d", "status", DavNamespace, "HTTP/1.1 200 OK");
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
