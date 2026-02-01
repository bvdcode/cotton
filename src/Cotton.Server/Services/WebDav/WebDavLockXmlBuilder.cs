// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Text;
using System.Xml;

namespace Cotton.Server.Services.WebDav;

public static class WebDavLockXmlBuilder
{
    private const string DavNamespace = "DAV:";

    public static string BuildPropResponse(WebDavLockInfo l)
    {
        var sb = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = false,
            Encoding = Encoding.UTF8
        };

        using var stringWriter = new StringWriter(sb);
        using (var writer = XmlWriter.Create(stringWriter, settings))
        {
            writer.WriteStartElement("d", "prop", DavNamespace);
            writer.WriteStartElement("d", "lockdiscovery", DavNamespace);
            WriteActiveLock(writer, l);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        return sb.ToString();
    }

    private static void WriteActiveLock(XmlWriter writer, WebDavLockInfo l)
    {
        writer.WriteStartElement("d", "activelock", DavNamespace);

        writer.WriteStartElement("d", "locktype", DavNamespace);
        writer.WriteElementString("d", "write", DavNamespace, string.Empty);
        writer.WriteEndElement();

        writer.WriteStartElement("d", "lockscope", DavNamespace);
        writer.WriteElementString("d", "exclusive", DavNamespace, string.Empty);
        writer.WriteEndElement();

        writer.WriteStartElement("d", "depth", DavNamespace);
        writer.WriteString("0");
        writer.WriteEndElement();

        if (l.ExpiresAt.HasValue)
        {
            var seconds = Math.Max(1, (long)(l.ExpiresAt.Value - DateTimeOffset.UtcNow).TotalSeconds);
            writer.WriteElementString("d", "timeout", DavNamespace, $"Second-{seconds}");
        }

        writer.WriteStartElement("d", "locktoken", DavNamespace);
        writer.WriteElementString("d", "href", DavNamespace, l.Token);
        writer.WriteEndElement();

        writer.WriteEndElement();
    }
}
